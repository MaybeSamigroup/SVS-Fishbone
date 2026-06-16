using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnityEngine;
using Character;
using DigitalCraft;
using ILLGames.Unity.UI.ColorPicker;
using HarmonyLib;
using Il2CppSystem.Linq;

namespace CoastalSmell
{
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
            _ => DigitalCraft.ColorPalette.Instance.Setup(name, getColor(), setColor, useAlpha, autoOpen);
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
