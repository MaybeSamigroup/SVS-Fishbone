using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if Aicomi
using ILLGAMES.Unity.UI;
using ILLGAMES.Unity.UI.ColorPicker;
#else
using ILLGames.Unity.UI;
using ILLGames.Unity.UI.ColorPicker;
#endif
using ScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode;
using ScreenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;

namespace CoastalSmell
{
    public enum SimpleSprites
    {
        ToggleBg,
        ToggleNa,
        ToggleHi,
        ToggleOn,
        CheckOn,
        AlphaSample
    }
    public enum BorderSprites
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
    public static class Sprites
    {
        static Dictionary<SimpleSprites, Sprite> Simples = new();
        static Dictionary<BorderSprites, Sprite> Borders = new();
        static string ToPath<T>(T item) =>
            Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Name, $"{item}.png");
        static void RegisterSprite(this GameObject go, Sprite item) =>
            go.With(UGUI.Cmp(UGUI.Image(sprite: item)));
        public static Sprite Get(this SimpleSprites item) => Simples[item];
        public static Sprite Get(this BorderSprites item) => Borders[item];
        static void Setup(Transform parent) =>
            (Simples, Borders) = (
                Enum.GetValues<SimpleSprites>()
                    .ToDictionary(item => item, item => UGUI.ToSimpleSprite(ToPath(item))
                        .With(new GameObject(item.ToString()).With(UGUI.Go(parent: parent)).RegisterSprite)),
                Enum.GetValues<BorderSprites>()
                    .ToDictionary(item => item, item => UGUI.ToBorderSprite(new(6, 6, 6, 6), ToPath(item))
                        .With(new GameObject(item.ToString()).With(UGUI.Go(parent: parent)).RegisterSprite)));
        internal static IDisposable Initialize() =>
            Hooks.OnCommonSpaceInitialize.Subscribe(Setup);
    }
    public class WindowConfig
    {
        public ConfigEntry<float> AnchorX { init; get; }
        public ConfigEntry<float> AnchorY { init; get; }
        public ConfigEntry<bool> State { init; get; }
        public ConfigEntry<KeyboardShortcut> Shortcut { init; get; }
        public WindowConfig(BasePlugin plugin, string prefix, Vector2 anchor, KeyboardShortcut shortcut, bool visible = false) =>
            (AnchorX, AnchorY, Shortcut, State) = (
                plugin.Config.Bind("UI", $"{prefix} window anchor X", anchor.x),
                plugin.Config.Bind("UI", $"{prefix} window anchor Y", anchor.y),
                plugin.Config.Bind("UI", $"{prefix} window toggle key", shortcut),
                plugin.Config.Bind("UI", $"{prefix} window visibility", visible));
        public void Update(Vector2 position) =>
            (AnchorX.Value, AnchorY.Value) = (position.x, position.y);
        public IObservable<bool> OnToggle =>
            UGUI.RootCanvas.OnUpdateAsObservable()
                .Where(_ => Shortcut.Value.IsDown())
                .Select(_ => State.Value = !State.Value);
        public Window Create(float width, float height, string name) =>
            new Window(this, width, height, name);
    }
    public class Window {
        public GameObject Background { init; get; }
        public GameObject Content { init; get; }
        public string Title { get => TitleUI.text; set => TitleUI.SetText(value);  }
        CompositeDisposable Subscriptions;
        IObservable<Vector2> OnPositionUpdate;
        TextMeshProUGUI TitleUI;
        Window(IObservable<Unit> OnUpdate) =>
            OnPositionUpdate = OnUpdate
                .Select(_ =>  Background.GetComponent<RectTransform>())
                .Select(rt => rt.anchoredPosition)
                .DistinctUntilChanged();

        Window(GameObject go) : this(go.OnUpdateAsObservable())=>
            (Background = go).OnDestroyAsObservable().Subscribe(_ => Subscriptions.Dispose());

        Window(WindowConfig config, string name) : this(new GameObject($"Window.{name}")) =>
            Subscriptions = [
                OnPositionUpdate.Subscribe(config.Update),
                config.OnToggle.Subscribe(Background.SetActive)
            ];
        internal Window(WindowConfig config, float width, float height, string name) : this(config, name) =>
            Content = Background
                .With(UGUI.Go(parent: UGUI.RootCanvas, active: config.State.Value))
                .With(UGUI.Cmp(UGUI.Rt(
                    anchoredPosition: new(config.AnchorX.Value, config.AnchorY.Value),
                    sizeDelta: new(width + 12, height + 48),
                    anchorMin: new(0, 1),
                    anchorMax: new(0, 1),
                    offsetMin: new(0, 0),
                    offsetMax: new(0, 0),
                    pivot: new(0, 1))))
                .With(UGUI.Cmp(UGUI.Image(color: new(0, 0, 0, 0))))
                .With(UGUI.Cmp(UGUI.LayoutGroup<VerticalLayoutGroup>(
                    spacing: 6,
                    padding: new() { left = 6, right = 6, top = 6, bottom = 6 })))
                .With(UGUI.Cmp<UI_DragWindow>())
                .With(UGUI.Content("Title")(
                    UGUI.Cmp(UGUI.Layout(width: width, height: 30)) +
                    UGUI.Cmp(UGUI.Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get())) +
                    UGUI.Cmp(UGUI.LayoutGroup<HorizontalLayoutGroup>(
                        padding: new() { left = 20, right = 20, top = 0, bottom = 0 })) +
                    UGUI.Content($"Label")(
                        UGUI.Cmp(UGUI.Font() + UGUI.Text(text: name)) +
                        UGUI.Cmp<TextMeshProUGUI>(ui => TitleUI = ui))))
                .Wrap(new GameObject(name))
                .With(UGUI.Cmp(UGUI.Layout(width: width, height: height)));
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
            go => _ = go.GetComponent<T>() ?? go.AddComponent<T>();
        public static Action<GameObject> Cmp<T>(this Action<T> action) where T : Component =>
            go => action(go.GetComponent<T>() ?? go.AddComponent<T>());
        public static Action<GameObject> Cmp<U, T>(Action<U, T> action) where T : Component where U : Component =>
            go => action(go.GetComponent<U>() ?? go.AddComponent<U>(), go.GetComponentInParent<T>(true));
        public static Action<Canvas> Canvas(RenderMode? renderMode = RenderMode.ScreenSpaceOverlay) => ui =>
            (ui.renderMode, ui.sortingOrder) = (renderMode ?? ui.renderMode, 1);
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
            ui => (ui.preferredWidth, ui.preferredHeight) =
                (ui.minWidth = width ?? ui.preferredWidth, ui.minHeight = height ?? ui.preferredHeight);
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
                    .With(Cmp<GraphicRaycaster>()).transform;

        public static Func<string, GameObject, GameObject> Panel =>
            (name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform, active: false))
                .With(Cmp(Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get())));

        public static GameObject Wrap(this GameObject parent, GameObject child) => child.With(Go(parent: parent.transform));
        public static Func<float, float, string, GameObject, GameObject> ScrollWrap =>
            (width, height, name, go) =>
                new GameObject($"ScrollView.{name}")
                    .With(Go(parent: go.transform.parent.transform))
                    .With(Cmp(Image(color: new(0, 0, 0, 0))))
                    .With(Cmp(Layout(width: width, height: height)))
                    .With(Cmp(LayoutGroup<HorizontalLayoutGroup>(
                        reverseArrangement: true,
                        childForceExpandHeight: true)))
                    .With(Cmp<ScrollRect>(ui => (ui.horizontal, ui.vertical, ui.scrollSensitivity) = (false, true, Math.Min(200, height / 2))))
                    .With(Content($"Scrollbar.{name}")(
                        Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get())) +
                        Cmp(Layout(width: 5, height: height)) +
                        Cmp<Scrollbar>(ui => ui.direction = Scrollbar.Direction.BottomToTop) +
                        Cmp<Scrollbar, ScrollRect>((scroll, ui) => ui.verticalScrollbar = scroll) +
                        Content($"Slider.{name}")(
                            Cmp(RtFill) +
                            Content($"Handle.{name}")(
                                Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get())) + Cmp(RtFill) +
                                Cmp<RectTransform, Scrollbar>((rt, ui) => ui.handleRect = rt)))))
                    .Wrap(new GameObject(name))
                        .With(Cmp(Layout(width: width - 5, height: height)))
                        .With(Cmp<RectMask2D>())
                        .With(Cmp<RectTransform, ScrollRect>((rt, ui) => ui.viewport = rt))
                    .Wrap(go)
                        .With(Cmp(Rt(
                            anchorMin: new(0, 1),
                            anchorMax: new(0, 1),
                            offsetMin: new(0, 0),
                            offsetMax: new(0, 0),
                            pivot: new(0, 1))))
                        .With(Cmp<RectTransform, ScrollRect>((rt, ui) => (ui.content, ui.normalizedPosition) = (rt, new(0, 1))));

        public static Func<float, float, string, GameObject, GameObject> ScrollPanel =>
            (width, height, name, parent) =>
                ScrollWrap(width, height, $"{name}.Panel", new GameObject(name).With(Go(parent: parent.transform)))
                    .With(go => go.transform.parent.gameObject
                        .With(Cmp(Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get()))));

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
                .With(Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new() { left = 5, right = 5, top = 0, bottom = 0 }, childAlignment: TextAnchor.MiddleCenter)))
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
                    Cmp(LayoutGroup<HorizontalLayoutGroup>(padding: new RectOffset() { left = 10, right = 10, top = 0, bottom = 0 })) +
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

        public static Func<float, float, string, GameObject, GameObject> Dropdown =
            (width, height, name, parent) => new GameObject(name)
                .With(Go(parent: parent.transform))
                .With(Cmp(Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get(), alphaHit: 0)))
                .With(Cmp(Layout(width: width, height: height)))
                .With(Cmp<TMP_Dropdown>(ui =>
                    (ui.transition, ui.spriteState, ui.image) =
                    (Selectable.Transition.SpriteSwap, InputSprites, ui.gameObject.GetComponent<Image>())))
                .With(Content($"{name}.Label")(Cmp(
                    Font(size: height) +
                    Text(hrAlign: HorizontalAlignmentOptions.Left, vtAlign: VerticalAlignmentOptions.Middle, margin: new(5, 0, 5, 0), text: name)
                ) + Cmp<TextMeshProUGUI, TMP_Dropdown>((text, ui) => ui.captionText = text) + Cmp(RtFill)))
                .With(DropdownTemplate.Apply(width).Apply(height).Apply(name))
                .With(ModifyAt($"ScrollView.{name}.Template")(Cmp<RectTransform, TMP_Dropdown>((temp, ui) => ui.template = temp) +
                    Cmp(Rt(anchorMin: new(0, 0), anchorMax: new(0, 0), offsetMin: new(0, 0), offsetMax: new(0, 0), sizeDelta: new (0,0)))));

        static Action<float, float, string, GameObject> DropdownTemplate =
            (width, height, name, dropdown) =>
                Toggle(width, height, $"{name}.Item",
                    ScrollWrap(width, height * 10, $"{name}.Template",
                        Panel($"{name}.Panel", dropdown)
                            .With(Cmp(LayoutGroup<VerticalLayoutGroup>()))
                            .With(Cmp(Fitter()))))
                        .With(ModifyAt($"{name}.Item.State", $"{name}.Item.Label")
                            (Cmp<TextMeshProUGUI, TMP_Dropdown>((text, ui) => ui.itemText = text)));

        static TMP_FontAsset FontAsset;
        static void Initialize(GameObject go) =>
            FontAsset = go.GetComponentsInChildren<TextMeshProUGUI>(true).First(tmp => tmp.font != null).font;
        internal static IDisposable Initialize() =>
            Hooks.OnFontInitialize.Subscribe(Initialize);
    }
}