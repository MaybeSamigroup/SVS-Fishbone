using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Character;
#if Aicomi
using ILLGAMES.IO;
using CharacterCreation;
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
        static void Implant(HumanData data, byte[] bytes) =>
            data.PngData = data.PngData != null ? Encode.Implant(data.PngData, bytes) : Encode.Implant(bytes);
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
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
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
    internal class CharaLoadHook
    {
        internal static Func<LoadFlags, CharaLoadHook> DisabledResolver = _ => Skip;
        internal static Func<LoadFlags, CharaLoadHook> EnabledResolver = _ => new CharaLoadWithPng();
        internal static Func<LoadFlags, CharaLoadHook> CraftFlagResolver = flags =>
            flags is (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About)
                  or (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About | LoadFlags.Status)
                ? new CharaLoadWithoutPng() : Skip;
        internal static Func<LoadFlags, CharaLoadHook> CustomFlagResolver = flags =>
            flags is (LoadFlags.About | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Graphic)
                  or (LoadFlags.About | LoadFlags.Custom | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam)
                  or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Custom | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam)
                  or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
                  or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
                  or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
                  ? new CharaLoadWithoutPng() : Skip;
        internal static Func<LoadFlags, CharaLoadHook> LoadFlagResolver = DisabledResolver;
        internal virtual CharaLoadHook Resolve(LoadFlags flags) => LoadFlagResolver(flags);
        internal virtual CharaLoadHook Resolve(Il2CppStream stream, long length) => this;
        internal virtual CharaLoadHook Resolve(HumanData data) => this;
        internal static readonly CharaLoadHook Skip = new CharaLoadHook();
    }
    internal class CharaLoadWithPng : CharaLoadHook
    {
        protected MemoryStream Stream;
        internal CharaLoadWithPng() => Stream = new MemoryStream();
        internal override CharaLoadHook Resolve(HumanData data) =>
            Skip.With(F.Apply(Intercept, data) + F.Apply(Extension.Preprocess, data, Stream));
        void Intercept(HumanData data) =>
            Stream.Write(Decode.Extract(data.PngData));
    }
    internal class CharaLoadWithoutPng : CharaLoadWithPng
    {
        internal CharaLoadWithoutPng() : base() { }
        internal override CharaLoadHook Resolve(Il2CppStream stream, long length) =>
            this.With(F.Apply(Intercept, stream, length));
        internal override CharaLoadHook Resolve(HumanData data) =>
            Skip.With(F.Apply(Extension.Preprocess, data, Stream));
        void Intercept(Il2CppStream stream, long length) =>
            F.Apply(Intercept, stream, stream.Position, new Il2CppBytes(length)).Try(Plugin.Instance.Log.LogError);
        void Intercept(Il2CppStream source, long offset, Il2CppBytes buffer)
        {
            source.Read(buffer);
            Stream.Write(Decode.Extract(buffer));
            source.Seek(offset, Il2CppSystem.IO.SeekOrigin.Begin);
        }
    }
    static partial class Hooks
    {
        static CharaLoadHook CharaLoadHook = CharaLoadHook.Skip;
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePrefix(LoadFlags flags) =>
            CharaLoadHook = CharaLoadHook.Resolve(flags);
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.GetPngSize), typeof(Il2CppStream))]
        static void GetPngSizePostfix(Il2CppStream st, long __result) =>
            CharaLoadHook = CharaLoadHook.Resolve(st, __result);
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePostfix(HumanData __instance) =>
            CharaLoadHook = CharaLoadHook.Resolve(__instance);
    }
    public static partial class Extension
    {
        internal static event Action<HumanData, ZipArchive, CopyTrack> OnPreprocessCharaWithCopyTrack =
            (data, archive, _) => OnPreprocessChara.Apply(data).Apply(archive).Try(Plugin.Instance.Log.LogError);
        internal static void Preprocess(HumanData data, Stream stream) =>
            OnPreprocessCharaWithCopyTrack.Apply(data)
                .Apply(new ZipArchive(stream, ZipArchiveMode.Update))
                .Apply(StartCopyTrack(data)).Try(Plugin.Instance.Log.LogError);
    }
    internal partial class CopyTrack
    {
        CharaLimit Flags;
        internal event Action<Human, CharaLimit> OnResolveHuman =
            (human, limit) => Plugin.Instance.Log.LogDebug($"Character loaded: {human.data.Pointer}, {limit}");
        internal CopyTrack() =>
             Flags = CharaLimit.None;
        internal void MergeLimit(CharaLimit limit) =>
            Flags = Flags is not CharaLimit.None ? Flags : limit;
        CharaLimit Limit => Flags is not CharaLimit.None ? Flags : CharaLimit.All;
        internal void Resolve(Human human) =>
            (OnResolveHuman.Apply(human).Apply(Limit) + F.Apply(Extension.LoadChara, human)).Try(Plugin.Instance.Log.LogError);
    }
    public static partial class Extension
    {
        internal static event Action<Human> OnHumanResolve = ResolveCopyTrack;
        internal static void HumanResolve(Human human) => OnHumanResolve(human);
        internal static event Action<HumanData, HumanData, CharaLimit> OnCopy = UpdateCopyTrack;
        internal static void Copy(HumanData src, HumanData dst, CharaLimit limit) => OnCopy(src, dst, limit);
        static Dictionary<HumanData, CopyTrack> CopyTracks = new();
        internal static void ClearCopyTrack() =>
            CopyTracks.Clear();
        internal static CopyTrack StartCopyTrack(HumanData data) =>
            CopyTracks.TryGetValue(data, out var track) ? track : CopyTracks[data] = new CopyTrack();
        internal static void UpdateCopyTrack(HumanData src, HumanData dst, CharaLimit limit) =>
            CopyTracks.Remove(src, out var track).Maybe(() => (CopyTracks[dst] = track).MergeLimit(limit));
        internal static void ResolveCopyTrack(Human human) =>
            CopyTracks.Remove(human.data, out var track).Maybe(() => track.Resolve(human));
        internal static void LoadChara(Human human) =>
            OnLoadChara.Apply(human).Try(Plugin.Instance.Log.LogError);
    }
    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Preprocess(HumanData data, ZipArchive archive, CopyTrack track) =>
            OnPreprocessChara.Apply(data).Apply(LoadChara(archive).With(CopyTrackStart(track))).Try(Plugin.Instance.Log.LogError);
        internal static event Action<CopyTrack, T> OnCopyTrackStart = delegate { };
        internal static Action<T> CopyTrackStart(CopyTrack track) =>
            value => OnCopyTrackStart(track, value);
    }
    public static partial class Extension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Preprocess(HumanData data, ZipArchive archive, CopyTrack track) =>
            OnPreprocessChara.Apply(data).Apply(LoadChara(archive).With(CopyTrackStart(track))).Try(Plugin.Instance.Log.LogError);
        internal static event Action<CopyTrack, T> OnCopyTrackStart = delegate { };
        internal static Action<T> CopyTrackStart(CopyTrack track) =>
            value => OnCopyTrackStart(track, value);
    }
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyPrefix(HumanData dst, HumanData src) =>
            Extension.Copy(src, dst, CharaLimit.None);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        static void HumanDataCopyLimitedPrefix(HumanData dst, HumanData src, CharaLimit flags) =>
            Extension.Copy(src, dst, flags);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyWithoutPngData), typeof(HumanData))]
        static void HumanDataCopyWithoutPngPrefix(HumanData __instance, HumanData src) =>
            Extension.Copy(src, __instance, CharaLimit.None);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateShader), [])]
        static void HumanLoadPrefix(HumanBody __instance) =>
