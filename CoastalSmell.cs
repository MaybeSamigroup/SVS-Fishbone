using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using ILLGames.Unity.UI;
using ILLGames.Unity.Component;
using ILLGames.Unity.UI.ColorPicker;
using ScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode;
using ScreenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode;

namespace CoastalSmell
{

    public static partial class Util
    {
         public static Action<Func<bool>, Action> DoOnCondition =
            (predicate, action) => predicate()
                .Either(DoNextFrame.Apply(DoOnCondition.Apply(predicate).Apply(action)), action);
    }

    public static class Util<T> where T : SingletonInitializer<T>
    {
        static readonly Il2CppSystem.Threading.CancellationTokenSource Canceler = new();
        static Action<Action, Action> AwaitStartup = (onStartup, onDestroy) =>
            Util.DoNextFrame.With(onDestroy)(Hook.Apply(onStartup).Apply(onDestroy));
        static Action<Action, Action> AwaitDestroy = (onStartup, onDestroy) =>
            SingletonInitializer<T>.Instance.With(onStartup).OnDestroyAsObservable()
                .Subscribe(AwaitStartup.Apply(onStartup).Apply(onDestroy).Ignoring<Unit>());
        public static Action<Action, Action> Hook = (onStartup, onDestroy) =>
             SingletonInitializer<T>.WaitUntilSetup(Canceler.Token)
                .ContinueWith(AwaitDestroy.Apply(onStartup).Apply(onDestroy));
    }
    /// <summary>
    /// functional utilities for favor
    /// </summary>
    public static class F
    {
        public static void Either(this bool value, Action a1, Action a2) => (value ? a2 : a1)();
        public static void Maybe(this bool value, Action action) => value.Either(DoNothing, action);
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
            () => { using (i1) { return f(i1); } };
        public static Func<I2, O> ApplyDisposable<I1, I2, O>(this Func<I1, I2, O> f, I1 i1) where I1 : IDisposable =>
           (i2) => { using (i1) { return f(i1, i2); } };
        public static Func<I2, I3, O> ApplyDisposable<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1) where I1 : IDisposable =>
            (i2, i3) => { using (i1) { return f(i1, i2, i3); } };
        public static Func<I2, I3, I4, O> ApplyDisposable<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1) where I1 : IDisposable =>
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
        public static Action Compose<T>(Func<T> get, Action<T> set) => () => set(get());
        public static Func<T> Constant<T>(T value) => () => value;
        public static Func<I, O> Constant<I, O>(this Action<I> f, O value) =>
            i => value.With(f.Apply(i));
        public static Action DoNothing = () => { };
        public static Func<Action, T, Action> Accumulate<T>(Action<T> action) =>
            (actions, value) => actions += action.Apply(value);
        public static IEnumerable<Tuple<T, int>> Index<T>(IEnumerable<T> values) =>
            values.Select<T, Tuple<T, int>>((v, i) => new(v, i));
        public static void ForEach<T>(this IEnumerable<T> values, Action<T> action) =>
            values.Aggregate(DoNothing, Accumulate(action))();
        public static void ForEachIndex<T>(this IEnumerable<T> values, Action<T, int> action) =>
            Index(values).Aggregate(DoNothing, Accumulate<Tuple<T, int>>(tuple => action(tuple.Item1, tuple.Item2)))();
        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<Tuple<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Item1, item => item.Item2);
        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Key, item => item.Value);
    }
    enum SimpleSprites
    {
        ToggleBg,
        ToggleNa,
        ToggleHi,
        ToggleOn,
        CheckOn,
        AlphaSample
    }
    enum BorderSprites
    {
        Border,
        DarkBg,
        LightBg,
        ColorBg,
        ButtonBg,
        ButtonNa,
        ButtonHi,
        ButtonOn
    }
    static class Sprites
    {
        static Dictionary<SimpleSprites, Sprite> Simples = new();
        static Dictionary<BorderSprites, Sprite> Borders = new();
        static string ToPath<T>(T item) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Guid, $"{item}.png");
        static void RegisterSprite(this GameObject go, Sprite item) =>
            go.With(UGUI.Cmp(UGUI.Image(sprite: item)));
        internal static Sprite Get(this SimpleSprites item) => Simples[item];
        internal static Sprite Get(this BorderSprites item) => Borders[item];
        static void Setup(Transform parent) =>
            (Simples, Borders) = (
                Enum.GetValues<SimpleSprites>()
                    .ToDictionary(item => item, item => UGUI.ToSimpleSprite(ToPath(item))
                        .With(new GameObject(item.ToString()).With(UGUI.Go(parent: parent)).RegisterSprite)),
                Enum.GetValues<BorderSprites>()
                    .ToDictionary(item => item, item => UGUI.ToBorderSprite(new(6, 6, 6, 6), ToPath(item))
                        .With(new GameObject(item.ToString()).With(UGUI.Go(parent: parent)).RegisterSprite)));
        static void Setup() =>
            Setup(new GameObject(Plugin.Name).With(UGUI.Go(parent: Manager.Scene.CommonSpace.transform)).transform);
        static bool Ready() =>
            Manager.Scene.CommonSpace != null;
        internal static void Initialize() =>
            Util.DoOnCondition(Ready, Setup);
    }
    public class WindowHandle
    {
        ConfigEntry<float> AnchorX;
        ConfigEntry<float> AnchorY;
        ConfigEntry<KeyboardShortcut> Shortcut;
        ConfigEntry<bool> State;
        public TextMeshProUGUI Title;
        public WindowHandle(BasePlugin plugin, string prefix, Vector2 anchor, KeyboardShortcut shortcut, bool visible = false) =>
            (AnchorX, AnchorY, Shortcut, State) = (
                plugin.Config.Bind("UI", $"{prefix} window anchor X", anchor.x),
                plugin.Config.Bind("UI", $"{prefix} window anchor Y", anchor.y),
                plugin.Config.Bind("UI", $"{prefix} window toggle key", shortcut),
                plugin.Config.Bind("UI", $"{prefix} window visibility", visible));
        void Toggle(GameObject go) => go.With(UGUI.Go(active: State.Value = !State.Value));
        Action<Unit> ToUpdate(GameObject go) =>
            _ => Shortcut.Value.IsDown().Maybe(F.Apply(Toggle, go));
        Action<Unit> ToUpdate(RectTransform ui) =>
            _ => (AnchorX.Value, AnchorY.Value) = (ui.anchoredPosition.x, ui.anchoredPosition.y);
        public void Apply(GameObject go) => go
            .With(UGUI.ModifyAt("Title", "Label")(UGUI.Cmp<TextMeshProUGUI>(ui => Title = ui)))
            .With(UGUI.Go(active: State.Value)).GetComponentInParent<ObservableUpdateTrigger>()
                .UpdateAsObservable().Subscribe(ToUpdate(go) + ToUpdate(go.GetComponent<RectTransform>()));
        public static implicit operator bool(WindowHandle handle) =>
            handle.State.Value;
        public static implicit operator Vector2(WindowHandle handle) =>
            new(handle.AnchorX.Value, handle.AnchorY.Value);
    }
    public class ChoiceList
    {
        GameObject View;
        Toggle State;
        TextMeshProUGUI Text;
        public ChoiceList(float width, float height, string name, params string[] values) =>
            View = UGUI.ScrollView(width, height * Math.Min(8, values.Length), name, UGUI.RootCanvas.gameObject)
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>()))
                .With(UGUI.Cmp(UGUI.ToggleGroup(allowSwitchOff: false)))
                .With(UGUI.Cmp(UGUI.Fitter()))
                .With(PopulateList(width, height, values))
                .transform.parent.parent.gameObject
                .With(UGUI.Cmp<LayoutElement>(UnityEngine.Object.Destroy))
                .With(UGUI.Cmp(UGUI.Rt(sizeDelta: new(width, height * Math.Min(8, values.Length)))))
                .With(UGUI.Go(active: false));
        Action<GameObject> PopulateList(float width, float height, string[] values) =>
            parent => values.ForEach(value =>
                UGUI.Toggle(width, height, value, parent)
                    .With(UGUI.Cmp<Toggle, ToggleGroup>((ui, group) => ui.group = group))
                    .GetComponent<Toggle>().OnPointerClickAsObservable()
                    .Subscribe(OnComplete(value)));
        Action<UnityEngine.EventSystems.PointerEventData> OnComplete(string value) =>
            state => (state.button == 0).Maybe(F.Apply(Complete, value));
        void Complete(string value) =>
            CloseChoice
                .With(F.Apply(View.SetActive, false))
                .With(F.Apply(Text.SetText, value, true))
                .With(F.Apply(State.Set, false, true))();
        void Cancel() =>
            CloseChoice
                .With(F.Apply(View.SetActive, false))
                .With(F.Apply(State.Set, false, false))();
        public void Assign(GameObject go) =>
            go.With(UGUI.Cmp<ObservableEnableTrigger>(ui => ui.OnDisableAsObservable().Subscribe(OnCancel)))
                .GetComponent<Toggle>().OnValueChangedAsObservable().Subscribe(OnValueChanged(go));
        Action<Unit> OnCancel => _ =>
            (State != null && Text != null).Maybe(Cancel);
        Action<bool> OnValueChanged(GameObject go) =>
            value => value.Maybe(F.Apply(OpenChoice, go));
        void OpenChoice(GameObject go) =>
            Relocate(View.With(UGUI.Go(active: true)).GetComponent<RectTransform>(),
                go.With(UGUI.ModifyAt($"{go.name}.State", $"{go.name}.Label")
                    (UGUI.Cmp<TextMeshProUGUI, Toggle>(Targets))).GetComponent<RectTransform>());
        Action CloseChoice =>
            () => (State, Text) = (null, null);
        void Relocate(RectTransform view, RectTransform ui) =>
            view.position = ui.position + new Vector3(
                (view.rect.width - ui.rect.width) / 2, -view.rect.height / 2 - ui.rect.height * 2, 0);
        void Targets(TextMeshProUGUI text, Toggle toggle) =>
            (State, Text) = (toggle, text.With(Initialize));
        void Initialize(TextMeshProUGUI text) =>
            View.GetComponentsInChildren<Toggle>()
                .Where(toggle => toggle.gameObject.name == text.text).First().Set(true, false);
    }

    public static partial class UGUI
    {
        static Func<string, Texture2D> ToTexture2D =
            (path) => new Texture2D(64, 64).With(t2d => t2d.LoadImage(File.ReadAllBytes(path)));
        static Func<Texture2D, Sprite> Texture2DToSimpleSprite =
            (t2d) => Sprite.Create(t2d, new(0, 0, t2d.width, t2d.height), new(0.5f, 0.5f));
        public static Func<string, Sprite> ToSimpleSprite =
            (path) => Texture2DToSimpleSprite(ToTexture2D(path));
        static Func<Vector4, Texture2D, Sprite> Texture2DToBorderSprite =
            (border, t2d) => Sprite.Create(t2d, new(0, 0, t2d.width, t2d.height), new(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        public static Func<Vector4, string, Sprite> ToBorderSprite =
            (border, path) => Texture2DToBorderSprite(border, ToTexture2D(path));
        static Func<Transform, Transform> TransformAt(string[] paths) =>
            paths.Length == 0 ? tf => tf : tf => TransformAt(paths[1..])(tf.Find(paths[0]));
        static Func<Transform, GameObject> GoAt = tf => tf.gameObject;
        public static Func<Action<GameObject>, Action<GameObject>> ModifyAt(params string[] paths) =>
            action => go => action(GoAt(TransformAt(paths)(go.transform)));
        public static Func<Action<GameObject>, Action<GameObject>> Content(string name) =>
            action => go => new GameObject(name).With(Go(parent: go.transform)).With(action);
        public static Action<GameObject> DestroyAt(params string[] paths) =>
            ModifyAt(paths)(UnityEngine.Object.Destroy);
        public static Action<GameObject> DestroyChildren =
            go => Enumerable.Range(0, go.transform.childCount).Select(go.transform.GetChild).Select(GoAt).ForEach(UnityEngine.Object.Destroy);
        public static Action<Transform> ParentAndScale(Transform parent = null, Vector2? scale = null) =>
            transform => transform.With(F.Apply(transform.SetParent, parent ?? transform.parent)).localScale = scale ?? new(1, 1);
        public static Action<GameObject> Go(Transform parent = null, string name = null, bool active = true) =>
            go => ((go.name, go.active) = (name ?? go.name, active)).With(ParentAndScale(parent).Apply(go.transform));
        public static Action<GameObject> Cmp<T>() where T : Component =>
            go => ObservableTriggerExtensions.GetOrAddComponent<T>(go);
        public static Action<GameObject> Cmp<T>(this Action<T> action) where T : Component =>
            go => action(ObservableTriggerExtensions.GetOrAddComponent<T>(go));
        public static Action<GameObject> Cmp<U, T>(Action<U, T> action) where T : Component where U : Component =>
            go => action(go.GetComponent<U>(), go.GetComponentInParent<T>(true));
        public static Action<T> Behavior<T>(bool? enabled) where T : Behaviour => ui => ui.enabled = enabled ?? ui.enabled;
        public static Action<Canvas> Canvas(RenderMode? renderMode = RenderMode.ScreenSpaceOverlay) => ui =>
            ui.renderMode = renderMode ?? ui.renderMode;
        public static Action<CanvasScaler> CanvasScaler(
            Vector2? referenceResolution = null,
            ScaleMode? scaleMode = ScaleMode.ScaleWithScreenSize,
            ScreenMatchMode? screenMatchMode = ScreenMatchMode.MatchWidthOrHeight
        ) => ui => (
            ui.uiScaleMode,
            ui.screenMatchMode,
            ui.referenceResolution
        ) = (
            scaleMode ?? ui.uiScaleMode,
            screenMatchMode ?? ui.screenMatchMode,
            referenceResolution ?? ui.referenceResolution
        );
        public static Action<Image> Image(
            Image.Type? type = UnityEngine.UI.Image.Type.Sliced,
            Color? color = null,
            Sprite sprite = null,
            float? alphaHit = null
        ) => ui => (
            ui.type,
            ui.color,
            ui.sprite,
            ui.alphaHitTestMinimumThreshold
        ) = (
            type ?? ui.type,
            color ?? ui.color,
            sprite ?? ui.sprite,
            alphaHit ?? ui.alphaHitTestMinimumThreshold
        );
        public static Action<RectTransform> Rt(
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? offsetMin = null,
            Vector2? offsetMax = null,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null
        ) => ui => (
            ui.anchorMin,
            ui.anchorMax,
            ui.offsetMin,
            ui.offsetMax,
            ui.sizeDelta,
            ui.anchoredPosition,
            ui.pivot
        ) = (
            anchorMin ?? ui.anchorMin,
            anchorMax ?? ui.anchorMax,
            offsetMin ?? ui.offsetMin,
            offsetMax ?? ui.offsetMax,
            sizeDelta ?? ui.sizeDelta,
            anchoredPosition ?? ui.anchoredPosition,
            pivot ?? ui.pivot
        );
        public static Action<RectTransform> RtFill => Rt(
            anchoredPosition: new(0, 0),
            sizeDelta: new(0, 0),
            anchorMin: new(0, 0),
            anchorMax: new(1, 1),
            offsetMin: new(0, 0),
            offsetMax: new(0, 0),
            pivot: new(0, 0)
        );
        public static Action<T> LayoutGroup<T>(
            bool? childScaleWidth = false,
            bool? childScaleHeight = false,
            bool? childControlWidth = true,
            bool? childControlHeight = true,
            bool? childForceExpandWidth = false,
            bool? childForceExpandHeight = false,
            bool? reverseArrangement = false,
            float? spacing = 0,
            RectOffset padding = null,
            TextAnchor? childAlignment = TextAnchor.UpperLeft
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
            childAlignment ?? ui.childAlignment
        );
        public static Action<LayoutElement> Layout(float? width = null, float? height = null) =>
            ui => (ui.preferredWidth, ui.preferredHeight) = (width ?? ui.preferredWidth, height ?? ui.preferredHeight);
        public static Action<ContentSizeFitter> Fitter(
            ContentSizeFitter.FitMode horizontal = ContentSizeFitter.FitMode.PreferredSize,
            ContentSizeFitter.FitMode vertical = ContentSizeFitter.FitMode.PreferredSize) =>
            ui => (ui.horizontalFit, ui.verticalFit) = (horizontal, vertical);
        public static Action<TextMeshProUGUI> Font(
            bool auto = true,
            float size = 18,
            float minSize = 12,
            float maxSize = 24,
            Color? color = null,
            Color? outline = null
        ) => ui => (
            ui.font,
            ui.enableAutoSizing,
            ui.fontSize,
            ui.fontSizeMin,
            ui.fontSizeMax,
            ui.faceColor,
            ui.outlineColor
        ) = (
            FontAsset,
            auto,
            size,
            auto ? minSize : size,
            auto ? maxSize : size,
            color ?? ui.color,
            outline ?? ui.outlineColor
        );
        public static Action<TextMeshProUGUI> Text(
            HorizontalAlignmentOptions? hrAlign = HorizontalAlignmentOptions.Left,
            VerticalAlignmentOptions? vtAlign = VerticalAlignmentOptions.Top,
            TextOverflowModes? overflow = TextOverflowModes.Ellipsis,
            Vector4? margin = null,
            string text = null
        ) => ui => (
            ui.horizontalAlignment,
            ui.verticalAlignment,
            ui.overflowMode,
            ui.margin,
            ui.m_text
        ) = (
            hrAlign ?? ui.horizontalAlignment,
            vtAlign ?? ui.verticalAlignment,
            overflow ?? ui.overflowMode,
            margin ?? ui.margin,
            text ?? ui.m_text
        );
        public static Action<TMP_InputField> InputField(
            bool? restoreOriginalTextOnEscape = true,
            int? characterLimit = 10,
            int? lineLimit = 1,
            TMP_InputField.ContentType? contentType = null,
            TMP_InputField.LineType? lineType = TMP_InputField.LineType.SingleLine
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
        public static Action<T> Interactable<T>(bool interactable) where T : Selectable =>
            ui => ui.interactable = interactable;
        public static Action<ToggleGroup> ToggleGroup(bool allowSwitchOff = false) =>
            ui => ui.allowSwitchOff = allowSwitchOff;
        public static Transform RootTransform =>
            Manager.Scene.GetRootComponent<Component>(Manager.Scene.NowData.LevelName).gameObject.transform;
        public static Transform RootCanvas =>
            RootTransform.Find(Plugin.Name) ??
                 new GameObject(Plugin.Name)
                    .With(Go(parent: RootTransform)).With(Cmp(Canvas()))
                    .With(Cmp(CanvasScaler(referenceResolution: new(1920, 1080))))
                    .With(Cmp<GraphicRaycaster>())
                    .With(Cmp<ObservableUpdateTrigger>())
                    .transform;
        public static Func<float, float, string, WindowHandle, GameObject> Window =>
            (width, height, name, handle) =>
                new GameObject(name)
                    .With(Go(parent: new GameObject($"Window.{name}")
                        .With(Go(parent: RootCanvas))
                        .With(Cmp(Image(color: new(0, 0, 0, 0))))
                        .With(Cmp(Rt(
                            anchoredPosition: handle,
                            sizeDelta: new(width + 12, height + 48),
                            anchorMin: new(0, 1),
                            anchorMax: new(0, 1),
                            offsetMin: new(0, 0),
                            offsetMax: new(0, 0),
                            pivot: new(0, 1))))
                        .With(Cmp(LayoutGroup<VerticalLayoutGroup>(spacing: 6, padding: new(6, 6, 6, 6))))
                        .With(Cmp<UI_DragWindow>())
                        .With(Content("Title")(
                            Cmp(Layout(width: width, height: 30)) +
                            Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get())) +
                            Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new(20, 20, 0, 0))) +
                            Content($"Label")(Cmp(Font() + Text(text: name)))))
                        .With(handle.Apply)
                        .transform))
                    .With(Cmp(Layout(width: width, height: height)));
        public static Func<float, float, string, GameObject, GameObject> Panel =>
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform, active: false))
                .With(Cmp(Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get())));
        public static Func<float, float, string, GameObject, GameObject> ScrollView =>
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: new GameObject($"Viewport.{name}")
                    .With(Go(parent: new GameObject($"ScrollView.{name}")
                        .With(Go(parent: parent.transform))
                        .With(Cmp(Image(color: new(0, 0, 0, 0))))
                        .With(Cmp(Layout(width: width, height: height)))
                        .With(Cmp<ScrollRect>(ui => (ui.horizontal, ui.vertical, ui.scrollSensitivity) = (false, true, Math.Min(200, height / 2))))
                        .With(Content($"Scrollbar.{name}")(
                            Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get())) +
                            Cmp(Rt(
                                sizeDelta: new(16, height),
                                anchorMin: new(1, 1),
                                anchorMax: new(1, 1),
                                offsetMin: new(0, 0),
                                offsetMax: new(0, 0),
                                pivot: new(1, 1))) +
                            Cmp<Scrollbar>(ui => ui.direction = Scrollbar.Direction.BottomToTop) +
                            Cmp<Scrollbar, ScrollRect>((scroll, ui) => ui.verticalScrollbar = scroll) +
                            Content($"Slider.{name}")(
                                Cmp(RtFill) +
                                Content($"Handle.{name}")(
                                    Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get())) + Cmp(RtFill) +
                                    Cmp<RectTransform, Scrollbar>((rt, ui) => ui.handleRect = rt)))))
                        .transform))
                    .With(Cmp(Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get())))
                    .With(Cmp(Rt(
                        sizeDelta: new(width - 16, height),
                        anchorMin: new(0, 1),
                        anchorMax: new(0, 1),
                        offsetMin: new(0, 0),
                        offsetMax: new(0, 0),
                        pivot: new(0, 1))))
                    .With(Cmp<RectMask2D>())
                    .With(Cmp<RectTransform, ScrollRect>((rt, ui) => ui.viewport = rt))
                    .transform))
                .With(Cmp(Rt(
                    anchorMin: new(0, 1),
                    anchorMax: new(0, 1),
                    offsetMin: new(0, 0),
                    offsetMax: new(0, 0),
                    pivot: new(0, 1))))
                .With(Cmp<RectTransform, ScrollRect>((rt, ui) => (ui.content, ui.normalizedPosition) = (rt, new(0, 1))));
        static SpriteState InputSprites => new SpriteState()
        {
            disabledSprite = BorderSprites.LightBg.Get(),
            selectedSprite = BorderSprites.DarkBg.Get(),
            highlightedSprite = BorderSprites.ColorBg.Get()
        };
        public static Func<float, float, string, GameObject, GameObject> Label =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp(Font(size: height) + Text(text: name)));
        public static Func<float, float, string, GameObject, GameObject> Input =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get())))
                .With(Cmp<TMP_InputField>(ui => (ui.transition, ui.image, ui.spriteState) =
                    (Selectable.Transition.SpriteSwap, ui.gameObject.GetComponent<Image>(), InputSprites)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Content($"{name}.Area")(
                    Cmp<RectMask2D>() + Cmp(RtFill) +
                    Cmp<RectTransform, TMP_InputField>((rt, ui) => ui.textViewport = rt) +
                    Content($"{name}.Charet")(
                        Cmp<TMP_SelectionCaret>() + Cmp(RtFill) +
                        Cmp<RectTransform, TMP_InputField>((rt, ui) => ui.caretRectTrans = rt) +
                        Cmp<TMP_SelectionCaret, RectMask2D>((caret, ui) => caret.m_ParentMask = ui)) +
                    Content($"{name}.Content")(
                        Cmp(Font(auto: false, size: 16) + Text(margin: new(5, 0, 5, 0), hrAlign: HorizontalAlignmentOptions.Right)) +
                        Cmp(RtFill) + Cmp<TextMeshProUGUI, TMP_InputField>((text, ui) => ui.textComponent = text))));
        public static Func<float, float, string, GameObject, GameObject> Check =
            (width, height, name, parent) => new GameObject($"Background.{name}")
                .With(Go(parent: parent.transform))
                .With(Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new(5, 5, 0, 0), childAlignment: TextAnchor.MiddleCenter)))
                .With(Cmp(Layout(width: width + 10, height: height)))
                .With(Content(name)(
                    Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.LightBg.Get(), alphaHit: 0)) +
                    Cmp(Layout(width: 18, height: 18)) +
                    Cmp<Toggle>(ui => (ui.isOn, ui.transition, ui.image) =
                        (false, Selectable.Transition.SpriteSwap, ui.gameObject.GetComponent<Image>())) +
                    Content($"{name}.State")(
                        Cmp(Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.CheckOn.Get(), alphaHit: 0)) +
                        Cmp(RtFill) + Cmp<Image, Toggle>((image, ui) => ui.graphic = image))));
        public static Func<float, float, string, GameObject, GameObject> Slider =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform, active: true))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp<Slider>(ui => ui.direction = UnityEngine.UI.Slider.Direction.LeftToRight))
                .With(Content($"{name}.Background")(Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get())) + Cmp(RtFill)))
                .With(Content($"{name}.Gauge")(
                    Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get())) + Cmp(RtFill) +
                    Cmp<RectTransform, Slider>((rt, ui) => ui.fillRect = rt)))
                .With(Content($"{name}.Handle")(
                    Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.LightBg.Get())) + Cmp(RtFill) +
                    Cmp<RectTransform, Slider>((rt, ui) => ui.handleRect = rt)));
        public static Func<float, float, string, GameObject, GameObject> Color =
           (width, height, name, parent) => new GameObject(name)
               .With(Go(parent: parent.transform))
               .With(Cmp(Layout(width: width, height: height)))
               .With(Cmp<ThumbnailColor>())
               .With(Cmp<UIText>(ui => ui.gameObject.GetComponent<ThumbnailColor>()._title = ui))
               .With(Content($"{name}.Button")(
                   Cmp(Image(type: UnityEngine.UI.Image.Type.Tiled, color: new(1, 1, 1, 1), sprite: SimpleSprites.AlphaSample.Get())) +
                   Cmp<Button>() + Cmp(RtFill) + Cmp<Button, ThumbnailColor>((ui, cp) => cp._button = ui) +
                   Content($"{name}.Sample")(
                       Cmp(Image(color: new(1, 1, 1, 1))) + Cmp(RtFill) +
                       Cmp<Image, ThumbnailColor>((ui, cp) => cp._graphicColor = ui))));
        public static Func<float, float, string, Color, GameObject, GameObject> Section =
            (width, height, name, color, parent) => new GameObject($"Bagckground.{name}")
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: color)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Content(name)(Cmp(Font(size: height) + Text(text: name)) + Cmp(RtFill)));
        static SpriteState ToggleSprites => new SpriteState()
        {
            disabledSprite = SimpleSprites.ToggleNa.Get(),
            highlightedSprite = SimpleSprites.ToggleHi.Get()
        };
        public static Func<float, float, string, GameObject, GameObject> Toggle =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.ToggleBg.Get(), alphaHit: 0)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp<Toggle>(ui => (ui.isOn, ui.transition, ui.spriteState, ui.image) =
                    (false, Selectable.Transition.SpriteSwap, ToggleSprites, ui.gameObject.GetComponent<Image>())))
                .With(Content($"{name}.State")(
                    Cmp(Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.ToggleOn.Get(), alphaHit: 0)) +
                    Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new(10, 10, 0, 0))) +
                    Cmp(RtFill) + Cmp<Image, Toggle>((image, ui) => ui.graphic = image) +
                    Content($"{name}.Label")(Cmp(Font(size: height) + Text(text: name)) + Cmp(RtFill))));
        static SpriteState ButtonSprites => new SpriteState()
        {
            pressedSprite = BorderSprites.ButtonOn.Get(),
            disabledSprite = BorderSprites.ButtonNa.Get(),
            selectedSprite = BorderSprites.ButtonHi.Get(),
            highlightedSprite = BorderSprites.ButtonHi.Get()
        };
        public static Func<float, float, string, GameObject, GameObject> Button =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ButtonBg.Get(), alphaHit: 0)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp<Button>(ui => (ui.transition, ui.image, ui.spriteState) =
                    (Selectable.Transition.SpriteSwap, ui.gameObject.GetComponent<Image>(), ButtonSprites)))
                .With(Content($"{name}.Label")(Cmp(Font(size: height) + Text(
                    hrAlign: HorizontalAlignmentOptions.Center,
                    vtAlign: VerticalAlignmentOptions.Middle,
                    margin: new(5, 0, 5, 0), text: name)) + Cmp(RtFill)));
        public static Func<float, float, string, GameObject, GameObject> Choice =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get(), alphaHit: 0)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp<Toggle>(ui => (ui.isOn, ui.transition, ui.spriteState, ui.image) =
                    (false, Selectable.Transition.SpriteSwap, InputSprites, ui.gameObject.GetComponent<Image>())))
                .With(Content($"{name}.State")(
                    Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.DarkBg.Get(), alphaHit: 0)) +
                    Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new(10, 10, 0, 0))) +
                    Cmp(RtFill) + Cmp<Image, Toggle>((image, ui) => ui.graphic = image) +
                    Content($"{name}.Label")(Cmp(Font(size: height) + Text(text: name)) + Cmp(RtFill))));
        static TMP_FontAsset FontAsset;
        static Action Setup =
            () => FontAsset = UnityEngine.Object.Instantiate(
                Manager.Scene.GetRootGameObjects("Title")
                    .SelectMany(go => go.GetComponentsInChildren<TextMeshProUGUI>(true))
                    .First(tmp => tmp.font != null)).font.With(UnityEngine.Object.DontDestroyOnLoad);
        static Func<bool> Ready =
            () => Manager.Scene.Instance != null
                && Manager.Scene.NowData.LevelName == "Title"
                && Manager.Scene.IsFadeEnd
                && Localize.Translate.Manager.Initialized;
        static internal void Initialize() =>
            Util.DoOnCondition(Ready, Setup);
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        internal static BepInEx.Logging.ManualLogSource Logger;
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "CoastalSmell";
        public const string Version = "1.0.0";
        public override void Load() => (Logger = Log).With(Sprites.Initialize).With(UGUI.Initialize);
    }
}