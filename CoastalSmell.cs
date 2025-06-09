using System;
using System.Linq;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using ILLGames.Unity.Component;
using TMPro;

namespace CoastalSmell
{
    public static class Util<T> where T : SingletonInitializer<T>
    {
        static readonly Il2CppSystem.Threading.CancellationTokenSource Canceler = new();
        static Action<Action, Action> AwaitStartup = (onStartup, onDestroy) =>
            Observable.NextFrame(FrameCountType.Update)
                .Subscribe(Hook.Apply(onStartup).Apply(onDestroy).Ignoring<Unit>());
        static Action<Action, Action> AwaitDestroy = (onStartup, onDestroy) =>
            SingletonInitializer<T>.Instance.OnDestroyAsObservable().With(onStartup)
                .Subscribe(AwaitStartup.Apply(onStartup).Apply(onDestroy).Ignoring<Unit>());
        public static Action<Action, Action> Hook = (onStartup, onDestroy) =>
             SingletonInitializer<T>.WaitUntilSetup(Canceler.Token)
                .ContinueWith(AwaitDestroy.Apply(onStartup).Apply(onDestroy));
    }
    public delegate void Either(Action a, Action b);
    /// <summary>
    /// functional utilities for favor
    /// </summary>
    public static class F
    {
        public static Either Either(this bool value) => value ? (_, a2) => a2() : (a1, _) => a1();
        public static void Either(this bool value, Action a1, Action a2) => Either(value)(a1, a2);
        public static void Maybe(this bool value, Action action) => Either(value)(() => { }, action);
        public static Action Ignoring<O>(this Func<O> f) =>
            () => f();
        public static Action<I1> Ignoring<I1>(this Action action) =>
            _ => action();
        public static Action Apply<I1>(this Action<I1> action, I1 i1) =>
            () => action(i1);
        public static Action Apply<I1, I2>(this Action<I1, I2> action, I1 i1, I2 i2) =>
            () => action(i1, i2);
        public static Action Apply<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1, I2 i2, I3 i3) =>
            () => action(i1, i2, i3);
        public static Action Apply<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1, I2 i2, I3 i3, I4 i4) =>
            () => action(i1, i2, i3, i4);
        public static Action Apply<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1, I2 i2, I3 i3, I4 i4, I5 i5) =>
            () => action(i1, i2, i3, i4, i5);
        public static Action<I2> Apply<I1, I2>(this Action<I1, I2> action, I1 i1) =>
            (i2) => action(i1, i2);
        public static Action<I2, I3> Apply<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1) =>
            (i2, i3) => action(i1, i2, i3);
        public static Action<I2, I3, I4> Apply<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1) =>
            (i2, i3, i4) => action(i1, i2, i3, i4);
        public static Action<I2, I3, I4, I5> Apply<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1) =>
            (i2, i3, i4, i5) => action(i1, i2, i3, i4, i5);
        public static Action ApplyDisposable<I1>(this Action<I1> action, I1 i1) where I1 : IDisposable =>
            () => { using (i1) { action(i1); } };
        public static Action<I2> ApplyDisposable<I1, I2>(this Action<I1, I2> action, I1 i1) where I1 : IDisposable =>
            (i2) => { using (i1) { action(i1, i2); } };
        public static Action<I2, I3> ApplyDisposable<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1) where I1 : IDisposable =>
            (i2, i3) => { using (i1) { action(i1, i2, i3); } };
        public static Action<I2, I3, I4> ApplyDisposable<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1) where I1 : IDisposable =>
            (i2, i3, i4) => { using (i1) { action(i1, i2, i3, i4); } };
        public static Action<I2, I3, I4, I5> ApplyDisposable<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1) where I1 : IDisposable =>
            (i2, i3, i4, i5) => { using (i1) { action(i1, i2, i3, i4, i5); } };
        public static Func<O> Apply<I1, O>(this Func<I1, O> f, I1 i1) =>
            () => f(i1);
        public static Func<O> Apply<I1, I2, O>(this Func<I1, I2, O> f, I1 i1, I2 i2) =>
            () => f(i1, i2);
        public static Func<O> Apply<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1, I2 i2, I3 i3) =>
            () => f(i1, i2, i3);
        public static Func<O> Apply<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1, I2 i2, I3 i3, I4 i4) =>
            () => f(i1, i2, i3, i4);
        public static Func<O> Apply<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1, I2 i2, I3 i3, I4 i4, I5 i5) =>
            () => f(i1, i2, i3, i4, i5);
        public static Func<I2, O> Apply<I1, I2, O>(this Func<I1, I2, O> f, I1 i1) =>
            (i2) => f(i1, i2);
        public static Func<I2, I3, O> Apply<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1) =>
            (i2, i3) => f(i1, i2, i3);
        public static Func<I2, I3, I4, O> Apply<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1) =>
            (i2, i3, i4) => f(i1, i2, i3, i4);
        public static Func<I2, I3, I4, I5, O> Apply<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1) =>
            (i2, i3, i4, i5) => f(i1, i2, i3, i4, i5);
        public static Func<O> ApplyDisposable<I1, O>(this Func<I1, O> f, I1 i1) where I1 : IDisposable =>
            () => { using(i1) { return f(i1); } };
        public static Func<I2, O> ApplyDisposable<I1, I2, O>(this Func<I1, I2, O> f, I1 i1) where I1 : IDisposable =>
           (i2) => { using(i1) { return f(i1, i2); } };
        public static Func<I2, I3, O> ApplyDisposable<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1) where I1 : IDisposable =>
            (i2, i3) => { using(i1) { return f(i1, i2, i3); } };
        public static Func<I2, I3, I4, O> ApplyDisposable<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1) where I1: IDisposable =>
            (i2, i3, i4) => { using (i1) { return f(i1, i2, i3, i4); } };
        public static Func<I2, I3, I4, I5, O> ApplyDisposable<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1) where I1 : IDisposable =>
            (i2, i3, i4, i5) => { using (i1) { return f(i1, i2, i3, i4, i5); } };
        public static T With<T>(this T value, Action<T> action) => value.With(action.Apply(value));
        public static T With<T, U>(this T value, Func<T, U> f) => value.With(f.Apply(value).Ignoring());
        public static T With<T>(this T value, Action action)
        {
            action();
            return value;
        }
        public static void Try(this Action action, Action<string> log)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                log(e.Message);
                log(e.StackTrace);
            }
        }
        public static bool Try<T>(this Func<T> f, Action<string> log, out T value)
        {
            try
            {
                value = f();
                return true;
            }
            catch (Exception e)
            {
                log(e.Message);
                log(e.StackTrace);
                value = default;
            }
            return false;
        }
        public static Action DoNoting = () => { };
        public static Func<Action, T, Action> Accumulate<T>(Action<T> action) =>
            (actions, value) => actions += action.Apply(value);
        public static IEnumerable<Tuple<int, T>> Index<T>(IEnumerable<T> values) =>
            values.Select<T, Tuple<int, T>>((v, i) => new(i, v));
        public static void ForEach<T>(this IEnumerable<T> values, Action<T> action) =>
            values.Aggregate(DoNoting, Accumulate(action))();
    }
    public static class UGUI
    {
        static Func<Transform, Transform> TransformAt(string[] paths) =>
            paths.Length == 0 ? tf => tf : tf => TransformAt(paths[1..])(tf.Find(paths[0]));
        static Func<Transform, GameObject> GoAt = tf => tf.gameObject;
        public static Func<Action<GameObject>, Action<GameObject>> ModifyAt(params string[] paths) =>
            action => go => action(GoAt(TransformAt(paths)(go.transform)));
        public static Action<GameObject> DestroyAt(params string[] paths) =>
            ModifyAt(paths)(UnityEngine.Object.Destroy);
        public static Action<GameObject> DestroyChildren =
            go => Enumerable.Range(0, go.transform.childCount).Select(go.transform.GetChild).Select(GoAt).ForEach(UnityEngine.Object.Destroy);
        public static Action<GameObject> Go(Transform parent = null, string name = null, bool? active = null) =>
            go => (go.transform.parent, go.name, go.active) = (parent ?? go.transform.parent, name ?? go.name, active ?? go.active);
        public static Action<GameObject> Cmp<T>(this Action<T> action) where T : Component =>
            go => action(ObservableTriggerExtensions.GetOrAddComponent<T>(go));
        public static Action<T> Behavior<T>(bool? enabled) where T : Behaviour => ui => ui.enabled = enabled ?? ui.enabled;
        public static Action<RectTransform> Rt(
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null,
            Vector2? offsetMin = null,
            Vector2? offsetMax = null,
            Vector2? sizeDelta = null
        ) => ui => (
            ui.anchorMin,
            ui.anchorMax,
            ui.anchoredPosition,
            ui.pivot,
            ui.offsetMin,
            ui.offsetMax,
            ui.sizeDelta
        ) = (
            anchorMin ?? ui.anchorMin,
            anchorMax ?? ui.anchorMax,
            anchoredPosition ?? ui.anchoredPosition,
            pivot ?? ui.pivot,
            offsetMin ?? ui.offsetMin,
            offsetMax ?? ui.offsetMax,
            sizeDelta ?? ui.sizeDelta
        );
        public static Action<T> LayoutGroup<T>(
            bool? childScaleWidth = null,
            bool? childScaleHeight = null,
            bool? childControlWidth = null,
            bool? childControlHeight = null,
            bool? childForceExpandWidth = null,
            bool? childForceExpandHeight = null,
            bool? reverseArrangement = null,
            float? spacing = null,
            RectOffset padding = null,
            TextAnchor? childAlignmentt = null
        ) where T : HorizontalOrVerticalLayoutGroup => ui => (
            ui.childScaleWidth,
            ui.childScaleHeight,
            ui.childControlWidth,
            ui.childControlHeight,
            ui.childForceExpandWidth,
            ui.childForceExpandHeight,
            ui.reverseArrangement,
            ui.spacing,
            ui.padding,
            ui.childAlignment
        ) = (
            childScaleWidth ?? ui.childScaleWidth,
            childScaleHeight ?? ui.childScaleHeight,
            childControlWidth ?? ui.childControlWidth,
            childControlHeight ?? ui.childControlHeight,
            childForceExpandWidth ?? ui.childForceExpandWidth,
            childForceExpandHeight ?? ui.childForceExpandHeight,
            reverseArrangement ?? ui.reverseArrangement,
            spacing ?? ui.spacing,
            padding ?? ui.padding,
            childAlignmentt ?? ui.childAlignment
        );
        public static Action<LayoutElement> Layout(float? width = null, float? height = null) =>
            ui => (ui.preferredWidth, ui.preferredHeight) = (width ?? ui.preferredWidth, height ?? ui.preferredHeight);
        public static Action<ContentSizeFitter> Fitter(ContentSizeFitter.FitMode? horizontal = null, ContentSizeFitter.FitMode? vertical = null) =>
            ui => (ui.horizontalFit, ui.verticalFit) = (horizontal ?? ui.horizontalFit, vertical ?? ui.verticalFit);
        public static Action<TextMeshProUGUI> Text(
            bool? enableAutoSizing = null,
            bool? autoSizeTextContainer = null,
            TextOverflowModes? overflowMode = null,
            HorizontalAlignmentOptions? horizontalAlignment = null,
            VerticalAlignmentOptions? verticalAlignment = null,
            string text = null
        ) => ui => (
            ui.enableAutoSizing,
            ui.autoSizeTextContainer,
            ui.overflowMode,
            ui.horizontalAlignment,
            ui.verticalAlignment,
            ui.m_text
        ) = (
            enableAutoSizing ?? ui.enableAutoSizing,
            autoSizeTextContainer ?? ui.autoSizeTextContainer,
            overflowMode ?? ui.overflowMode,
            horizontalAlignment ?? ui.horizontalAlignment,
            verticalAlignment ?? ui.verticalAlignment,
            text ?? ui.m_text
        );
        public static Action<TMP_InputField> Input(
            bool? restoreOriginalTextOnEscape,
            int? characterLimit,
            int? lineLimit,
            TMP_InputField.ContentType? contentType,
            TMP_InputField.LineType? lineType
        ) => ui => (
            ui.restoreOriginalTextOnEscape,
            ui.characterLimit,
            ui.lineLimit,
            ui.contentType,
            ui.lineType
        ) = (
            restoreOriginalTextOnEscape ?? ui.restoreOriginalTextOnEscape,
            characterLimit ?? ui.characterLimit,
            lineLimit ?? ui.lineLimit,
            contentType ?? ui.contentType,
            lineType ?? ui.lineType
        );
        public static Action<ToggleGroup> ToggleGroup(bool? allowSwitchOff = null) => ui =>
            ui.allowSwitchOff = allowSwitchOff ?? ui.allowSwitchOff;
        public static Action<Toggle> Toggle(ToggleGroup group) => ui =>
            ui.group = group ?? ui.group;
        public static Action<Selectable> Selectable(bool? interactable) => ui =>
            ui.interactable = interactable ?? ui.interactable;
        public static GameObject Test() =>
            new GameObject("go").With(Go())
                .With(Cmp(Rt(anchorMin: new(0, 0), anchorMax: new(0, 0))))
                .With(ModifyAt("foo", "bar", "baz")(Cmp(Rt()) + Cmp(Layout())));
    }
}