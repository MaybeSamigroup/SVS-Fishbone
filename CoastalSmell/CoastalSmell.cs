using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Il2CppSystem.Threading;
#if Aicomi
using ILLGAMES.Unity.Component;
#else
using ILLGames.Unity.Component;
#endif
using Cysharp.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace CoastalSmell
{
    #region Utilities
    public static partial class Util
    {
        static IDisposable DelayFrames(this CancellationTokenSource cts, Action action, int frames) =>
            UniTask.DelayFrame(frames, PlayerLoopTiming.Update, cts.Token)
                .ContinueWith(action) switch { _ => Disposable.Create(cts.Cancel) };
        public static IDisposable DelayFrames(this Action action, int frames) =>
            new CancellationTokenSource().DelayFrames(action, frames);
    }

    public static class SingletonInitializerExtension<T> where T : SingletonInitializer<T>
    {
        public static IObservable<T> OnStartup =>
            Startup.AsObservable().Select(_ => SingletonInitializer<T>.Instance);
        public static IObservable<Unit> OnDestroy => Destroy.AsObservable();
        static Subject<Unit> Startup = new();
        static Subject<Unit> Destroy = new();
        static Action Initialize = () =>
            SingletonInitializer<T>
                .WaitUntilSetup(CancellationToken.None)
                .ContinueWith(F.Apply(Startup.OnNext, Unit.Default));
        static SingletonInitializerExtension() => (
            OnDestroy.Subscribe(_ => UniTask.NextFrame().ContinueWith(Initialize)),
            OnStartup.Subscribe(cmp => cmp.OnDestroyAsObservable().Subscribe(Destroy.OnNext))
        ).With(Initialize);
    }

    #endregion


    /// <summary>
    /// Functional utilities for favor.
    /// </summary>
    public static class F
    {
        #region Either/Maybe

        public static void Either(this bool value, Action a1, Action a2) => (value ? a2 : a1)();
        public static void Maybe(this bool value, Action action) => value.Either(DoNothing, action);

        #endregion

        #region Ignoring

        public static Action Ignoring<O>(this Func<O> f) => () => f();
        public static Action<I1> Ignoring<I1>(this Action action) => _ => action();
        public static Action Ignoring<I1>(I1 _) => DoNothing;

        #endregion

        #region Apply (Currying)

        public static Action Apply<I1>(this Action<I1> action, I1 i1) => () => action(i1);
        public static Action Apply<I1, I2>(this Action<I1, I2> action, I1 i1, I2 i2) => () => action(i1, i2);
        public static Action Apply<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1, I2 i2, I3 i3) => () => action(i1, i2, i3);
        public static Action Apply<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1, I2 i2, I3 i3, I4 i4) => () => action(i1, i2, i3, i4);
        public static Action Apply<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1, I2 i2, I3 i3, I4 i4, I5 i5) => () => action(i1, i2, i3, i4, i5);

        public static Action<I2> Apply<I1, I2>(this Action<I1, I2> action, I1 i1) => i2 => action(i1, i2);
        public static Action<I2, I3> Apply<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1) => (i2, i3) => action(i1, i2, i3);
        public static Action<I2, I3, I4> Apply<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1) => (i2, i3, i4) => action(i1, i2, i3, i4);
        public static Action<I2, I3, I4, I5> Apply<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1) => (i2, i3, i4, i5) => action(i1, i2, i3, i4, i5);

        #endregion

        #region ApplyDisposable

        public static Action ApplyDisposable<I1>(this Action<I1> action, I1 i1) where I1 : IDisposable => () => { using (i1) { action(i1); } };
        public static Action<I2> ApplyDisposable<I1, I2>(this Action<I1, I2> action, I1 i1) where I1 : IDisposable => i2 => { using (i1) { action(i1, i2); } };
        public static Action<I2, I3> ApplyDisposable<I1, I2, I3>(this Action<I1, I2, I3> action, I1 i1) where I1 : IDisposable => (i2, i3) => { using (i1) { action(i1, i2, i3); } };
        public static Action<I2, I3, I4> ApplyDisposable<I1, I2, I3, I4>(this Action<I1, I2, I3, I4> action, I1 i1) where I1 : IDisposable => (i2, i3, i4) => { using (i1) { action(i1, i2, i3, i4); } };
        public static Action<I2, I3, I4, I5> ApplyDisposable<I1, I2, I3, I4, I5>(this Action<I1, I2, I3, I4, I5> action, I1 i1) where I1 : IDisposable => (i2, i3, i4, i5) => { using (i1) { action(i1, i2, i3, i4, i5); } };

        #endregion

        #region Apply (Func)

        public static Func<O> Apply<I1, O>(this Func<I1, O> f, I1 i1) => () => f(i1);
        public static Func<O> Apply<I1, I2, O>(this Func<I1, I2, O> f, I1 i1, I2 i2) => () => f(i1, i2);
        public static Func<O> Apply<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1, I2 i2, I3 i3) => () => f(i1, i2, i3);
        public static Func<O> Apply<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1, I2 i2, I3 i3, I4 i4) => () => f(i1, i2, i3, i4);
        public static Func<O> Apply<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1, I2 i2, I3 i3, I4 i4, I5 i5) => () => f(i1, i2, i3, i4, i5);

        public static Func<I2, O> Apply<I1, I2, O>(this Func<I1, I2, O> f, I1 i1) => i2 => f(i1, i2);
        public static Func<I2, I3, O> Apply<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1) => (i2, i3) => f(i1, i2, i3);
        public static Func<I2, I3, I4, O> Apply<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1) => (i2, i3, i4) => f(i1, i2, i3, i4);
        public static Func<I2, I3, I4, I5, O> Apply<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1) => (i2, i3, i4, i5) => f(i1, i2, i3, i4, i5);

        #endregion

        #region ApplyDisposable (Func)

        public static Func<O> ApplyDisposable<I1, O>(this Func<I1, O> f, I1 i1) where I1 : IDisposable => () => { using (i1) { return f(i1); } };
        public static Func<I2, O> ApplyDisposable<I1, I2, O>(this Func<I1, I2, O> f, I1 i1) where I1 : IDisposable => i2 => { using (i1) { return f(i1, i2); } };
        public static Func<I2, I3, O> ApplyDisposable<I1, I2, I3, O>(this Func<I1, I2, I3, O> f, I1 i1) where I1 : IDisposable => (i2, i3) => { using (i1) { return f(i1, i2, i3); } };
        public static Func<I2, I3, I4, O> ApplyDisposable<I1, I2, I3, I4, O>(this Func<I1, I2, I3, I4, O> f, I1 i1) where I1 : IDisposable => (i2, i3, i4) => { using (i1) { return f(i1, i2, i3, i4); } };
        public static Func<I2, I3, I4, I5, O> ApplyDisposable<I1, I2, I3, I4, I5, O>(this Func<I1, I2, I3, I4, I5, O> f, I1 i1) where I1 : IDisposable => (i2, i3, i4, i5) => { using (i1) { return f(i1, i2, i3, i4, i5); } };

        #endregion

        #region With

        public static T With<T>(this T value, Action<T> action) => value.With(action.Apply(value));
        public static T With<T, U>(this T value, Func<T, U> f) => value.With(f.Apply(value).Ignoring());
        public static T With<T>(this T value, Action action)
        {
            action();
            return value;
        }

        #endregion

        #region Try

        public static void Try(this Action action, Action<string> log)
        {
            try { action(); }
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

        #endregion

        #region Misc

        public static Func<I, O> Constant<I, O>(this Action<I> f, O value) => i => value.With(f.Apply(i));
        public static Action DoNothing = () => { };
        public static Func<Action, T, Action> Accumulate<T>(Action<T> action) =>
            (actions, value) => actions += action.Apply(value);
        public static Il2CppSystem.Action AsIl2Cpp(Action action) => action;
        #endregion

        #region Enumerable Extensions

        public static IEnumerable<(T Value, int Index)> Index<T>(this IEnumerable<T> values) =>
            values.Select((v, i) => (v, i));

        public static void ForEach<T>(this IEnumerable<T> values, Action<T> action) =>
            values.Aggregate(DoNothing, Accumulate(action))();

        public static void ForEach<K, V>(this IEnumerable<(K, V)> values, Action<K, V> action) =>
            values.Aggregate(DoNothing, Accumulate<(K Key, V Value)>(entry => action(entry.Key, entry.Value)))();

        public static void ForEach<K, V>(this IEnumerable<KeyValuePair<K, V>> values, Action<K, V> action) =>
            values.Aggregate(DoNothing, Accumulate<KeyValuePair<K, V>>(entry => action(entry.Key, entry.Value)))();

        public static void ForEachIndex<T>(this IEnumerable<T> values, Action<T, int> action) =>
            Index(values).Aggregate(DoNothing, Accumulate<(T Value, int Index)>(tuple => action(tuple.Value, tuple.Index)))();

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<(K Key, V Value)> tuples) =>
            tuples.ToDictionary(item => item.Key, item => item.Value);

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<Tuple<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Item1, item => item.Item2);

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Key, item => item.Value);

        public static Il2CppSystem.Collections.Generic.List<T> AsIl2Cpp<T>(this IEnumerable<T> values) =>
            new Il2CppSystem.Collections.Generic.List<T>().With(list => values.ForEach(list.Add));

        public static IEnumerable<(K Key, V Value)> Yield<K, V>(this Il2CppSystem.Collections.Generic.Dictionary<K, V> items)
        {
            foreach (var (k, v) in items) yield return (k, v);
        }
        public static IEnumerable<T> Yield<T>(this Il2CppSystem.Collections.Generic.IReadOnlyList<T> items) =>
            Yield(items, new Il2CppSystem.Collections.Generic.ICollection<T>(items.Pointer).Count);
        static IEnumerable<T> Yield<T>(this Il2CppSystem.Collections.Generic.IReadOnlyList<T> items, int count)
        {
            for (var index = 0; index < count; index++) yield return items[index];
        }
        #endregion
    }
    public static partial class Hooks
    {
        static Subject<GameObject> FontInitialize = new();
        static Subject<Unit> CommonSpaceInitialize = new();
        internal static IObservable<GameObject> OnFontInitialize =>
            FontInitialize.AsObservable().FirstAsync();
        internal static IObservable<Transform> OnCommonSpaceInitialize =>
            CommonSpaceInitialize.AsObservable().Select(_ => Manager.Scene.CommonSpace.transform).FirstAsync();

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Manager.Scene), nameof(Manager.Scene.CreateSpace))]
        static void NotifyCommonSpaceInitialize() =>
            CommonSpaceInitialize.OnNext(Unit.Default);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.BindFont), typeof(GameObject), typeof(int))]
        static void NotifyTranslateReady(GameObject target) =>
            FontInitialize.OnNext(target);

        internal static IDisposable Initialize() =>
            Disposable.Create(Harmony.CreateAndPatchAll(typeof(Hooks), $"Hooks.{Plugin.Name}").UnpatchSelf);
    }

    #region Plugin

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "CoastalSmell";
        public const string Version = "2.0.0";
        internal static Plugin Instance;
        CompositeDisposable Subscriptions;
        public Plugin() : base() => Instance = this;
        public override void Load() => Subscriptions = [
            Sprites.Initialize(), UGUI.Initialize(), Hooks.Initialize()
        ];
        public override bool Unload() => true.With(Subscriptions.Dispose) && base.Unload();
    }
    #endregion

}