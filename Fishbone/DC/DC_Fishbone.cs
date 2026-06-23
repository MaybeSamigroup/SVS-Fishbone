using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.IO;
using System.IO.Compression;
using Character;
using DigitalCraft;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;
using Entry = (int[] Path, DigitalCraft.ObjectInfo Info);

namespace Fishbone
{
    /// <summary>
    /// Platform-specific adapters that expose storage and preprocessing observables for registered extensions.
    /// </summary>
    public static partial class Extension<T, U>
    {
        static readonly HumansStorage<T, U> Storage = new HumansStorage<T, U>();

        /// <summary>Extension storage indexed by human</summary>
        public static Storage<T, U, Human> Humans => Storage;

        /// <summary>Raised when a character data has been loaded and an extension data is bound to it.</summary>
        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));

        /// <summary>Raised when a coordinate data has been loaded and an extension data is bound to it.</summary>
        public static IObservable<(HumanDataCoordinate Data, U Value)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Data, tuple.Value));
    }
    /// <summary>
    /// Platform-specific adapters for simple character extensions.
    /// </summary>
    public static partial class Extension<T>
    {
        static readonly HumansStorage<T> Storage = new HumansStorage<T>();

        /// <summary>Extension storage indexed by human</summary>
        public static readonly Storage<T, Human> Humans = Storage;

        /// <summary>Raised when a character data has been loaded and an extension data is bound to it.</summary>
        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));
    }

    /// <summary>
    /// Platform-level extension entrypoints for scene/object save and load hooks.
    /// </summary>
    public static partial class Extension
    {
        /// <summary>Raised when a character data is about to be saved.</summary>
        public static IObservable<Human> OnPrepareSaveChara => PrepareSaveChara.AsObservable();

        /// <summary>Raised when a character is saved.</summary>
        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveChara => SaveChara.AsObservable();

        /// <summary>Raised when a character is loaded.</summary>
        public static IObservable<Human> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve);

        /// <summary>Raised when a coordinate is loaded.</summary>
        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human));

        /// <summary>Raised when a character's coordinate has changed.</summary>
        public static IObservable<(Human Human, int Index)> OnChangeCoord = Hooks.OnChangeCoordinate;

        /// <summary>Raised when a character is deleted from scene.</summary>
        public static IObservable<Human> OnDeleteChara => Hooks.DeleteChara.AsObservable();

        /// <summary>Register complex character and coordinate extension</summary>
        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnInitScene.Subscribe(_ => Extension<T, U>.Clear()),
            OnSaveChara.Subscribe(Extension<T, U>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T,U>.Remove),
            Extension<T, U>.OnLoadChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human] = tuple.Value),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value)
        ];

        /// <summary>Register simple character extension</summary>
        public static IDisposable[] Register<T>()
            where T : CharacterExtension<T>, new() => [
            OnInitScene.Subscribe(_ => Extension<T>.Clear()),
            OnSaveChara.Subscribe(Extension<T>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T>.Remove),
            Extension<T>.OnLoadChara.Subscribe(tuple => Extension<T>.Humans[tuple.Human] = tuple.Value)
        ];
    }
    #region Object
    public static partial class Extension
    {
        /// <summary>Raised when scene data has initialized.</summary>
        public static IObservable<Unit> OnInitScene =>
            Hooks.InitScene.AsObservable();

        /// <summary>Raised when scene data is about to load.</summary>
        public static IObservable<ZipArchive> OnLoadScene =>
            Hooks.LoadScene.AsObservable();

        /// <summary>Raised when scene data is about to import.</summary>
        public static IObservable<ZipArchive> OnImportScene =>
            Hooks.ImportScene.AsObservable();

        /// <summary>Raised when object added to scene.</summary>
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnAddObject =>
            Hooks.AddObjectCtrl.AsObservable(); 

        /// <summary>Raised when object deleted from scene.</summary>
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnDeleteObject =>
            Hooks.DeleteObject.AsObservable();

        /// <summary>Raised when object is about to save.</summary>
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnPrepareSaveObject =>
            Hooks.PrepareSaveObject.AsObservable();

        /// <summary>Raised when scene extension saved.</summary>
        public static IObservable<ZipArchive> OnSaveScene =>
            SaveScene.AsObservable();

        /// <summary>Raised when a object's data has been loaded and an extension archive is available.</summary>
        public static IObservable<(TargetType Kind, (ZipArchive Archive, Entry Entry) Value)> OnPreprocessObject =>
            OnLoadScene.Merge(OnImportScene)
                .SelectMany(archive => Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(scene => scene.Select(entry => (entry.Kind, (archive, entry.Value)))));
        
        /// <summary>Raised when object's data is saved to extension archive</summary>
        internal static IObservable<(TargetType Kind, (ZipArchive Archive, Entry Entry) Value)> OnSaveObject =>
            Hooks.SaveObjects.AsObservable().SelectMany(scene => OnSaveScene.FirstAsync()
                .SelectMany(archive => scene.Select(entry => (entry.Kind, (archive, entry.Value)))));
        internal static string Compose(this string path, int[] indices) =>
            Path.Combine(path, string.Join("-", indices));
    }

    /// <summary>Marks a object extension type and declares its related data type and storage path components.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ObjectExtensionAttribute<T> : PathAttribute {
        /// <summary>Target types of extension</summary>
        public TargetType Types { init; get; }

        /// <summary>Create attribute and define related data type and path in archive</summary>
        /// <param name="types">related data types</param>
        /// <param name="paths">path in archive</param>
        public ObjectExtensionAttribute(TargetType types, params string[] paths) : base(paths) => Types = types;

        internal IObservable<(ZipArchive Archive, string From, string To)> OnTranslate(string path) =>
            Extension.OnPreprocessObject.Where(Types).Select(tuple =>
                (tuple.Archive, path.Compose(tuple.Entry.Path), Path.Compose(tuple.Entry.Path)));

        internal IObservable<(ZipArchive Archive, string Path, ObjectInfo Info)> OnPreprocess =>
            Extension.OnPreprocessObject.Where(Types).Select(tuple => (tuple.Archive, Path.Compose(tuple.Entry.Path), tuple.Entry.Info));
        internal IObservable<ObjectCtrlInfo> OnAdd =>
            Extension.OnAddObject.Where(Types);

        internal IObservable<ObjectCtrlInfo> OnDelete =>
            Extension.OnDeleteObject.Where(Types);

        internal IObservable<ObjectCtrlInfo> OnPrepareSave =>
            Extension.OnPrepareSaveObject.Where(Types);

        internal IObservable<(ZipArchive Archive, string Path, ObjectInfo Info)> OnSave =>
            Extension.OnSaveObject.Where(Types).Select(tuple => (tuple.Archive, Path.Compose(tuple.Entry.Path), tuple.Entry.Info));
    }
    /// <summary>
    /// Platform-specific adapters for object extensions.
    /// </summary>
    public static partial class ObjectExtension<T> where T: new () {

        /// <summary>Extension storage indexed by ObjectCtrlInfo and ObjectInfo</summary>
        public static MapStorage<T, ObjectCtrlInfo, ObjectInfo> Values => Storage;

        /// <summary>Serializer for object extension instances.</summary>
        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Deserializer for object extension instances.</summary>
        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Helper to convert extension data path and type.</summary>
        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Attribute.OnTranslate(path).Subscribe(tuple => SaveValue(tuple.Archive, tuple.To, LoadValue(tuple.Archive, tuple.From, map)));

        /// <summary>Raised when a object data has been preprocessed and an extension data is bound to it.</summary>
        public static IObservable<(ObjectInfo Info, T Value)> OnPreprocess =>
            Attribute.OnPreprocess.Select(tuple => (tuple.Info, LoadValue(tuple.Archive, tuple.Path)));

        /// <summary>Raised when a object is loaded.</summary>
        public static IObservable<(ObjectCtrlInfo Info, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry => Attribute.OnAdd
                .Where(ctrl => entry.Info.Pointer == ctrl.objectInfo.Pointer)
                .FirstAsync().Select(ctrl => (ctrl, entry.Value)));
    }
 
    public static partial class Extension
    {
        /// <summary>Register extension bound to objects</summary>
        public static IDisposable[] RegisterObject<T>() where T: new() => ObjectExtension<T>.Initialize();
    }
    #endregion

    /// <summary>
    /// DigitalCraft platform plugin marker for Fishbone.
    /// </summary>
    public partial class Plugin : BasePlugin
    {
        /// <summary>supported process name</summary>
        public const string Process = "DigitalCraft";
    }
}