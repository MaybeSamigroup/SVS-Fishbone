using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
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
using Cysharp.Threading.Tasks;

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
    }
    class HumansStorage<T> : Storage<T, Human>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        Dictionary<Human, T> Humans = new(Il2CppEquals.Instance);
        public T Get(Human human) => Humans.GetValueOrDefault(human, new());
        public void Set(Human human, T value) => Humans[human] = value; 
        internal void Remove(Human human) => Humans.Remove(human);
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
    }
    public static partial class Extension<T>
    {
        static IObservable<(CharaCopyTrack Track, HumanData Data, T Value)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, T Value)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve.Select(human => (human, tuple.Value)));
        internal static void Remove(Human human) => Storage.Remove(human);
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
        internal static Subject<string> LoadScene = new();
        internal static Subject<(string Path, int Offset)> ImportScene = new();
        internal static Subject<(int Index, ObjectInfo Info)> PreprocessObject = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPrefix(string _path) =>
            LoadScene.OnNext(_path);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPrefix(SceneInfo __instance, string _path) =>
            ImportScene.OnNext((_path, __instance.ObjectInfos.Count));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPostfix(SceneInfo __instance) =>
            Enumerable.Range(0, __instance.ObjectInfos.Count)
                .ForEach(index => PreprocessObject.OnNext((index, __instance?.ObjectInfos[index])));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPostfix(SceneInfo __instance) =>
            __instance?.DicImport?.Yield()?.Select(entry => entry.Value)
                .ForEach(value => PreprocessObject.OnNext((__instance.ObjectInfos.IndexOf(value), value)));

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

        static void TryResolve<T>(int index, T value, Action<(int, T)> action) =>
            (index >= 0).Maybe(F.Apply(action, (index, value)));

        internal static Subject<(int Index, ObjectCtrlInfo Value)> ObjectCtrlResolve = new();
        internal static Subject<(int Index, ObjectCtrlInfo Value)> ObjectCtrlAttach = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.AddNode), typeof(ObjectCtrlInfo), typeof(string), typeof(TreeNodeObject))]
        static void TreeNodeCtrlAddNodePrefix(ObjectCtrlInfo _objectCtrl) =>
            TryResolve(_objectCtrl.ToIndex(), _objectCtrl, ObjectCtrlResolve.OnNext);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.AddNode), typeof(ObjectCtrlInfo), typeof(string), typeof(TreeNodeObject))]
        static void TreeNodeCtrlAddNodePostfix(ObjectCtrlInfo _objectCtrl) =>
            TryResolve(_objectCtrl.ToIndex(), _objectCtrl, ObjectCtrlAttach.OnNext);

        internal static Subject<(int, OCIItem)> DeleteItem = new();
        internal static Subject<(int, OCILight)> DeleteLight = new();
        internal static Subject<(int, OCIRoute)> DeleteRoute = new();
        internal static Subject<(int, OCICamera)> DeleteCamera = new();
        internal static Subject<(int, OCIFolder)> DeleteFolder = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIItem), nameof(OCIItem.OnDelete))]
        static void OCIItemOnDelete(OCIItem __instance) =>
            TryResolve(__instance.ToIndex(), __instance, DeleteItem.OnNext);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCILight), nameof(OCILight.OnDelete))]
        static void OCILightOnDelete(OCILight __instance) =>
            TryResolve(__instance.ToIndex(), __instance, DeleteLight.OnNext);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIRoute), nameof(OCIRoute.OnDelete))]
        static void OCIRouteOnDelete(OCIRoute __instance) =>
            TryResolve(__instance.ToIndex(), __instance, DeleteRoute.OnNext);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCICamera), nameof(OCICamera.OnDelete))]
        static void OCICameraOnDelete(OCICamera __instance) =>
            TryResolve(__instance.ToIndex(), __instance, DeleteCamera.OnNext);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIFolder), nameof(OCIFolder.OnDelete))]
        static void OCIFolderOnDelete(OCIFolder __instance) =>
            TryResolve(__instance.ToIndex(), __instance, DeleteFolder.OnNext);

        internal static Subject<(int, OCIItem)> PrepareSaveItem = new();
        internal static Subject<(int, OCILight)> PrepareSaveLight = new();
        internal static Subject<(int, OCIRoute)> PrepareSaveRoute = new();
        internal static Subject<(int, OCICamera)> PrepareSaveCamera = new();
        internal static Subject<(int, OCIFolder)> PrepareSaveFolder = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ObjectCtrlInfo), nameof(ObjectCtrlInfo.OnSavePreprocessing))]
        static void ObjectCtrlInfoOnSavePreprocessingPostfix(ObjectCtrlInfo __instance) =>
            (__instance.objectInfo.Kind switch {
                1 => F.Apply(TryResolve, __instance.ToIndex(), new OCIItem(__instance.Pointer), PrepareSaveItem.OnNext),
                2 => F.Apply(TryResolve, __instance.ToIndex(), new OCILight(__instance.Pointer), PrepareSaveLight.OnNext),
                4 => F.Apply(TryResolve, __instance.ToIndex(), new OCIRoute(__instance.Pointer), PrepareSaveRoute.OnNext),
                5 => F.Apply(TryResolve, __instance.ToIndex(), new OCICamera(__instance.Pointer), PrepareSaveCamera.OnNext),
                3 => F.Apply(TryResolve, __instance.ToIndex(), new OCIFolder(__instance.Pointer), PrepareSaveFolder.OnNext),
                _ => F.DoNothing
            }).Invoke();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Save), typeof(string), typeof(Il2CppBytes))]
        static void ScenInfoSavePostfix(string _path) =>
            UniTask.NextFrame().ContinueWith(F.Apply(Extension.SaveObjects, _path));
    }
    public static partial class Extension
    {
        internal static ZipArchive Extract(string path) =>
            new ZipArchive(Extract(File.ReadAllBytes(path)), ZipArchiveMode.Update);

        internal static void SaveObjects(string path) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(SaveScene.OnNext)));

        static Subject<ZipArchive> SaveScene = new();
    }
    #endregion

    #region Item
    public static partial class ItemExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(ItemExtensionAttribute<T>))
                is ItemExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveValues(archive, Json<Dictionary<int, V>>.Load(Plugin.Instance.Log.LogError, entry.Open())
                .ToDictionary(entry => entry.Key, entry => map(entry.Value)));

        static void Cleanup(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry).Maybe(entry.Delete);

        static void SaveValues(ZipArchive archive, Dictionary<int, T> value) =>
            SerializeItems(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        static Dictionary<int, T> LoadValues(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry) ? DeserializeItems(entry.Open()) : new();

        static Dictionary<int, T> LoadValues((ZipArchive Archive, int Offset) values) =>
            LoadValues(values.Archive).ToDictionary(entry => entry.Key + values.Offset, entry => entry.Value);

        static IObservable<(int Index, T Value)> OnLoadValue =>
            Extension.OnLoadScene.Select(LoadValues).Merge(Extension.OnImportScene.Select(LoadValues))
                .SelectMany(entries => entries.Select(entry => (entry.Key, entry.Value)));

        internal static void SaveScene(ZipArchive archive) => SaveValues(archive, Storage);
    }
    #endregion

    #region Light
    public static partial class LightExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(LightExtensionAttribute<T>))
                is LightExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveValues(archive, Json<Dictionary<int, V>>.Load(Plugin.Instance.Log.LogError, entry.Open())
                .ToDictionary(entry => entry.Key, entry => map(entry.Value)));

        static void Cleanup(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry).Maybe(entry.Delete);

        static void SaveValues(ZipArchive archive, Dictionary<int, T> value) =>
            SerializeLights(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        static Dictionary<int, T> LoadValues(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry) ? DeserializeLights(entry.Open()) : new();

        static Dictionary<int, T> LoadValues((ZipArchive Archive, int Offset) values) =>
            LoadValues(values.Archive).ToDictionary(entry => entry.Key + values.Offset, entry => entry.Value);

        static IObservable<(int Index, T Value)> OnLoadValue =>
            Extension.OnLoadScene.Select(LoadValues).Merge(Extension.OnImportScene.Select(LoadValues))
                .SelectMany(entries => entries.Select(entry => (entry.Key, entry.Value)));

        internal static void SaveScene(ZipArchive archive) => SaveValues(archive, Storage);
    }
    #endregion

    #region Route
    public static partial class RouteExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(RouteExtensionAttribute<T>))
                is RouteExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveValues(archive, Json<Dictionary<int, V>>.Load(Plugin.Instance.Log.LogError, entry.Open())
                .ToDictionary(entry => entry.Key, entry => map(entry.Value)));

        static void Cleanup(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry).Maybe(entry.Delete);

        static void SaveValues(ZipArchive archive, Dictionary<int, T> value) =>
            SerializeRoutes(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        static Dictionary<int, T> LoadValues(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry) ? DeserializeRoutes(entry.Open()) : new();

        static Dictionary<int, T> LoadValues((ZipArchive Archive, int Offset) values) =>
            LoadValues(values.Archive).ToDictionary(entry => entry.Key + values.Offset, entry => entry.Value);

        static IObservable<(int Index, T Value)> OnLoadValue =>
            Extension.OnLoadScene.Select(LoadValues).Merge(Extension.OnImportScene.Select(LoadValues))
                .SelectMany(entries => entries.Select(entry => (entry.Key, entry.Value)));

        internal static void SaveScene(ZipArchive archive) => SaveValues(archive, Storage);
    }
    #endregion

    #region Camera
    public static partial class CameraExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(CameraExtensionAttribute<T>))
                is CameraExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveValues(archive, Json<Dictionary<int, V>>.Load(Plugin.Instance.Log.LogError, entry.Open())
                .ToDictionary(entry => entry.Key, entry => map(entry.Value)));

        static void Cleanup(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry).Maybe(entry.Delete);

        static void SaveValues(ZipArchive archive, Dictionary<int, T> value) =>
            SerializeCameras(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        static Dictionary<int, T> LoadValues(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry) ? DeserializeCameras(entry.Open()) : new();

        static Dictionary<int, T> LoadValues((ZipArchive Archive, int Offset) values) =>
            LoadValues(values.Archive).ToDictionary(entry => entry.Key + values.Offset, entry => entry.Value);

        static IObservable<(int Index, T Value)> OnLoadValue =>
            Extension.OnLoadScene.Select(LoadValues).Merge(Extension.OnImportScene.Select(LoadValues))
                .SelectMany(entries => entries.Select(entry => (entry.Key, entry.Value)));

        internal static void SaveScene(ZipArchive archive) => SaveValues(archive, Storage);
    }
    #endregion

    #region Folder
    public static partial class FolderExtension<T>
    {
        static readonly string Path =
            typeof(T).GetCustomAttribute(typeof(FolderExtensionAttribute<T>))
                is FolderExtensionAttribute<T> extension ? extension.Path :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");

        static void Translate<V>(Func<V, T> map, ZipArchive archive, ZipArchiveEntry entry) where V : new() =>
            SaveValues(archive, Json<Dictionary<int, V>>.Load(Plugin.Instance.Log.LogError, entry.Open())
                .ToDictionary(entry => entry.Key, entry => map(entry.Value)));

        static void Cleanup(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry).Maybe(entry.Delete);

        static void SaveValues(ZipArchive archive, Dictionary<int, T> value) =>
            SerializeFolders(archive.With(Cleanup).CreateEntry(Path).Open(), value);

        static Dictionary<int, T> LoadValues(ZipArchive archive) =>
            archive.TryGetEntry(Path, out var entry) ? DeserializeFolders(entry.Open()) : new();

        static Dictionary<int, T> LoadValues((ZipArchive Archive, int Offset) values) =>
            LoadValues(values.Archive).ToDictionary(entry => entry.Key + values.Offset, entry => entry.Value);

        static IObservable<(int Index, T Value)> OnLoadValue =>
            Extension.OnLoadScene.Select(LoadValues).Merge(Extension.OnImportScene.Select(LoadValues))
                .SelectMany(entries => entries.Select(entry => (entry.Key, entry.Value)));

        internal static void SaveScene(ZipArchive archive) => SaveValues(archive, Storage);
    }
    #endregion

    public static partial class Extension
    {
        internal static IDisposable[] Initialize() => [
#if DEBUG
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load")),
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load")),
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change")),

            OnPrepareSaveItem.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save item")),
            OnPrepareSaveLight.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save light")),
            OnPrepareSaveRoute.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save route")),
            OnPrepareSaveCamera.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save camera")),
            OnPrepareSaveFolder.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save folder")),

            OnPreprocessItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess item: {entry.Index}")),
            OnPreprocessLight.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess light: {entry.Index}")),
            OnPreprocessRoute.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess route: {entry.Index}")),
            OnPreprocessCamera.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess camera: {entry.Index}")),
            OnPreprocessFolder.Subscribe(entry => Plugin.Instance.Log.LogDebug($"preprocess folder: {entry.Index}")),

            OnLoadItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"item load: {entry.Index}")),
            OnLoadLight.Subscribe(entry => Plugin.Instance.Log.LogDebug($"light load: {entry.Index}")),
            OnLoadRoute.Subscribe(entry => Plugin.Instance.Log.LogDebug($"route load: {entry.Index}")),
            OnLoadCamera.Subscribe(entry => Plugin.Instance.Log.LogDebug($"camera load: {entry.Index}")),
            OnLoadFolder.Subscribe(entry => Plugin.Instance.Log.LogDebug($"folder load: {entry.Index}")),

            OnAttachItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"attach item: {entry.Index}")),
            OnAttachLight.Subscribe(entry => Plugin.Instance.Log.LogDebug($"attach light: {entry.Index}")),
            OnAttachRoute.Subscribe(entry => Plugin.Instance.Log.LogDebug($"attach route: {entry.Index}")),
            OnAttachCamera.Subscribe(entry => Plugin.Instance.Log.LogDebug($"attach camera: {entry.Index}")),
            OnAttachFolder.Subscribe(entry => Plugin.Instance.Log.LogDebug($"attach folder: {entry.Index}")),

            OnDeleteItem.Subscribe(entry => Plugin.Instance.Log.LogDebug($"delete item: {entry.Index}")),
            OnDeleteLight.Subscribe(entry => Plugin.Instance.Log.LogDebug($"delete light: {entry.Index}")),
            OnDeleteRoute.Subscribe(entry => Plugin.Instance.Log.LogDebug($"delete route: {entry.Index}")),
            OnDeleteCamera.Subscribe(entry => Plugin.Instance.Log.LogDebug($"delete camera: {entry.Index}")),
            OnDeleteFolder.Subscribe(entry => Plugin.Instance.Log.LogDebug($"delete folder: {entry.Index}")),
#endif
        ];
    }
}