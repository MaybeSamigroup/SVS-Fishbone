using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.IO;
using System.IO.Compression;
using Character;
using DigitalCraft;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;
using Scene = (
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICharInfo Info)> Charas,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIItemInfo Info)> Items,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OILightInfo Info)> Lights,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIFolderInfo Info)> Folders,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIRouteInfo Info)> Routes,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICameraInfo Info)> Cameras);

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
    public class ExtensionAttribute<S,T,U,V> : PathAttribute, TargetObject<T,U>
        where S: TargetObject<T, U>
        where T: ObjectInfo
        where U: ObjectCtrlInfo
        where V: new()
    {
        S Delegate { init; get; }
        public ExtensionAttribute(S target, params string[] paths) : base(paths) => Delegate = target;
        public T ToInfo(U value) => Delegate.ToInfo(value);
        public bool ToCtrl(ObjectCtrlInfo ojb, out U value) => Delegate.ToCtrl(ojb, out value);
        public IEnumerable<(int[] Indices, T Info)> Entries(Scene scene) => Delegate.Entries(scene);
        public virtual IEnumerable<U> ToCtrl(ObjectCtrlInfo info) => ToCtrl(info, out var value) ? [value] : [];
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class ItemExtensionAttribute<T>: ExtensionAttribute<Item, OIItemInfo, OCIItem, T> where T: new()
    {
        public ItemExtensionAttribute(params string[] paths) : base(Item.Instance, paths) { }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class LightExtensionAttribute<T>: ExtensionAttribute<Light, OILightInfo, OCILight, T> where T: new()
    {
        public LightExtensionAttribute(params string[] paths) : base(Light.Instance, paths) { }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class FolderExtensionAttribute<T>: ExtensionAttribute<Folder, OIFolderInfo, OCIFolder, T> where T: new()
    {
        public FolderExtensionAttribute(params string[] paths) : base(Folder.Instance, paths) { }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class RouteExtensionAttribute<T>: ExtensionAttribute<Route, OIRouteInfo, OCIRoute, T> where T: new()
    {
        public RouteExtensionAttribute(params string[] paths) : base(Route.Instance, paths) { }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class CameraExtensionAttribute<T>: ExtensionAttribute<Camera, OICameraInfo, OCICamera, T> where T: new()
    {
        public CameraExtensionAttribute(params string[] paths) : base(Camera.Instance, paths) { }
    }
    public static partial class Extension
    {
        public static IObservable<Unit> OnSceneInit =>
            Hooks.SceneInit.AsObservable();
        public static IObservable<ZipArchive> OnLoadScene =>
            Hooks.LoadScene.AsObservable();
        public static IObservable<ZipArchive> OnImportScene =>
            Hooks.ImportScene.AsObservable();
        public static IObservable<Scene> OnPreprocess =>
            Hooks.Preprocess.AsObservable();
        public static IObservable<OCIItem> OnDeleteItem =>
            Hooks.DeleteItem.AsObservable();
        public static IObservable<OCILight> OnDeleteLight =>
            Hooks.DeleteLight.AsObservable();
        public static IObservable<OCIFolder> OnDeleteFolder =>
            Hooks.DeleteFolder.AsObservable();
        public static IObservable<OCIRoute> OnDeleteRoute =>
            Hooks.DeleteRoute.AsObservable();
        public static IObservable<OCICamera> OnDeleteCamera =>
            Hooks.DeleteCamera.AsObservable();
        public static IObservable<OCIItem> OnPrepareSaveItem =>
            Hooks.PrepareSaveItem.AsObservable();
        public static IObservable<OCILight> OnPrepareSaveLight =>
            Hooks.PrepareSaveLight.AsObservable();
        public static IObservable<OCIFolder> OnPrepareSaveFolder =>
            Hooks.PrepareSaveFolder.AsObservable();
        public static IObservable<OCIRoute> OnPrepareSaveRoute =>
            Hooks.PrepareSaveRoute.AsObservable();
        public static IObservable<OCICamera> OnPrepareSaveCamera =>
            Hooks.PrepareSaveCamera.AsObservable();
        public static IObservable<ZipArchive> OnSaveScene =>
            SaveScene.AsObservable();
    }
    public static partial class Extension<S, T, U, V>
        where S: TargetObject<T,U>
        where T: ObjectInfo
        where U: ObjectCtrlInfo
        where V: new()
    {
        static readonly ObjectStorage<T, U, V> Storage = new ObjectStorage<T, U, V>(Attribute);

        public static ValueStorage<V, U> Values => Storage;

        public static Action<Stream, V> Serialize =
            Json<V>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, V> Deserialize =
            Json<V>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<W>(string path, Func<W, V> map) where W : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene).SelectMany(archive =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(Attribute.Entries)
                    .Select(entry => (archive, entry.Indices)))
                    .Subscribe(tuple => tuple.Item1.TryGetEntry(path, out var entry)
                        .Maybe(F.Apply(Translate, map, tuple.Item1, entry, Attribute.Path.Compose(tuple.Item2))));

        public static IObservable<(T Info, V Value)> OnPreprocess =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene).SelectMany(archive =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(Attribute.Entries)
                        .Select(entry => (entry.Info, LoadValue(archive, entry.Indices))));
        public static IObservable<(U Index, V Value)> OnLoad =>
            OnPreprocess.SelectMany(entry => OnAdd
                .Where(ctrl => entry.Info.Pointer == Attribute.ToInfo(ctrl).Pointer)
                .FirstAsync().Select(ctrl => (ctrl, entry.Value)));
    }
 
    public static partial class Extension
    {
        static IDisposable[] Register<S, T, U, V>(IObservable<U> onDelete)
            where S : TargetObject<T, U>
            where T : ObjectInfo
            where U : ObjectCtrlInfo
            where V : new() => Extension<S, T, U, V>.Initialize(onDelete);
        public static IDisposable[] RegisterItem<T>() where T : new() =>
            Register<Item, OIItemInfo, OCIItem, T>(OnDeleteItem);
        public static IDisposable[] RegisterLight<T>() where T : new() =>
            Register<Light, OILightInfo, OCILight, T>(OnDeleteLight);
        public static IDisposable[] RegisterFolder<T>() where T : new() =>
            Register<Folder, OIFolderInfo, OCIFolder, T>(OnDeleteFolder);
        public static IDisposable[] RegisterRouter<T>() where T : new() =>
            Register<Route, OIRouteInfo, OCIRoute, T>(OnDeleteRoute);
        public static IDisposable[] RegisterCamerar<T>() where T : new() =>
            Register<Camera, OICameraInfo, OCICamera, T>(OnDeleteCamera);
    }
    #endregion

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}