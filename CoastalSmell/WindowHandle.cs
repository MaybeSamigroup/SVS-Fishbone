using System;
using UnityEngine;
using TMPro;
using UniRx;
using UniRx.Triggers;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;

namespace CoastalSmell
{
    public class WindowHandle
    {
        ConfigEntry<float> AnchorX;
        ConfigEntry<float> AnchorY;
        ConfigEntry<KeyboardShortcut> Shortcut;
        ConfigEntry<bool> State;
        public TextMeshProUGUI Title;
        public CompositeDisposable Disposables;
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
        void PrepareDisposable(GameObject go) =>
            Disposables = new CompositeDisposable(Disposable.Create(F.Apply(UnityEngine.Object.Destroy, go)));
        public void Apply(GameObject go) => go.With(PrepareDisposable)
            .With(UGUI.ModifyAt("Title", "Label")(UGUI.Cmp<TextMeshProUGUI>(ui => Title = ui)))
            .With(UGUI.Go(active: State.Value)).GetComponentInParent<ObservableUpdateTrigger>()
                .UpdateAsObservable().Subscribe(ToUpdate(go) + ToUpdate(go.GetComponent<RectTransform>()))
                .With(Disposables.Add);
        public void Dispose() =>
            Disposables.With(Disposables.Dispose).Clear();

        public static implicit operator bool(WindowHandle handle) =>
            handle.State.Value;
        public static implicit operator Vector2(WindowHandle handle) =>
            new(handle.AnchorX.Value, handle.AnchorY.Value);
    }
}