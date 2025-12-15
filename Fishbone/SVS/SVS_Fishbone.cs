using System;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Extension
    {
        public static IObservable<Unit> OnPrepareSaveChara =>
            OnSaveCustomChara.Select(_ => Unit.Default).Merge(OnCopyCustomToActor.Select(_ => Unit.Default));
        public static IObservable<Unit> OnPrepareSaveCoord =>
            Hooks.OnChangeCustomCoord.Select(_ => Unit.Default);
        public static IObservable<ZipArchive> OnSaveCustomChara =>
            Observable.Create<ZipArchive>(observer => Hooks.OnSaveCustomChara.Subscribe(path => Save(path, observer)));
        public static IObservable<ZipArchive> OnSaveCustomCoord =>
            Observable.Create<ZipArchive>(observer => Hooks.OnSaveCustomCoord.Subscribe(path => Save(path, observer)));
        public static IObservable<(int, ZipArchive)> OnSaveActor =
            Observable.Create<(int, ZipArchive)>(observer => Hooks.OnSaveActor.Subscribe(actor => Save(actor, observer)));
        public static IObservable<Human> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Item1.OnResolve.Select(_ => HumanCustom.Instance.Human));
        public static IObservable<int> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Item1.OnResolve);
        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Item1.OnResolve.Select(pair => pair.Item1));
        public static IObservable<Human> OnLoadCustomCoord =>
            OnLoadCoord.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware);
        public static IObservable<Human> OnLoadActorCoord =>
            OnLoadCoord.Where(_ => CharaLoadTrack.Mode != CharaLoadTrack.FlagAware);
        public static IObservable<(Human, int)> OnActorHumanize => OnActorHumanizeInternal;

        public static T Chara<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Chara(index)
                : HumanExtension<T, U>.Chara();

        public static U Coord<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Coord(index)
                : HumanExtension<T, U>.Coord();

        public static void Chara<T, U>(this Human human, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Chara<T, U>, mods), F.Apply(ActorExtension<T, U>.Chara, index, mods));

        public static void Coord<T, U>(this Human human, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Coord<T, U>, mods), F.Apply(ActorExtension<T, U>.Coord, index, mods));

        public static T Chara<T, U>(this SaveData.Actor actor)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Chara(actor.charasGameParam.Index);

        public static U Coord<T, U>(this SaveData.Actor actor)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Coord(actor.charasGameParam.Index);

        public static void Chara<T, U>(this SaveData.Actor actor, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Chara(actor.charasGameParam.Index, mods);

        public static void Coord<T, U>(this SaveData.Actor actor, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Coord(actor.charasGameParam.Index, mods);

        public static T Chara<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara();

        public static U Coord<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord();

        public static void Chara<T, U>(T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(mods);

        public static void Coord<T, U>(U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(mods);

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            OnSaveCustomChara.Subscribe(archive => Extension<T, U>.SaveChara(archive, HumanExtension<T, U>.Chara()));
            OnSaveCustomCoord.Subscribe(archive => Extension<T, U>.SaveCoord(archive, HumanExtension<T, U>.Coord()));
            OnConvertChara.Subscribe(archive => Extension<T, U>.SaveChara(archive, HumanExtension<T, U>.Chara()));
            OnConvertCoord.Subscribe(archive => Extension<T, U>.SaveChara(archive, HumanExtension<T, U>.Chara()));
            OnSaveActor.Subscribe(tuple => Extension<T, U>.SaveChara(tuple.Item2, ActorExtension<T, U>.Chara(tuple.Item1)));

            Extension<T, U>.OnLoadCustomChara.Subscribe(tuple => HumanExtension<T, U>.LoadChara(tuple.Item1, tuple.Item2));
            Extension<T, U>.OnLoadActorChara.Subscribe(tuple => ActorExtension<T, U>.LoadActor(tuple.Item1, tuple.Item2));
            OnCustomInitialize.Subscribe(HumanExtension<T, U>.Initialize);
            OnCopyCustomToActor.Subscribe(ActorExtension<T, U>.Copy);
            OnCopyActorToCustom.Subscribe(HumanExtension<T, U>.Copy);

            Extension<T, U>.OnLoadCoordInternal.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Subscribe(tuple => HumanExtension<T, U>.Coord().Merge(tuple.Item2, tuple.Item3));
            Extension<T, U>.OnLoadCoordInternal.Where(_ => CharaLoadTrack.Mode != CharaLoadTrack.FlagAware)
                .Subscribe(tuple => ActorExtension<T, U>.LoadActorCoord(tuple.Item1, tuple.Item3, tuple.Item2));

            OnChangeActorCoord.Subscribe(tuple => ActorExtension<T, U>.CoordinateChange(tuple.Item1, tuple.Item2));
            Plugin.Instance.Log.LogDebug($"ComplexExtension<{typeof(T)},{typeof(U)}> registered.");
        }

        public static T Chara<T>(this Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T>.Chara(index)
                : HumanExtension<T>.Chara();

        public static void Chara<T>(this Human human, T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Chara, mods), F.Apply(ActorExtension<T>.Chara, index, mods));

        public static T Chara<T>(this SaveData.Actor actor)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            ActorExtension<T>.Chara(actor.charasGameParam.Index);

        public static void Chara<T>(this SaveData.Actor actor, T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            ActorExtension<T>.Chara(actor.charasGameParam.Index, mods);

        public static T Chara<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara();

        public static void Chara<T>(T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara(mods);

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            OnSaveCustomChara.Subscribe(archive => Extension<T>.SaveChara(archive, HumanExtension<T>.Chara()));
            OnConvertChara.Subscribe(archive => Extension<T>.SaveChara(archive, HumanExtension<T>.Chara()));
            OnConvertCoord.Subscribe(archive => Extension<T>.SaveChara(archive, HumanExtension<T>.Chara()));
            OnSaveActor.Subscribe(tuple => Extension<T>.SaveChara(tuple.Item2, ActorExtension<T>.Chara(tuple.Item1)));

            Extension<T>.OnLoadCustomChara.Subscribe(tuple => HumanExtension<T>.LoadChara(tuple.Item1, tuple.Item2));
            Extension<T>.OnLoadActorChara.Subscribe(tuple => ActorExtension<T>.LoadActor(tuple.Item1, tuple.Item2));
            OnCustomInitialize.Subscribe(HumanExtension<T>.Initialize);
            OnCopyCustomToActor.Subscribe(ActorExtension<T>.Copy);
            OnCopyActorToCustom.Subscribe(HumanExtension<T>.Copy);

            Plugin.Instance.Log.LogDebug($"SimpleExtension<{typeof(T)}> registered.");
        }
        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }
    public static partial class Extension<T, U>
    {
        public static IObservable<(HumanData, T)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Item2, tuple.Item3))
                .Merge(OnTrackActor.Select(tuple => (tuple.Item2, tuple.Item3)));
        public static IObservable<(HumanDataCoordinate, U)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Item2, tuple.Item3));
    }
    public static partial class Extension<T>
    {
        public static IObservable<(HumanData, T)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Item2, tuple.Item3))
                .Merge(OnTrackActor.Select(tuple => (tuple.Item2, tuple.Item3)));
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public override void Load()
        {
            Instance = this;
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            Extension.Initialize();
            Util<HumanCustom>.OnStartup
                .Subscribe(_ => CharaLoadTrack.Mode = CharaLoadTrack.FlagAware);
            Util<HumanCustom>.OnDestroy
                .Subscribe(_ => CharaLoadTrack.Mode = CharaLoadTrack.Ignore);
        }
    }
}