using System;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using Character;
using CharacterCreation;
using BepInEx.Unity.IL2CPP;
using Actor = AC.User.ActorData;
using ActorIndex = (int, int);

namespace Fishbone
{
    public static partial class Extension<T, U>
    {
        public static Storage<T, U, Human> Humans =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static Storage<T, U, Actor> Actors =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static Storage<T, U, ActorIndex> Indices =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Data, tuple.Value))
                .Merge(OnTrackActor.Select(tuple => (tuple.Data, tuple.Value)));
        public static IObservable<(HumanDataCoordinate Data, U Value)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Data, tuple.Value));
    }

    public static partial class Extension<T>
    {
        public static Storage<T, Human> Humans =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static Storage<T, Actor> Actors =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static Storage<T, ActorIndex> Indices =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Data, tuple.Value))
                .Merge(OnTrackActor.Select(tuple => (tuple.Data, tuple.Value)));
    }

    public static partial class Extension
    {
        public static IObservable<Unit> OnPrepareSaveChara =>
            OnSaveCustomChara.Select(_ => Unit.Default).Concat(OnCopyCustomToActor.Select(_ => Unit.Default));
        public static IObservable<Unit> OnPrepareSaveCoord =>
            Hooks.OnChangeCustomCoord.Select(_ => Unit.Default);
        public static IObservable<ZipArchive> OnSaveCustomChara =>
            Observable.Create<ZipArchive>(observer => Hooks.OnSaveCustomChara.Subscribe(path => Save(path, observer)));
        public static IObservable<ZipArchive> OnSaveCustomCoord =>
            Observable.Create<ZipArchive>(observer => Hooks.OnSaveCustomCoord.Subscribe(path => Save(path, observer)));
        public static IObservable<(ZipArchive Value, ActorIndex Index)> OnSaveActor =
            Observable.Create<(ZipArchive, ActorIndex)>(observer => Hooks.OnSaveActor.Subscribe(actor => Save(actor, observer)));
        public static IObservable<Human> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human));
        public static IObservable<(int, int)> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve);
        public static IObservable<(Human Human, ActorIndex Index)> OnActorHumanize => OnActorHumanizeInternal;
        public static IObservable<Human> OnLoadChara =>
            OnLoadCustomChara.Merge(OnActorHumanize.Select(tuple => tuple.Human));
        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human));
        public static IObservable<Human> OnLoadCustomCoord =>
            OnLoadCoord.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware);
        public static IObservable<Human> OnLoadActorCoord =>
            OnLoadCoord.Where(_ => CharaLoadTrack.Mode != CharaLoadTrack.FlagAware);
        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnSaveCustomChara.Subscribe(Extension<T, U>.SaveCustomChara),
            OnSaveCustomCoord.Subscribe(Extension<T, U>.SaveCustomCoord),
            OnConvertChara.Subscribe(Extension<T, U>.SaveCustomChara),
            OnConvertCoord.Subscribe(Extension<T, U>.SaveCustomCoord),
            OnSaveActor.Subscribe(Extension<T, U>.SaveActorChara),
            Hooks.OnSwapActor.Subscribe(Extension<T, U>.Swap),
            Hooks.OnInitializeActors.Subscribe(Extension<T, U>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T, U>.ClearCustom),
            Extension<T, U>.OnLoadCustomChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T, U>.OnLoadActorChara.Subscribe(tuple => Extension<T, U>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T, U>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T, U>.ActorToCustom),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value),
            OnChangeActorCoord.Subscribe(tuple => Extension<T, U>.Indices.NowCoordinate[tuple.Index] =  Extension<T, U>.Indices[tuple.Index, tuple.CoordinateType])
        ];

        public static IDisposable[] Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() => [
            OnSaveCustomChara.Subscribe(Extension<T>.SaveCustomChara),
            OnConvertChara.Subscribe(Extension<T>.SaveCustomChara),
            OnSaveActor.Subscribe(Extension<T>.SaveActorChara),
            Hooks.OnSwapActor.Subscribe(Extension<T>.Swap),
            Hooks.OnInitializeActors.Subscribe(Extension<T>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T>.ClearCustom),
            Extension<T>.OnLoadCustomChara.Subscribe(tuple =>Extension<T>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T>.OnLoadActorChara.Subscribe(tuple => Extension<T>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T>.ActorToCustom),
        ];
        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "Aicomi";
    }
}