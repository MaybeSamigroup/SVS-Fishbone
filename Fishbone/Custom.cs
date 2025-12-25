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
        internal static IEnumerable<Actor> CurrentActors() => Game.Charas.Yield().Select(entry => entry.Value);
    }
#endif

    #region Storages
  
    class CustomStorage<T, U> :
        Storage<T, U, Human>,
        Storage<T, U, Actor>,
        Storage<T, U, ActorIndex>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        T Custom { get; set; } = new();
        U NowCoordinate
        {
            get => Custom.Get(Extension.CustomCoordinateType);
            set => Custom = Custom.Merge(HumanCustom.Instance.Human.data.Status.coordinateType, value);
        }
        public T Get(Human _) => Custom;
        public T Get(Actor _) => Custom;
        public T Get(ActorIndex _) => Custom;
        public void Set(Human _, T value) => Custom = value;
        public void Set(Actor _, T value) => Custom = value; 
        public void Set(ActorIndex _, T value) => Custom = value;
        public U GetNowCoordinate(Human _) => NowCoordinate;
        public U GetNowCoordinate(Actor _) => NowCoordinate;
        public U GetNowCoordinate(ActorIndex _) => NowCoordinate;
        public void SetNowCoordinate(Human _, U value) => NowCoordinate = value;
        public void SetNowCoordinate(Actor _, U value) => NowCoordinate = value;
        public void SetNowCoordinate(ActorIndex _, U value) => NowCoordinate = value;
    }
    class ActorsStorage<T,U> :
        Storage<T, U, Human>,
        Storage<T, U, Actor>,
        Storage<T, U, ActorIndex>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        Dictionary<ActorIndex, (T Chara, U Coord)> Actors = new();
        public T Get(Human human) => Get(human.ToIndex());
        public T Get(Actor actor) => Get(actor.ToIndex());
        public T Get(ActorIndex index) => Actors[index].Chara;
        public void Set(Human human, T value) => Set(human.ToIndex(), value);
        public void Set(Actor actor, T value) => Set(actor.ToIndex(), value);
        public void Set(ActorIndex index, T value) => Actors[index] = (value, Actors[index].Coord);
        public U GetNowCoordinate(Human human) => GetNowCoordinate(human.ToIndex());
        public U GetNowCoordinate(Actor actor) => GetNowCoordinate(actor.ToIndex());
        public U GetNowCoordinate(ActorIndex index) => Actors[index].Coord; 
        public void SetNowCoordinate(Human human, U value) => SetNowCoordinate(human.ToIndex(), value);
        public void SetNowCoordinate(Actor actor, U value) => SetNowCoordinate(actor.ToIndex(), value);
        public void SetNowCoordinate(ActorIndex index, U value) => Actors[index] = (Actors[index].Chara, value);
        internal void Swap(ActorIndex src, ActorIndex dst) =>
            Actors.Remove(src, out var value).Maybe(() => Actors[dst] = value);
    }
    public static partial class Extension<T, U>
    {
        static CustomStorage<T, U> CustomValues = new();
        static ActorsStorage<T, U> ActorsValues = new();
        static void Copy(ActorIndex index, Storage<T, U, ActorIndex> src, Storage<T, U, ActorIndex> dst) => dst[index, CharaLimit.All] = src[index];
        internal static void ClearCustom(HumanData _) => CustomValues = new();
        internal static void ClearActors(Unit _) => ActorsValues = new();
        internal static void ActorToCustom(ActorIndex index) => Copy(index, ActorsValues, CustomValues);
        internal static void CustomToActor(ActorIndex index) => Copy(index, CustomValues, ActorsValues);
    }

    class CustomStorage<T> :
        Storage<T, Human>,
        Storage<T, Actor>,
        Storage<T, ActorIndex>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        T Custom { get; set; } = new();
        public T Get(Human _) => Custom;
        public T Get(Actor _) => Custom;
        public T Get(ActorIndex _) => Custom;
        public void Set(Human _, T value) => Custom = value;
        public void Set(Actor _, T value) => Custom = value; 
        public void Set(ActorIndex _, T value) => Custom = value;
    }

    class ActorsStorage<T> :
        Storage<T, Human>,
        Storage<T, Actor>,
        Storage<T, ActorIndex>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        Dictionary<ActorIndex, T> Actors = new();
        public T Get(Human human) => Get(human.ToIndex());
        public T Get(Actor actor) => Get(actor.ToIndex());
        public T Get(ActorIndex index) => Actors[index];
        public void Set(Human human, T value) => Set(human.ToIndex(), value);
        public void Set(Actor actor, T value) => Set(actor.ToIndex(), value);
        public void Set(ActorIndex index, T value) => Actors[index] = value;
        internal void Swap(ActorIndex src, ActorIndex dst) =>
            Actors.Remove(src, out var value).Maybe(() => Actors[dst] = value);
    }
    public static partial class Extension<T>
    {
        static CustomStorage<T> CustomValues = new();
        static ActorsStorage<T> ActorsValues = new();
        static void Copy(ActorIndex index, Storage<T, ActorIndex> src, Storage<T, ActorIndex> dst) => dst[index, CharaLimit.All] = src[index];
        internal static void ClearCustom(HumanData _) => CustomValues = new();
        internal static void ClearActors(Unit _) => ActorsValues = new();
        internal static void ActorToCustom(ActorIndex index) => Copy(index, ActorsValues, CustomValues);
        internal static void CustomToActor(ActorIndex index) => Copy(index, CustomValues, ActorsValues);
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
        static void Save(Actor actor, IObserver<(ZipArchive, ActorIndex)> observer) =>
            Implant(actor.ToHumanData(), ToBinary(Observer.Create<ZipArchive>(archive => observer.OnNext((archive, actor.ToIndex())))));
        static void Save(string path, IObserver<ZipArchive> observer) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(observer)));
        static Subject<ZipArchive> ConvertChara = new();
        static Subject<ZipArchive> ConvertCoord = new();
        internal static IObservable<ZipArchive> OnConvertChara => ConvertChara.AsObservable();
        internal static IObservable<ZipArchive> OnConvertCoord => ConvertCoord.AsObservable();
    }

    public static partial class Extension<T, U>
    {
        internal static void SaveCustomChara(ZipArchive archive) =>
            SaveChara(archive, Humans[HumanCustom.Instance.Human]);
        internal static void SaveCustomCoord(ZipArchive archive) =>
            SaveCoord(archive, Humans.NowCoordinate[HumanCustom.Instance.Human]);
        internal static void SaveActorChara((ZipArchive Value, ActorIndex Index) tuple) =>
            SaveChara(tuple.Value, Indices[tuple.Index]);
    }
    public static partial class Extension<T>
    {
        internal static void SaveCustomChara(ZipArchive archive) =>
            SaveChara(archive, Humans[HumanCustom.Instance.Human]);
        internal static void SaveActorChara((ZipArchive Value, ActorIndex Index) tuple) =>
            SaveChara(tuple.Value, Indices[tuple.Index]);
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
                Hooks.OnHumanDataCopy.Where(Match).Select(tuple => tuple.Dst),
                Hooks.OnHumanDataLimit.Where(Match).Select(tuple => tuple.Value),
                Hooks.OnHumanResolve.Where(Match).FirstAsync(),
                Hooks.OnActorResolve.Where(Match).FirstAsync());
        protected CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                OnResolveHuman.Subscribe(F.Ignoring<Human>(F.DoNothing), Dispose),
                OnResolveActor.Subscribe(F.Ignoring<Actor>(F.DoNothing), Dispose),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData Data, T Value) tuple) => Data == tuple.Data;
        bool Match(Human human) => Data == human.data; 
        bool Match(Actor actor) => Data == actor.ToHumanData();
        void Resolve(HumanData value) => Data = value;
        public void Dispose() => Subscription.Dispose();
    }
    class CustomTrack : CharaCopyTrack
    {
        internal IObservable<(Human Human, CharaLimit Limit)> OnResolve { init; get; }
        CharaLimit Limit;
        CustomTrack(HumanData data, CharaLimit limit) : base(data) =>
            (Limit, OnResolve) = (limit, OnResolveHuman.Select(human => (human, Limit)));
        internal CustomTrack(HumanData data) : this(data, CharaLimit.None) =>
            Subscription.Append(OnLimitUpdate.Subscribe(Resolve))
                .Append(CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()));
        void Resolve(CharaLimit value) => Limit = value;
    }
    public static partial class Extension
    {
        internal static IObservable<(CustomTrack Track, HumanData Data, ZipArchive Value)> OnTrackCustom =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Select(tuple => (new CustomTrack(tuple.Data), tuple.Data, tuple.Value));
    }
    public static partial class Extension<T, U>
    {
        static IObservable<(CustomTrack Track, HumanData Data, T Value)> OnTrackCustom =>
            Extension.OnTrackCustom.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, CharaLimit Limit, T Value)> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => (pair.Human, pair.Limit, tuple.Value)));
    }
    public static partial class Extension<T>
    {
        static IObservable<(CustomTrack Track, HumanData Data, T Value)> OnTrackCustom =>
            Extension.OnTrackCustom.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, CharaLimit Limit, T Value)> OnLoadCustomChara =>
            OnTrackCustom.SelectMany(tuple => tuple.Track.OnResolve.Select(pair => (pair.Human, pair.Limit, tuple.Value)));
    }
    class ActorTrack : CharaCopyTrack
    {
        internal IObservable<ActorIndex> OnResolve { init; get; }
        internal ActorTrack(HumanData data) : base(data) => OnResolve = OnResolveActor.Select(actor => actor.ToIndex());
    }
    public static partial class Extension
    {
        internal static IObservable<(ActorTrack Track, HumanData Data, ZipArchive Value)> OnTrackActor =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagIgnore)
                .Select(tuple => (new ActorTrack(tuple.Data), tuple.Data, tuple.Value));
        internal static IObservable<(ActorIndex Index, HumanData Data)> OnLoadActorCharaInternal =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve.Select(actor => (actor, tuple.Data)));
    }
    public static partial class Extension<T, U>
    {
        internal static IObservable<(ActorTrack Track, HumanData Data, T Value)> OnTrackActor =>
            Extension.OnTrackActor.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(ActorIndex Index, T Value)> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve.Select(actor => (actor, tuple.Value)));
    }
    public static partial class Extension<T>
    {
        internal static IObservable<(ActorTrack Track, HumanData Data, T Value)> OnTrackActor =>
            Extension.OnTrackActor.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(ActorIndex Index, T Value)> OnLoadActorChara =>
            OnTrackActor.SelectMany(tuple => tuple.Track.OnResolve.Select(actor => (actor, tuple.Value)));
    }
    class HumanTrack : CharaCopyTrack
    {
        internal IObservable<(Human Human, ActorIndex Index)> OnResolve { init; get; }
        ActorIndex Index;
        internal HumanTrack(ActorIndex index, HumanData data) : base(data) =>
            (Index, OnResolve) = (index, OnResolveHuman.Select(human => (human, Index)));
    }
    public static partial class Extension
    {
        internal static ActorIndex ToIndex(this Human human) => HumanToActors.GetValueOrDefault(human, NotFound);
        static Dictionary<Human, ActorIndex> HumanToActors = new();
        static IObservable<(ActorIndex Index, HumanData Data)> OnHumanToHumanCopy =>
            Hooks.OnHumanDataCopy
                .Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.Ignore)
                .SelectMany(tuple => HumanToActors
                    .Where(entry => tuple.Src == entry.Key.data)
                    .Select(entry => (entry.Value, tuple.Dst)).ToObservable());
        static IObservable<(Human Human, ActorIndex Index)> OnActorHumanizeInternal =>
            OnHumanToHumanCopy.Merge(OnLoadActorCharaInternal)
                .SelectMany(tuple => new HumanTrack(tuple.Index, tuple.Data).OnResolve);
    }
    #endregion

    #region Copy Between Actor And Custom
    public static partial class Extension
    {
        internal static bool ToActorIndex(HumanData data, out ActorIndex actor) =>
            (actor = CurrentActors()
                .Where(actor => data == actor.ToHumanData())
                .Select(actor => actor.ToIndex())
                .FirstOrDefault(NotFound)) != NotFound;
        static IObservable<(HumanData Src, HumanData Dst)> OnCopyCustomChara =>
            Hooks.OnHumanDataCopy.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware);
        internal static IObservable<HumanData> OnInitializeCustom =>
            OnCopyCustomChara.Where(tuple => tuple.Src == HumanCustom.Instance?.DefaultData).Select(tuple => tuple.Dst);
        internal static IObservable<ActorIndex> OnCopyActorToCustom =>
            OnCopyCustomChara.Select(tuple => tuple.Src)
                .Where(data => data == HumanCustom.Instance?.Received?.HumanData)
                .SelectMany(data => ToActorIndex(data, out var index)
                    ? Observable.Return(index) : Observable.Empty<ActorIndex>());
        internal static IObservable<ActorIndex> OnCopyCustomToActor =>
            OnCopyCustomChara.Select(tuple => tuple.Dst)
                .Where(data => data == HumanCustom.Instance?.EditHumanData)
                .SelectMany(data => ToActorIndex(HumanCustom.Instance?.Received?.HumanData, out var index)
                    ? Observable.Return(index) : Observable.Empty<ActorIndex>());
    }
    #endregion

    #region Coordinate Change
    static partial class Hooks
    {
        static Subject<(Human, int)> ChangeCustomCoordinate = new();
        static Subject<(Human, int)> ChangeActorCoordinate = new();
        internal static IObservable<(Human Human, int CoordinateType)> OnChangeCustomCoord => ChangeCustomCoordinate.AsObservable();
        internal static IObservable<(Human Human, int CoordinateType)> OnChangeActorCoord => ChangeActorCoordinate.AsObservable();
    }
    public static partial class Extension
    {
        static void InitializeCustom(HumanData data) =>
           CustomCoordinateType = 0;
        static void InitializeActors(Unit _) => HumanToActors.Clear();
        static IObservable<int> OnChangeCustomCoord =>
            Hooks.OnChangeCustomCoord.Select(tuple => tuple.CoordinateType);
        static IObservable<(ActorIndex Index, int CoordinateType)> OnChangeActorCoord =>
            Hooks.OnChangeActorCoord.Select(tuple => (tuple.Human.ToIndex(), tuple.CoordinateType)); 
    }

    public static partial class Extension
    {
        internal static int CustomCoordinateType { get; set; } = 0;
        internal static IDisposable[] Initialize() => [
            SingletonInitializerExtension<HumanCustom>.OnStartup.Subscribe(_ => CharaLoadTrack.Mode = CharaLoadTrack.FlagAware),
            SingletonInitializerExtension<HumanCustom>.OnDestroy.Subscribe(_ => CharaLoadTrack.Mode = CharaLoadTrack.Ignore),
            OnInitializeCustom.Subscribe(CharaLoadTrack.OnDefault),
            OnInitializeCustom.Subscribe(InitializeCustom),
            Hooks.OnInitializeActors.Subscribe(InitializeActors),
            OnChangeCustomCoord.Subscribe(coordinateType => CustomCoordinateType = coordinateType),
            OnActorHumanizeInternal.Select(tuple => tuple.Human)
               .Where(human => !HumanToActors.ContainsKey(human))
               .Subscribe(human => human.component
                   .OnDestroyAsObservable()
                   .Subscribe(_ => HumanToActors.Remove(human))),
            OnActorHumanizeInternal.Subscribe(tuple => HumanToActors[tuple.Human] = tuple.Index),
#if DEBUG
            OnCopyActorToCustom.Subscribe(_ => Plugin.Instance.Log.LogDebug("copy actor to custom")),
            OnCopyCustomToActor.Subscribe(_ => Plugin.Instance.Log.LogDebug("copy custom to actor")),
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPrepareSaveCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save coord")),
            OnSaveCustomChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("save chara")),
            OnSaveCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("save coord")),
            OnSaveActor.Subscribe(_ => Plugin.Instance.Log.LogDebug("save actor")),
            Hooks.OnInitializeActors.Subscribe(_ => Plugin.Instance.Log.LogDebug("actors initialized")),
            OnInitializeCustom.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom initialized")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara:{CharaLoadTrack.Mode.ToString()}")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadCustomChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom chara load")),
            OnLoadCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom coord load")),
            OnLoadActorChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor chara load")),
            OnLoadActorCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor coord load")),
            OnActorHumanize.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor humanized")),
            OnChangeCustomCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("custom coordinate change")),
            OnChangeActorCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("actor coordinate change"))
#endif
        ];
    }

    #endregion
}