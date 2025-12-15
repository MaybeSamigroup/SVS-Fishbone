using System;
using System.IO.Compression;
using System.Reactive.Linq;
using Character;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;

namespace Fishbone
{
    public static partial class Extension
    {
        public static IObservable<Human> OnPrepareSaveChara =>
            OnSaveChara.Select(tuple => tuple.Item1);

        public static IObservable<(Human, ZipArchive)> OnSaveChara =
            Observable.Create<(Human, ZipArchive)>(observer => Hooks.OnSaveHuman.Subscribe(actor => Save(actor, observer)));

        public static IObservable<Human> OnLoadChara =
            OnTrackChara.SelectMany(tuple => tuple.Item1.OnResolve);

        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Item1.OnResolve.Select(pair => pair.Item1));

        public static IObservable<(Human, int)> OnChangeCoord = Hooks.OnChangeCoordinate;

        public static T Chara<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(human);

        public static U Coord<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(human);

        public static void Chara<T, U>(this Human human, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(human, mods);

        public static void Coord<T, U>(this Human human, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(human, mods);

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            OnSaveChara.Subscribe(tuple => Extension<T, U>.SaveChara(tuple.Item2, HumanExtension<T, U>.Chara(tuple.Item1)));
            Extension<T, U>.OnLoadChara.Subscribe(tuple => HumanExtension<T, U>.Chara(tuple.Item1, tuple.Item2));
            Extension<T, U>.OnLoadChara.Subscribe(tuple => HumanExtension<T, U>.Prepare(tuple.Item1));
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => HumanExtension<T, U>.Coord(tuple.Item1).Merge(tuple.Item2, tuple.Item3));
            Plugin.Instance.Log.LogDebug($"ComplexExtension<{typeof(T)},{typeof(U)}> registered.");
        }

        public static T Chara<T>(this Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara(human);

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T,T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            OnSaveChara.Subscribe(tuple => Extension<T>.SaveChara(tuple.Item2, HumanExtension<T>.Chara(tuple.Item1)));
            Extension<T>.OnLoadChara.Subscribe(tuple => HumanExtension<T>.Chara(tuple.Item1, tuple.Item2));
            Extension<T>.OnLoadChara.Subscribe(tuple => HumanExtension<T>.Prepare(tuple.Item1));
            Plugin.Instance.Log.LogDebug($"SimpleExtension<{typeof(T)}> registered.");
        }
    }
    public static partial class Extension<T, U>
    {
        public static IObservable<(HumanData, T)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Item2, tuple.Item3));
        public static IObservable<(HumanDataCoordinate, U)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Item2, tuple.Item3));
    }
    public static partial class Extension<T>
    {
        public static IObservable<(HumanData, T)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Item2, tuple.Item3));
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
        public override void Load()
        {
            Instance = this;
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            Extension.Initialize();
        }
    }
}