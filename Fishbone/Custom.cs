using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using Cysharp.Threading.Tasks;
using Manager;
using Character;
using CharacterCreation;
#if Aicomi
using ILLGAMES.Rigging;
using ILLGAMES.Extensions;
using AC.User;
using AC.Scene.FreeH;
using Actor = AC.User.ActorData;
using ActorIndex = (int, int);
#else
using ILLGames.Rigging;
using ILLGames.Extensions;
using Actor = SaveData.Actor;
using ActorIndex = int;
#endif
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using CoastalSmell;

namespace Fishbone
{
#if Aicomi
    public static partial class Extension
    {
        internal static readonly ActorIndex NotFound = (-1, -1);
        internal static HumanData ToHumanData(this Actor actor) => actor?._humanData ?? null;
        internal static Actor ToActor(this ActorIndex value) =>
            value switch
            {
                var (group, index) when group is -1 => Game.Instance.SaveData.UniqueNPCList[index],
                var (group, index) when group is -2 => FreeHScene.Instance._attackList[index].ActorData,
                var (group, index) when group is -3 => FreeHScene.Instance._receiveList[index].ActorData,
                var (group, index) => Game.Instance.SaveData[group, index],
            };

        internal static ActorIndex ToIndex(this Actor actor) =>
            (actor, actor.Guid, FreeHScene.Instance) switch
            {
                (_, _, not null) => ToFreeHSceneIndex(actor),
                (UniqueNPCData, _, _) or
                (_, (-1, -1), null) => ToUniqueNPCIndex(actor.With(actor.SolveCharaFileName)),
                (_, _, _) => (actor.Guid.Group, actor.Guid.Index)
            };

        static ActorIndex ToUniqueNPCIndex(Actor actor) =>
            Path.GetFileName(actor.CharaFileName) switch
            {
                "AC_F_-1" => (-1, 0),
                "AC_F_-2" => (-1, 1),
                "AC_F_-3" => (-1, 2),
                _ => (-1, -1)
            };

        static ActorIndex ToFreeHSceneIndex(Actor actor) =>
            IndexInList(actor, -2, FreeHScene.Instance._attackList)
                .Concat(IndexInList(actor, -3, FreeHScene.Instance._receiveList))
                .FirstOrDefault((-1, -1));

        static IEnumerable<ActorIndex> IndexInList(Actor actor, int group,
            Il2CppSystem.Collections.Generic.List<FreeHScene.CharaInfo> infos) =>
            Enumerable.Range(0, infos.Count)
                .Where(index => actor == infos[index].ActorData)
                .Select(index => (group, index));

        internal static IEnumerable<Actor> CurrentActors() =>
            FreeHScene.Instance is null ? Game.Instance?.SaveData?.AllActors() ?? []
                : FreeHScene.Instance._attackList.ToArray()
                    .Concat(FreeHScene.Instance._receiveList.ToArray())
                    .Select(info => info.ActorData)
                    .Where(actor => actor.HumanData != null); 

        internal static IEnumerable<Actor> AllActors(this SaveData input) =>
            [.. input.Actors.ToArray().SelectMany(group => group.ToArray() ?? []), ..input.UniqueNPCList.Values];
    }
#else
    public static partial class Extension
    {
        internal static readonly ActorIndex NotFound = -1;
        internal static HumanData ToHumanData(this Actor actor) => actor.charFile;
        internal static Actor ToActor(this ActorIndex index) => Game.Charas[index];
        internal static ActorIndex ToIndex(this Actor actor) => actor.charasGameParam.Index;
        internal static IEnumerable<Actor> CurrentActors() => Game.Charas.Yield().Select(entry => entry.Item2);
    }
#endif

