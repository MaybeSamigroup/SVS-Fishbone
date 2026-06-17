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
using Entry = (int[] Indices, DigitalCraft.ObjectInfo Info);
using Scene = (
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICharInfo Info)> Charas,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIItemInfo Info)> Items,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OILightInfo Info)> Lights,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIFolderInfo Info)> Folders,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIRouteInfo Info)> Routes,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICameraInfo Info)> Cameras);

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
        internal static Subject<ZipArchive> LoadScene = new();
        internal static Subject<ZipArchive> ImportScene = new();
        internal static Subject<Scene> Preprocess = new();
        static int ImportOffset = 0;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Init), [])]
        static void SceneInfoInitPrefix() => SceneInit.OnNext(Unit.Default);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPrefix(string _path) =>
            LoadScene.With(F.Apply(SceneInit.OnNext, Unit.Default)).OnNext(Extension.Extract(_path));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPrefix(SceneInfo __instance, string _path) =>
            ImportScene.With(() => ImportOffset = __instance.ObjectInfos.Count).OnNext(Extension.Extract(_path));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPostfix(SceneInfo __instance) =>
            Preprocess.OnNext(Extension.Deconstruct(__instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Import), typeof(string))]
        static void SceneInfoImportPostfix(SceneInfo __instance) =>
            Preprocess.OnNext(Extension.Deconstruct(__instance, ImportOffset));

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

        internal static Subject<Scene> SaveObjects = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Save), typeof(string), typeof(Il2CppBytes))]
        static void ScenInfoSavePrefix(SceneInfo __instance) =>
            SaveObjects.OnNext(Extension.Deconstruct(__instance)); 

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Save), typeof(string), typeof(Il2CppBytes))]
        static void ScenInfoSavePostfix(string _path) =>
            F.Apply(Extension.SaveObjects, _path).DelayFrames(1);
    }

    public static partial class Extension
    {
        internal static ZipArchive Extract(string path) =>
            new ZipArchive(Extract(File.ReadAllBytes(path)), ZipArchiveMode.Update);
        internal static Scene Deconstruct(SceneInfo info) =>
            Deconstruct(Deconstruct(info.ObjectInfos.ToArray(), []), ([], [], [], [], [], []));
        internal static Scene Deconstruct(SceneInfo info, int offset) =>
            Deconstruct(info.DicImport.Yield().Select(entry => entry.Value)
                .Select<ObjectInfo, Entry>(value => ([info.ObjectInfos.IndexOf(value) - offset], value)), ([], [], [], [], [], []));
        static Scene Deconstruct(IEnumerable<Entry> infos, Scene scene) =>
            infos.Aggregate(scene, Deconstruct);
        static Scene Deconstruct(Scene scene, int[] indices, OICharInfo value) =>
            Deconstruct(value.Child.Yield().SelectMany(entry => Deconstruct(entry.Value.ToArray(), [.. indices, entry.Key])),
                ([.. scene.Charas, (indices, value)], scene.Items, scene.Lights, scene.Folders, scene.Routes, scene.Cameras));
        static Scene Deconstruct(Scene scene, int[] indices, OIItemInfo value) =>
            Deconstruct(Deconstruct(value.Child.ToArray(), indices),
                (scene.Charas, [.. scene.Items, (indices, value)], scene.Lights, scene.Folders, scene.Routes, scene.Cameras));
        static Scene Deconstruct(Scene scene, int[] indices, OILightInfo value) =>
            (scene.Charas, scene.Items, [..scene.Lights, (indices, value)], scene.Folders, scene.Routes, scene.Cameras);
        static Scene Deconstruct(Scene scene, int[] indices, OIFolderInfo value) =>
            Deconstruct(Deconstruct(value.Child.ToArray(), indices),
                (scene.Charas, scene.Items, scene.Lights, [..scene.Folders, (indices, value)], scene.Routes, scene.Cameras));
        static Scene Deconstruct(Scene scene, int[] indices, OIRouteInfo value) =>
            Deconstruct(Deconstruct(value.Child.ToArray(), indices),
                (scene.Charas, scene.Items, scene.Lights, scene.Folders, [..scene.Routes, (indices, value)], scene.Cameras));
        static Scene Deconstruct(Scene scene, int[] indices, OICameraInfo value) =>
            (scene.Charas, scene.Items, scene.Lights, scene.Folders, scene.Routes, [..scene.Cameras, (indices, value)]);
        static Scene Deconstruct(Scene scene, Entry entry) =>
            entry.Info.Kind switch
            {
                0 => Deconstruct(scene, entry.Indices, new OICharInfo(entry.Info.Pointer)),
                1 => Deconstruct(scene, entry.Indices, new OIItemInfo(entry.Info.Pointer)),
                2 => Deconstruct(scene, entry.Indices, new OILightInfo(entry.Info.Pointer)),
                3 => Deconstruct(scene, entry.Indices, new OIFolderInfo(entry.Info.Pointer)),
                4 => Deconstruct(scene, entry.Indices, new OIRouteInfo(entry.Info.Pointer)),
                5 => Deconstruct(scene, entry.Indices, new OICameraInfo(entry.Info.Pointer)),
                _ => scene
            };
        static IEnumerable<Entry> Deconstruct(IEnumerable<ObjectInfo> infos, int[] indices) =>
            infos.Select<ObjectInfo, (int[], ObjectInfo)>((info, index) => ([.. indices, index], info));
        static Subject<ZipArchive> SaveScene = new();
        internal static void SaveObjects(string path) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(SaveScene.OnNext)));
        internal static string Compose(this string path, int[] indices) =>
            Path.Combine(path, string.Join("-", indices));
    }
    class ObjectStorage<T,U,V> : ValueStorage<V, U>
        where T: ObjectInfo
        where U: ObjectCtrlInfo
        where V: new()
    {
        internal ObjectStorage(TargetObject<T,U> target) => Target = target;
        TargetObject<T,U> Target { init; get; }
        Dictionary<T, V> Values = new(Il2CppEquals.Instance);
        public V Get(T index) => Values.GetValueOrDefault(index, new());
        public void Set(T index, V value) => Values[index] = value;
        public V Get(U index) => Get(Target.ToInfo(index));
        public void Set(U index, V value) => Set(Target.ToInfo(index), value); 
        internal void Remove(U index) => Values.Remove(Target.ToInfo(index));
        internal void Clear() => Values.Clear();
    }
    public static partial class Extension<S, T, U, V>
        where S: TargetObject<T,U>
        where T: ObjectInfo
        where U: ObjectCtrlInfo
        where V: new()
    {
        static readonly ExtensionAttribute<S,T,U,V> Attribute =
            typeof(V).GetCustomAttribute(typeof(ExtensionAttribute<S,T,U,V>))
                is ExtensionAttribute<S,T,U,V> extension ? extension :
                throw new InvalidDataException($"{typeof(V)} does not have valid extension attribute.");
        static void Translate<W>(Func<W, V> map, ZipArchive archive, ZipArchiveEntry entry, string path) where W : new() =>
            SaveValue(archive, path, map(Json<W>.Load(Plugin.Instance.Log.LogError, entry.Open())));

        static void SaveValue(ZipArchive archive, string path, V value) =>
            Serialize(archive.CreateEntry(path).Open(), value);

        static V LoadValue(ZipArchive archive, int[] indices) =>
            archive.TryGetEntry(Attribute.Path.Compose(indices), out var entry) ? Deserialize(entry.Open()) : new();

        static IObservable<U> OnAdd =>
            Hooks.AddObjectCtrl.SelectMany(Attribute.ToCtrl);

        static IObservable<(ZipArchive Archive, string Path, V Value)> OnSave =>
            Hooks.SaveObjects.AsObservable().SelectMany(Attribute.Entries)
                .SelectMany(entry => Extension.OnSaveScene.FirstAsync()
                .Select(archive => (archive, Attribute.Path.Compose(entry.Indices), Storage.Get(entry.Info))));

        internal static IDisposable[] Initialize(IObservable<U> onDelete) => [
            Extension.OnSceneInit.Subscribe(_ => Storage.Clear()),
            onDelete.Subscribe(Storage.Remove),
            OnAdd.Subscribe(index => Storage.Set(index, new())),
            OnLoad.Subscribe(entry => Storage.Set(entry.Index, entry.Value)),
            OnSave.Subscribe(entry => SaveValue(entry.Archive, entry.Path, entry.Value))
        ];
    }
    
    #endregion
    public static partial class Extension
    {
        internal static IDisposable[] Initialize() => [
#if DEBUG
            OnSceneInit.Subscribe(_ => Plugin.Instance.Log.LogDebug("scene initialized")),
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load")),
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load")),
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change")),
            OnPreprocess.Subscribe(_ => Plugin.Instance.Log.LogDebug("scene preprocess")),
            OnPrepareSaveItem.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save item")),
#endif
        ];
    }
}