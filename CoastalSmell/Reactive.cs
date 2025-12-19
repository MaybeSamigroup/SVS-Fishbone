using System;
using System.Reactive;
using System.Reactive.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if Aicomi
using Il2CppSystem.Threading;
using Rx = R3;
#else
using Rx = UniRx;
#endif
using BepInEx.Configuration;

namespace CoastalSmell
{
    public static class RxExtensions
    {
        public static IObservable<T> AsObservable<T>(this ConfigEntry<T> config) =>
            Observable.FromEvent<EventHandler, EventArgs>(
                handler => (s, e) => handler(e),
                handler => config.SettingChanged += handler,
                handler => config.SettingChanged -= handler
            ).Select(args => config.Value);

#if Aicomi
        public static IObservable<T> Wrap<T>(this Rx.Observable<T> il2cpp) =>
            Observable.Create<T>(mono => Rx.ObservableSubscribeExtensions.Subscribe(il2cpp, (Action<T>)mono.OnNext).Dispose);

        public static IObservable<Unit> Wrap(this Rx.Observable<Rx.Unit> il2cpp) =>
            il2cpp.Wrap<Rx.Unit>().Select<Rx.Unit, Unit>(_ => default);

        public static IObservable<T> AsObservable<T>(this UnityEvent<T> ev) =>
            Rx.UnityEventExtensions.AsObservable(ev, CancellationToken.None).Wrap();

        public static IObservable<Unit> OnClickAsObservable(this Button ui) =>
            Rx.UnityEventExtensions.AsObservable(ui.onClick, CancellationToken.None).Wrap();
#else
        public static IObservable<T> Wrap<T>(this Il2CppSystem.IObservable<T> il2cpp) =>
            Observable.Create<T>(mono => Rx.ObservableExtensions.Subscribe(il2cpp, (Action<T>)mono.OnNext).Dispose);

        public static IObservable<Unit> Wrap(this Il2CppSystem.IObservable<Rx.Unit> il2cpp) => 
            il2cpp.Wrap<Rx.Unit>().Select<Rx.Unit, Unit>(_ => default);

        public static IObservable<T> AsObservable<T>(this UnityEvent<T> ev) =>
            Rx.UnityEventExtensions.AsObservable(ev).Wrap();

        public static IObservable<Unit> OnClickAsObservable(this Button ui) =>
            Rx.UnityEventExtensions.AsObservable(ui.onClick).Wrap();
#endif
        public static Il2CppSystem.IDisposable Unwrap(this IDisposable mono) =>
            Rx.Disposable.Create((Action)mono.Dispose);

        public static IObservable<bool> OnValueChangedAsObservable(this Toggle ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<int> OnValueChangedAsObservable(this TMP_Dropdown ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<float> OnValueChangedAsObservable(this Slider ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<float> OnValueChangedAsObservable(this Scrollbar ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<Vector2> OnValueChangedAsObservable(this ScrollRect ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<string> OnValueChangedAsObservable(this InputField ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<string> OnValueChangedAsObservable(this TMP_InputField ui) =>
            ui.onValueChanged.AsObservable();

        public static IObservable<Unit> OnUpdateAsObservable(this Component cmp) =>
            Rx.Triggers.ObservableTriggerExtensions.UpdateAsObservable(cmp).Wrap();
        public static IObservable<Unit> OnEnableAsObservable(this Component cmp) =>
            Rx.Triggers.ObservableTriggerExtensions.OnEnableAsObservable(cmp).Wrap(); 
        public static IObservable<Unit> OnDisableAsObservable(this Component cmp) =>
            Rx.Triggers.ObservableTriggerExtensions.OnEnableAsObservable(cmp).Wrap(); 
        public static IObservable<Unit> OnDestroyAsObservable(this Component cmp) =>
            Rx.Triggers.ObservableTriggerExtensions.OnDestroyAsObservable(cmp).Wrap();
        public static IObservable<Unit> OnUpdateAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableUpdateTrigger>(go)
                .UpdateAsObservable().Wrap();
        public static IObservable<Unit> OnEnableAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableEnableTrigger>(go)
                .OnEnableAsObservable().Wrap();
        public static IObservable<Unit> OnDisableAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableEnableTrigger>(go)
                .OnDisableAsObservable().Wrap();
        public static IObservable<Unit> OnDestroyAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableDestroyTrigger>(go)
                .OnDestroyAsObservable().Wrap();
    }
}