    #region Storages
    internal partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static T Current = new();
        internal static T Chara() => Current;
        internal static U Coord() => Current.Get(Extension.CustomCoordinateType);
        internal static void Chara(T value) => Current = value;
        internal static void Coord(U value) => Current = Current.Merge(Extension.CustomCoordinateType, value);
    }
    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static T Current = new();
        internal static T Chara() => Current;
        internal static void Chara(T value) => Current = value;
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly Dictionary<ActorIndex, T> Charas = new();
        static readonly Dictionary<ActorIndex, U> Coords = new();
        internal static T Chara(ActorIndex index) =>
            Charas.GetValueOrDefault(index, new());
        internal static U Coord(ActorIndex index) =>
            Coords.GetValueOrDefault(index, new());
        internal static void Chara(ActorIndex index, T mods) => Charas[index] = mods;
        internal static void Coord(ActorIndex index, U mods) => Coords[index] = mods;
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<ActorIndex, T> Charas = new();
        internal static T Chara(ActorIndex index) => Charas.GetValueOrDefault(index, new());
        internal static void Chara(ActorIndex index, T mods) => Charas[index] = mods;
    }
    #endregion

    #region Save
    static partial class Hooks
    {
        static Subject<string> SaveCustomChara = new();
        static Subject<string> SaveCustomCoord = new();
        static Subject<Actor> SaveActor = new();
        internal static IObservable<string> OnSaveCustomChara => SaveCustomChara.AsObservable();
        internal static IObservable<string> OnSaveCustomCoord => SaveCustomCoord.AsObservable();
        internal static IObservable<Actor> OnSaveActor => SaveActor.AsObservable();
    }
    public static partial class Extension
    {
        static void Save(Actor actor, IObserver<(ActorIndex, ZipArchive)> observer) =>
            Implant(actor.ToHumanData(), ToBinary(Observer.Create<ZipArchive>(archive => observer.OnNext((actor.ToIndex(), archive)))));
        static void Save(string path, IObserver<ZipArchive> observer) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(observer)));
        static Subject<ZipArchive> ConvertChara = new();
        static Subject<ZipArchive> ConvertCoord = new();
        internal static IObservable<ZipArchive> OnConvertChara => ConvertChara.AsObservable();
        internal static IObservable<ZipArchive> OnConvertCoord => ConvertCoord.AsObservable();
    }
    #endregion

    #region Special for SardineTail
    public static partial class Extension
    {
        static void HumanCustomReload(HumanCustom custom) =>
            (custom?.Human is not null)
                .Maybe(F.Apply(HumanCustomReload, custom, custom.Human));
        static void HumanCustomReload(HumanCustom custom, Human human) =>
            (custom?._motionIK is not null).With(human.Load)
                .Maybe(F.Apply(HumanCustomReload, custom, human, custom._motionIK));
        static void HumanCustomReload(HumanCustom custom, Human human, MotionIK motionIK)
        {
            custom._motionIK = new MotionIK(human, custom._motionIK._data);
            custom.LoadPlayAnimation(custom.NowPose, new() { value = 0.0f });
        }
    }
    #endregion

    #region Custom and Actor switch
    static partial class Hooks
    {
        internal static IObservable<Actor> OnActorResolve => ActorResolve.AsObservable();
        static Subject<Actor> ActorResolve = new();
        static Subject<Unit> EnterConversion = new();
        static Subject<Unit> LeaveConversion = new();
        internal static IObservable<Unit> OnEnterConversion => EnterConversion.AsObservable();
        internal static IObservable<Unit> OnLeaveConversion => LeaveConversion.AsObservable();
    }

    class CharaCopyTrack : IDisposable
    {
        protected CompositeDisposable Subscription;
        protected IObservable<HumanData> OnDataUpdate;
        protected IObservable<CharaLimit> OnLimitUpdate;
        protected IObservable<Human> OnResolveHuman;
        protected IObservable<Actor> OnResolveActor;
        HumanData Data;
        CharaCopyTrack() =>
            (OnDataUpdate, OnLimitUpdate, OnResolveHuman, OnResolveActor) = (
                Hooks.OnHumanDataCopy.Where(Match).Select(tuple => tuple.Item2),
                Hooks.OnHumanDataLimit.Where(Match).Select(tuple => tuple.Item2),
                Hooks.OnHumanResolve.Where(Match).FirstAsync(),
                Hooks.OnActorResolve.Where(Match).FirstAsync());
        protected CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                OnResolveHuman.Subscribe(F.Ignoring<Human>(F.DoNothing), Dispose),
                OnResolveActor.Subscribe(F.Ignoring<Actor>(F.DoNothing), Dispose),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData, T) tuple) => Data == tuple.Item1;
        bool Match(Human human) => Data == human.data; 
        bool Match(Actor actor) => Data == actor.ToHumanData();
        void Resolve(HumanData value) => Data = value;
        public void Dispose() => Subscription.Dispose();
    }
    class CustomTrack : CharaCopyTrack
    {
        internal IObservable<CharaLimit> OnResolve { init; get; }
        CharaLimit Limit;
        CustomTrack(HumanData data, CharaLimit limit) : base(data) =>
            (Limit, OnResolve) = (limit, OnResolveHuman.Select(_ => Limit));
        internal CustomTrack(HumanData data) : this(data, CharaLimit.None) =>
            Subscription.Append(OnLimitUpdate.Subscribe(Resolve))
                .Append(CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()));
        void Resolve(CharaLimit value) => Limit = value;
    }
    public static partial class Extension
    {
        internal static IObservable<(CustomTrack, HumanData, ZipArchive)> OnTrackCustom =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Select(tuple => (new CustomTrack(tuple.Item1), tuple.Item1, tuple.Item2));
    }
    public static partial class Extension<T, U>
    {
        static IObservable<(CustomTrack, HumanData, T)> OnTrackCustom =>
            Extension.OnTrackCustom.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(CharaLimit, T)> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Item1.OnResolve.Select(limit => (limit, tuple.Item3)));
    }
    public static partial class Extension<T>
    {
        static IObservable<(CustomTrack, HumanData, T)> OnTrackCustom =>
            Extension.OnTrackCustom.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(CharaLimit, T)> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Item1.OnResolve.Select(limit => (limit, tuple.Item3)));
    }
    class ActorTrack : CharaCopyTrack
    {
        internal IObservable<ActorIndex> OnResolve { init; get; }
        internal ActorTrack(HumanData data) : base(data) => OnResolve = OnResolveActor.Select(actor => actor.ToIndex());
    }
    public static partial class Extension
    {
        internal static IObservable<(ActorTrack, HumanData, ZipArchive)> OnTrackActor =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagIgnore)
                .Select(tuple => (new ActorTrack(tuple.Item1), tuple.Item1, tuple.Item2));
        internal static IObservable<(ActorIndex, HumanData)> OnLoadActorCharaInternal =>
            OnTrackActor.SelectMany(tuple => tuple.Item1.OnResolve.Select(actor => (actor, tuple.Item2)));
    }
    public static partial class Extension<T, U>
    {
        internal static IObservable<(ActorTrack, HumanData, T)> OnTrackActor =>
            Extension.OnTrackActor.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(ActorIndex, T)> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Item1.OnResolve.Select(actor => (actor, tuple.Item3)));
    }
    public static partial class Extension<T>
    {
        internal static IObservable<(ActorTrack, HumanData, T)> OnTrackActor =>
            Extension.OnTrackActor.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(ActorIndex, T)> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Item1.OnResolve.Select(actor => (actor, tuple.Item3)));
    }
    class HumanTrack : CharaCopyTrack
    {
        internal IObservable<(Human, ActorIndex)> OnResolve { init; get; }
        ActorIndex Actor;
        internal HumanTrack(ActorIndex actor, HumanData data) : base(data) =>
            (Actor, OnResolve) = (actor, OnResolveHuman.Select(human => (human, Actor)));
    }
    public static partial class Extension
    {
        internal static bool ToActorIndex(Human human, out ActorIndex index) =>
            HumanToActors.TryGetValue(human, out index);
        static Dictionary<Human, ActorIndex> HumanToActors = new();
        static IObservable<(Human, ActorIndex)> OnActorHumanizeInternal =>
            Hooks.OnHumanDataCopy
                .Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.Ignore)
                .SelectMany(tuple => HumanToActors
                    .Where(entry => tuple.Item1 == entry.Key.data)
                    .Select(entry => (entry.Value, tuple.Item2)).ToObservable())
                .Merge(OnLoadActorCharaInternal)
            .SelectMany(tuple => new HumanTrack(tuple.Item1, tuple.Item2).OnResolve);
    }
    #endregion

    #region Load Custom
    internal static partial class HumanExtension<T, U>
    {
        internal static void LoadChara(CharaLimit limit, T value) =>
            Current = Current.Merge(limit, value);
    }
    internal static partial class HumanExtension<T>
    {
        internal static void LoadChara(CharaLimit limit, T value) =>
            Current = Current.Merge(limit, value);
    }
    #endregion

    #region Load Actor

    internal static partial class ActorExtension<T, U>
    {
        internal static void LoadActor(ActorIndex index, T value) =>
            Charas[index] = value;
        internal static void LoadActorChara(Human human, ActorIndex index) =>
            Coords[index] = Charas[index].Get(human.data.Status.coordinateType);
        internal static void LoadActorCoord(ActorIndex index, U value, CoordLimit limit) =>
            Coords[index] = Coords[index].Merge(limit, value);
        internal static void LoadActorCoord(Human human, U value, CoordLimit limit) =>
            Extension.ToActorIndex(human, out var index)
                .Maybe(F.Apply(LoadActorCoord, index, value, limit));
    }
    internal static partial class ActorExtension<T>
    {
        internal static void LoadActor(ActorIndex index, T value) =>
            Charas[index] = value;
    }
    #endregion

    #region Complex Definition For Actor To Human Copy
    #endregion

    #region Copy Between Actor And Custom
    public static partial class Extension
    {
        internal static bool ToActorIndex(HumanData data, out ActorIndex index) =>
            (index = CurrentActors()
                .Where(actor => data == actor.ToHumanData())
                .Select(actor => actor.ToIndex())
                .FirstOrDefault(NotFound)) != NotFound;
        static IObservable<(HumanData, HumanData)> OnCopyCustomChara =>
            Hooks.OnHumanDataCopy.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware);
        internal static IObservable<HumanData> OnCustomInitialize =>
            OnCopyCustomChara.Where(tuple => tuple.Item1 == HumanCustom.Instance?.DefaultData).Select(tuple => tuple.Item2);
        internal static IObservable<ActorIndex> OnCopyActorToCustom =>
            OnCopyCustomChara.Select(tuple => tuple.Item1)
                .Where(data => data == HumanCustom.Instance?.Received?.HumanData)
                .SelectMany(data => ToActorIndex(data, out var index)
                    ? Observable.Return(index) : Observable.Empty<ActorIndex>());
        internal static IObservable<ActorIndex> OnCopyCustomToActor =>
            OnCopyCustomChara.Select(tuple => tuple.Item2)
                .Where(data => data == HumanCustom.Instance?.EditHumanData)
                .SelectMany(data => ToActorIndex(HumanCustom.Instance?.Received?.HumanData, out var index)
                    ? Observable.Return(index) : Observable.Empty<ActorIndex>());
    }
    internal static partial class HumanExtension<T, U>
    {
        internal static void Initialize(HumanData _) => Current = new();
        internal static void Copy(ActorIndex index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T, U>.Chara(index));
    }
    internal static partial class HumanExtension<T>
    {
        internal static void Initialize(HumanData _) => Current = new();
        internal static void Copy(ActorIndex index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T>.Chara(index));
    }
    internal static partial class ActorExtension<T, U>
    {
        internal static void Copy(ActorIndex index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T, U>.Chara());
    }
    internal static partial class ActorExtension<T>
    {
        internal static void Copy(ActorIndex index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T>.Chara());
    }
    #endregion

    #region Coordinate Change
    static partial class Hooks
    {
        static Subject<(Human, int)> ChangeCustomCoordinate = new();
        static Subject<(Human, int)> ChangeActorCoordinate = new();
        internal static IObservable<(Human, int)> OnChangeCustomCoord => ChangeCustomCoordinate.AsObservable();
        internal static IObservable<(Human, int)> OnChangeActorCoord => ChangeActorCoordinate.AsObservable();
    }
    public static partial class Extension
    {
        static void InitializeCustom(HumanData data) =>
           (CustomCoordinateType = 0).With(HumanToActors.Clear);
        static IObservable<int> OChangeCustomCoord =>
            Hooks.OnChangeCustomCoord.Select(tuple => tuple.Item2);
        static IObservable<(ActorIndex, int)> OnChangeActorCoord =>
            Hooks.OnChangeActorCoord.SelectMany(tuple => ToActorIndex(tuple.Item1, out var index)
                ? Observable.Return((index, tuple.Item2)) : Observable.Empty<(ActorIndex, int)>());
    }
    internal static partial class ActorExtension<T, U>
    {
        internal static void CoordinateChange(ActorIndex actor, int coordinateType) =>
            Coords[actor] = Charas[actor].Get(coordinateType);
    }
    public static partial class Extension
    {
        internal static int CustomCoordinateType { get; set; } = 0;
        internal static void Initialize()
        {
            OnCustomInitialize.Subscribe(InitializeCustom);
            OnCustomInitialize.Subscribe(CharaLoadTrack.OnDefault);
            OChangeCustomCoord.Subscribe(coordinateType => CustomCoordinateType = coordinateType);

            OnTrackActor.Subscribe(_ => Plugin.Instance.Log.LogInfo("actor tracking start"));
            OnActorHumanizeInternal.Select(tuple => tuple.Item1)
               .Where(human => !HumanToActors.ContainsKey(human))
               .Subscribe(human => human.component
                   .OnDestroyAsObservable()
                   .Subscribe(_ => HumanToActors.Remove(human)));
            OnActorHumanizeInternal.Subscribe(tuple => HumanToActors[tuple.Item1] = tuple.Item2);
#if DEBUG
            OnCopyActorToCustom.Subscribe(_ => Plugin.Instance.Log.LogDebug("copy actor to custom"));
            OnCopyCustomToActor.Subscribe(_ => Plugin.Instance.Log.LogDebug("copy custom to actor"));
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara"));
            OnPrepareSaveCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save coord"));
            OnSaveCustomChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("save chara"));
            OnSaveCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("save coord"));
            OnSaveActor.Subscribe(_ => Plugin.Instance.Log.LogDebug("save actor"));
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara:{CharaLoadTrack.Mode.ToString()}"));
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord"));
            OnCustomInitialize.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom initialized"));
            OnLoadCustomChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom chara load"));
            OnLoadCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom coord load"));
            OnLoadActorChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor chara load"));
            OnLoadActorCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor coord load"));
            OnActorHumanize.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor humanized"));
            OChangeCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom coordinate change"));
#endif
        }
    }

    #endregion
}