using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Character;
#if AICOMI
using ILLGAMES.IO;
#else
using ILLGames.IO;
#endif
using HarmonyLib;
using CoastalSmell;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppStream = Il2CppSystem.IO.Stream;

namespace Fishbone
{
    #region Common Definitions

    public static partial class Extension
    {
        static readonly byte[] NoExtension = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();

        static void Implant(HumanData data, byte[] bytes) =>
            data.PngData = data.PngData != null ? Encode.Implant(data.PngData, bytes) : Encode.Implant(bytes);

        static ZipArchive ToArchive(byte[] bytes) =>
            new ZipArchive(new MemoryStream()
                .With(stream => stream.Write(bytes.Length == 0 ? NoExtension : bytes))
                .With(stream => stream.Seek(0, SeekOrigin.Begin)), ZipArchiveMode.Update);

        static byte[] ToBinary(Action<ZipArchive> actions) =>
            new MemoryStream().With(Save(actions)).ToArray();

        static Action<MemoryStream> Save(Action<ZipArchive> actions) =>
            stream => actions.ApplyDisposable(new ZipArchive(stream, ZipArchiveMode.Create)).Try(Plugin.Instance.Log.LogError);
    }

    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(ExtensionAttribute<T, U>))
                is ExtensionAttribute<T, U> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} is not bones to stuck.");

        static bool TryGetEntry(ZipArchive archive, string path, out ZipArchiveEntry entry) =>
            null != (entry = archive.GetEntry(path));

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveChara(archive, map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        static void Translate<V>(Func<V, U> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveCoord(archive, map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        internal static void SaveChara(ZipArchive archive, T value) =>
            SerializeChara(archive.CreateEntry(Path).Open(), value);

        internal static void SaveCoord(ZipArchive archive, U value) =>
            SerializeCoord(archive.CreateEntry(Path).Open(), value);

        internal static T LoadChara(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry)
                ? DeserializeChara(entry.Open()) : new();

        internal static U LoadCoord(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry)
                ? DeserializeCoord(entry.Open()) : new();
    }

    public static partial class Extension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(ExtensionAttribute<T>))
                is ExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static bool TryGetEntry(ZipArchive archive, string path, out ZipArchiveEntry entry) =>
            null != (entry = archive.GetEntry(path));

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveChara(archive, map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        static void Cleanup(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry).Maybe(entry.Delete);

        internal static void SaveChara(ZipArchive archive, T value) =>
            SerializeChara(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        internal static T LoadChara(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry)
                ? DeserializeChara(entry.Open()) : new();
    }

    #endregion

    #region Character Definitions

    delegate (Action<Il2CppStream, long, long>, Action<HumanData>) HumanDataLoadActions(HumanData data, LoadFlags flags);

    static partial class Hooks
    {
        static CharaLimit CharaLimits = CharaLimit.All;
        static Action<Il2CppStream, long, long> GetPngSizeSkip = (stream, offset, length) => { };
        static Action<Il2CppStream, long, long> GetPngSizeProc(HumanData data) =>
            (stream, offset, length) =>
            {
                var buffer = new Il2CppBytes(length);
                stream.Read(buffer);
                stream.Seek(offset, Il2CppSystem.IO.SeekOrigin.Begin);
                data.PngData = Encode.Implant(Decode.Extract(buffer));
            };
        static Action<Il2CppStream, long, long> OnGetPngSize = GetPngSizeSkip;
        static Action<HumanData> HumanDataLoadFileSkip = _ => { };
        static Action<HumanData> OnHumanDataLoadFile = HumanDataLoadFileSkip;
        static HumanDataLoadActions HumanDataLoadActions = (data, flags) =>
                flags is (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About)
                    or (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About | LoadFlags.Status)
                ? (GetPngSizeProc(data), Extension.Preprocess)
                : (GetPngSizeSkip, HumanDataLoadFileSkip);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePrefix(HumanData __instance, LoadFlags flags) =>
            ((OnGetPngSize, OnHumanDataLoadFile), CharaLimits, NowLoading) =
                (HumanDataLoadActions(__instance, flags), CharaLimit.All, null);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePostfix(HumanData __instance) =>
            (OnGetPngSize, OnHumanDataLoadFile, CharaLimits) =
                (GetPngSizeSkip, HumanDataLoadFileSkip, CharaLimit.All).With(OnHumanDataLoadFile.Apply(__instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.GetPngSize), typeof(Il2CppStream))]
        static void GetPngSizePostfix(Il2CppStream st, long __result) =>
            OnGetPngSize.Apply(st).Apply(st.Position).Apply(__result).Try(Plugin.Instance.Log.LogError);

        [HarmonyPrefix, HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyHook(HumanData dst, HumanData src) =>
            (CharaLimits = CharaLimit.All).With(F.Apply(Extension.Copy, src, dst));

        [HarmonyPrefix, HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        static void HumanDataCopyLimitedHook(HumanData dst, HumanData src, CharaLimit flags) =>
            (CharaLimits = flags).With(F.Apply(Extension.Copy, src, dst));

        static Human NowLoading = null;
        static Action OnCharacterLoad(Human human) =>
            F.Apply(Extension.LoadChara, NowLoading = human, CharaLimits);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateShader), [])]
        static void HumanLoadPrefix(HumanBody __instance) =>
#if AICOMI
            (NowLoading is null).Maybe(OnCharacterLoad(__instance._human));
#else
            (NowLoading is null).Maybe(OnCharacterLoad(__instance.human));
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.LoadGagMaterial), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateShader), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void HumanLoadPrefix(HumanFace __instance) =>
#if AICOMI
            (NowLoading is null).Maybe(OnCharacterLoad(__instance._human));
