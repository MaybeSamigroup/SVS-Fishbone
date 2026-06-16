using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Character;
using DigitalCraft;
using HarmonyLib;
using CoastalSmell;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;

namespace Fishbone
{
    class HumansStorage<T,U> : Storage<T, U, Human>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new() where U : CoordinateExtension<U>, new()
    {
        Dictionary<Human, T> Humans = new(Il2CppEquals.Instance);
        public T Get(Human human) => Humans.GetValueOrDefault(human, new ());
        public void Set(Human human, T value) => Humans[human] = value; 
        public U GetNowCoordinate(Human human) => Get(human).Get(human.data.Status.coordinateType);
        public void SetNowCoordinate(Human human, U value) => Humans[human] = Get(human).Merge(human.data.Status.coordinateType, value);
        internal void Remove(Human human) => Humans.Remove(human);
        internal void Clear() => Humans.Clear();
    }
    class HumansStorage<T> : Storage<T, Human>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        Dictionary<Human, T> Humans = new(Il2CppEquals.Instance);
        public T Get(Human human) => Humans.GetValueOrDefault(human, new());
        public void Set(Human human, T value) => Humans[human] = value; 
        internal void Remove(Human human) => Humans.Remove(human);
        internal void Clear() => Humans.Clear();
    }

    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppWriter), typeof(bool))]
        static void HumanDataSaveFilePrefix(HumanData __instance, Il2CppWriter bw) =>
            (__instance.PngData is not null).Maybe(() => bw.Write(__instance.PngData));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), nameof(MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), nameof(MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPrefix() => CoordLoadTrack.Mode = CoordLoadTrack.Ignore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), nameof(MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(MPCharCtrl.CostumeInfo), nameof(MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPostfix() => CoordLoadTrack.Mode = CoordLoadTrack.Aware;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIChar), nameof(OCIChar.OnSavePreprocessing))]
        static void OCICharOnSavePreprocessingPostfix(OCIChar __instance) =>
            Extension.Save(__instance.charInfo);

        internal static Subject<Human> DeleteChara = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCICharFemale), nameof(OCICharFemale.OnDelete))]
        static void OCICharFemaleOnDelete(OCICharFemale __instance) =>
            (__instance.charInfo is not null).Maybe(() => DeleteChara.OnNext(__instance.charInfo));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCICharMale), nameof(OCICharMale.OnDelete))]
        static void OCICharMaleOnDelete(OCICharMale __instance) =>
            (__instance.charInfo is not null).Maybe(() => DeleteChara.OnNext(__instance.charInfo));
    } 

    public static partial class Extension
    {
        static Subject<Human> PrepareSaveChara = new();
        static Subject<(ZipArchive, Human)> SaveChara = new();
        internal static void Save(Human human) =>
            Implant(human.data, ToBinary(SaveChara, human.With(PrepareSaveChara.OnNext)));
    }

    public static partial class Extension<T, U>
    {
        internal static void SaveChara((ZipArchive Archive, Human Human) tuple) =>
            SaveChara(tuple.Archive, Humans[tuple.Human]);
    }

    public static partial class Extension<T>
    {
        internal static void SaveChara((ZipArchive Archive, Human Human) tuple) =>
            SaveChara(tuple.Archive, Humans[tuple.Human]);
    }

    class CharaCopyTrack : IDisposable
    {
        protected CompositeDisposable Subscription;
        protected IObservable<HumanData> OnDataUpdate;
        internal IObservable<Human> OnResolve;
        HumanData Data;
        CharaCopyTrack() =>
            (OnDataUpdate, OnResolve) = (
                Hooks.OnHumanDataCopy.Where(Match).Select(tuple => tuple.Dst),
                Hooks.OnHumanResolve.Where(Match).FirstAsync());
        internal CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()),
                OnResolve.Subscribe(_ => Dispose()),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData Data, T Value) tuple) => Il2CppEquals.Apply(Data, tuple.Data);
        bool Match(Human human) => Il2CppEquals.Apply(Data, human.data); 
        void Resolve(HumanData value) => Il2CppEquals.Apply(Data, value);
        public void Dispose() => Subscription.Dispose();
    }

    public static partial class Extension
    {
        internal static IObservable<(CharaCopyTrack Track, HumanData Data, ZipArchive Value)> OnTrackChara =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Select(tuple => (new CharaCopyTrack(tuple.Data), tuple.Data, tuple.Archive));
    }
    public static partial class Extension<T, U>
    {
        static IObservable<(CharaCopyTrack Track, HumanData Data, T Value)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, T Value)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve.Select(human => (human, tuple.Value)));
        internal static void Remove(Human human) => Storage.Remove(human);
        internal static void Clear() => Storage.Clear();
    }
    public static partial class Extension<T>
    {
        static IObservable<(CharaCopyTrack Track, HumanData Data, T Value)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, T Value)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve.Select(human => (human, tuple.Value)));
        internal static void Remove(Human human) => Storage.Remove(human);
        internal static void Clear() => Storage.Clear();
    }
    static partial class Hooks
    {
        static Subject<(Human, int)> ChangeCoordinate = new();
        internal static IObservable<(Human, int)> OnChangeCoordinate => ChangeCoordinate.AsObservable();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(ChangeCoordinate.OnNext, (__instance.human, (int)type)));
    }

    #region Object
    static partial class Hooks
    {
        internal static Subject<Unit> SceneInit = new();
        internal static Subject<(ZipArchive Archive, int Offset)> LoadScene = new();
        internal static Subject<(ZipArchive Archive, int Offset)> ImportScene = new();
        internal static Subject<IEnumerable<(int[] Indices, ObjectInfo Info)>> Preprocess = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Init), [])]
        static void SceneInfoInitPrefix() => SceneInit.OnNext(Unit.Default);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPrefix(string _path) =>
            LoadScene.With(F.Apply(SceneInit.OnNext, Unit.Default)).OnNext((Extension.Extract(_path), 0));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPrefix(SceneInfo __instance, string _path) =>
            ImportScene.OnNext((Extension.Extract(_path), __instance.ObjectInfos.Count));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]

        static void SceneInfoLoadPostfix(SceneInfo __instance) =>
            Preprocess.OnNext(Enumerable.Range(0, __instance.ObjectInfos.Count)
                .SelectMany(index => Extension.Deconstruct([index], __instance.ObjectInfos[index])));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPostfix(SceneInfo __instance) =>
            Preprocess.OnNext(__instance.DicImport.Yield()
                .SelectMany(entry => Extension.Deconstruct([__instance.ObjectInfos.IndexOf(entry.Value)], entry.Value)));

        internal static Subject<OIItemInfo> TrackItem = new();
        internal static Subject<OILightInfo> TrackLight = new();
        internal static Subject<OIRouteInfo> TrackRoute = new();
        internal static Subject<OICameraInfo> TrackCamera = new();
        internal static Subject<OIFolderInfo> TrackFolder = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OIItemInfo), nameof(OIItemInfo.Load),
            typeof(Il2CppReader), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool))]
        static void OIItemInfoLoad(OIItemInfo __instance) => TrackItem.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OILightInfo), nameof(OILightInfo.Load),
            typeof(Il2CppReader), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool))]
        static void OILightInfoLoad(OILightInfo __instance) => TrackLight.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OIRouteInfo), nameof(OIRouteInfo.Load),
            typeof(Il2CppReader), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool))]
        static void OIRouteInfoLoad(OIRouteInfo __instance) => TrackRoute.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OICameraInfo), nameof(OICameraInfo.Load),
            typeof(Il2CppReader), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool))]
        static void OICameraInfoLoad(OICameraInfo __instance) => TrackCamera.OnNext(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OIFolderInfo), nameof(OIFolderInfo.Load),
            typeof(Il2CppReader), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool))]
        static void OIFolderInfoLoad(OIFolderInfo __instance) => TrackFolder.OnNext(__instance);

        internal static Subject<ObjectCtrlInfo> AddObjectCtrl = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.AddNode), typeof(ObjectCtrlInfo), typeof(string), typeof(TreeNodeObject))]
        static void TreeNodeCtrlAddNodePostfix(ObjectCtrlInfo _objectCtrl) => AddObjectCtrl.OnNext(_objectCtrl);

        internal static Subject<OCIItem> DeleteItem = new();
        internal static Subject<OCILight> DeleteLight = new();
        internal static Subject<OCIRoute> DeleteRoute = new();
        internal static Subject<OCICamera> DeleteCamera = new();
        internal static Subject<OCIFolder> DeleteFolder = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIItem), nameof(OCIItem.OnDelete))]
        static void OCIItemOnDelete(OCIItem __instance) => DeleteItem.OnNext(__instance);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCILight), nameof(OCILight.OnDelete))]
        static void OCILightOnDelete(OCILight __instance) => DeleteLight.OnNext(__instance);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIFolder), nameof(OCIFolder.OnDelete))]
        static void OCIFolderOnDelete(OCIFolder __instance) => DeleteFolder.OnNext(__instance);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIRoute), nameof(OCIRoute.OnDelete))]
        static void OCIRouteOnDelete(OCIRoute __instance) => DeleteRoute.OnNext(__instance);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCICamera), nameof(OCICamera.OnDelete))]
        static void OCICameraOnDelete(OCICamera __instance) => DeleteCamera.OnNext(__instance);

        internal static Subject<OCIItem> PrepareSaveItem = new();
        internal static Subject<OCILight> PrepareSaveLight = new();
        internal static Subject<OCIFolder> PrepareSaveFolder = new();
        internal static Subject<OCIRoute> PrepareSaveRoute = new();
        internal static Subject<OCICamera> PrepareSaveCamera = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ObjectCtrlInfo), nameof(ObjectCtrlInfo.OnSavePreprocessing))]
        static void ObjectCtrlInfoOnSavePreprocessingPostfix(ObjectCtrlInfo __instance) =>
            (__instance.objectInfo.Kind switch {
                1 => F.Apply(PrepareSaveItem.OnNext, new OCIItem(__instance.Pointer)),
                2 => F.Apply(PrepareSaveLight.OnNext, new OCILight(__instance.Pointer)),
                3 => F.Apply(PrepareSaveFolder.OnNext, new OCIFolder(__instance.Pointer)),
                4 => F.Apply(PrepareSaveRoute.OnNext, new OCIRoute(__instance.Pointer)),
                5 => F.Apply(PrepareSaveCamera.OnNext, new OCICamera(__instance.Pointer)),
                _ => F.DoNothing
            }).Invoke();

        internal static Subject<IEnumerable<(int[] Indices, ObjectInfo Info)>> SaveObjects = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Save), typeof(string), typeof(Il2CppBytes))]
        static void ScenInfoSavePrefix(SceneInfo __instance) =>
            SaveObjects.OnNext(Enumerable.Range(0, __instance.ObjectInfos.Count)
                .SelectMany(index => Extension.Deconstruct([index], __instance.ObjectInfos[index])));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Save), typeof(string), typeof(Il2CppBytes))]
        static void ScenInfoSavePostfix(string _path) =>
            F.Apply(Extension.SaveObjects, _path).DelayFrames(1);
    }
    public static partial class Extension
    {
        internal static ZipArchive Extract(string path) =>
            new ZipArchive(Extract(File.ReadAllBytes(path)), ZipArchiveMode.Update);

        static int[] ApplyOffset(int[] indices, int offset) => offset == 0 ? indices : [indices[0] - offset, ..indices[1..]];

        static IEnumerable<(ZipArchive Archive, int[] Indices, T Info)>
            Transform<T>(IEnumerable<(int[] Indices, ObjectInfo Info)> values,
                ZipArchive archive, int offset, Func<ObjectInfo, bool> filter, Func<ObjectInfo, T> map) =>
            values.Where(value => filter(value.Info))
                .Select(value => (archive, ApplyOffset(value.Indices, offset), map(value.Info)));

        static IEnumerable<(ZipArchive Archive, int[] indices, OIItemInfo)> ToItems(
            IEnumerable<(int[] Indices, ObjectInfo Info)> values, ZipArchive archive, int offset) =>
            Transform(values, archive, offset, info => info.Kind == 1, info => new OIItemInfo(info.Pointer));

        static IEnumerable<(ZipArchive Archive, int[] indices, OILightInfo)> ToLights(
            IEnumerable<(int[] Indices, ObjectInfo Info)> values, ZipArchive archive, int offset) =>
            Transform(values, archive, offset, info => info.Kind == 2, info => new OILightInfo(info.Pointer));

        static IEnumerable<(ZipArchive Archive, int[] indices, OIFolderInfo)> ToFolders(
            IEnumerable<(int[] Indices, ObjectInfo Info)> values, ZipArchive archive, int offset) =>
            Transform(values, archive, offset, info => info.Kind == 3, info => new OIFolderInfo(info.Pointer));

        static IEnumerable<(ZipArchive Archive, int[] indices, OIRouteInfo)> ToRoutes(
            IEnumerable<(int[] Indices, ObjectInfo Info)> values, ZipArchive archive, int offset) =>
            Transform(values, archive, offset, info => info.Kind == 4, info => new OIRouteInfo(info.Pointer));

        static IEnumerable<(ZipArchive Archive, int[] indices, OICameraInfo)> ToCameras(
            IEnumerable<(int[] Indices, ObjectInfo Info)> values, ZipArchive archive, int offset) =>
            Transform(values, archive, offset, info => info.Kind == 5, info => new OICameraInfo(info.Pointer));

        internal static IEnumerable<(int[] Indices, ObjectInfo)> Deconstruct(int[] indices, ObjectInfo info) =>
            info.Kind switch
            {
                0 => Deconstruct(indices, new OICharInfo(info.Pointer)),
                1 => Deconstruct(indices, new OIItemInfo(info.Pointer)),
                2 => [(indices, info)],
                3 => Deconstruct(indices, new OIFolderInfo(info.Pointer)),
                4 => Deconstruct(indices, new OIRouteInfo(info.Pointer)),
                5 => [(indices, info)],
                _ => []
            };
        static IEnumerable<(int[] indices, ObjectInfo)> Deconstruct(int[] indices, ObjectInfo[] infos) =>
            infos.Index().SelectMany(entry => Deconstruct([.. indices, entry.Index], entry.Value));
        static IEnumerable<(int[] indices, ObjectInfo)> Deconstruct(int[] indices, OICharInfo info) =>
            info.Child.Yield().SelectMany(entry => Deconstruct([..indices, entry.Key], entry.Value.ToArray()));
        static IEnumerable<(int[] indices, ObjectInfo)> Deconstruct(int[] indices, OIItemInfo info) =>
            Deconstruct(indices, info.Child.ToArray()).Prepend((indices, info)); 
        static IEnumerable<(int[] indices, ObjectInfo)> Deconstruct(int[] indices, OIFolderInfo info) =>
            Deconstruct(indices, info.Child.ToArray()).Prepend((indices, info)); 
        static IEnumerable<(int[] indices, ObjectInfo)> Deconstruct(int[] indices, OIRouteInfo info) =>
            Deconstruct(indices, info.Child.ToArray()).Prepend((indices, info)); 
        static Subject<ZipArchive> SaveScene = new();
        internal static void SaveObjects(string path) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(SaveScene.OnNext)));
    }
    #endregion

    #region Item
    public static partial class ItemExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(ItemExtensionAttribute<T>))
                is ItemExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry, int[] indices) where V : new() =>
            SaveValue(archive, ToPath(indices), map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));
        static string ToPath(int[] indices) =>
            indices.Aggregate(Path, (path, index) => System.IO.Path.Combine(path, index.ToString()));
        static Action<ZipArchive> Cleanup(string path) =>
            archive => archive.TryGetEntry(path, out var entry).Maybe(entry.Delete);
        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.With(Cleanup(path)).CreateEntry(path).Open(), value);
        static T LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(ToPath(indices), out var entry) ? Deserialize(entry.Open()) : new();
        static IEnumerable<T> ToValue(OIItemInfo info) =>
            Items.Where(entry => entry.Key.objectInfo.Pointer == info.Pointer).Select(entry => entry.Value);
        internal static void SaveValue((ZipArchive Archive, int[] Indices, OIItemInfo Info) entry) =>
            ToValue(entry.Info).ForEach(Value => SaveValue(entry.Archive, ToPath(entry.Indices), Value));
    }
    #endregion

    #region Light
    public static partial class LightExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(LightExtensionAttribute<T>))
                is LightExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry, int[] indices) where V : new() =>
            SaveValue(archive, ToPath(indices), map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));
        static string ToPath(int[] indices) =>
            indices.Aggregate(Path, (path, index) => System.IO.Path.Combine(path, index.ToString()));
        static Action<ZipArchive> Cleanup(string path) =>
            archive => archive.TryGetEntry(path, out var entry).Maybe(entry.Delete);
        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.With(Cleanup(path)).CreateEntry(path).Open(), value);
        static T LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(ToPath(indices), out var entry) ? Deserialize(entry.Open()) : new();
        static IEnumerable<T> ToValue(OILightInfo info) =>
            Lights.Where(entry => entry.Key.objectInfo.Pointer == info.Pointer).Select(entry => entry.Value);
        internal static void SaveValue((ZipArchive Archive, int[] Indices, OILightInfo Info) entry) =>
            ToValue(entry.Info).ForEach(Value => SaveValue(entry.Archive, ToPath(entry.Indices), Value));
    }
    #endregion

    #region Folder
    public static partial class FolderExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(FolderExtensionAttribute<T>))
                is FolderExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry, int[] indices) where V : new() =>
            SaveValue(archive, ToPath(indices), map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));
        static string ToPath(int[] indices) =>
            indices.Aggregate(Path, (path, index) => System.IO.Path.Combine(path, index.ToString()));
        static Action<ZipArchive> Cleanup(string path) =>
            archive => archive.TryGetEntry(path, out var entry).Maybe(entry.Delete);
        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.With(Cleanup(path)).CreateEntry(path).Open(), value);
        static T LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(ToPath(indices), out var entry) ? Deserialize(entry.Open()) : new();
        static IEnumerable<T> ToValue(OIFolderInfo info) =>
            Folders.Where(entry => entry.Key.objectInfo.Pointer == info.Pointer).Select(entry => entry.Value);
        internal static void SaveValue((ZipArchive Archive, int[] Indices, OIFolderInfo Info) entry) =>
            ToValue(entry.Info).ForEach(Value => SaveValue(entry.Archive, ToPath(entry.Indices), Value));
    }

    #endregion

    #region Route
    public static partial class RouteExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(RouteExtensionAttribute<T>))
                is RouteExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry, int[] indices) where V : new() =>
            SaveValue(archive, ToPath(indices), map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));
        static string ToPath(int[] indices) =>
            indices.Aggregate(Path, (path, index) => System.IO.Path.Combine(path, index.ToString()));
        static Action<ZipArchive> Cleanup(string path) =>
            archive => archive.TryGetEntry(path, out var entry).Maybe(entry.Delete);
        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.With(Cleanup(path)).CreateEntry(path).Open(), value);
        static T LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(ToPath(indices), out var entry) ? Deserialize(entry.Open()) : new();
        static IEnumerable<T> ToValue(OIRouteInfo info) =>
            Routes.Where(entry => entry.Key.objectInfo.Pointer == info.Pointer).Select(entry => entry.Value);
        internal static void SaveValue((ZipArchive Archive, int[] Indices, OIRouteInfo Info) entry) =>
            ToValue(entry.Info).ForEach(Value => SaveValue(entry.Archive, ToPath(entry.Indices), Value));
    }
    #endregion

    #region Camera
    public static partial class CameraExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(CameraExtensionAttribute<T>))
                is CameraExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry, int[] indices) where V : new() =>
            SaveValue(archive, ToPath(indices), map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())));
        static string ToPath(int[] indices) =>
            indices.Aggregate(Path, (path, index) => System.IO.Path.Combine(path, index.ToString()));
        static Action<ZipArchive> Cleanup(string path) =>
            archive => archive.TryGetEntry(path, out var entry).Maybe(entry.Delete);
        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.With(Cleanup(path)).CreateEntry(path).Open(), value);
        static T LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(ToPath(indices), out var entry) ? Deserialize(entry.Open()) : new();
        static IEnumerable<T> ToValue(OICameraInfo info) =>
            Cameras.Where(entry => entry.Key.objectInfo.Pointer == info.Pointer).Select(entry => entry.Value);
        internal static void SaveValue((ZipArchive Archive, int[] Indices, OICameraInfo Info) entry) =>
            ToValue(entry.Info).ForEach(Value => SaveValue(entry.Archive, ToPath(entry.Indices), Value));
    }
    #endregion

    public static partial class Extension
    {
        internal static IDisposable[] Initialize() => [
#if DEBUG
            OnSceneInit.Subscribe(_ => Plugin.Instance.Log.LogInfo("scene initialized")),

            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load")),
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load")),
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change")),

            OnPreprocessItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess item: {string.Join(",", entry.Indices)}")),
            OnAddItem.Subscribe(entry => Plugin.Instance.Log.LogDebug("add item")),
            OnDeleteItem.Subscribe(entry => Plugin.Instance.Log.LogDebug("delete item")),
            OnPrepareSaveItem.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save item")),
            OnSaveItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"save item: {string.Join(",", entry.Indices)}")),
#endif
        ];
    }
}