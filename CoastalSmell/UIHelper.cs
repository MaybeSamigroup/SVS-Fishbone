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
        static Dictionary<SimpleSprites, Sprite> Simples;
        static Dictionary<BorderSprites, Sprite> Borders;
        static void InitializeSprites() => (Simples, Borders) = (
            Enum.GetValues<SimpleSprites>().ToDictionary(item => item, item => ToSimpleSprite(ToPath(item))),
            Enum.GetValues<BorderSprites>().ToDictionary(item => item, item => ToBorderSprite(new(6, 6, 6, 6), ToPath(item)))
        );
        static string ToPath<T>(T item) =>
            Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, $"{item}.png").With(Plugin.Instance.Log.LogInfo);
        static Func<string, Texture2D> ToTexture2D =
            (path) => new Texture2D(64, 64).With(t2d => t2d.LoadImage(File.ReadAllBytes(path)));
        static Func<Texture2D, Sprite> Texture2DToSimpleSprite =
            (t2d) => Sprite.Create(t2d, new(0, 0, t2d.width, t2d.height), new(0.5f, 0.5f));
        static Func<Vector4, Texture2D, Sprite> Texture2DToBorderSprite =
            (border, t2d) => Sprite.Create(t2d, new(0, 0, t2d.width, t2d.height), new(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        public static Sprite Get(this SimpleSprites item) => Simples[item];
        public static Sprite Get(this BorderSprites item) => Borders[item];
        public static Func<string, Sprite> ToSimpleSprite =
            (path) => Texture2DToSimpleSprite(ToTexture2D(path));
        public static Func<Vector4, string, Sprite> ToBorderSprite =
            (border, path) => Texture2DToBorderSprite(border, ToTexture2D(path));
        static IEnumerable<UIDesign> Setup<T>(Dictionary<T, Sprite> entries) where T : Enum =>
            entries.Select(entry => entry.Key.ToString().AsChild(UGUI.Image(sprite: entry.Value)));
        static void Setup(Transform parent) =>
            parent.With(InitializeSprites).With(UGUI.AsDesign([.. Setup(Simples), .. Setup(Borders)]));
        internal static IDisposable Initialize() =>
            Hooks.OnCommonSpaceInitialize.Subscribe(Setup);
    }

    public delegate void UIDesign(GameObject go);

    public static partial class UGUI
    {
        static UIDesign Identity = new UIDesign(F.Ignoring<GameObject>(F.DoNothing));

        public static Transform TransformAt(this Transform tf, string[] paths) =>
            paths.Length == 0 ? tf : tf.Find(paths[0]).TransformAt(paths[1..]);

        public static Transform TransformAt(this GameObject go, string[] paths) =>
            go.transform.TransformAt(paths);

        public static Transform With(this Transform tf, UIDesign design) =>
            tf.gameObject.With(design).transform;

        public static GameObject With(this GameObject go, UIDesign design) =>
            F.With(go, design.Invoke);

        public static UIDesign AsDesign(this IEnumerable<UIDesign> designs) =>
            (designs ?? []).Aggregate(Identity, (f, g) => f + g);

        public static UIDesign Component<T>() where T : Component =>
            go => _ = go.GetComponent<T>() ?? go.AddComponent<T>();

        public static UIDesign Component<T>(Action<T> action) where T : Component =>
            go => action(go.GetComponent<T>() ?? go.AddComponent<T>());

        public static UIDesign Component<T, U>(Action<T, U> action) where T : Component where U : Component =>
            go => action(go.GetComponent<T>() ?? go.AddComponent<T>(), go.GetComponent<U>() ?? go.GetComponentInParent<U>(true));

        public static UIDesign GameObject(bool active = true) => go => go.SetActive(active);

        public static UIDesign AsParent(this GameObject parent, bool active = true, Vector2? scale = null) =>
            parent.transform.AsParent(active, scale);

        public static UIDesign AsParent(this Transform parent, bool active = true, Vector2? scale = null) =>
            go => (go.transform.parent, go.active, go.transform.localScale) = (parent, active, scale ?? new (1.0f, 1.0f));

        public static UIDesign AsParent(this string name, UIDesign design) =>
            go => new GameObject(name).With(go.transform.parent.AsParent() + design).AsParent().Invoke(go);

        public static UIDesign AsChild(this string name, UIDesign design) =>
            go => new GameObject(name).With(go.transform.AsParent() + design);

        public static UIDesign AsTemplate(this string name, UIDesign design) =>
            go => new GameObject(name).With(go.transform.AsParent(active: false) + design);

        public static UIDesign Canvas(RenderMode? renderMode = RenderMode.ScreenSpaceOverlay) =>
            Component<Canvas>(cmp => (cmp.renderMode, cmp.sortingOrder) = (renderMode ?? cmp.renderMode, 1));

        public static UIDesign CanvasScaler(
            Vector2? referenceResolution = null,
            ScaleMode? scaleMode = ScaleMode.ScaleWithScreenSize,
            ScreenMatchMode? screenMatchMode = ScreenMatchMode.MatchWidthOrHeight
        ) => Component<CanvasScaler>(cmp => (
            cmp.uiScaleMode,
            cmp.screenMatchMode,
            cmp.referenceResolution
        ) = (
            scaleMode ?? cmp.uiScaleMode,
            screenMatchMode ?? cmp.screenMatchMode,
            referenceResolution ?? cmp.referenceResolution
        ));

        public static UIDesign Image(
            Image.Type? type = UnityEngine.UI.Image.Type.Sliced,
            Color? color = null,
            Sprite sprite = null,
            float? alphaHit = null
        ) => Component<Image>(cmp => (
            cmp.type,
            cmp.color,
            cmp.sprite,
            cmp.alphaHitTestMinimumThreshold
        ) = (
            type ?? cmp.type,
            color ?? cmp.color,
            sprite ?? cmp.sprite,
            alphaHit ?? cmp.alphaHitTestMinimumThreshold
        ));

        public static UIDesign Rt(
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? offsetMin = null,
            Vector2? offsetMax = null,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null,
            Vector2? pivot = null
        ) => Component<RectTransform>(cmp => (
            cmp.pivot,
            cmp.anchorMin,
            cmp.anchorMax,
            cmp.offsetMin,
            cmp.offsetMax,
            cmp.sizeDelta,
            cmp.anchoredPosition
        ) = (
            pivot ?? cmp.pivot,
            anchorMin ?? cmp.anchorMin,
            anchorMax ?? cmp.anchorMax,
            offsetMin ?? cmp.offsetMin,
            offsetMax ?? cmp.offsetMax,
            sizeDelta ?? cmp.sizeDelta,
            anchoredPosition ?? cmp.anchoredPosition
        ));

        public static UIDesign RtZero = Rt(
            pivot: new(0, 0),
            sizeDelta: new(0, 0),
            anchorMin: new(0, 0),
            anchorMax: new(0, 0),
            offsetMin: new(0, 0),
            offsetMax: new(0, 0),
            anchoredPosition: new(0, 0)
        );

        public static UIDesign RtFill = Rt(
            pivot: new(0, 0),
            sizeDelta: new(0, 0),
            anchorMin: new(0, 0),
            anchorMax: new(1, 1),
            offsetMin: new(0, 0),
            offsetMax: new(0, 0),
            anchoredPosition: new(0, 0)
        );

        public static RectOffset Offset(int left = 0, int right = 0, int top = 0, int bottom = 0) =>
            new() { left = left, right = right, top = top, bottom = bottom };

        public static RectOffset Offset(int hr = 0, int vt = 0) =>
            new() { left = hr, right = hr, top = vt, bottom = vt };

        public static UIDesign Layout<T>(
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
        ) where T : HorizontalOrVerticalLayoutGroup => Component<T>(cmp => (
            cmp.childAlignment,
            cmp.childScaleWidth,
            cmp.childScaleHeight,
            cmp.childControlWidth,
            cmp.childControlHeight,
            cmp.childForceExpandWidth,
            cmp.childForceExpandHeight,
            cmp.reverseArrangement,
            cmp.spacing,
            cmp.padding
        ) = (
            childAlignment ?? cmp.childAlignment,
            childScaleWidth ?? cmp.childScaleWidth,
            childScaleHeight ?? cmp.childScaleHeight,
            childControlWidth ?? cmp.childControlWidth,
            childControlHeight ?? cmp.childControlHeight,
            childForceExpandWidth ?? cmp.childForceExpandWidth,
            childForceExpandHeight ?? cmp.childForceExpandHeight,
            reverseArrangement ?? cmp.reverseArrangement,
            spacing ?? cmp.spacing,
            padding ?? cmp.padding
        ));

        public static UIDesign LayoutV(
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
        ) => Layout<VerticalLayoutGroup>(
            childScaleWidth, childScaleHeight,
            childControlWidth, childControlHeight,
            childForceExpandWidth, childForceExpandHeight,
            reverseArrangement, spacing, padding, childAlignment);

        public static UIDesign LayoutH(
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
        ) => Layout<HorizontalLayoutGroup>(
            childScaleWidth, childScaleHeight,
            childControlWidth, childControlHeight,
            childForceExpandWidth, childForceExpandHeight,
            reverseArrangement, spacing, padding, childAlignment);

        public static UIDesign Size(float? width = null, float? height = null) =>
            Component<LayoutElement>(cmp => (cmp.preferredWidth, cmp.preferredHeight) = (cmp.minWidth = width ?? cmp.preferredWidth, cmp.minHeight = height ?? cmp.preferredHeight));

        public static UIDesign Fitter(
            ContentSizeFitter.FitMode horizontal = ContentSizeFitter.FitMode.PreferredSize,
            ContentSizeFitter.FitMode vertical = ContentSizeFitter.FitMode.PreferredSize) =>
            Component<ContentSizeFitter>(cmp => (cmp.horizontalFit, cmp.verticalFit) = (horizontal, vertical));

        public static UIDesign Font(
            bool auto = true,
            float size = 18,
            float minSize = 12,
            float maxSize = 24,
            Color? color = null,
            Color? outline = null
        ) => Component<TextMeshProUGUI>(cmp => (
            cmp.font,
            cmp.enableAutoSizing,
            cmp.fontSize,
            cmp.fontSizeMin,
            cmp.fontSizeMax,
            cmp.faceColor,
            cmp.outlineColor
        ) = (
            FontAsset,
            auto,
            size,
            auto ? minSize : size,
            auto ? maxSize : size,
            color ?? cmp.color,
            outline ?? cmp.outlineColor
        ));

        public static UIDesign Text(
            HorizontalAlignmentOptions? hrAlign = HorizontalAlignmentOptions.Left,
            VerticalAlignmentOptions? vtAlign = VerticalAlignmentOptions.Top,
            TextOverflowModes? overflow = TextOverflowModes.Ellipsis,
            Vector4? margin = null,
            string text = null
        ) => Component<TextMeshProUGUI>(cmp => (
            cmp.horizontalAlignment,
            cmp.verticalAlignment,
            cmp.overflowMode,
            cmp.margin,
            cmp.m_text
        ) = (
            hrAlign ?? cmp.horizontalAlignment,
            vtAlign ?? cmp.verticalAlignment,
            overflow ?? cmp.overflowMode,
            margin ?? cmp.margin,
            text ?? cmp.m_text
        ));

        public static UIDesign Input(
            bool? restoreOriginalTextOnEscape = true,
            int? characterLimit = 10,
            int? lineLimit = 1,
            TMP_InputField.ContentType? contentType = null,
            TMP_InputField.LineType? lineType = TMP_InputField.LineType.SingleLine
        ) => Component<TMP_InputField>(cmp => (
            cmp.restoreOriginalTextOnEscape,
            cmp.characterLimit,
            cmp.lineLimit,
            cmp.contentType,
            cmp.lineType
        ) = (
            restoreOriginalTextOnEscape ?? cmp.restoreOriginalTextOnEscape,
            characterLimit ?? cmp.characterLimit,
            lineLimit ?? cmp.lineLimit,
            contentType ?? cmp.contentType,
            lineType ?? cmp.lineType
        ));

        public static UIDesign Interactable(bool interactable) => Component<Selectable>(cmp => cmp.interactable = interactable);

        public static UIDesign AssignSprites(this SpriteState spriteState) =>
            Component<Image, Selectable>((image, cmp) =>
                (cmp.image, cmp.spriteState, cmp.transition) =
                (image, spriteState, Selectable.Transition.SpriteSwap));

        public static UIDesign ToggleGroup(bool allowSwitchOff = false) =>
            Component<ToggleGroup>(cmp => cmp.allowSwitchOff = allowSwitchOff);

        public static GameObject SceneRoot =>
            Manager.Scene.GetRootComponent<Component>(Manager.Scene.NowData.LevelName).gameObject;

        public static GameObject Root =>
            SceneRoot.transform.Find(Plugin.Name)?.gameObject ??
                SceneRoot.With(Plugin.Name.AsChild(Canvas() +
                    CanvasScaler(referenceResolution: new(1920, 1080)) +
                    Component<GraphicRaycaster>()));

        public static UIDesign ClearPanel =
            Image(color: new(0.0f, 0.0f, 0.0f, 0.0f));

        public static UIDesign ColorPanel =
            Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get());

        public static UIDesign VtScroll(float width, float height, UIDesign design) =>
            Size(width: width, height: height) +
            LayoutH(childForceExpandHeight: true) +
            Component<ScrollRect>(cmp =>
                (cmp.horizontal, cmp.vertical, cmp.verticalScrollbarVisibility, cmp.scrollSensitivity) =
                    (false, true, ScrollRect.ScrollbarVisibility.AutoHide, Math.Min(200, height / 2))) +
            "ViewPort".AsChild(
                Size(width: width - 5, height: height) +
                Component<RectMask2D>() +
                Component<RectTransform, ScrollRect>((rect, scroll) => scroll.viewport = rect) +
                "Content".AsChild(
                    Component<RectTransform, ScrollRect>((rect, scroll) => (
                        rect.anchorMin,
                        rect.anchorMax,
                        rect.offsetMin,
                        rect.offsetMax,
                        rect.pivot,
                        scroll.normalizedPosition,
                        scroll.content
                    ) = (
                        new(0, 1),
                        new(0, 1),
                        new(0, 0),
                        new(0, 0),
                        new(0, 1),
                        new(0, 1),
                        rect
                    )) + design)) +
            "Scrollbar".AsChild(
                Size(width: 5, height: height) +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get()) +
                Component<Scrollbar, ScrollRect>((scroll, rect) =>
                    (scroll.direction, rect.verticalScrollbar) = (Scrollbar.Direction.BottomToTop, scroll)) +
                "Slider".AsChild(
                    RtFill +
                    "Handle".AsChild(RtFill +
                        Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get()) +
                        Component<RectTransform, Scrollbar>((rect, scroll) => scroll.handleRect = rect))));

        public static UIDesign VtScrollAsParent(string name, float width, float height, UIDesign design) =>
            name.AsParent(Image(color: new(0, 0, 0, 0)) + VtScroll(width, height, design));

        public static UIDesign VtScrollAsChild(string name, float width, float height, UIDesign design) =>
            name.AsChild(Image(color: new(0.5f, 0.5f, 0.5f, 0.7f),sprite: BorderSprites.ColorBg.Get()) + VtScroll(width, height, design));

        static SpriteState InputSprites => new SpriteState()
        {
            disabledSprite = BorderSprites.LightBg.Get(),
            selectedSprite = BorderSprites.DarkBg.Get(),
            highlightedSprite = BorderSprites.ColorBg.Get()
        };

        public static UIDesign Label(float width, float height) =>
            Size(width: width, height: height) + Font(size: height);

        public static UIDesign Section(float width, float height, Color bg, UIDesign design) =>
            Size(width: width, height: height) + Image(color: bg) + "Label".AsChild(RtFill + Font(size: height) + design);

        public static UIDesign Input(float width, float height, UIDesign design) =>
            Size(width: width, height: height) +
            Input() +
            Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get()) +
            AssignSprites(InputSprites) +
            "TextArea".AsChild(
                RtFill +
                Component<RectMask2D>() +
                Component<RectTransform, TMP_InputField>((rect, input) => input.textViewport = rect) +
                "Charet".AsChild(
                    RtFill +
                    Component<TMP_SelectionCaret>() +
                    Component<RectTransform, TMP_InputField>((rect, input) => input.caretRectTrans = rect) +
                    Component<TMP_SelectionCaret, RectMask2D>((caret, mask) => caret.m_ParentMask = mask)) +
                "Content".AsChild(
                    RtFill +
                    Font(auto: false, size: 16) +
                    Text(margin: new(5, 0, 5, 0), hrAlign: HorizontalAlignmentOptions.Right) +
                    Component<TextMeshProUGUI, TMP_InputField>((label, input) => input.textComponent = label) + design));

        public static UIDesign Check(float width, float height) =>
            Size(width: width, height: height) +
            LayoutH(padding: Offset(5, 0), childAlignment: TextAnchor.MiddleCenter) +
            Component<Toggle>(cmp => (cmp.isOn, cmp.transition) = (false, Selectable.Transition.SpriteSwap)) +
            "Toggle".AsChild(
                Size(width: 18, height: 18) +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.LightBg.Get(), alphaHit: 0) +
                "State".AsChild(
                    RtFill +
                    Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.CheckOn.Get(), alphaHit: 0) +
                    Component<Image, Toggle>((image, cmp) => (cmp.image, cmp.graphic) = (image, image))));

        public static UIDesign Slider(float width, float height) =>
            Size(width: width, height: height) +
            Component<Slider>(ui => ui.direction = UnityEngine.UI.Slider.Direction.LeftToRight) +
            "Guide".AsChild(
                RtFill +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get())) +
            "Gauge".AsChild(
                RtFill +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get()) +
                Component<RectTransform, Slider>((rect, slider) => slider.fillRect = rect)) +
            "Handle".AsChild(
                RtFill +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.LightBg.Get()) +
                Component<RectTransform, Slider>((rect, slider) => slider.handleRect = rect));

        public static UIDesign Color(float width, float height) =>
            Size(width: width, height: height) +
            Component<ThumbnailColor>() +
            Component<UIText, ThumbnailColor>((text, color) => color._title = text) +
            "Button".AsChild(
                RtFill +
                Image(type: UnityEngine.UI.Image.Type.Tiled, color: new(1, 1, 1, 1), sprite: SimpleSprites.AlphaSample.Get()) +
                Component<Button>() +
                Component<Button, ThumbnailColor>((cmp, color) => color._button = cmp) +
                "Sample".AsChild(
                    RtFill +
                    Image(color: new(1, 1, 1, 1)) +
                    Component<Image, ThumbnailColor>((image, color) => color._graphicColor = image)));

        static SpriteState ToggleSprites => new SpriteState()
        {
            disabledSprite = SimpleSprites.ToggleNa.Get(),
            highlightedSprite = SimpleSprites.ToggleHi.Get()
        };

        public static UIDesign Toggle(float width, float height, UIDesign design) =>
            Size(width: width, height: height) +
            Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.ToggleBg.Get(), alphaHit: 0) +
            Component<Toggle>() + AssignSprites(ToggleSprites) +
            "State".AsChild(
                RtFill +
                LayoutH(padding: Offset(10, 0)) +
                Image(color: new(1, 1, 1, 1), sprite: SimpleSprites.ToggleOn.Get(), alphaHit: 0) +
                Component<Image, Toggle>((image, toggle) => toggle.graphic = image) +
                "Label".AsChild(RtFill + Font(size: height) + design));

        static SpriteState ButtonSprites => new SpriteState()
        {
            pressedSprite = BorderSprites.ButtonOn.Get(),
            disabledSprite = BorderSprites.ButtonNa.Get(),
            selectedSprite = BorderSprites.ButtonHi.Get(),
            highlightedSprite = BorderSprites.ButtonHi.Get()
        };

        public static UIDesign Button(float width, float height, UIDesign design) =>
            Size(width: width, height: height) +
            Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ButtonBg.Get(), alphaHit: 0) +
            Component<Button>() + AssignSprites(ButtonSprites) +
            "Label".AsChild(
                RtFill +
                Font(size: height) +
                Text(
                    hrAlign: HorizontalAlignmentOptions.Center,
                    vtAlign: VerticalAlignmentOptions.Middle,
                    margin: new(5, 0, 5, 0)
                ) + design);

        public static UIDesign Dropdown(float width, float height, UIDesign design) =>
            Size(width: width, height: height) +
            Image(color: new(1, 1, 1, 1), sprite: BorderSprites.Border.Get(), alphaHit: 0) +
            Component<TMP_Dropdown>() + AssignSprites(InputSprites) +
            "Label".AsChild(
                RtFill +
                Font(size: height) +
                Text(
                    hrAlign: HorizontalAlignmentOptions.Left,
                    vtAlign: VerticalAlignmentOptions.Middle,
                    margin: new(5, 0, 5, 0)
                ) +
                Component<TextMeshProUGUI, TMP_Dropdown>((text, dropdown) => dropdown.captionText = text) + design) +
            "Template".AsChild(
                RtZero +
                VtScroll(width, height * 10,
                    Fitter() +
                    LayoutV() +
                    Image(color: new(0.5f, 0.5f, 0.5f, 0.7f), sprite: BorderSprites.ColorBg.Get()) +
                    "Option".AsChild(Toggle(width, height, Component<TextMeshProUGUI, TMP_Dropdown>((label, dropdown) => dropdown.itemText = label)))
                ) + Component<RectTransform, TMP_Dropdown>((rect, dropdown) => dropdown.template = rect));

        public static UIDesign Window(float width, float height, UIDesign design) =>
            ClearPanel +
            LayoutV(spacing: 6, padding: Offset(6, 6)) +
            Component<UI_DragWindow>() +
            "Title".AsChild(
                Size(width: width, height: 30) +
                LayoutH(padding: Offset(20, 0)) +
                Image(color: new(1, 1, 1, 1), sprite: BorderSprites.ColorBg.Get()) +
                "Label".AsChild(Font() + design));

        static TMP_FontAsset FontAsset;
        static void Initialize(GameObject go) =>
            FontAsset = go.GetComponentsInChildren<TextMeshProUGUI>(true).First(tmp => tmp.font != null).font;
        internal static IDisposable Initialize() =>
            Hooks.OnFontInitialize.Subscribe(Initialize);
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
            UGUI.Root.OnUpdateAsObservable()
                .Where(_ => Shortcut.Value.IsDown())
                .Select(_ => State.Value = !State.Value);

        public Window Create(float width, float height, string name) =>
            new Window(this,


            width, height, name);
    }
    public class Window : IDisposable {
        public GameObject Background { init; get; }
        public GameObject Content { init; get; }
        public IObservable<Unit> OnUpdate { init; get; }
        public CompositeDisposable Subscriptions { init; get; }
        public string Title { get => TitleUI.text; set => TitleUI.SetText(value);  }
        TextMeshProUGUI TitleUI;
        public void Dispose() => Subscriptions.Dispose();
        Window(GameObject go, IObservable<Unit> observable) =>
            (Background, OnUpdate) = (go, observable);
        Window(GameObject go) : this(go, go.OnUpdateAsObservable()) =>
            go.OnDestroyAsObservable().Subscribe(_ => Dispose());
        Window(WindowConfig config, string name) : this(new GameObject(name)) =>
            Subscriptions = [
                config.OnToggle.Subscribe(Background.SetActive),
                OnUpdate
                    .Select(_ =>  Background.GetComponent<RectTransform>())
                    .Select(rt => rt.anchoredPosition)
                    .DistinctUntilChanged()
                    .Subscribe(config.Update),
            ];
        internal Window(WindowConfig config, float width, float height, string name) : this(config, name) =>
            Content = new GameObject("Content").With(
                Background.With(
                    UGUI.Root.AsParent(active: config.State.Value) +
                    UGUI.Rt(
                        anchoredPosition: new(config.AnchorX.Value, config.AnchorY.Value),
                        sizeDelta: new(width + 12, height + 48),
                        anchorMin: new(0, 1),
                        anchorMax: new(0, 1),
                        offsetMin: new(0, 0),
                        offsetMax: new(0, 0),
                        pivot: new(0, 1)) +
                    UGUI.Window(width, height,
                        UGUI.Text(text: name) +
                        UGUI.Component<TextMeshProUGUI>(text => TitleUI = text))
            ).AsParent() + UGUI.Size(width, height));
    }


}