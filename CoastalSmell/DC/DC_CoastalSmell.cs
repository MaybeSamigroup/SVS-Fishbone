using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnityEngine;
using DigitalCraft;
using ILLGames.Unity.UI.ColorPicker;
using HarmonyLib;
using Il2CppSystem.Linq;
using System.Collections.Generic;

namespace CoastalSmell
{
    public enum TargetType
    {
        Undefined = 0,
        Chara = 2,
        Item = 4,
        Light = 8,
        Folder = 16,
        Route = 32,
        Camera = 64,
        Text = 128,
    }
    public static class DigitalCraftExtension
    {
        public static IObservable<MainScene> OnSceneStartup =>
            Hooks.OnSceneLoad.Where("Main".Equals).Select(_ => MainScene.Instance);

        public static IObservable<Unit> OnSceneDestroy =>
            OnSceneStartup.SelectMany(scene => scene.OnDestroyAsObservable());

        public static TargetType Classify(this ObjectInfo info) =>
            info.Kind switch
            {
                0 => TargetType.Chara,
                1 => TargetType.Item,
                2 => TargetType.Light,
                3 => TargetType.Folder,
                4 => TargetType.Route,
                5 => TargetType.Camera,
                7 => TargetType.Folder,
                _ => TargetType.Undefined
            };
        public static TargetType Classify(this ObjectCtrlInfo ctrl) =>
            ctrl?.objectInfo?.Classify() ?? TargetType.Undefined;

        public static TargetType Classify(this TreeNodeObject node) =>
            node?.ObjectCtrl?.Classify() ?? TargetType.Undefined;

        public static IEnumerable<V> Where<V>(this IEnumerable<(TargetType, V)> query, TargetType types) =>
            query.Where(tuple => (types & tuple.Item1) is not TargetType.Undefined).Select(tuple => tuple.Item2);

        public static IObservable<V> Where<V>(this IObservable<(TargetType, V)> query, TargetType types) =>
            query.Where(tuple => (types & tuple.Item1) is not TargetType.Undefined).Select(tuple => tuple.Item2);

        public static IObservable<Unit> OnSelectNothing =>
            Hooks.SelectionChange.AsObservable()
                .Where(nodes => nodes.Length == 0).Select(_ => Unit.Default);

        public static IObservable<IEnumerable<(TargetType, ObjectCtrlInfo)>> OnSelectMultiple =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length > 1)
                .Select(nodes => nodes.Select(node => (node.Classify(), node.ObjectCtrl)));

        public static IObservable<(TargetType, ObjectCtrlInfo)> OnSelectSingle =>
            Hooks.SelectionChange.AsObservable().Where(nodes => nodes.Length == 1)
                .SelectMany(nodes => nodes.Select(node => (node.Classify(), node.ObjectCtrl)));
    }

    public static partial class Hooks
    {
        internal static Subject<TreeNodeObject[]> SelectionChange = new();
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.DeselectAll), [])]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.Deselect), typeof(TreeNodeObject))]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.AddSelect), typeof(TreeNodeObject), typeof(bool))]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectSingle), typeof(TreeNodeObject), typeof(bool), typeof(bool))]
        [HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectMultiple), typeof(TreeNodeObject), typeof(TreeNodeObject))]
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
