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
using Entry = (int[] Path, DigitalCraft.ObjectInfo Info);
using Scene = System.Collections.Generic.IEnumerable<(CoastalSmell.TargetType Kind, (int[] Path, DigitalCraft.ObjectInfo Info) Value)>;

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
    class HumansStorage<T> : Storage<T, Human> where T : CharacterExtension<T>, new()
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
                HumanExtension.OnConstructionStart.Where(Match).Select(entry => entry.Human).FirstAsync());
        internal CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()),
                OnResolve.Subscribe(_ => Dispose()),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData Data, T Value) tuple) => Il2CppEquals.Apply(Data, tuple.Data);
        bool Match((Human Human, HumanData Data) entry) => Il2CppEquals.Apply(Data, entry.Data); 
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
        internal static Subject<Unit> InitScene = new();
        internal static Subject<ZipArchive> LoadScene = new();
        internal static Subject<ZipArchive> ImportScene = new();
        internal static Subject<Scene> Preprocess = new();
        static int ImportOffset = 0;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Init), [])]
        static void SceneInfoInitPrefix() => InitScene.OnNext(Unit.Default);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SceneInfo), nameof(SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)],
            [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void SceneInfoLoadPrefix(string _path) =>
            LoadScene.With(F.Apply(InitScene.OnNext, Unit.Default)).OnNext(Extension.Extract(_path));

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

        internal static Subject<(TargetType, ObjectCtrlInfo)> DeleteObject = new();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIItem), nameof(OCIItem.OnDelete))]
        static void OCIItemOnDelete(OCIItem __instance) =>
            DeleteObject.OnNext((TargetType.Item, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCILight), nameof(OCILight.OnDelete))]
        static void OCILightOnDelete(OCILight __instance) =>
            DeleteObject.OnNext((TargetType.Light, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIFolder), nameof(OCIFolder.OnDelete))]
        static void OCIFolderOnDelete(OCIFolder __instance) =>
            DeleteObject.OnNext((TargetType.Folder, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCIRoute), nameof(OCIRoute.OnDelete))]
        static void OCIRouteOnDelete(OCIRoute __instance) => 
            DeleteObject.OnNext((TargetType.Route, __instance));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(OCICamera), nameof(OCICamera.OnDelete))]
        static void OCICameraOnDelete(OCICamera __instance) =>
            DeleteObject.OnNext((TargetType.Camera, __instance));

        static void Resolve(TargetType kind, ObjectCtrlInfo info, Action<(TargetType, ObjectCtrlInfo)> action) =>
            (kind is not TargetType.Undefined).Maybe(action.Apply((kind, info)));

        internal static Subject<(TargetType, ObjectCtrlInfo)> AddObjectCtrl = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.AddNode), typeof(ObjectCtrlInfo), typeof(string), typeof(TreeNodeObject))]
        static void TreeNodeCtrlAddNodePostfix(ObjectCtrlInfo _objectCtrl) =>
            Resolve(_objectCtrl.Classify(), _objectCtrl, AddObjectCtrl.OnNext);

        internal static Subject<(TargetType, ObjectCtrlInfo)> PrepareSaveObject = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ObjectCtrlInfo), nameof(ObjectCtrlInfo.OnSavePreprocessing))]
        static void ObjectCtrlInfoOnSavePreprocessingPostfix(ObjectCtrlInfo __instance) =>
            Resolve(__instance.Classify(), __instance, PrepareSaveObject.OnNext);

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
            Deconstruct([], info.ObjectInfos.ToArray());

        internal static Scene Deconstruct(SceneInfo info, int offset) =>
            info.DicImport.Yield().Select(entry => entry.Value)
                .Select<ObjectInfo, Entry>(value => ([info.ObjectInfos.IndexOf(value) - offset], value))
                .SelectMany(Deconstruct);

        static Scene Deconstruct(int[] path, IEnumerable<ObjectInfo> infos) =>
            infos.Select<ObjectInfo, Entry>((info, index) => ([.. path, index], info)).SelectMany(Deconstruct);

        static Scene Deconstruct(Entry entry) =>
            entry.Info.Classify() switch
            {
                TargetType.Chara =>
                    Deconstruct(entry.Path, new OICharInfo(entry.Info.Pointer)
                        .Child.Yield().SelectMany(entry => entry.Value.ToArray())).Prepend((TargetType.Chara, entry)),
                TargetType.Item =>
                    Deconstruct(entry.Path, new OIItemInfo(entry.Info.Pointer).Child.ToArray()).Prepend((TargetType.Item, entry)),
                TargetType.Light =>
                    [(TargetType.Item, entry)],
                TargetType.Folder =>
                    Deconstruct(entry.Path, new OIFolderInfo(entry.Info.Pointer).Child.ToArray()).Prepend((TargetType.Item, entry)),
                TargetType.Route =>
                    Deconstruct(entry.Path, new OIRouteInfo(entry.Info.Pointer).Child.ToArray()).Prepend((TargetType.Item, entry)),
                TargetType.Camera =>
                    [(TargetType.Item, entry)],
                _ => []
            };
        
        static Subject<ZipArchive> SaveScene = new();
        internal static void SaveObjects(string path) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(SaveScene.OnNext)));
    }
    class ObjectStorage<T> : MapStorage<T, ObjectCtrlInfo, ObjectInfo> where T: new() {
        Dictionary<ObjectInfo, T> Values = new(Il2CppEquals.Instance);
        public ObjectInfo Map(ObjectCtrlInfo index) => index.objectInfo;
        public T Get(ObjectInfo index) => Values.GetValueOrDefault(index, new());
        public void Set(ObjectInfo index, T value) => Values[index] = value;
        internal void Remove(ObjectCtrlInfo index) => Values.Remove(Map(index));
        internal void Clear() => Values.Clear();
    }
    public static partial class ObjectExtension<T> where T: new() {
        static readonly ObjectExtensionAttribute<T> Attribute =
            typeof(T).GetCustomAttribute(typeof(ObjectExtensionAttribute<T>))
                is ObjectExtensionAttribute<T> extension ? extension :
                throw new InvalidDataException($"{typeof(T)} does not have valid extension attribute.");
        static readonly ObjectStorage<T> Storage = new(); 

        static void SaveValue((ZipArchive Archive, string Path, ObjectInfo Info) tuple) =>
            SaveValue(tuple.Archive, tuple.Path, Values[tuple.Info]);

        static void SaveValue(ZipArchive archive, string path, T value) =>
            Serialize(archive.CreateEntry(path).Open(), value);

        static T LoadValue<V>(ZipArchive archive, string path, Func<V, T> map) where V: new() =>
            archive.TryGetEntry(path, out var entry) ? map(Json<V>.Load(Plugin.Instance.Log.LogError, entry.Open())) : new();

        static T LoadValue(ZipArchive archive, string path) =>
            archive.TryGetEntry(path, out var entry) ? Deserialize(entry.Open()) : new();

        internal static IDisposable[] Initialize() => [
            Extension.OnInitScene.Subscribe(_ => Storage.Clear()),
            Attribute.OnDelete.Subscribe(Storage.Remove),
            Attribute.OnAdd.Subscribe(index => Values[index] = new()),
            Attribute.OnSave.Subscribe(SaveValue),
            OnLoad.Subscribe(entry => Values[entry.Info] = entry.Value)
        ];
    }
    
    #endregion
    public static partial class Extension
    {
        internal static IDisposable[] Initialize() => [
#if DEBUG
            OnInitScene.Subscribe(_ => Plugin.Instance.Log.LogDebug("scene initialized")),
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load")),
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load")),
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change")),
            OnPreprocessObject.Subscribe(_ => Plugin.Instance.Log.LogDebug("scene preprocess")),
            OnPrepareSaveObject.Subscribe(entry => Plugin.Instance.Log.LogDebug("prepare save item")),
#endif
        ];
    }
}