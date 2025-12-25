using System;
using System.Reactive.Linq;
using System.IO.Compression;
using Character;
using BepInEx.Unity.IL2CPP;

namespace Fishbone
{

    public static partial class Extension<T, U>
    {
        static readonly HumansStorage<T, U> Storage = new HumansStorage<T, U>();

        public static Storage<T, U, Human> Values => Storage;

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));

        public static IObservable<(HumanDataCoordinate Data, U Value)> OnPreprocessCoord =>
            OnCoordLimitTrack.Select(tuple => (tuple.Data, tuple.Value));
    }
    public static partial class Extension<T>
    {
        static readonly HumansStorage<T> Storage = new HumansStorage<T>();

        public static readonly Storage<T, Human> Values = Storage; 

        public static IObservable<(HumanData Data, T Value)> OnPreprocessChara =>
            OnTrackChara.Select(tuple => (tuple.Data, tuple.Value));
    }

    public static partial class Extension
    {
        public static IObservable<Human> OnPrepareSaveChara =>
            OnSaveChara.Select(tuple => tuple.Human);

        public static IObservable<(ZipArchive Value, Human Human)> OnSaveChara =
            Observable.Create<(ZipArchive, Human)>(observer => Hooks.OnSaveHuman.Subscribe(actor => Save(actor, observer)));

        public static IObservable<Human> OnLoadChara =
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve);

        public static IObservable<Human> OnLoadCoord =>
            OnTrackCoord.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => pair.Human));

        public static IObservable<(Human Human, int Index)> OnChangeCoord = Hooks.OnChangeCoordinate;

        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnSaveChara.Subscribe(Extension<T, U>.SaveChara),
            Extension<T, U>.OnLoadChara.Subscribe(tuple => Extension<T, U>.Values[tuple.Human] = tuple.Value),
            Extension<T, U>.OnLoadChara.Subscribe(tuple => Extension<T, U>.Prepare(tuple.Human)),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Values.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value)
        ];

        public static IDisposable[] Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() => [
            OnSaveChara.Subscribe(Extension<T>.SaveChara),
            Extension<T>.OnLoadChara.Subscribe(tuple => Extension<T>.Values[tuple.Human] = tuple.Value),
            Extension<T>.OnLoadChara.Subscribe(tuple => Extension<T>.Prepare(tuple.Human))
        ];
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}