#else
            (NowLoading is null).Maybe(OnCharacterLoad(__instance.human));
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create), typeof(HumanData))]
        [HarmonyPatch(typeof(Human), nameof(Human.StatusNormalize), typeof(HumanData))]
        static void HumanCreateAndStatusNormalizePrefix() =>
            NowLoading = null;
    }

    public static partial class Extension
    {
        internal static event Action<HumanData, HumanData> OnCopy =
            (src, dst) => Plugin.Instance.Log.LogDebug($"Character copied from {src.Pointer} to {dst.Pointer}");

        internal static event Action<Human, CharaLimit> PreLoadChara =
            (human, limit) => Plugin.Instance.Log.LogDebug($"Character loaded: {human.data.Pointer}, {limit}");

        internal static void Preprocess(HumanData data) =>
            OnPreprocessChara.Apply(data)
                .Apply(ToArchive(Decode.Extract(data.PngData)))
                .Try(Plugin.Instance.Log.LogError);

        internal static void LoadChara(Human human, CharaLimit limit) =>
            (PreLoadChara.Apply(human).Apply(limit) + OnLoadChara.Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static void Copy(HumanData src, HumanData dst) =>
            OnCopy.Apply(src).Apply(dst).Try(Plugin.Instance.Log.LogError);
    }

    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static Dictionary<HumanData, T> LoadingCharas = new();

        internal static void Initialize() => LoadingCharas.Clear();

        internal static void Preprocess(HumanData data, ZipArchive archive) =>
            OnPreprocessChara.Apply(data).Apply(LoadingCharas[data] = LoadChara(archive)).Try(Plugin.Instance.Log.LogError);

        internal static void Copy(HumanData src, HumanData dst) =>
            (LoadingCharas.TryGetValue(src, out var value) && LoadingCharas.Remove(src)).Maybe(F.Apply(Copy, dst, value));

        static void Copy(HumanData data, T value) =>
            LoadingCharas[data] = value;

        internal static T Resolve(HumanData data, T current) =>
            LoadingCharas.TryGetValue(data, out var value) && LoadingCharas.Remove(data) ? value : current;
    }

    public static partial class Extension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static Dictionary<HumanData, T> LoadingCharas = new();

        internal static void Initialize() => LoadingCharas.Clear();

        internal static void Preprocess(HumanData data, ZipArchive archive) =>
            OnPreprocessChara.Apply(data).Apply(LoadingCharas[data] = LoadChara(archive)).Try(Plugin.Instance.Log.LogError);

        internal static void Copy(HumanData src, HumanData dst) =>
            (LoadingCharas.TryGetValue(src, out var value) && LoadingCharas.Remove(src))
                .Maybe(F.Apply(Copy, dst, value));

        static void Copy(HumanData data, T value) =>
            LoadingCharas[data] = value;

        internal static T Resolve(HumanData data, T current) =>
            LoadingCharas.TryGetValue(data, out var value) && LoadingCharas.Remove(data) ? value : current;
    }

    #endregion

    #region Coordinate Definitions

    static partial class Hooks
    {
        static CoordLimit CoordLimits = CoordLimit.None;
        static Action<Il2CppReader> SkipPngSkip = _ => { };
        static Action<Il2CppReader> SkipPngProc = reader =>
        {
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
            var bytes = Decode.Extract(PngFile.LoadPngBytes(reader));
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
            OnLoadBytes = data =>
            {
                Extension.Preprocess(data, bytes);
                (CoordLimits, OnLoadBytes, CoordLimitsCheck) =
                    (CoordLimit.None, LoadBytesSkip, CoordLimitsCheckProc);
            };
        };
        static Action<HumanDataCoordinate> LoadBytesSkip = _ => { };
        static Action<CoordLimit> CoordLimitsCheckSkip => _ => { };
        static Action<CoordLimit> CoordLimitsCheckProc =>
            limit => CoordLimits |= limit;
        static Action<Il2CppReader> OnSkipPng = SkipPngProc;
        static Action<HumanDataCoordinate> OnLoadBytes = LoadBytesSkip;
        static Action<CoordLimit> CoordLimitsCheck = CoordLimitsCheckSkip;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            OnSkipPng.Apply(br).Try(Plugin.Instance.Log.LogError);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void HumanDataCoordinateLoadBytesPrefix() =>
            CoordLimitsCheck = CoordLimitsCheckSkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void HumanDataCoordinateLoadBytesPostfix(HumanDataCoordinate __instance) =>
            OnLoadBytes(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataClothes), nameof(HumanDataClothes.CopyBase))]
        static void HumanDataClothesCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Clothes);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataAccessory), nameof(HumanDataAccessory.Copy))]
        static void HumanDataAccessoryCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Accessory);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataHair), nameof(HumanDataHair.Copy))]
        static void HumanDataHairCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Hair);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataFaceMakeup), nameof(HumanDataFaceMakeup.Copy))]
        static void HumanDataFaceMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.FaceMakeup);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataBodyMakeup), nameof(HumanDataBodyMakeup.Copy))]
        static void HumanDataBodyMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.BodyMakeup);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateCoordinate))]
        static void HumanBodyOnUpdateCoordinatePrefix(HumanBody __instance) =>
