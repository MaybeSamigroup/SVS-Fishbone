using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Character;
#if Aicomi
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
using System.Reactive.Disposables;

namespace Fishbone
{
    #region Common Definitions
    public static partial class Extension
    {
        internal static MemoryStream Extract(byte[] buffer) =>
            new MemoryStream().With(stream => stream.Write(Decode.Extract(buffer)));

        internal static void Implant(HumanData data, byte[] bytes) =>
            data.PngData = data.PngData != null ? Encode.Implant(data.PngData, bytes) : Encode.Implant(bytes);

        static byte[] ToBinary(IObserver<ZipArchive> observer) =>
            new MemoryStream().With(Save(observer)).ToArray();

        static Action<MemoryStream> Save(IObserver<ZipArchive> observer) =>
            stream => F.ApplyDisposable(observer.OnNext, new ZipArchive(stream, ZipArchiveMode.Create)).Try(Plugin.Instance.Log.LogError);
    }
    public static partial class Extension<T, U>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(ExtensionAttribute<T, U>))
                is ExtensionAttribute<T, U> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static bool TryGetEntry(ZipArchive archive, string path, out ZipArchiveEntry entry) => null != (entry = archive.GetEntry(path));

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveChara(archive, map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        static void Translate<V>(Func<V, U> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveCoord(archive, map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        internal static void SaveChara(ZipArchive archive, T value) =>
            SerializeChara(archive.CreateEntry(Path).Open(), value);

        internal static void SaveCoord(ZipArchive archive, U value) =>
            SerializeCoord(archive.CreateEntry(Path).Open(), value);

        internal static T LoadChara(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry) ? DeserializeChara(entry.Open()) : new();

        internal static U LoadCoord(ZipArchive archive) =>
            TryGetEntry(archive, Path, out var entry) ? DeserializeCoord(entry.Open()) : new();
    }

    public static partial class Extension<T>
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
            TryGetEntry(archive, Path, out var entry) ? DeserializeChara(entry.Open()) : new();
    }
    #endregion

    #region Character Definitions
    partial class CharaLoadTrack
    {
        protected static Subject<(HumanData, ZipArchive)> LoadComplete = new();
        protected virtual void Complete(HumanData data, MemoryStream stream) =>
            LoadComplete.OnNext((data, new ZipArchive(stream, ZipArchiveMode.Update)));
        CharaLoadTrack Track;
        protected CharaLoadTrack() => Track = this;
        protected CharaLoadTrack(CharaLoadTrack track) => Track = track;
        protected virtual CharaLoadTrack Process(LoadFlags flags) => Track;
        protected virtual CharaLoadTrack Process(Il2CppStream stream, long offset, long length) => Current = this;
        protected virtual CharaLoadTrack Process(HumanData data) => Current = this;
        internal static readonly CharaLoadTrack Ignore = new CharaLoadTrack();
        internal static readonly CharaLoadTrack FlagAware = new CharaLoadFlagAware();
        internal static readonly CharaLoadTrack FlagIgnore = new CharaLoadFlagIgnore();
        protected static readonly CharaLoadTrack GetPngSizeAware = new CharaLoadGetPngSizeAware() { Track = FlagAware };
        protected static readonly CharaLoadTrack GetPngSizeIgnore = new CharaLoadGetPngSizeIgnore() { Track = FlagIgnore };
        protected static readonly CharaLoadTrack WithPng = new CharaLoadWithPng() { Track = FlagIgnore };
        internal static IObservable<(HumanData, ZipArchive)> OnLoadComplete => LoadComplete.AsObservable();
        internal static void OnDefault(HumanData data) => Current.Complete(data, new MemoryStream());
    }
    class CharaLoadFlagAware : CharaLoadTrack
    {
        protected override CharaLoadTrack Process(LoadFlags flags) => CheckFlags(flags) ? GetPngSizeAware : this;
#if DigitalCraft
        static Func<LoadFlags, bool> CheckFlags = flags => flags
            is (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About)
            or (LoadFlags.Custom | LoadFlags.Coorde | LoadFlags.Parameter | LoadFlags.Graphic | LoadFlags.About | LoadFlags.Status);
#else
        static Func<LoadFlags, bool> CheckFlags = flags => flags
            is (LoadFlags.About | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic)
            or (LoadFlags.About | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom);
#endif
    }
    class CharaLoadGetPngSizeAware : CharaLoadTrack
    {
        protected override CharaLoadTrack Process(Il2CppStream stream, long offset, long length) =>
            new CharaLoadWithoutPng(
                Extension.Extract(new Il2CppBytes(length)
                    .With(buffer => stream.Read(buffer))
                    .With(() => stream.Seek(offset, Il2CppSystem.IO.SeekOrigin.Begin))));
    }
    class CharaLoadWithoutPng : CharaLoadTrack
    {
        MemoryStream Stream;
        internal CharaLoadWithoutPng(MemoryStream stream) : base(FlagAware) => Stream = stream;
        protected override CharaLoadTrack Process(HumanData data) =>
            FlagAware.With(F.Apply(Complete, data, Stream));
    }
    class CharaLoadFlagIgnore : CharaLoadTrack
    {
        protected override CharaLoadTrack Process(LoadFlags flags) => GetPngSizeIgnore;
    }
    class CharaLoadGetPngSizeIgnore : CharaLoadTrack
    {
        protected override CharaLoadTrack Process(Il2CppStream stream, long offset, long length) => WithPng;
    }
    class CharaLoadWithPng : CharaLoadTrack
    {
        protected override CharaLoadTrack Process(HumanData data) =>
            FlagIgnore.With(F.Apply(Complete, data, Extension.Extract(data.PngData)));
    }

    partial class CharaLoadTrack
    {
        static Subject<CharaLoadTrack> ModeUpdate = new();
        internal static IObservable<CharaLoadTrack> OnModeUpdate =>
            ModeUpdate.AsObservable().DistinctUntilChanged();
        internal static CharaLoadTrack Mode
        {
            get => Current.Track; set => ModeUpdate.OnNext(Current = value);
        }
#if DigitalCraft
        static CharaLoadTrack Current = FlagAware;
#else
        static CharaLoadTrack Current = Ignore;
#endif
        internal static void Resolve(LoadFlags flags) =>
            Current = Current.Process(flags);
        internal static void Resolve(Il2CppStream stream, long offset, long length) =>
            Current = Current.Process(stream, offset, length);
        internal static void Resolve(HumanData data) =>
            Current = Current.Process(data);
    }

    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePrefix(LoadFlags flags) =>
            CharaLoadTrack.Resolve(flags);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.GetPngSize), typeof(Il2CppStream))]
        static void GetPngSizePostfix(Il2CppStream st, long __result) =>
            CharaLoadTrack.Resolve(st, st.Position, __result);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void HumanDataLoadFilePostfix(HumanData __instance) =>
            CharaLoadTrack.Resolve(__instance);
    }

    static partial class Hooks
    {
        static Subject<(HumanData, HumanData)> HumanDataCopy = new();
        static Subject<(HumanData, CharaLimit)> HumanDataLimit = new();
        static Subject<Human> HumanResolve = new();
        internal static IObservable<(HumanData, HumanData)> OnHumanDataCopy => HumanDataCopy.AsObservable();
        internal static IObservable<(HumanData, CharaLimit)> OnHumanDataLimit => HumanDataLimit.AsObservable();
        internal static IObservable<Human> OnHumanResolve => HumanResolve.AsObservable();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyPrefix(HumanData dst, HumanData src) => HumanDataCopy.OnNext((src, dst));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyWithoutPngData), typeof(HumanData))]
        static void HumanDataCopyWithoutPngPrefix(HumanData __instance, HumanData src) => HumanDataCopy.OnNext((src, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        static void HumanDataCopyLimitedPrefix(HumanData dst, HumanData src, CharaLimit flags) =>
            (F.Apply(HumanDataCopy.OnNext, (src, dst)) + F.Apply(HumanDataLimit.OnNext, (dst, flags))).Invoke();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateShader), [])]
        static void HumanLoadPrefix(HumanBody __instance) =>
