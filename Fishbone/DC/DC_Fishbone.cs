using System;
using System.IO;
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
            OnSaveChara.Subscribe(Extension<T, U>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T,U>.Remove),
            Extension<T, U>.OnLoadChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human] = tuple.Value),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value)
        ];

        public static IDisposable[] Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() => [
            OnSaveChara.Subscribe(Extension<T>.SaveChara),
            OnDeleteChara.Subscribe(Extension<T>.Remove),
            Extension<T>.OnLoadChara.Subscribe(tuple => Extension<T>.Humans[tuple.Human] = tuple.Value)
        ];
    }

    #region Object

    public static partial class Extension
    {
        public static int ToIndex(this ObjectCtrlInfo ctrl) => ctrl?.objectInfo?.ToIndex() ?? -1;

        public static int ToIndex(this ObjectInfo info) => DigitalCraft.DigitalCraft.Instance.SceneInfo.ObjectInfos.IndexOf(info);

        public static IObservable<ZipArchive> OnLoadScene => Hooks.LoadScene.Select(Extract).AsObservable();

        public static IObservable<ZipArchive> OnSaveScene => SaveScene.AsObservable();

        public static IObservable<(ZipArchive Archive, int Offset)> OnImportScene =>
            Hooks.ImportScene.AsObservable().Select(entry => (Extract(entry.Path), entry.Offset));
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
        static readonly Dictionary<int, T> Storage = new();

        public static Dictionary<int, T> Items => Storage;

        public static Action<Stream, Dictionary<int,T>> SerializeItems =
            Json<Dictionary<int,T>>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, Dictionary<int,T>> DeserializeItems =
            Json<Dictionary<int,T>>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene.Select(entry => entry.Archive))
                .Subscribe(archive => archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, archive, entry)));

        public static IObservable<(OIItemInfo Info, T Value)> OnPreprocessItem =>
            OnLoadValue.SelectMany(iv => Extension.OnPreprocessItem
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => (entry.Info, iv.Value)));

        public static IObservable<(int Index, T Value)> OnLoadItem =>
            OnLoadValue.SelectMany(iv => Extension.OnLoadItem
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => iv));
    }
    public static partial class Extension
    {
        public static IObservable<(int Index, OCIItem Value)> OnPrepareSaveItem => Hooks.PrepareSaveItem.AsObservable();

        public static IObservable<(int Index, OIItemInfo Info)> OnPreprocessItem =>
            Hooks.TrackItem.AsObservable().SelectMany(item =>
                Hooks.PreprocessObject.AsObservable()
                    .Where(entry => item.Pointer == entry.Info.Pointer)
                    .Select(entry => (entry.Index, item)).FirstAsync());

        public static IObservable<(int Index, OCIItem Value)> OnLoadItem =>
            OnPreprocessItem.Select(entry => entry.Index)
                .SelectMany(index => Hooks.ObjectCtrlResolve.AsObservable()
                    .Where(entry => entry.Index == index).FirstAsync()
                    .Select(entry => (entry.Index, new OCIItem(entry.Value.Pointer))));

        public static IObservable<(int Index, OCIItem Value)> OnAttachItem =>
            Hooks.ObjectCtrlAttach.AsObservable()
                .Where(entry => entry.Value.objectInfo.Kind == 1)
                .Select(entry => (entry.Index, new OCIItem(entry.Value.Pointer)));

        public static IObservable<(int Index, OCIItem Value)> OnDeleteItem => Hooks.DeleteItem.AsObservable();

        public static IDisposable[] RegisterItem<T>() where T : new() => [
            OnSaveScene.Subscribe(ItemExtension<T>.SaveScene),
            ItemExtension<T>.OnLoadItem.Subscribe(tuple => ItemExtension<T>.Items[tuple.Index] = tuple.Value),
            OnDeleteItem.Subscribe(tuple => ItemExtension<T>.Items.Remove(tuple.Index))
        ];
    }
    #endregion 

    #region Light
    [AttributeUsage(AttributeTargets.Class)]
    public class LightExtensionAttribute<V> : PathAttribute where V : new()
    {
        public LightExtensionAttribute(params string[] paths) : base(paths) {}
    }
    public static partial class LightExtension<T> where T: new()
    {
        static readonly Dictionary<int, T> Storage = new();

        public static Dictionary<int, T> Lights => Storage;

        public static Action<Stream, Dictionary<int,T>> SerializeLights =
            Json<Dictionary<int,T>>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, Dictionary<int,T>> DeserializeLights =
            Json<Dictionary<int,T>>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene.Select(entry => entry.Archive))
                .Subscribe(archive => archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, archive, entry)));

        public static IObservable<(OILightInfo Info, T Value)> OnPreprocessLight =>
            OnLoadValue.SelectMany(iv => Extension.OnPreprocessLight
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => (entry.Info, iv.Value)));

        public static IObservable<(int Index, T Value)> OnLoadLight =>
            OnLoadValue.SelectMany(iv => Extension.OnLoadLight
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => iv));
    }
    public static partial class Extension
    {
        public static IObservable<(int Index, OCILight Value)> OnPrepareSaveLight => Hooks.PrepareSaveLight.AsObservable();

        public static IObservable<(int Index, OILightInfo Info)> OnPreprocessLight =>
            Hooks.TrackLight.AsObservable().SelectMany(item =>
                Hooks.PreprocessObject.AsObservable()
                    .Where(entry => item.Pointer == entry.Info.Pointer)
                    .Select(entry => (entry.Index, item)).FirstAsync());

        public static IObservable<(int Index, OCILight Value)> OnLoadLight =>
            OnPreprocessLight.Select(entry => entry.Index)
                .SelectMany(index => Hooks.ObjectCtrlResolve.AsObservable()
                    .Where(entry => entry.Index == index).FirstAsync()
                    .Select(entry => (entry.Index, new OCILight(entry.Value.Pointer))));

        public static IObservable<(int Index, OCILight Value)> OnAttachLight =>
            Hooks.ObjectCtrlAttach.AsObservable()
                .Where(entry => entry.Value.objectInfo.Kind == 2)
                .Select(entry => (entry.Index, new OCILight(entry.Value.Pointer)));

        public static IObservable<(int Index, OCILight Value)> OnDeleteLight => Hooks.DeleteLight.AsObservable();

        public static IDisposable[] RegisterLight<T>() where T : new() => [
            OnSaveScene.Subscribe(LightExtension<T>.SaveScene),
            LightExtension<T>.OnLoadLight.Subscribe(tuple => LightExtension<T>.Lights[tuple.Index] = tuple.Value),
            OnDeleteLight.Subscribe(tuple => LightExtension<T>.Lights.Remove(tuple.Index))
        ];
    }
    #endregion

    #region Route
    [AttributeUsage(AttributeTargets.Class)]
    public class RouteExtensionAttribute<V> : PathAttribute where V : new()
    {
        public RouteExtensionAttribute(params string[] paths) : base(paths) {}
    }
    public static partial class RouteExtension<T> where T: new()
    {
        static readonly Dictionary<int, T> Storage = new();

        public static Dictionary<int, T> Routes => Storage;

        public static Action<Stream, Dictionary<int,T>> SerializeRoutes =
            Json<Dictionary<int,T>>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, Dictionary<int,T>> DeserializeRoutes =
            Json<Dictionary<int,T>>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene.Select(entry => entry.Archive))
                .Subscribe(archive => archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, archive, entry)));

        public static IObservable<(OIRouteInfo Info, T Value)> OnPreprocessRoute =>
            OnLoadValue.SelectMany(iv => Extension.OnPreprocessRoute
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => (entry.Info, iv.Value)));

        public static IObservable<(int Index, T Value)> OnLoadRoute =>
            OnLoadValue.SelectMany(iv => Extension.OnLoadRoute
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => iv));
    }
    public static partial class Extension
    {
        public static IObservable<(int Index, OCIRoute Value)> OnPrepareSaveRoute => Hooks.PrepareSaveRoute.AsObservable();

        public static IObservable<(int Index, OIRouteInfo Info)> OnPreprocessRoute =>
            Hooks.TrackRoute.AsObservable().SelectMany(item =>
                Hooks.PreprocessObject.AsObservable()
                    .Where(entry => item.Pointer == entry.Info.Pointer)
                    .Select(entry => (entry.Index, item)).FirstAsync());

        public static IObservable<(int Index, OCIRoute Value)> OnLoadRoute =>
            OnPreprocessRoute.Select(entry => entry.Index)
                .SelectMany(index => Hooks.ObjectCtrlResolve.AsObservable()
                    .Where(entry => entry.Index == index).FirstAsync()
                    .Select(entry => (entry.Index, new OCIRoute(entry.Value.Pointer))));

        public static IObservable<(int Index, OCIRoute Value)> OnAttachRoute =>
            Hooks.ObjectCtrlAttach.AsObservable()
                .Where(entry => entry.Value.objectInfo.Kind == 4)
                .Select(entry => (entry.Index, new OCIRoute(entry.Value.Pointer)));

        public static IObservable<(int Index, OCIRoute Value)> OnDeleteRoute => Hooks.DeleteRoute.AsObservable();

        public static IDisposable[] RegisterRoute<T>() where T : new() => [
            OnSaveScene.Subscribe(RouteExtension<T>.SaveScene),
            RouteExtension<T>.OnLoadRoute.Subscribe(tuple => RouteExtension<T>.Routes[tuple.Index] = tuple.Value),
            OnDeleteRoute.Subscribe(tuple => RouteExtension<T>.Routes.Remove(tuple.Index))
        ];
    }
    #endregion

    #region Camera
    [AttributeUsage(AttributeTargets.Class)]
    public class CameraExtensionAttribute<V> : PathAttribute where V : new()
    {
        public CameraExtensionAttribute(params string[] paths) : base(paths) {}
    }
    public static partial class CameraExtension<T> where T: new()
    {
        static readonly Dictionary<int, T> Storage = new();

        public static Dictionary<int, T> Cameras => Storage;

        public static Action<Stream, Dictionary<int,T>> SerializeCameras =
            Json<Dictionary<int,T>>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, Dictionary<int,T>> DeserializeCameras =
            Json<Dictionary<int,T>>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene.Select(entry => entry.Archive))
                .Subscribe(archive => archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, archive, entry)));

        public static IObservable<(OICameraInfo Info, T Value)> OnPreprocessCamera =>
            OnLoadValue.SelectMany(iv => Extension.OnPreprocessCamera
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => (entry.Info, iv.Value)));

        public static IObservable<(int Index, T Value)> OnLoadCamera =>
            OnLoadValue.SelectMany(iv => Extension.OnLoadCamera
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => iv));
    }
    public static partial class Extension
    {
        public static IObservable<(int Index, OCICamera Value)> OnPrepareSaveCamera => Hooks.PrepareSaveCamera.AsObservable();

        public static IObservable<(int Index, OICameraInfo Info)> OnPreprocessCamera =>
            Hooks.TrackCamera.AsObservable().SelectMany(item =>
                Hooks.PreprocessObject.AsObservable()
                    .Where(entry => item.Pointer == entry.Info.Pointer)
                    .Select(entry => (entry.Index, item)).FirstAsync());

        public static IObservable<(int Index, OCICamera Value)> OnLoadCamera =>
            OnPreprocessCamera.Select(entry => entry.Index)
                .SelectMany(index => Hooks.ObjectCtrlResolve.AsObservable()
                    .Where(entry => entry.Index == index).FirstAsync()
                    .Select(entry => (entry.Index, new OCICamera(entry.Value.Pointer))));

        public static IObservable<(int Index, OCICamera Value)> OnAttachCamera =>
            Hooks.ObjectCtrlAttach.AsObservable()
                .Where(entry => entry.Value.objectInfo.Kind == 5)
                .Select(entry => (entry.Index, new OCICamera(entry.Value.Pointer)));

        public static IObservable<(int Index, OCICamera Value)> OnDeleteCamera => Hooks.DeleteCamera.AsObservable();

        public static IDisposable[] RegisterCamera<T>() where T : new() => [
            OnSaveScene.Subscribe(CameraExtension<T>.SaveScene),
            CameraExtension<T>.OnLoadCamera.Subscribe(tuple => CameraExtension<T>.Cameras[tuple.Index] = tuple.Value),
            OnDeleteCamera.Subscribe(tuple => CameraExtension<T>.Cameras.Remove(tuple.Index))
        ];
    }
    #endregion

    #region Folder
    [AttributeUsage(AttributeTargets.Class)]
    public class FolderExtensionAttribute<V> : PathAttribute where V : new()
    {
        public FolderExtensionAttribute(params string[] paths) : base(paths) {}
    }
    public static partial class FolderExtension<T> where T: new()
    {
        static readonly Dictionary<int, T> Storage = new();

        public static Dictionary<int, T> Folders => Storage;

        public static Action<Stream, Dictionary<int,T>> SerializeFolders =
            Json<Dictionary<int,T>>.Save.Apply(Plugin.Instance.Log.LogError);

        public static Func<Stream, Dictionary<int,T>> DeserializeFolders =
            Json<Dictionary<int,T>>.Load.Apply(Plugin.Instance.Log.LogError);

        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnLoadScene.Merge(Extension.OnImportScene.Select(entry => entry.Archive))
                .Subscribe(archive => archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, archive, entry)));

        public static IObservable<(OIFolderInfo Info, T Value)> OnPreprocessFolder =>
            OnLoadValue.SelectMany(iv => Extension.OnPreprocessFolder
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => (entry.Info, iv.Value)));

        public static IObservable<(int Index, T Value)> OnLoadFolder =>
            OnLoadValue.SelectMany(iv => Extension.OnLoadFolder
                .Where(entry => iv.Index == entry.Index).FirstAsync().Select(entry => iv));
    }
    public static partial class Extension
    {
        public static IObservable<(int Index, OCIFolder Value)> OnPrepareSaveFolder => Hooks.PrepareSaveFolder.AsObservable();

        public static IObservable<(int Index, OIFolderInfo Info)> OnPreprocessFolder =>
            Hooks.TrackFolder.AsObservable().SelectMany(item =>
                Hooks.PreprocessObject.AsObservable()
                    .Where(entry => item.Pointer == entry.Info.Pointer)
                    .Select(entry => (entry.Index, item)).FirstAsync());

        public static IObservable<(int Index, OCIFolder Value)> OnLoadFolder =>
            OnPreprocessFolder.Select(entry => entry.Index)
                .SelectMany(index => Hooks.ObjectCtrlResolve.AsObservable()
                    .Where(entry => entry.Index == index).FirstAsync()
                    .Select(entry => (entry.Index, new OCIFolder(entry.Value.Pointer))));

        public static IObservable<(int Index, OCIFolder Value)> OnAttachFolder =>
            Hooks.ObjectCtrlAttach.AsObservable()
                .Where(entry => entry.Value.objectInfo.Kind == 3)
                .Select(entry => (entry.Index, new OCIFolder(entry.Value.Pointer)));

        public static IObservable<(int Index, OCIFolder Value)> OnDeleteFolder => Hooks.DeleteFolder.AsObservable();

        public static IDisposable[] RegisterFolder<T>() where T : new() => [
            OnSaveScene.Subscribe(FolderExtension<T>.SaveScene),
            FolderExtension<T>.OnLoadFolder.Subscribe(tuple => FolderExtension<T>.Folders[tuple.Index] = tuple.Value),
            OnDeleteFolder.Subscribe(tuple => FolderExtension<T>.Folders.Remove(tuple.Index))
        ];
    }
    #endregion

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}