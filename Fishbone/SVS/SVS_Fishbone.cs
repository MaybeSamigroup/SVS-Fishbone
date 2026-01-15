using System;
using System.IO.Compression;
using System.Reactive.Linq;
using Character;
using CharacterCreation;
using BepInEx.Unity.IL2CPP;
using Actor = SaveData.Actor;
using ActorIndex = int;

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
        public static IObservable<(ZipArchive Output, ZipArchive Input, HumanData Data)> OnConvertChara =>
            ConvertChara.AsObservable().Select(pair => (pair.Output, pair.Value.Input, pair.Value.Data));
        
        public static IObservable<(ZipArchive Output, ZipArchive Input, HumanDataCoordinate Data)> OnConvertCoord =>
            ConvertCoord.AsObservable().Select(pair => (pair.Output, pair.Value.Input, pair.Value.Data));

        public static IObservable<Human> OnPrepareSaveChara =>
            PrepareSaveChara.AsObservable().Merge(OnCopyCustomToActor.Select(_ => HumanCustom.Instance.Human));

        public static IObservable<Human> OnPrepareSaveCoord =>
            PrepareSaveCoord.AsObservable().Merge(Hooks.OnChangeCustomCoord.Select(_ => HumanCustom.Instance.Human));

        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveChara => SaveChara.AsObservable();

        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveCoord => SaveCoord.AsObservable();

        public static IObservable<(ZipArchive Archive, ActorIndex Index)> OnSaveActor => SaveActor.AsObservable();

        public static IObservable<Human> OnLoadCustomChara =>
            OnHumanCustomReload
                .Merge(OnTrackCustom.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human)))
                .Merge(OnActorHumanizeInternal.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware).Select(pair => pair.Human));

        public static IObservable<int> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve);

        public static IObservable<(Human Human, int Index)> OnActorHumanize =>
            OnActorHumanizeInternal.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.Ignore);

        public static IObservable<Human> OnLoadChara =>
            OnLoadCustomChara.Merge(OnActorHumanize.Select(tuple => tuple.Human));

        public static IObservable<Human> OnLoadCoord =>
            OnLoadCustomCoord.Merge(OnLoadActorCoord);

        public static IObservable<Human> OnLoadCustomCoord =>
            Hooks.OnChangeCustomCoord.Select(pair => pair.Human)
                .Merge(OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve
                    .Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware).Select(pair => pair.Human)));
        public static IObservable<Human> OnLoadActorCoord =>
            Hooks.OnChangeActorCoord.Select(pair => pair.Human)
                .Merge(OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve
                    .Where(_ => CharaLoadTrack.Mode != CharaLoadTrack.FlagAware).Select(pair => pair.Human)));

        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnSaveActor.Subscribe(Extension<T, U>.SaveActorChara),
            OnSaveChara.Subscribe(Extension<T, U>.SaveCustomChara),
            OnSaveCoord.Subscribe(Extension<T, U>.SaveCustomCoord),
            Hooks.OnInitializeActors.Subscribe(Extension<T, U>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T, U>.ClearCustom),
            Extension<T, U>.OnLoadCustomChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T, U>.OnLoadActorChara.Subscribe(tuple => Extension<T, U>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T, U>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T, U>.ActorToCustom),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value),
            OnActorHumanize.Subscribe(tuple => Extension<T,U>.Indices.NowCoordinate[tuple.Index] = Extension<T, U>.Indices[tuple.Index, tuple.Human.data.Status.coordinateType]),
            OnChangeActorCoord.Subscribe(tuple => Extension<T, U>.Indices.NowCoordinate[tuple.Index] =  Extension<T, U>.Indices[tuple.Index, tuple.CoordinateType])
        ];

        public static IDisposable[] Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() => [
            OnSaveActor.Subscribe(Extension<T>.SaveActorChara),
            OnSaveChara.Subscribe(Extension<T>.SaveCustomChara),
            Hooks.OnInitializeActors.Subscribe(Extension<T>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T>.ClearCustom),
            Extension<T>.OnLoadCustomChara.Subscribe(tuple => Extension<T>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T>.OnLoadActorChara.Subscribe(tuple => Extension<T>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T>.ActorToCustom)
        ];

        public static IDisposable[] RegisterConversion<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, CharacterConversion<T>, new()
            where U : CoordinateExtension<U>, CoordinateConversion<U>, new() => [
            OnConvertChara.Subscribe(Conversion<T, U>.ConvertChara),
            OnConvertCoord.Subscribe(Conversion<T, U>.ConvertCoord),
        ];

        public static IDisposable[] RegisterConversion<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, CharacterConversion<T>, new() => [
            OnConvertChara.Subscribe(Conversion<T>.ConvertChara),
        ];

        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
    }
}