#if AICOMI
            CoordLimits = CoordLimit.None.With(OnCoordinateReload(__instance._human, CoordLimits));
#else
            CoordLimits = CoordLimit.None.With(OnCoordinateReload(__instance.human, CoordLimits));
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateCoordinate))]
        static void HumanFaceOnUpdateCoordinatePrefix(HumanFace __instance) =>
#if AICOMI
            CoordLimits = CoordLimit.None.With(OnCoordinateReload(__instance._human, CoordLimits));
#else
            CoordLimits = CoordLimit.None.With(OnCoordinateReload(__instance.human, CoordLimits));
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPrefix(Human __instance) =>
            CoordLimits = CoordLimit.None.With(OnCoordinateReload(__instance, CoordLimits));
        static Action OnCoordinateReload(Human human, CoordLimit flags) =>
            flags is not CoordLimit.None ? F.Apply(Extension.LoadCoord, human, CoordLimits) : F.DoNothing;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePrefix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
#if AICOMI
            (changeBackCoordinateType || __instance._human.data.Status.coordinateType != (int)type).Maybe(PreCoordinateChange(__instance._human, (int)type));
#else
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type).Maybe(PreCoordinateChange(__instance.human, (int)type));
#endif

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance) =>
#if AICOMI
            OnCoordinateChange(__instance._human);
#else
            OnCoordinateChange(__instance.human);
#endif

        static Action PreCoordinateChange(Human human, int coordinateType) =>
            ((OnCoordinateChange, _) = (OnCoordinateChangeProc, F.Apply(Extension.ChangeCoord, human, coordinateType))).Item2;

        static Action<Human> OnCoordinateChange;
        static Action<Human> OnCoordinateChangeSkip = _ => { };
        static Action<Human> OnCoordinateChangeProc = human =>
            (OnCoordinateChange = OnCoordinateChangeSkip).With(F.Apply(Extension.LoadCoord, human));
    }

    public static partial class Extension
    {
        internal static event Action<Human, CoordLimit> PreLoadCoord =
            (human, limit) => Plugin.Instance.Log.LogDebug($"Coordinate loaded: {human.data.Pointer}, {limit}");

        internal static event Action<Human, int> PreChangeCoord =
            (human, coordinateType) => Plugin.Instance.Log.LogDebug($"Coordinate changed: {human.data.Pointer}, {coordinateType}");

        internal static void Preprocess(HumanDataCoordinate data, byte[] bytes) =>
            OnPreprocessCoord.Apply(data).Apply(ToArchive(bytes)).Try(Plugin.Instance.Log.LogError);

        internal static void LoadCoord(Human human, CoordLimit limit) =>
            (PreLoadCoord.Apply(human).Apply(limit) + OnLoadCoord.Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static void LoadCoord(Human human) =>
            OnLoadCoord.Apply(human).Try(Plugin.Instance.Log.LogError);

        internal static void ChangeCoord(Human human, int coordinateType) =>
            PreChangeCoord.Apply(human).Apply(coordinateType).Try(Plugin.Instance.Log.LogError);
    }

    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static U LoadingCoordinate = new();
        static Func<U, U> ResolveSkip = value => value;
        static Func<U, U> ResolveInit = value =>
            ((OnResolve, LoadingCoordinate) = (ResolveProc, value)).Item2;
        static Func<U, U> ResolveProc = value =>
            ((OnResolve, LoadingCoordinate, _) = (ResolveSkip, new(), LoadingCoordinate)).Item3;
        static Func<U, U> OnResolve = ResolveSkip;
        internal static void Preprocess(HumanDataCoordinate data, ZipArchive archive) =>
            OnPreprocessCoord.Apply(data).Apply(ResolveInit(LoadCoord(archive))).Try(Plugin.Instance.Log.LogError);
        internal static U Resolve(U current) =>
            OnResolve(current);
    }

    public static partial class Extension
    {
        internal static void RegisterInternal<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            OnPreprocessChara += Extension<T, U>.Preprocess;
            OnPreprocessCoord += Extension<T, U>.Preprocess;
            OnCopy += Extension<T, U>.Copy;
        }

        internal static void RegisterInternal<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            OnPreprocessChara += Extension<T>.Preprocess;
            OnCopy += Extension<T>.Copy;
        }
    }
    #endregion
}