#if Aicomi
            HumanResolve.OnNext(__instance._human);
#else
            HumanResolve.OnNext(__instance.human);
#endif
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.LoadGagMaterial), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateShader), [])]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void HumanLoadPrefix(HumanFace __instance) =>
#if Aicomi
            HumanResolve.OnNext(__instance._human);
#else
            HumanResolve.OnNext(__instance.human);
#endif
    }
    #endregion

    #region Coordinate Definitions
    partial class CoordLoadTrack
    {
        static Subject<(HumanDataCoordinate, ZipArchive)> LoadComplete = new();
        internal static IObservable<(HumanDataCoordinate, ZipArchive)> OnLoadComplete => LoadComplete.AsObservable();
        CoordLoadTrack Track;
        protected CoordLoadTrack() => Track = this;
        protected CoordLoadTrack(CoordLoadTrack track) => Track = track;
        protected virtual CoordLoadTrack Process(Il2CppReader reader) => Track;
        protected virtual CoordLoadTrack Process(HumanDataCoordinate data) => Track;
        protected void Complete(HumanDataCoordinate data, MemoryStream stream) =>
            LoadComplete.OnNext((data, new ZipArchive(stream, ZipArchiveMode.Update)));
        internal static readonly CoordLoadTrack Ignore = new CoordLoadTrack();
        internal static readonly CoordLoadTrack Aware = new CoordLoadAware();
    }
    class CoordLoadAware : CoordLoadTrack
    {
        static Action Seek(Il2CppReader reader) =>
            () => reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
        static MemoryStream ToStream(Il2CppReader reader) =>
            Extension.Extract(PngFile.LoadPngBytes(reader));
        protected override CoordLoadTrack Process(Il2CppReader reader) =>
            new CoordLoadResolve(ToStream(reader.With(Seek(reader))).With(Seek(reader)));
    }
    class CoordLoadResolve : CoordLoadTrack
    {
        MemoryStream Stream;
        internal CoordLoadResolve(MemoryStream stream) : base(Aware) => Stream = stream;
        protected override CoordLoadTrack Process(HumanDataCoordinate data) =>
            Aware.With(F.Apply(Complete, data, Stream));
    }
    partial class CoordLoadTrack
    {
        static CoordLoadTrack Current = Aware;
        static Subject<CoordLoadTrack> ModeUpdate = new();
        internal static IObservable<CoordLoadTrack> OnModeUpdate =>
            ModeUpdate.AsObservable().DistinctUntilChanged();
        internal static CoordLoadTrack Mode
        {
            get => Current; set => ModeUpdate.OnNext(Current = value);
        }
        internal static void Resolve(Il2CppReader reader) =>
            Current = Current.Process(reader);
        internal static void Resolve(HumanDataCoordinate data) =>
            Current = Current.Process(data);
    }
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            CoordLoadTrack.Resolve(br);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void HumanDataCoordinateLoadBytesPostfix(HumanDataCoordinate __instance) =>
            CoordLoadTrack.Resolve(__instance);
    }
    class CoordLimitTrack : IDisposable
    {
        internal IObservable<(Human, CoordLimit)> OnResolve;
        CoordLimit Limit = CoordLimit.None;
        Il2CppBytes Bytes = new Il2CppBytes([]);
        HumanDataBodyMakeup Body; 
        HumanDataFaceMakeup Face; 
        HumanDataHair Hair; 
        HumanDataClothes Clothes; 
        HumanDataAccessory Acs;
        CompositeDisposable Subscription;
        CoordLimitTrack() =>
            OnResolve = Hooks.OnHumanCoordinateResolve.Where(Match).FirstAsync().Select(human => (human, Limit));
        internal CoordLimitTrack(HumanDataCoordinate data) : this() =>
            (Body, Face, Hair, Clothes, Acs, Subscription) =
                (data.BodyMakeup, data.FaceMakeup, data.Hair, data.Clothes, data.Accessory, [
                    Hooks.OnHumanDataBodyMakeupCopy
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataFaceMakeupCopy
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataHairCopy
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataClothesCopy
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataAccessoryCopy
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataCoordinateSaveBytes
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    Hooks.OnHumanDataCoordinateLoadBytes
                        .Where(Match).Select(tuple => tuple.Item2).Subscribe(Resolve),
                    OnResolve.Subscribe(F.Ignoring<(Human,CoordLimit)>(F.DoNothing), Dispose)
                ]);
        bool Match((HumanDataBodyMakeup, HumanDataBodyMakeup) tuple) => Body == tuple.Item1;
        bool Match((HumanDataFaceMakeup, HumanDataFaceMakeup) tuple) => Face == tuple.Item1;
        bool Match((HumanDataHair, HumanDataHair) tuple) => Hair == tuple.Item1;
        bool Match((HumanDataClothes, HumanDataClothes) tuple) => Clothes == tuple.Item1;
        bool Match((HumanDataAccessory, HumanDataAccessory) tuple) => Acs == tuple.Item1;
        bool Match((HumanDataCoordinate, Il2CppBytes) tuple) => Match(tuple.Item1);
        bool Match((Il2CppBytes, HumanDataCoordinate) tuple) => Bytes == tuple.Item1;
        bool Match(Human human) => Match(human.coorde.Now);
        bool Match(HumanDataCoordinate data) =>
            (Body == data.BodyMakeup) || (Face == data.FaceMakeup) || (Hair == data.Hair) || (Clothes == data.Clothes) || (Acs == data.Accessory);
        void Resolve(HumanDataBodyMakeup data) => (Body, Limit) = (data, Limit | CoordLimit.BodyMakeup);
        void Resolve(HumanDataFaceMakeup data) => (Face, Limit) = (data, Limit | CoordLimit.FaceMakeup);
        void Resolve(HumanDataHair data) => (Hair, Limit) = (data, Limit | CoordLimit.Hair);
        void Resolve(HumanDataClothes data) => (Clothes, Limit) = (data, Limit | CoordLimit.Clothes);
        void Resolve(HumanDataAccessory data) => (Acs, Limit) = (data, Limit | CoordLimit.Accessory);
        void Resolve(Il2CppBytes data) => Bytes = data;
        void Resolve(HumanDataCoordinate data) =>
            (Body, Face, Hair, Clothes, Acs) = (data.BodyMakeup, data.FaceMakeup, data.Hair, data.Clothes, data.Accessory);
        public void Dispose() => Subscription.Dispose();
    }
    public static partial class Extension
    {
        internal static IObservable<(CoordLimitTrack, HumanDataCoordinate, ZipArchive)> OnTrackCoord =>
            CoordLoadTrack.OnLoadComplete.Select(tuple => (new CoordLimitTrack(tuple.Item1), tuple.Item1, tuple.Item2));
    }
    public static partial class Extension<T,U>
    {
        internal static IObservable<(CoordLimitTrack, HumanDataCoordinate, U)> OnCoordLimitTrack =>
            Extension.OnTrackCoord.Select(tuple => (tuple.Item1, tuple.Item2, LoadCoord(tuple.Item3)));
        internal static IObservable<(Human, CoordLimit, U)> OnLoadCoordInternal =>
            OnCoordLimitTrack.SelectMany(tuple => tuple.Item1.OnResolve.Select(pair => (pair.Item1, pair.Item2, tuple.Item3)));
    }
    static partial class Hooks
    {
        static Subject<(HumanDataBodyMakeup, HumanDataBodyMakeup)> HumanDataBodyMakeupCopy = new();
        static Subject<(HumanDataFaceMakeup, HumanDataFaceMakeup)> HumanDataFaceMakeupCopy = new();
        static Subject<(HumanDataHair, HumanDataHair)> HumanDataHairCopy = new();
        static Subject<(HumanDataClothes, HumanDataClothes)> HumanDataClothesCopy = new();
        static Subject<(HumanDataAccessory, HumanDataAccessory)> HumanDataAccessoryCopy = new();
        static Subject<(HumanDataCoordinate, Il2CppBytes)> HumanDataCoordinateSaveBytes = new();
        static Subject<(Il2CppBytes, HumanDataCoordinate)> HumanDataCoordinateLoadBytes = new();
        static Subject<Human> HumanCoordinateResolve = new();
        internal static IObservable<(HumanDataBodyMakeup, HumanDataBodyMakeup)> OnHumanDataBodyMakeupCopy => HumanDataBodyMakeupCopy.AsObservable();
        internal static IObservable<(HumanDataFaceMakeup, HumanDataFaceMakeup)> OnHumanDataFaceMakeupCopy => HumanDataFaceMakeupCopy.AsObservable();
        internal static IObservable<(HumanDataHair, HumanDataHair)> OnHumanDataHairCopy => HumanDataHairCopy.AsObservable();
        internal static IObservable<(HumanDataClothes, HumanDataClothes)> OnHumanDataClothesCopy => HumanDataClothesCopy.AsObservable();
        internal static IObservable<(HumanDataAccessory, HumanDataAccessory)> OnHumanDataAccessoryCopy => HumanDataAccessoryCopy.AsObservable();
        internal static IObservable<(HumanDataCoordinate, Il2CppBytes)> OnHumanDataCoordinateSaveBytes => HumanDataCoordinateSaveBytes.AsObservable();
        internal static IObservable<(Il2CppBytes, HumanDataCoordinate)> OnHumanDataCoordinateLoadBytes => HumanDataCoordinateLoadBytes.AsObservable();
        internal static IObservable<Human> OnHumanCoordinateResolve => HumanCoordinateResolve.AsObservable();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveBytes))]
        static void HumanDataCoordinateSaveBytesPostfix(HumanDataCoordinate __instance, Il2CppBytes __result) =>
            HumanDataCoordinateSaveBytes.OnNext((__instance, __result));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void HumanDataCoordinateLoadBytesPostfix(HumanDataCoordinate __instance, Il2CppBytes data) =>
            HumanDataCoordinateLoadBytes.OnNext((data, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataBodyMakeup), nameof(HumanDataBodyMakeup.Copy))]
        static void HumanDataBodyMakeupCopyPostfix(HumanDataBodyMakeup __instance, HumanDataBodyMakeup src) =>
            HumanDataBodyMakeupCopy.OnNext((src, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataFaceMakeup), nameof(HumanDataFaceMakeup.Copy))]
        static void HumanDataFaceMakeupCopyPostfix(HumanDataFaceMakeup __instance, HumanDataFaceMakeup src) =>
            HumanDataFaceMakeupCopy.OnNext((src, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataHair), nameof(HumanDataHair.Copy))]
        static void HumanDataHairCopyPostfix(HumanDataHair __instance, HumanDataHair src) =>
            HumanDataHairCopy.OnNext((src, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataClothes), nameof(HumanDataClothes.CopyBase))]
        static void HumanDataClothesCopyPostfix(HumanDataClothes __instance, HumanDataClothes src) =>
            HumanDataClothesCopy.OnNext((src, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataAccessory), nameof(HumanDataAccessory.Copy))]
        static void HumanDataAccessoryCopyPostfix(HumanDataAccessory __instance, HumanDataAccessory src) =>
            HumanDataAccessoryCopy.OnNext((src, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.OnUpdateCoordinate))]
        static void HumanBodyOnUpdateCoordinatePrefix(HumanBody __instance) =>
#if Aicomi
            HumanCoordinateResolve.OnNext(__instance._human);
#else
            HumanCoordinateResolve.OnNext(__instance.human);
#endif

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.OnUpdateCoordinate))]
        static void HumanFaceOnUpdateCoordinatePrefix(HumanFace __instance) =>
#if Aicomi
            HumanCoordinateResolve.OnNext(__instance._human);
#else
            HumanCoordinateResolve.OnNext(__instance.human);
#endif

        [HarmonyPrefix, HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPrefix(Human __instance) =>
            HumanCoordinateResolve.OnNext(__instance);
    }
    #endregion
}