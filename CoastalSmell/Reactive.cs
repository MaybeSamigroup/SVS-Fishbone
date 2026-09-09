using System;
using System.Reactive;
using System.Reactive.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if Aicomi
using Il2CppSystem.Threading;
using Rx = R3;
#else
using Rx = UniRx;
#endif
using BepInEx.Configuration;

namespace CoastalSmell
{
    /// <summary>
    /// Convenience adapters between Unity/Il2Cpp reactive types and <see cref="System.Reactive"/> observables.
    /// Provides wrappers to convert events, UnityEvents and Il2Cpp observables into <see cref="IObservable{T}"/>.
    /// </summary>
    public static class RxExtensions
    {
        public static IObserver<T> Compose<T>(this IObserver<T> o1, IObserver<T> o2) =>
            Observer.Create<T>(value => o2.OnNext(value.With(o1.OnNext)));

        public static IObserver<T> Compose<T, U>(this IObserver<T> ot, IObserver<U> ou, Func<T, U> f) =>
            Observer.Create<T>(value => ou.OnNext(f(value.With(ot.OnNext))));

        /// <summary>
        /// Converts a BepInEx <see cref="ConfigEntry{T}"/> into an observable stream of its current value.
        /// </summary>
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
        /// <summary>
        /// Wraps an Il2Cpp observable into a <see cref="System.Reactive"/> <see cref="IObservable{T}"/>.
        /// </summary>
        public static IObservable<T> Wrap<T>(this Il2CppSystem.IObservable<T> il2cpp) =>
            Observable.Create<T>(mono => Rx.ObservableExtensions.Subscribe(il2cpp, (Action<T>)mono.OnNext).Dispose);

        /// <summary>
        /// Wraps an Il2Cpp observable of unit values into a System.Reactive unit observable.
        /// </summary>
        public static IObservable<Unit> Wrap(this Il2CppSystem.IObservable<Rx.Unit> il2cpp) => 
            il2cpp.Wrap<Rx.Unit>().Select<Rx.Unit, Unit>(_ => default);

        /// <summary>
        /// Converts a UnityEvent into an <see cref="IObservable{T}"/> sequence.
        /// </summary>
        public static IObservable<T> AsObservable<T>(this UnityEvent<T> ev) =>
            Rx.UnityEventExtensions.AsObservable(ev).Wrap();

        /// <summary>
        /// Returns an observable sequence that signals when the <see cref="Button"/> is clicked.
        /// </summary>
        public static IObservable<Unit> OnClickAsObservable(this Button ui) =>
            Rx.UnityEventExtensions.AsObservable(ui.onClick).Wrap();
#endif
        /// <summary>
        /// Converts a System.IDisposable into an Il2Cpp disposable wrapper.
        /// </summary>
        public static Il2CppSystem.IDisposable Unwrap(this IDisposable mono) =>
            Rx.Disposable.Create((Action)mono.Dispose);

        /// <summary>
        /// Observable that emits a boolean value when the Toggle's state changes.
        /// </summary>
        public static IObservable<bool> OnValueChangedAsObservable(this Toggle ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits the selected index when the TMP_Dropdown value changes.
        /// </summary>
        public static IObservable<int> OnValueChangedAsObservable(this TMP_Dropdown ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits a float value when a Slider changes.
        /// </summary>
        public static IObservable<float> OnValueChangedAsObservable(this Slider ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits a float value when a Scrollbar changes.
        /// </summary>
        public static IObservable<float> OnValueChangedAsObservable(this Scrollbar ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits a Vector2 when a ScrollRect's value changes.
        /// </summary>
        public static IObservable<Vector2> OnValueChangedAsObservable(this ScrollRect ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits the input string when an InputField's value changes.
        /// </summary>
        public static IObservable<string> OnValueChangedAsObservable(this InputField ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits the input string when a TMP_InputField's value changes.
        /// </summary>
        public static IObservable<string> OnValueChangedAsObservable(this TMP_InputField ui) =>
            ui.onValueChanged.AsObservable();

        /// <summary>
        /// Observable that emits on every frame update for the component's GameObject.
        /// </summary>
        public static IObservable<Unit> OnUpdateAsObservable(this Component cmp) =>
            cmp.gameObject.OnUpdateAsObservable();
        public static IObservable<Unit> OnEnableAsObservable(this Component cmp) =>
            cmp.gameObject.OnEnableAsObservable();
        public static IObservable<Unit> OnDisableAsObservable(this Component cmp) =>
            cmp.gameObject.OnDisableAsObservable();
        public static IObservable<Unit> OnDestroyAsObservable(this Component cmp) =>
            cmp.gameObject.OnDestroyAsObservable();
        public static IObservable<Unit> OnTransformChildrenChangedAsObservable(this Component cmp) =>
            cmp.gameObject.OnTransformChildrenChangedAsObservable(); 
        public static IObservable<PointerEventData> OnPointerEnterAsObservable(this Component cmp) =>
            cmp.gameObject.OnPointerEnterAsObservable();
        public static IObservable<PointerEventData> OnPointerExitAsObservable(this Component cmp) =>
            cmp.gameObject.OnPointerExitAsObservable();
        public static IObservable<BaseEventData> OnSelectAsObservable(this Component cmp) =>
            cmp.gameObject.OnSelectAsObservable();
        public static IObservable<BaseEventData> OnDeselectAsObservable(this Component cmp) =>
            cmp.gameObject.OnDeselectAsObservable();

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
        public static IObservable<PointerEventData> OnPointerEnterAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservablePointerEnterTrigger>(go)
                .OnPointerEnterAsObservable().Wrap();
        public static IObservable<PointerEventData> OnPointerExitAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservablePointerExitTrigger>(go)
                .OnPointerExitAsObservable().Wrap();
        public static IObservable<BaseEventData> OnSelectAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableSelectTrigger>(go)
                .OnSelectAsObservable().Wrap();
        public static IObservable<BaseEventData> OnDeselectAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableDeselectTrigger>(go)
                .OnDeselectAsObservable().Wrap();

        public static IObservable<Unit> OnTransformChildrenChangedAsObservable(this GameObject go) =>
            Rx.Triggers.ObservableTriggerExtensions
                .GetOrAddComponent<Rx.Triggers.ObservableTransformChangedTrigger>(go)
                .OnTransformChildrenChangedAsObservable().Wrap();
    }
}