#if Aicomi
            Extension.HumanResolve(__instance._human);
#else
            Extension.HumanResolve(__instance.human);
#endif
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.LoadGagMaterial), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateShader), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void HumanLoadPrefix(HumanFace __instance) =>
#if Aicomi
            Extension.HumanResolve(__instance._human);
#else
            Extension.HumanResolve(__instance.human);
#endif
    }
    #endregion

    #region Coordinate Definitions
    internal partial class CoordLoadHook
    {
        internal virtual CoordLoadHook Resolve(Il2CppReader reader) => this;
        internal virtual CoordLoadHook Resolve(HumanDataCoordinate data) => this;
        internal static readonly CoordLoadHook Skip = new CoordLoadHook();
    }
    internal partial class CoordLoadWait : CoordLoadHook
    {
        internal override CoordLoadHook Resolve(Il2CppReader reader) => new CoordLoadProc(reader);
    }
    internal partial class CoordLoadProc : CoordLoadHook
    {
        MemoryStream Stream = new MemoryStream();
        internal CoordLoadProc(Il2CppReader reader)
        {
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
            Stream.Write(Decode.Extract(PngFile.LoadPngBytes(reader)));
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
        }
        internal override CoordLoadHook Resolve(HumanDataCoordinate data) =>
           new CoordLoadWait().With(F.Apply(Extension.Preprocess, data, Stream, new CoordTrack()));
    }
    static partial class Hooks
    {
        static CoordLoadHook CoordLoadHook = new CoordLoadWait();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            CoordLoadHook = CoordLoadHook.Resolve(br);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void HumanDataCoordinateLoadBytesPostfix(HumanDataCoordinate __instance) =>
            CoordLoadHook = CoordLoadHook.Resolve(__instance);
    }

    internal partial class CoordTrack
    {
        CoordLimit Flags;
        internal event Action<Human, CoordLimit> OnResolve =
            (human, limit) => Plugin.Instance.Log.LogDebug($"coord loaded: {human.data.Pointer}, {limit}");

        internal CoordTrack()
        {
            Flags = CoordLimit.None;
            Hooks.OnCoordLimitMerge += LimitMerge;
            Hooks.OnCoordinateResolve += Resolve;
        }
        internal void LimitMerge(CoordLimit limit) => Flags |= limit;
        CoordLimit Limit => Flags is not CoordLimit.None ? Flags : CoordLimit.All;
        internal void Resolve(Human human)
        {
            Hooks.OnCoordLimitMerge -= LimitMerge;
            Hooks.OnCoordinateResolve -= Resolve;
            (OnResolve.Apply(human).Apply(Limit) + F.Apply(Extension.LoadCoord, human)).Try(Plugin.Instance.Log.LogError);
        }
    }
    static partial class Hooks
    {
        internal static event Action<CoordLimit> OnCoordLimitMerge = delegate { };
        internal static event Action<Human> OnCoordinateResolve = delegate { };

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataClothes), nameof(HumanDataClothes.CopyBase))]
        static void HumanDataClothesCopyPostfix() => OnCoordLimitMerge(CoordLimit.Clothes);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataAccessory), nameof(HumanDataAccessory.Copy))]
        static void HumanDataAccessoryCopyPostfix() => OnCoordLimitMerge(CoordLimit.Accessory);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataHair), nameof(HumanDataHair.Copy))]
        static void HumanDataHairCopyPostfix() => OnCoordLimitMerge(CoordLimit.Hair);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataFaceMakeup), nameof(HumanDataFaceMakeup.Copy))]
        static void HumanDataFaceMakeupCopyPostfix() => OnCoordLimitMerge(CoordLimit.FaceMakeup);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataBodyMakeup), nameof(HumanDataBodyMakeup.Copy))]
        static void HumanDataBodyMakeupCopyPostfix() => OnCoordLimitMerge(CoordLimit.BodyMakeup);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateCoordinate))]
        static void HumanBodyOnUpdateCoordinatePrefix(HumanBody __instance) =>
