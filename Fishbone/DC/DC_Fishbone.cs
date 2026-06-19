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
    public static partial class Extension<T, U>
    {
        static readonly HumansStorage<T, U> Storage = new HumansStorage<T, U>();

        public static Storage<T, U, Human> Humans => Storage;

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));

        public static IObservable<(HumanDataCoordinate Data, U Value)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Data, tuple.Value));
    }
    public static partial class Extension<T>
    {
        static readonly HumansStorage<T> Storage = new HumansStorage<T>();

        public static readonly Storage<T, Human> Humans = Storage;

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));
    }

    public static partial class Extension
    {
        public static IObservable<Human> OnPrepareSaveChara => PrepareSaveChara.AsObservable();

        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveChara => SaveChara.AsObservable();

        public static IObservable<Human> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve);

        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human));

        public static IObservable<(Human Human, int Index)> OnChangeCoord = Hooks.OnChangeCoordinate;

        public static IObservable<Human> OnDeleteChara => Hooks.DeleteChara.AsObservable();

        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnSceneInit.Subscribe(_ => Extension<T, U>.Clear()),
            OnSaveChara.Subscribe(Extension<T, U>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T,U>.Remove),
            Extension<T, U>.OnLoadChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human] = tuple.Value),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value)
        ];
        public static IDisposable[] Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() => [
            OnSceneInit.Subscribe(_ => Extension<T>.Clear()),
            OnSaveChara.Subscribe(Extension<T>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T>.Remove),
            Extension<T>.OnLoadChara.Subscribe(tuple => Extension<T>.Humans[tuple.Human] = tuple.Value)
        ];
    }
    #region Object
    public static partial class Extension
    {
        public static IObservable<Unit> OnSceneInit =>
            Hooks.SceneInit.AsObservable();
        public static IObservable<ZipArchive> OnLoadScene =>
            Hooks.LoadScene.AsObservable();
        public static IObservable<ZipArchive> OnImportScene =>
            Hooks.ImportScene.AsObservable();
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnAddObject =>
            Hooks.AddObjectCtrl.AsObservable(); 
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnDeleteObject =>
            Hooks.DeleteObject.AsObservable();
        public static IObservable<(TargetType Kind, ObjectCtrlInfo Value)> OnPrepareSaveObject =>
            Hooks.PrepareSaveObject.AsObservable();
        public static IObservable<ZipArchive> OnSaveScene =>
            SaveScene.AsObservable();
        public static IObservable<(TargetType Kind, (ZipArchive Archive, Entry Entry) Value)> OnPreprocessObject =>
            OnLoadScene.Merge(OnImportScene)
                .SelectMany(archive => Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(scene => scene.Select(entry => (entry.Kind, (archive, entry.Value)))));
        public static IObservable<(TargetType Kind, (ZipArchive Archive, Entry Entry) Value)> OnSaveObject =>
            Hooks.SaveObjects.AsObservable().SelectMany(scene => OnSaveScene.FirstAsync()
                .SelectMany(archive => scene.Select(entry => (entry.Kind, (archive, entry.Value)))));
        internal static string Compose(this string path, int[] indices) =>
            Path.Combine(path, string.Join("-", indices));
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ObjectExtensionAttribute<T> : PathAttribute {
        public TargetType Types { init; get; }

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
    public static partial class ObjectExtension<T> where T: new () {
        public static MapStorage<T, ObjectCtrlInfo, ObjectInfo> Values => Storage;

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Attribute.OnTranslate(path).Subscribe(tuple => SaveValue(tuple.Archive, tuple.To, LoadValue(tuple.Archive, tuple.From, map)));

        public static IObservable<(ObjectInfo Info, T Value)> OnPreprocess =>
            Attribute.OnPreprocess.Select(tuple => (tuple.Info, LoadValue(tuple.Archive, tuple.Path)));

        public static IObservable<(ObjectCtrlInfo Info, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry => Attribute.OnAdd
                .Where(ctrl => entry.Info.Pointer == ctrl.objectInfo.Pointer)
                .FirstAsync().Select(ctrl => (ctrl, entry.Value)));
    }
 
    public static partial class Extension
    {
        public static IDisposable[] RegisterObject<T>() where T: new() => ObjectExtension<T>.Initialize();
    }
    #endregion

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}