using System;
using System.Linq;
using System.Collections.Generic; 
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnityEngine;
using DigitalCraft;
using ILLGames.Unity.UI.ColorPicker;
using HarmonyLib;
using Il2CppSystem.Linq;
using Scene = (
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICharInfo Info)> Charas,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIItemInfo Info)> Items,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OILightInfo Info)> Lights,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIFolderInfo Info)> Folders,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OIRouteInfo Info)> Routes,
    System.Collections.Generic.IEnumerable<(int[] Indices, DigitalCraft.OICameraInfo Info)> Cameras);

namespace CoastalSmell
{
    public interface TargetObject<T,U> where T: ObjectInfo where U: ObjectCtrlInfo
    {
        public T ToInfo(U value);
        public bool ToCtrl(ObjectCtrlInfo obj, out U value);
        public IEnumerable<(int[] Indices, T Info)> Entries(Scene scene);
    }
    public sealed class Chara: TargetObject<OICharInfo, OCIChar>
    {
        public static Chara Instance = new ();
        private Chara() { }
        public OICharInfo ToInfo(OCIChar value) => value.oiCharInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCIChar value) =>
            (value = obj?.objectInfo?.Kind is 0 ? new OCIChar(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OICharInfo Info)> Entries(Scene scene) => scene.Charas;
    }
    public sealed class Item: TargetObject<OIItemInfo, OCIItem>
    {
        public static Item Instance = new ();
        private Item() { }
        public OIItemInfo ToInfo(OCIItem value) => value.ItemInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCIItem value) =>
            (value = obj?.objectInfo?.Kind is 1 ? new OCIItem(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OIItemInfo Info)> Entries(Scene scene) => scene.Items;
    }
    public sealed class Light: TargetObject<OILightInfo, OCILight>
    {
        public static Light Instance = new ();
        private Light() { }
        public OILightInfo ToInfo(OCILight value) => value.LightInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCILight value) =>
            (value = obj?.objectInfo?.Kind is 2 ? new OCILight(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OILightInfo Info)> Entries(Scene scene) => scene.Lights;
    }
    public sealed class Folder: TargetObject<OIFolderInfo, OCIFolder>
    {
        public static Folder Instance = new ();
        private Folder() { }
        public OIFolderInfo ToInfo(OCIFolder value) => value.FolderInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCIFolder value) =>
            (value = obj?.objectInfo?.Kind is 3 ? new OCIFolder(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OIFolderInfo Info)> Entries(Scene scene) => scene.Folders;
    }
    public sealed class Route: TargetObject<OIRouteInfo, OCIRoute>
    {
        public static Route Instance = new ();
        private Route() { }
        public OIRouteInfo ToInfo(OCIRoute value) => value.RouteInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCIRoute value) =>
            (value = obj?.objectInfo?.Kind is 4 ? new OCIRoute(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OIRouteInfo Info)> Entries(Scene scene) => scene.Routes;
    }
    public sealed class Camera: TargetObject<OICameraInfo, OCICamera>
    {
        public static Camera Instance = new ();
        private Camera() { }
        public OICameraInfo ToInfo(OCICamera value) => value.CameraInfo;
        public bool ToCtrl(ObjectCtrlInfo obj, out OCICamera value) =>
            (value = obj?.objectInfo?.Kind is 5 ? new OCICamera(obj.Pointer) : null) is not null;
        public IEnumerable<(int[] Indices, OICameraInfo Info)> Entries(Scene scene) => scene.Cameras;
    }
    public static class DigitalCraftExtension
    {
        public static IObservable<MainScene> OnSceneStartup =>
            Hooks.OnSceneLoaded.Where("Main".Equals).Select(_ => MainScene.Instance);

        public static IObservable<Unit> OnSceneDestroy =>
            OnSceneStartup.SelectMany(scene => scene.OnDestroyAsObservable());

        public static IObservable<Unit> OnSelectNothing =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 0).Select(_ => Unit.Default);

        public static IObservable<Unit> OnSelectMultiple =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length > 1).Select(_ => Unit.Default);

        public static IObservable<OCIChar> OnSelectSingleChara =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 0)
                    .Select(node => new OCIChar(node.ObjectCtrl.Pointer));

        public static IObservable<OCIItem> OnSelectSingleItem =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 1)
                    .Select(node => new OCIItem(node.ObjectCtrl.Pointer));

        public static IObservable<OCILight> OnSelectSingleLight =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 2)
                    .Select(node => new OCILight(node.ObjectCtrl.Pointer));

        public static IObservable<OCIFolder> OnSelectSingleFolder =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 3)
                    .Select(node => new OCIFolder(node.ObjectCtrl.Pointer));

        public static IObservable<OCIRoute> OnSelectSingleRoute =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 4)
                    .Select(node => new OCIRoute(node.ObjectCtrl.Pointer));

        public static IObservable<OCICamera> OnSelectSingleCamera =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => (node?.ObjectInfo?.Kind ?? -1) is 5)
                    .Select(node => new OCICamera(node.ObjectCtrl.Pointer));

        public static IObservable<ObjectCtrlInfo> OnSelectSingleOthers =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .Select(nodes => nodes[0]).Where(node => !((node?.ObjectInfo?.Kind ?? -1) is 0 or 1 or 2 or 3 or 4 or 5))
                .Select(node => node.ObjectCtrl);
    }

    public static partial class Hooks
    {
        internal static Subject<TreeNodeObject[]> SelectionChange = new();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectSingle), typeof(TreeNodeObject), typeof(bool), typeof(bool))]
        static void ObjectCtrlInfoOnSelectPostfix() =>
            SelectionChange.OnNext((TreeNodeObject[])DigitalCraft.DigitalCraft.Instance.TreeNodeCtrl.SelectNodes.ToArray());
    }

    public static partial class UGUI
    {
        static Action<Unit> ColorPaletteSetup(string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha, bool autoOpen) =>
            _ => ColorPalette.Instance.Setup(name, getColor(), setColor, useAlpha, autoOpen);
        public static UIAction ThumbnailColor(string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => Component<ThumbnailColor>(ui => ui._button.OnClickAsObservable()
            .Subscribe(ColorPaletteSetup(name, getColor, ui.SetGraphic + setColor, useAlpha, autoOpen)));
    }

    #region Plugin
    public partial class Plugin
    {
        public const string Process = "DigitalCraft";
    }
    #endregion
}
