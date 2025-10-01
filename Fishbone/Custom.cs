using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Manager;
using Character;
using CharacterCreation;
#if Aicomi
using R3;
using R3.Triggers;
using ILLGAMES.Rigging;
using ILLGAMES.Extensions;
using AC.User;
using AC.Scene.FreeH;
using Actor = AC.User.ActorData;
using ActorIndex = (int, int);
#else
using UniRx;
using UniRx.Triggers;
using ILLGames.Rigging;
using ILLGames.Extensions;
using Actor = SaveData.Actor;
using ActorIndex = int;
#endif
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using CoastalSmell;
using System.Data;

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
            FreeHScene.Instance is null ? Game.Instance?.SaveData?.AllActors()
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
    public static partial class Extension
    {
        static void Save(string path, Action<ZipArchive> actions) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(actions)));
        internal static void SaveChara(string path) => Save(path, OnSaveChara);
        internal static void SaveCoord(string path) => Save(path, OnSaveCoord);
        internal static void SaveActor(Actor actor) =>
            Implant(actor.ToHumanData(), ToBinary(OnSaveActor.Apply(actor)));
    }
    internal static partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Chara());
        internal static void SaveCoord(ZipArchive archive) =>
            Extension<T, U>.SaveCoord(archive, Coord());
    }
    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Chara());
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Save(Actor actor, ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Charas.GetValueOrDefault(actor.ToIndex(), new()));
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Save(Actor actor, ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Charas.GetValueOrDefault(actor.ToIndex(), new()));
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
    public static partial class Extension
    {
        internal static event Action OnEnterCustom = delegate
        {
            Hooks.OnEnterCustom();
            OnCustomInitialize();
            CustomCoordinateType = 0;
            ClearCopyTrack();
            ClearHumanToActors();

            OnCopy += CheckCustomCopy;
            OnLoadChara += LoadCustomChara;
            OnLoadCoord += LoadCustomCoord;

            OnCopy -= CheckActorCopy;
            OnLoadCoord -= LoadActorCoord;
        };
        internal static event Action OnLeaveCustom = delegate
        {
            Hooks.OnLeaveCustom();
            OnCustomInitialize();
            CustomCoordinateType = 0;
            ClearCopyTrack();
            ClearHumanToActors();

            OnCopy -= CheckCustomCopy;
            OnLoadChara -= LoadCustomChara;
            OnLoadCoord -= LoadCustomCoord;

            OnCopy += CheckActorCopy;
            OnLoadCoord += LoadActorCoord;
        };
        internal static void Initialize()
        {
            OnLeaveCustom();
            Util<HumanCustom>.Hook(() => OnEnterCustom(), () => OnLeaveCustom());
        }
    }
    #endregion

    #region Load Custom
    public static partial class Extension
    {
        internal static void LoadCustomChara(Human human) =>
            OnLoadCustomChara.Apply(human).Try(Plugin.Instance.Log.LogError);
        internal static void LoadCustomCoord(Human human) =>
            OnLoadCustomCoord.Apply(human).Try(Plugin.Instance.Log.LogError);
    }
    internal static partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveHuman += (human, limit) => Current = Current.Merge(limit, value);
        internal static void JoinCoordTrack(CoordTrack track, U value) =>
            track.OnResolve += (human, limit) =>
                Current = Current.Merge(human.data.Status.coordinateType, limit, value);
        internal static void Initialize() =>
            Current = new();
        internal static void EnterCustom()
        {
            Extension<T, U>.OnCopyTrackStart += JoinCopyTrack;
            Extension<T, U>.OnCoordTrackStart += JoinCoordTrack;
        }
        internal static void LeaveCustom()
        {
            Extension<T, U>.OnCopyTrackStart -= JoinCopyTrack;
            Extension<T, U>.OnCoordTrackStart -= JoinCoordTrack;
        }
    }
    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadChara(Human _, CharaLimit limit, T value) =>
            Current = Current.Merge(limit, value);
        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveHuman += (human, limit) => LoadChara(human, limit, value);
        internal static void Initialize() =>
            Current = new();
        internal static void EnterCustom() =>
            Extension<T>.OnCopyTrackStart += JoinCopyTrack;
        internal static void LeaveCustom() =>
            Extension<T>.OnCopyTrackStart -= JoinCopyTrack;
    }
    #endregion

    #region Load Actor
    public static partial class Extension
    {
        internal static void ResolveCopy(Actor actor) =>
            CopyTracks.Remove(actor.ToHumanData(), out var track).Maybe(() => track.Resolve(actor));
        internal static void LoadActor(Actor actor) =>
            OnLoadActor.Apply(actor).Try(Plugin.Instance.Log.LogError);
        internal static void LoadActorCoord(Human human) =>
            ToActorIndex(human, out var index)
                .Maybe(() => OnLoadActorCoord.Apply(index.ToActor()).Apply(human).Try(Plugin.Instance.Log.LogError));
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void LoadActor(Actor actor, T value) =>
            Charas[actor.ToIndex()] = value;
        internal static void LoadActorChara(Human human, ActorIndex index) =>
            Coords[index] = Charas[index].Get(human.data.Status.coordinateType);
        internal static void LoadActorCoord(ActorIndex index, U value, CoordLimit limit) =>
            Coords[index] = Coords[index].Merge(limit, value);
        internal static void LoadActorCoord(Human human, U value, CoordLimit limit) =>
            Extension.ToActorIndex(human, out var index)
                .Maybe(F.Apply(LoadActorCoord, index, value, limit));
        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveActor += actor => LoadActor(actor, value);
        internal static void JoinCoordTrack(CoordTrack track, U value) =>
            track.OnResolve += (human, limit) => LoadActorCoord(human, value, limit);
        internal static void EnterCustom()
        {
            Extension<T, U>.OnCopyTrackStart -= JoinCopyTrack;
            Extension<T, U>.OnCoordTrackStart -= JoinCoordTrack;
            Extension.OnActorHumanize -= LoadActorChara;
            Extension.OnActorCoordChange -= CoordinateChange;
        }
        internal static void LeaveCustom()
        {
            Extension<T, U>.OnCopyTrackStart += JoinCopyTrack;
            Extension<T, U>.OnCoordTrackStart += JoinCoordTrack;
            Extension.OnActorHumanize += LoadActorChara;
            Extension.OnActorCoordChange += CoordinateChange;
        }
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadActor(Actor actor, T value) =>
            Charas[actor.ToIndex()] = value;
        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveActor += actor => LoadActor(actor, value);
        internal static void EnterCustom() =>
            Extension<T>.OnCopyTrackStart -= JoinCopyTrack;
        internal static void LeaveCustom() =>
            Extension<T>.OnCopyTrackStart += JoinCopyTrack;
    }
    #endregion

    #region Complex Definition For Actor To Human Copy
    internal partial class CopyTrack
    {
        internal event Action<Actor> OnResolveActor =
            actor => Plugin.Instance.Log.LogDebug($"Actor{actor.ToIndex()} loaded: {actor.ToHumanData().Pointer}");
        internal void Resolve(Actor actor) =>
            (F.Apply(OnResolveActor, actor) + F.Apply(Extension.LoadActor, actor)).Try(Plugin.Instance.Log.LogError);
    }
    public static partial class Extension
    {
        static Dictionary<Human, ActorIndex> HumanToActors = new();
        internal static event Action<Human, ActorIndex> OnActorHumanize = UpdateHumanToActor;
        internal static bool ToActorIndex(HumanData data, out ActorIndex index) =>
            (index = CurrentActors()
                .Where(actor => data == actor.ToHumanData())
                .Select(actor => actor.ToIndex())
                .FirstOrDefault(
                    HumanToActors
                        .Where(entry => entry.Key.data == data)
                        .Select(entry => entry.Value)
                        .FirstOrDefault(NotFound))) != NotFound;
        internal static bool ToActorIndex(Human human, out ActorIndex index) =>
            HumanToActors.TryGetValue(human, out index);
        static void CheckActorCopy(HumanData src, HumanData dst, CharaLimit limit) =>
            ToActorIndex(src, out var index).Maybe(F.Apply(StartActorTrack, dst, index));
        static void StartActorTrack(HumanData data, ActorIndex index) =>
            StartCopyTrack(data).OnResolveHuman += (human, _) => ResolveHumanToActor(human, index);
        static void ResolveHumanToActor(Human human, ActorIndex index) =>
            (OnActorHumanize.Apply(human).Apply(index) + OnLoadChara.Apply(human) +
                OnLoadActorChara.Apply(index.ToActor()).Apply(human)).Try(Plugin.Instance.Log.LogError);
        internal static void UpdateHumanToActor(Human human, ActorIndex index) =>
            HumanToActors.ContainsKey(human).Either(
                F.Apply(ObserveOnDestroy, human) +
                F.Apply(AssignHumanToActor, human, index),
                F.Apply(AssignHumanToActor, human, index));
        static void AssignHumanToActor(Human human, ActorIndex index) =>
            HumanToActors[human] = index;
        static void ObserveOnDestroy(Human human) =>
            human.gameObject.GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(Dispose(human));
        static Action<Unit> Dispose(Human human) => _ => HumanToActors.Remove(human);
        static void ClearHumanToActors() => HumanToActors.Clear();
    }
    #endregion

    #region Copy Between Actor And Custom
    public static partial class Extension
    {
        static void CheckCustomCopy(HumanData src, HumanData dst, CharaLimit limit) =>
            (HumanCustom.Instance == null ? F.DoNothing :
                src == HumanCustom.Instance.DefaultData ?
                    OnCustomInitialize + TrackSpecial(dst):
                src == HumanCustom.Instance.Received.HumanData &&
                    ToActorIndex(src, out var srcIndex) ?
                    OnCopyActorToCustom.Apply(srcIndex) + TrackSpecial(dst) :
                dst == HumanCustom.Instance.EditHumanData &&
                    ToActorIndex(HumanCustom.Instance.Received.HumanData, out var dstIndex) ?
                    PrepareSaveChara + OnCopyCustomToActor.Apply(dstIndex) : F.DoNothing).Try(Plugin.Instance.Log.LogError);

        static Action TrackSpecial(HumanData data) =>
            () => StartCopyTrack(data).OnResolveHuman += (human, _) => LoadChara(human);
        internal static event Action<ActorIndex> OnCopyActorToCustom =
            index => Plugin.Instance.Log.LogDebug($"Simulation actor{index} copied to custom.");
        internal static event Action<ActorIndex> OnCopyCustomToActor =
            index => Plugin.Instance.Log.LogDebug($"Custom copied to simulation actor{index}.");
        internal static event Action OnCustomInitialize =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom initialized.");
    }
    internal static partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Copy(ActorIndex index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T, U>.Chara(index));
    }
    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Copy(ActorIndex index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T>.Chara(index));
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Copy(ActorIndex index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T, U>.Chara());
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Copy(ActorIndex index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T>.Chara());
    }
    #endregion

    #region Coordinate Change
    static partial class Extension
    {
        internal static int CustomCoordinateType { get; set; } = 0;
        internal static void CustomChangeCoord(Human human, int coordinateType) =>
            (PrepareSaveCoord + F.Apply(CustomChangeCoord, coordinateType) + OnLoadCoord.Apply(human)).Try(Plugin.Instance.Log.LogError);
        static void CustomChangeCoord(int coordinateType) =>
            CustomCoordinateType = coordinateType;

        internal static event Action<ActorIndex, int> OnActorCoordChange = delegate { };
        internal static void ActorChangeCoord(Human human, int coordinateType) =>
            HumanToActors.TryGetValue(human, out var actor).Maybe(F.Apply(ActorChangeCoord, actor, human, coordinateType));
        static void ActorChangeCoord(ActorIndex actor, Human human, int coordinateType) =>
            (OnActorCoordChange.Apply(actor).Apply(coordinateType) + OnLoadCoord.Apply(human)).Try(Plugin.Instance.Log.LogError);
    }
    internal static partial class ActorExtension<T, U>
    {
        internal static void CoordinateChange(ActorIndex actor, int coordinateType) =>
            Coords[actor] = Charas[actor].Get(coordinateType);
    }
    #endregion
}