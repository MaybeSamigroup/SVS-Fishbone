using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.Linq;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using ILLGames.Unity.Component;

namespace CoastalSmell
{
    #region Utilities

    public static partial class Util
    {
        /// <summary>
        /// Executes <paramref name="action"/> when <paramref name="predicate"/> is true, otherwise tries again next frame.
        /// </summary>
        public static Action<Func<bool>, Action> DoOnCondition =
            (predicate, action) => predicate()
                .Either(DoNextFrame.Apply(DoOnCondition.Apply(predicate).Apply(action)), action);
    }

    public static class Util<T> where T : SingletonInitializer<T>
    {
        static Action<Action, Action> AwaitStartup = (onStartup, onDestroy) =>
            Util.DoNextFrame.With(onDestroy)(Hook.Apply(onStartup).Apply(onDestroy));

        static Action<Action, Action> AwaitDestroy = (onStartup, onDestroy) =>
            SingletonInitializer<T>.Instance.With(onStartup).OnDestroyAsObservable()
                .Subscribe(AwaitStartup.Apply(onStartup).Apply(onDestroy).Ignoring<Unit>());

        public static Action<Action, Action> Hook = (onStartup, onDestroy) =>
            SingletonInitializer<T>.WaitUntilSetup(Il2CppSystem.Threading.CancellationToken.None)
                .ContinueWith(AwaitDestroy.Apply(onStartup).Apply(onDestroy));
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

        #endregion

        #region Enumerable Extensions

        public static IEnumerable<Tuple<T, int>> Index<T>(this IEnumerable<T> values) =>
            values.Select((v, i) => new Tuple<T, int>(v, i));

        public static void ForEach<T>(this IEnumerable<T> values, Action<T> action) =>
            values.Aggregate(DoNothing, Accumulate(action))();

        public static void ForEach<K, V>(this IEnumerable<Tuple<K, V>> values, Action<K, V> action) =>
            values.Aggregate(DoNothing, Accumulate<Tuple<K, V>>(entry => action(entry.Item1, entry.Item2)))();

        public static void ForEach<K, V>(this IEnumerable<KeyValuePair<K, V>> values, Action<K, V> action) =>
            values.Aggregate(DoNothing, Accumulate<KeyValuePair<K, V>>(entry => action(entry.Key, entry.Value)))();

        public static void ForEachIndex<T>(this IEnumerable<T> values, Action<T, int> action) =>
            Index(values).Aggregate(DoNothing, Accumulate<Tuple<T, int>>(tuple => action(tuple.Item1, tuple.Item2)))();

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<Tuple<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Item1, item => item.Item2);

        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<KeyValuePair<K, V>> tuples) =>
            tuples.ToDictionary(item => item.Key, item => item.Value);

        public static IEnumerable<Tuple<K, V>> Yield<K, V>(this Il2CppSystem.Collections.Generic.Dictionary<K, V> items)
        {
            foreach (var (k, v) in items)
                yield return new Tuple<K, V>(k, v);
        }

        #endregion
    }

    #region Plugin

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public partial class Plugin : BasePlugin
    {
        internal static BepInEx.Logging.ManualLogSource Logger;
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "CoastalSmell";
        public const string Version = "1.0.4";

        public override void Load() =>
            (Logger = Log).With(Sprites.Initialize).With(UGUI.Initialize);
    }

    #endregion
}