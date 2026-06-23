using System;
using System.IO.Compression;
using System.Reactive.Linq;
using Character;
using CharacterCreation;
#if Aicomi
using Actor = AC.User.ActorData;
using ActorIndex = (int, int);
#else
using Actor = SaveData.Actor;
using ActorIndex = int;
#endif

namespace Fishbone
{
    /// <summary>
    /// Platform-specific adapters that expose storage and preprocessing observables for registered extensions.
    /// </summary>
    public static partial class Extension<T, U>
    {
        /// <summary>Extension storage indexed by human</summary>
        public static Storage<T, U, Human> Humans =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Extension storage indexed by actor</summary>
        public static Storage<T, U, Actor> Actors =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Extension storage indexed by actor index</summary>
        public static Storage<T, U, ActorIndex> Indices =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Raised when a character data has been preprocessed and an extension data is bound to it.</summary>
        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Data, tuple.Value))
                .Merge(OnTrackActor.Select(tuple => (tuple.Data, tuple.Value)));

        /// <summary>Raised when a coordinate data has been preprocessed and an extension data is bound to it.</summary>
        public static IObservable<(HumanDataCoordinate Data, U Value)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Data, tuple.Value));
    }

    /// <summary>
    /// Platform-specific adapters for simple character extensions.
    /// </summary>
    public static partial class Extension<T>
    {
        /// <summary>Extension storage indexed by human</summary>
        public static Storage<T, Human> Humans =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Extension storage indexed by actor</summary>
        public static Storage<T, Actor> Actors =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Extension storage indexed by actor index</summary>
        public static Storage<T, ActorIndex> Indices =>
            CharaLoadTrack.Mode == CharaLoadTrack.FlagAware ? CustomValues : ActorsValues;

        /// <summary>Raised when a character data has been preprocessed and an extension data is bound to it.</summary>
        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackCustom.Select(tuple => (tuple.Data, tuple.Value))
                .Merge(OnTrackActor.Select(tuple => (tuple.Data, tuple.Value)));
    }
    /// <summary>Inter game conversion interface for character extension. </summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CharacterConversion<T> where T : CharacterExtension<T>, CharacterConversion<T>, new ()
    {
        /// <summary>Convert <b>this</b> extension originated in another game to applicatable form to provided <see cref="HumanData"/>.</summary>
        T Convert(HumanData data);
    }
    /// <summary>Inter game conversion interface for coordinate extension. </summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CoordinateConversion<T> where T : CoordinateExtension<T>, CoordinateConversion<T>, new ()
    {
        /// <summary>Convert <b>this</b> extension originated in another game to applicatable form to provided <see cref="HumanDataCoordinate"/>.</summary>
        T Convert(HumanDataCoordinate data);
    }

    /// <summary>
    /// Platform-level extension event entrypoints (save/load/convert hooks) used by plugin registration.
    /// </summary>
    public static partial class Extension
    {
        /// <summary>Raised when a character data is about to be converted.</summary>
        public static IObservable<(ZipArchive Output, ZipArchive Input, HumanData Data)> OnConvertChara =>
            ConvertChara.AsObservable().Select(pair => (pair.Output, pair.Value.Input, pair.Value.Data));
        
        /// <summary>Raised when a coordinate data is about to be converted.</summary>
        public static IObservable<(ZipArchive Output, ZipArchive Input, HumanDataCoordinate Data)> OnConvertCoord =>
            ConvertCoord.AsObservable().Select(pair => (pair.Output, pair.Value.Input, pair.Value.Data));

        /// <summary>Raised when a character data is about to be saved.</summary>
        public static IObservable<Human> OnPrepareSaveChara =>
            PrepareSaveChara.AsObservable().Merge(OnCopyCustomToActor.Select(_ => HumanCustom.Instance.Human));

        /// <summary>Raised when a coordinate data is about to be saved.</summary>
        public static IObservable<Human> OnPrepareSaveCoord =>
            PrepareSaveCoord.AsObservable().Merge(Hooks.OnChangeCustomCoord.Select(_ => HumanCustom.Instance.Human));

        /// <summary>Raised when a character is saved.</summary>
        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveChara => SaveChara.AsObservable();

        /// <summary>Raised when a coordinate is saved.</summary>
        public static IObservable<(ZipArchive Archive, Human Human)> OnSaveCoord => SaveCoord.AsObservable();

        /// <summary>Raised when a actor is saved.</summary>
        public static IObservable<(ZipArchive Archive, ActorIndex Index)> OnSaveActor => SaveActor.AsObservable();

        /// <summary>Raised when a character is loaded in character creation scene.</summary>
        public static IObservable<Human> OnLoadCustomChara =>
            OnHumanCustomReload
                .Merge(OnTrackCustom.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human)))
                .Merge(OnActorHumanizeInternal.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware).Select(pair => pair.Human));

        /// <summary>Raised when a actor is loaded in simulation scene.</summary>
        public static IObservable<ActorIndex> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve);

        /// <summary>Raised when a actor is bound to actual human in simulation scene.</summary>
        public static IObservable<(Human Human, ActorIndex Index)> OnActorHumanize =>
            OnActorHumanizeInternal.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.Ignore);

        /// <summary>Raised when a human is loaded in scenes.</summary>
        public static IObservable<Human> OnLoadChara =>
            OnLoadCustomChara.Merge(OnActorHumanize.Select(tuple => tuple.Human));

        /// <summary>Raised when a coordinate is loaded in scenes.</summary>
        public static IObservable<Human> OnLoadCoord =>
            OnLoadCustomCoord.Merge(OnLoadActorCoord);

        /// <summary>Raised when a coordinate is loaded in character creation scene.</summary>
        public static IObservable<Human> OnLoadCustomCoord =>
            Hooks.OnChangeCustomCoord.Select(pair => pair.Human)
                .Merge(OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve
                    .Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware).Select(pair => pair.Human)));

        /// <summary>Raised when a coordinate is loaded in simulation scene.</summary>
        public static IObservable<Human> OnLoadActorCoord =>
            Hooks.OnChangeActorCoord.Select(pair => pair.Human)
                .Merge(OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve
                    .Where(_ => CharaLoadTrack.Mode != CharaLoadTrack.FlagAware).Select(pair => pair.Human)));

        /// <summary>Register conversion for complex character and coordinate extension</summary>
        public static IDisposable[] RegisterConversion<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, CharacterConversion<T>, new()
            where U : CoordinateExtension<U>, CoordinateConversion<U>, new() => [
            OnConvertChara.Subscribe(Conversion<T, U>.ConvertChara),
            OnConvertCoord.Subscribe(Conversion<T, U>.ConvertCoord),
        ];

        /// <summary>Register conversion for simple character extension</summary>
        public static IDisposable[] RegisterConversion<T>()
            where T : CharacterExtension<T>, CoordinateExtension<T>, CharacterConversion<T>, new() => [
            OnConvertChara.Subscribe(Conversion<T>.ConvertChara),
        ];

        /// <summary>force character reloading in character creation scene.</summary>
        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }
}