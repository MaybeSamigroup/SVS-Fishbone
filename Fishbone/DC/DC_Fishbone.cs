using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.IO.Compression;
using Character;
using DigitalCraft;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;
using System.Collections.Generic;

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
        public static IObservable<(ZipArchive Archive, int Offset)> OnLoadScene =>
            Hooks.LoadScene.AsObservable();
        public static IObservable<(ZipArchive Archive, int Offset)> OnImportScene =>
            Hooks.ImportScene.AsObservable();
        public static IObservable<(ZipArchive Archive, int[] Indices, OIItemInfo Info)> OnPreprocessItem =>
            OnLoadScene.Merge(OnImportScene).SelectMany(entry =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(values => ToItems(values, entry.Archive, entry.Offset)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OILightInfo Info)> OnPreprocessLight =>
            OnLoadScene.Merge(OnImportScene).SelectMany(entry =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(values => ToLights(values, entry.Archive, entry.Offset)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OIFolderInfo Info)> OnPreprocessFolder =>
            OnLoadScene.Merge(OnImportScene).SelectMany(entry =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(values => ToFolders(values, entry.Archive, entry.Offset)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OIRouteInfo Info)> OnPreprocessRoute =>
            OnLoadScene.Merge(OnImportScene).SelectMany(entry =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(values => ToRoutes(values, entry.Archive, entry.Offset)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OICameraInfo Info)> OnPreprocessCamera =>
            OnLoadScene.Merge(OnImportScene).SelectMany(entry =>
                Hooks.Preprocess.AsObservable().FirstAsync()
                    .SelectMany(values => ToCameras(values, entry.Archive, entry.Offset)));
        public static IObservable<OCIItem> OnAddItem =>
            Hooks.AddObjectCtrl.Where(obj => 1 == (obj?.objectInfo?.Kind ?? -1)).Select(obj => new OCIItem(obj.Pointer));
        public static IObservable<OCILight> OnAddLight =>
            Hooks.AddObjectCtrl.Where(obj => 2 == (obj?.objectInfo?.Kind ?? -1)).Select(obj => new OCILight(obj.Pointer));
        public static IObservable<OCIFolder> OnAddFolder =>
            Hooks.AddObjectCtrl.Where(obj => 3 == (obj?.objectInfo?.Kind ?? -1)).Select(obj => new OCIFolder(obj.Pointer));
        public static IObservable<OCIRoute> OnAddRoute =>
            Hooks.AddObjectCtrl.Where(obj => 4 == (obj?.objectInfo?.Kind ?? -1)).Select(obj => new OCIRoute(obj.Pointer));
        public static IObservable<OCICamera> OnAddCamera =>
            Hooks.AddObjectCtrl.Where(obj => 5 == (obj?.objectInfo?.Kind ?? -1)).Select(obj => new OCICamera(obj.Pointer));
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
        public static IObservable<(ZipArchive Archive, int[] Indices, OIItemInfo Info)> OnSaveItem =>
            Hooks.SaveObjects.AsObservable().SelectMany(values =>
                OnSaveScene.FirstAsync().SelectMany(archive => ToItems(values, archive, 0)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OILightInfo Info)> OnSaveLight =>
            Hooks.SaveObjects.AsObservable().SelectMany(values =>
                OnSaveScene.FirstAsync().SelectMany(archive => ToLights(values, archive, 0)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OIFolderInfo Info)> OnSaveFolder =>
            Hooks.SaveObjects.AsObservable().SelectMany(values =>
                OnSaveScene.FirstAsync().SelectMany(archive => ToFolders(values, archive, 0)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OIRouteInfo Info)> OnSaveRoute =>
            Hooks.SaveObjects.AsObservable().SelectMany(values =>
                OnSaveScene.FirstAsync().SelectMany(archive => ToRoutes(values, archive, 0)));
        public static IObservable<(ZipArchive Archive, int[] Indices, OICameraInfo Info)> OnSaveCamera =>
            Hooks.SaveObjects.AsObservable().SelectMany(values =>
                OnSaveScene.FirstAsync().SelectMany(archive => ToCameras(values, archive, 0)));
    }
    #endregion
    
    #region Item
    [AttributeUsage(AttributeTargets.Class)]
    public class ItemExtensionAttribute<T> : PathAttribute where T : new()
    {
        public ItemExtensionAttribute(params string[] paths) : base(paths) { }
    }
    public static partial class ItemExtension<T> where T: new()
    {
        public static Dictionary<OCIItem,T> Items { get; } = new(); 

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessItem.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry, tuple.Indices)));

        public static IObservable<(OIItemInfo Info, T Value)> OnPreprocess =>
            Extension.OnPreprocessItem.Select(entry => (entry.Info, LoadValue(entry.Archive, entry.Indices)));

        public static IObservable<(OCIItem Index, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry =>
                Extension.OnAddItem.AsObservable()
                    .Where(obj => entry.Info.Pointer == obj.objectInfo.Pointer)
                    .FirstAsync().Select(obj => (new OCIItem(obj.Pointer), entry.Value)));
    }
    public static partial class Extension
    {
        public static IDisposable[] RegisterItem<T>() where T : new() => [
            OnSceneInit.Subscribe(_ => ItemExtension<T>.Items.Clear()),
            OnSaveItem.Subscribe(ItemExtension<T>.SaveValue),
            ItemExtension<T>.OnLoad.Subscribe(tuple => ItemExtension<T>.Items[tuple.Index] = tuple.Value),
            OnAddItem.Subscribe(index => ItemExtension<T>.Items[index] = new()),
            OnDeleteItem.Subscribe(index => ItemExtension<T>.Items.Remove(index))
        ];
    }
    #endregion 

    #region Light
    [AttributeUsage(AttributeTargets.Class)]
    public class LightExtensionAttribute<T> : PathAttribute where T : new()
    {
        public LightExtensionAttribute(params string[] paths) : base(paths) { }
    }
    public static partial class LightExtension<T> where T: new()
    {
        public static Dictionary<OCILight,T> Lights { get; } = new(); 

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessLight.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry, tuple.Indices)));

        public static IObservable<(OILightInfo Info, T Value)> OnPreprocess =>
            Extension.OnPreprocessLight.Select(entry => (entry.Info, LoadValue(entry.Archive, entry.Indices)));

        public static IObservable<(OCILight Index, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry =>
                Extension.OnAddLight.AsObservable()
                    .Where(obj => entry.Info.Pointer == obj.objectInfo.Pointer)
                    .FirstAsync().Select(obj => (new OCILight(obj.Pointer), entry.Value)));
    }
    public static partial class Extension
    {
        public static IDisposable[] RegisterLight<T>() where T : new() => [
            OnSceneInit.Subscribe(_ => LightExtension<T>.Lights.Clear()),
            OnSaveLight.Subscribe(LightExtension<T>.SaveValue),
            LightExtension<T>.OnLoad.Subscribe(tuple => LightExtension<T>.Lights[tuple.Index] = tuple.Value),
            OnAddLight.Subscribe(index => LightExtension<T>.Lights[index] = new()),
            OnDeleteLight.Subscribe(index => LightExtension<T>.Lights.Remove(index))
        ];
    }
    #endregion

    #region Folder
    [AttributeUsage(AttributeTargets.Class)]
    public class FolderExtensionAttribute<T> : PathAttribute where T : new()
    {
        public FolderExtensionAttribute(params string[] paths) : base(paths) { }
    }
    public static partial class FolderExtension<T> where T: new()
    {
        public static Dictionary<OCIFolder,T> Folders { get; } = new(); 

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessFolder.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry, tuple.Indices)));

        public static IObservable<(OIFolderInfo Info, T Value)> OnPreprocess =>
            Extension.OnPreprocessFolder.Select(entry => (entry.Info, LoadValue(entry.Archive, entry.Indices)));

        public static IObservable<(OCIFolder Index, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry =>
                Extension.OnAddFolder.AsObservable()
                    .Where(obj => entry.Info.Pointer == obj.objectInfo.Pointer)
                    .FirstAsync().Select(obj => (new OCIFolder(obj.Pointer), entry.Value)));
    }
    public static partial class Extension
    {
        public static IDisposable[] RegisterFolder<T>() where T : new() => [
            OnSceneInit.Subscribe(_ => FolderExtension<T>.Folders.Clear()),
            OnSaveFolder.Subscribe(FolderExtension<T>.SaveValue),
            FolderExtension<T>.OnLoad.Subscribe(tuple => FolderExtension<T>.Folders[tuple.Index] = tuple.Value),
            OnAddFolder.Subscribe(index => FolderExtension<T>.Folders[index] = new()),
            OnDeleteFolder.Subscribe(index => FolderExtension<T>.Folders.Remove(index))
        ];
    }
    #endregion

    #region Route
    [AttributeUsage(AttributeTargets.Class)]
    public class RouteExtensionAttribute<T> : PathAttribute where T : new()
    {
        public RouteExtensionAttribute(params string[] paths) : base(paths) { }
    }
    public static partial class RouteExtension<T> where T: new()
    {
        public static Dictionary<OCIRoute,T> Routes { get; } = new(); 

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessRoute.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry, tuple.Indices)));

        public static IObservable<(OIRouteInfo Info, T Value)> OnPreprocess =>
            Extension.OnPreprocessRoute.Select(entry => (entry.Info, LoadValue(entry.Archive, entry.Indices)));

        public static IObservable<(OCIRoute Index, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry =>
                Extension.OnAddRoute.AsObservable()
                    .Where(obj => entry.Info.Pointer == obj.objectInfo.Pointer)
                    .FirstAsync().Select(obj => (new OCIRoute(obj.Pointer), entry.Value)));
    }
    public static partial class Extension
    {
        public static IDisposable[] RegisterRoute<T>() where T : new() => [
            OnSceneInit.Subscribe(_ => RouteExtension<T>.Routes.Clear()),
            OnSaveRoute.Subscribe(RouteExtension<T>.SaveValue),
            RouteExtension<T>.OnLoad.Subscribe(tuple => RouteExtension<T>.Routes[tuple.Index] = tuple.Value),
            OnAddRoute.Subscribe(index => RouteExtension<T>.Routes[index] = new()),
            OnDeleteRoute.Subscribe(index => RouteExtension<T>.Routes.Remove(index))
        ];
    }
    #endregion

    #region Camera
    [AttributeUsage(AttributeTargets.Class)]
    public class CameraExtensionAttribute<T> : PathAttribute where T : new()
    {
        public CameraExtensionAttribute(params string[] paths) : base(paths) { }
    }
    public static partial class CameraExtension<T> where T: new()
    {
        public static Dictionary<OCICamera,T> Cameras { get; } = new(); 

        public static Action<Stream, T> Serialize =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, T> Deserialize =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessCamera.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry, tuple.Indices)));

        public static IObservable<(OICameraInfo Info, T Value)> OnPreprocess =>
            Extension.OnPreprocessCamera.Select(entry => (entry.Info, LoadValue(entry.Archive, entry.Indices)));

        public static IObservable<(OCICamera Index, T Value)> OnLoad =>
            OnPreprocess.SelectMany(entry =>
                Extension.OnAddCamera.AsObservable()
                    .Where(obj => entry.Info.Pointer == obj.objectInfo.Pointer)
                    .FirstAsync().Select(obj => (new OCICamera(obj.Pointer), entry.Value)));
    }
    public static partial class Extension
    {
        public static IDisposable[] RegisterCamera<T>() where T : new() => [
            OnSceneInit.Subscribe(_ => CameraExtension<T>.Cameras.Clear()),
            OnSaveCamera.Subscribe(CameraExtension<T>.SaveValue),
            CameraExtension<T>.OnLoad.Subscribe(tuple => CameraExtension<T>.Cameras[tuple.Index] = tuple.Value),
            OnAddCamera.Subscribe(index => CameraExtension<T>.Cameras[index] = new()),
            OnDeleteCamera.Subscribe(index => CameraExtension<T>.Cameras.Remove(index))
        ];
    }
    #endregion

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}