#if Aicomi
            OnCoordinateResolve(__instance._human);
#else
            OnCoordinateResolve(__instance.human);
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateCoordinate))]
        static void HumanFaceOnUpdateCoordinatePrefix(HumanFace __instance) =>
#if Aicomi
            OnCoordinateResolve(__instance._human);
#else
            OnCoordinateResolve(__instance.human);
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPrefix(Human __instance) =>
            OnCoordinateResolve(__instance);
    }
    public static partial class Extension
    {
        internal static event Action<HumanDataCoordinate, ZipArchive, CoordTrack> OnPreprocessCoordWithLimitTrack =
            (data, archive, _) => OnPreprocessCoord.Apply(data).Apply(archive).Try(Plugin.Instance.Log.LogError);
        internal static void Preprocess(HumanDataCoordinate data, Stream stream, CoordTrack track) =>
            OnPreprocessCoordWithLimitTrack.Apply(data)
                .Apply(new ZipArchive(stream, ZipArchiveMode.Update))
                .Apply(track).Try(Plugin.Instance.Log.LogError);
        internal static void LoadCoord(Human human) =>
            OnLoadCoord.Apply(human).Try(Plugin.Instance.Log.LogError);
    }

    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Preprocess(HumanDataCoordinate data, ZipArchive archive, CoordTrack track) =>
            OnPreprocessCoord.Apply(data).Apply(LoadCoord(archive).With(CoordTrackStart(track))).Try(Plugin.Instance.Log.LogError);
        internal static event Action<CoordTrack, U> OnCoordTrackStart = delegate { };
        internal static Action<U> CoordTrackStart(CoordTrack track) => value => OnCoordTrackStart(track, value);
    }
    public static partial class Extension
    {
        internal static void RegisterInternal<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            OnPreprocessCharaWithCopyTrack += Extension<T, U>.Preprocess;
            OnPreprocessCoordWithLimitTrack += Extension<T, U>.Preprocess;
        }
        internal static void RegisterInternal<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            OnPreprocessCharaWithCopyTrack += Extension<T>.Preprocess;
        }
    }
    #endregion
}