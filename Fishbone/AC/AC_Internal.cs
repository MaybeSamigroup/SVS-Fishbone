using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using Manager;
using AC.User;
using Character;
using CharacterCreation;
using ILLGAMES.Rigging;
using ILLGAMES.Extensions;
using HarmonyLib;
using CoastalSmell;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
    internal partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static T Current = new();

        static int CoordinateType =>
            HumanCustom.Instance?.Human?.data?.Status.coordinateType ?? 0;

        internal static T Chara() => Current;
        internal static U Coord() => Current.Get(CoordinateType);
        internal static void Chara(T value) => Current = value;
        internal static void Coord(U value) => Plugin.Instance.Log
            .LogInfo(Util.ToJson(Current = Current.Merge(CoordinateType, value)));
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
        static readonly Dictionary<(int, int), T> Charas = new();
        static readonly Dictionary<(int, int), U> Coords = new();

        internal static T Chara((int, int) index) =>
            Charas.GetValueOrDefault(index, new());

        internal static U Coord((int, int) index) =>
            Coords.GetValueOrDefault(index, new());

        internal static void Chara((int, int) index, T mods) => Charas[index] = mods;

        internal static void Coord((int, int) index, U mods) => Coords[index] = mods; 
    }

    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<(int, int), T> Charas = new();

        internal static T Chara((int, int) index) => Charas.GetValueOrDefault(index, new());

        internal static void Chara((int, int) index, T mods) => Charas[index] = mods;
    }

    #region Save

    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveCharaFileBeforeAction))]
        static void HumanDataSaveCharaFileBeforeAction(string path) =>
            Extension.SaveChara(path);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile))]
        static void HumanDataCoordinateSaveFilePostfix(string path) =>
            Extension.SaveCoord(path);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData), nameof(SaveData.Save), typeof(string), typeof(bool), typeof(bool), typeof(bool))]
        static void SaveDataSavePrefix(SaveData __instance) =>
            __instance.Actors.ToArray().SelectMany(group => group)
                .Where(actor => actor != null).ForEach(Extension.SaveActor);
    }

    public static partial class Extension
    {
        static void Save(string path, Action<ZipArchive> actions) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(actions)));

        internal static void SaveChara(string path) => Save(path, OnSaveChara);

        internal static void SaveCoord(string path) => Save(path, OnSaveCoord);

        internal static void SaveActor(ActorData actor) =>
            Implant(actor.HumanData, ToBinary(OnSaveActor.Apply(actor)));

        internal static (int, int) Index(this ActorData actor) => (actor.Guid.Group, actor.Guid.Index);
        internal static AssignGuid Guid(this (int, int) index) => new (index.Item1, index.Item2);
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
        internal static void Save(ActorData actor, ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Charas.GetValueOrDefault(actor.Index(), new()));
    }

    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Save(ActorData actor, ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Charas.GetValueOrDefault(actor.Index(), new()));
    }

    #endregion

    #region Load

    static partial class Hooks
    {
        static HumanDataLoadActions CustomLoadActions = (data, flags) => flags
            is (LoadFlags.About | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic)
            or (LoadFlags.About | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
            ? (GetPngSizeProc(data), Extension.Preprocess) : (GetPngSizeSkip, HumanDataLoadFileSkip);

        internal static void OnEnterCustom() =>
            HumanDataLoadActions = CustomLoadActions;

        internal static void OnLeaveCustom() =>
            HumanDataLoadActions = ActorLoadSkip;

        static HumanDataLoadActions ActorLoadProc = (data, flags) => (GetPngSizeSkip, Extension.Preprocess);
        static HumanDataLoadActions ActorLoadSkip = (data, flags) => (GetPngSizeSkip, HumanDataLoadFileSkip);
        static HumanDataLoadActions ActorEntryActions = ActorLoadProc;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPrefix() => OnSkipPng = SkipPngSkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPostfix() => OnSkipPng = SkipPngProc;
    }

    public static partial class Extension
    {
        internal static event Action<Human, CharaLimit> PreLoadCustomChara = (human, _) =>
            Plugin.Instance.Log.LogDebug($"Custom chara loaded: {human.data.Pointer}");
        internal static void LoadCustomChara(Human human, CharaLimit limit) =>
            (PreLoadCustomChara.Apply(human).Apply(limit) + OnLoadCustomChara.Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static event Action<Human, CoordLimit> PreLoadCustomCoord = (human, _) =>
            Plugin.Instance.Log.LogDebug($"Custom coord loaded: {human.data.Pointer}");
        internal static void LoadCustomCoord(Human human, CoordLimit limit) =>
            (PreLoadCustomCoord.Apply(human).Apply(limit) + OnLoadCustomCoord.Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static void LoadActor(ActorData actor) => OnLoadActor.Apply(actor).Try(Plugin.Instance.Log.LogError);
        internal static event Action<ActorData> PreLoadActor = actor =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.Index()} loaded: {actor.HumanData.Pointer}");

        internal static event Action<ActorData, CharaLimit> PreLoadActorChara = (actor, _) =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.Index()} loaded: {actor.HumanData.Pointer}");
        internal static void LoadActorChara(Human human, CharaLimit limit) =>
            Resolve(human, out var actor).Maybe(F.Apply(LoadActorChara, actor, human, limit));
        static void LoadActorChara(ActorData actor, Human human, CharaLimit limit) =>
            (PreLoadActorChara.Apply(actor).Apply(limit) + OnLoadActorChara.Apply(actor).Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static event Action<ActorData, CoordLimit> PreLoadActorCoord = (actor, _) =>
            Plugin.Instance.Log.LogDebug($"Simulation coord{actor.Index()} loaded: {actor.HumanData.Pointer}");
        internal static void LoadActorCoord(Human human, CoordLimit limit) =>
            Resolve(human, out var actor).Maybe(F.Apply(LoadActorCoord, actor, human, limit));
        static void LoadActorCoord(ActorData actor, Human human, CoordLimit limit) =>
            (PreLoadActorCoord.Apply(actor).Apply(limit) + OnLoadActorCoord.Apply(actor).Apply(human)).Try(Plugin.Instance.Log.LogError);

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

        internal static event Action OnEnterCustom = delegate
        {
            Hooks.OnEnterCustom();
            PreLoadChara -= LoadActorChara;
            PreLoadChara += LoadCustomChara;
            PreLoadCoord -= LoadActorCoord;
            PreLoadCoord += LoadCustomCoord;
            PreChangeCoord -= ActorChangeCoord;
            PreChangeCoord += CustomChangeCoord;
        };
        internal static event Action OnLeaveCustom = delegate
        {
            PreLoadChara -= LoadCustomChara;
            PreLoadChara += LoadActorChara;
            PreLoadCoord -= LoadCustomCoord;
            PreLoadCoord += LoadActorCoord;
            PreChangeCoord -= CustomChangeCoord;
            PreChangeCoord += ActorChangeCoord;
        };
    }

    internal static partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Initialize() =>
            Current = new();

        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T, U>.Resolve(human.data, Current));

        internal static void LoadCoord(Human human, CoordLimit limit) =>
            Current = Current.Merge(human.data.Status.coordinateType, limit, Extension<T, U>.Resolve(Coord()));
    }

    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Initialize() =>
            Current = new();

        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T>.Resolve(human.data, Current));
    }

    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void LoadActor(ActorData actor) =>
            Charas[actor.Index()] = Extension<T, U>.Resolve(actor.HumanData, new());

        internal static void LoadChara(ActorData actor, CharaLimit _) =>
            Coords[actor.Index()] =
                Charas[actor.Index()].Get(actor.HumanData.Status.coordinateType);

        internal static void LoadCoord(ActorData actor, CoordLimit limit) =>
            Coords[actor.Index()] =
                Coords[actor.Index()].Merge(limit,
                    Extension<T, U>.Resolve(Coords[actor.Index()]));
    }

    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadActor(ActorData actor) =>
            Charas[actor.Index()] = Extension<T>.Resolve(actor.HumanData, new());

        internal static void LoadChara(ActorData actor, CharaLimit limit) =>
            Charas[actor.Index()] =
                Charas[actor.Index()].Merge(limit,
                    Extension<T>.Resolve(actor.HumanData, Charas[actor.Index()]));
    }

    #endregion

    #region Copy Between Actor And Human

    public static partial class Extension
    {
        static (int, int) ToActor(HumanData data) =>
            (Game.Instance?.SaveData?.Actors?.ToArray()?.SelectMany(group => group) ?? [])
                .Where(actor => actor.HumanData == data)
                .Select(actor => actor.Index())
                .FirstOrDefault(
                    HumanActors
                        .Where(entry => entry.Key.data == data)
                        .Select(entry => entry.Value)
                        .FirstOrDefault((-1, -1)));

        static Dictionary<HumanData, (int, int)> TrackActors = new();

        static Dictionary<Human, (int, int)> HumanActors = new();

        static void TrackActor(HumanData src, HumanData dst) =>
            (TrackActors.TryGetValue(src, out var value) && TrackActors.Remove(src))
                .Either(F.Apply(TrackActor, ToActor(src), dst), F.Apply(TrackActor, dst, value));

        static void TrackActor((int, int) index, HumanData data) =>
            (index is not (-1, -1)).Maybe(F.Apply(TrackActor, data, index));

        static void TrackActor(HumanData data, (int, int) index) =>
            TrackActors[data] = index;
        static bool Resolve(HumanData data, out (int, int) index) =>
            (index = TrackActors.TryGetValue(data, out var value) &&
                TrackActors.Remove(data) ? value : (-1, -1)) is not (-1, -1);

        internal static bool Resolve(Human human, out ActorData actor) =>
            null != (actor = Resolve(human));

        static ActorData Resolve(Human human) =>
            (Resolve(human.data, out var track), HumanActors.TryGetValue(human, out var value)) switch
            {
                (true, true) => Game.Instance.SaveData[Register(human, track).Guid()],
                (true, false) => Game.Instance.SaveData[(HumanActors[human] = track).Guid()],
                (false, true) => Game.Instance.SaveData[value.Guid()],
                (false, false) => null
            };

        internal static (int, int) Register(Human human, (int, int) index) =>
            HumanActors[human.With(Register)] = index;

        static void Register(Human human) =>
            human.gameObject.GetComponent<ObservableDestroyTrigger>()
                .OnDestroyAsObservable().Subscribe(Dispose(human));

        static Action<Unit> Dispose(Human human) => _ => HumanActors.Remove(human);

        internal static void Initialize()
        {
            OnCopy += TrackActor;
            OnEnterCustom += TrackActors.Clear;
            OnLeaveCustom += TrackActors.Clear;
            OnEnterCustom += HumanActors.Clear;
            OnLeaveCustom += HumanActors.Clear;
            Util<HumanCustom>.Hook(() => OnEnterCustom(), () => OnLeaveCustom());
            OnLeaveCustom();
        }
    }

    #endregion

    #region Copy Between Actor And Custom

    static partial class Hooks
    {
        static Action OnCopyAction(HumanCustom custom, HumanData dst, HumanData src) =>
            src == custom?.Received?.HumanData ? Extension.CopyActorToCustom :
            dst == custom?.EditHumanData ? Extension.CopyCustomToActor :
            src == custom?.DefaultData ? Extension.CustomInitialize : F.DoNothing;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyPrefix(HumanData dst, HumanData src) =>
            OnCopyAction(HumanCustom.Instance, dst, src).Invoke();
    }

    public static partial class Extension
    {
        static (int, int) GetHumanCustomTarget =>
            ToActor(HumanCustom.Instance.Received.HumanData);
        internal static event Action<(int, int)> OnCopyActorToCustom =
            index => Plugin.Instance.Log.LogDebug($"Simulation actor{index} copied to custom.");

        internal static event Action<(int, int)> OnCopyCustomToActor =
            index => Plugin.Instance.Log.LogDebug($"Custom copied to simulation actor{index}.");

        internal static event Action OnCustomInitialize =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom initialized.");

        internal static void CustomInitialize() =>
            ChangeCustomCoord = HumanCustom.Instance.Human == null ? NotifySaveCoord : PrepareInitialize;

        internal static void CopyActorToCustom() =>
            OnCopyActorToCustom.Apply(GetHumanCustomTarget).Try(Plugin.Instance.Log.LogError);

        internal static void CopyCustomToActor() =>
            OnCopyCustomToActor.Apply(GetHumanCustomTarget).Try(Plugin.Instance.Log.LogError);
    }

    internal static partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy((int, int) index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T, U>.Chara(index));
    }

    internal static partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy((int, int) index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T>.Chara(index));
    }

    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy((int, int) index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T, U>.Chara());
    }

    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy((int, int) index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T>.Chara());
    }

    #endregion

    #region Actor Coordinate Change

    static partial class Extension
    {
        internal static event Action<(int, int), int> OnActorCoordChange = delegate { };
        internal static Action ChangeCustomCoord = NotifySaveCoord;
        internal static void PrepareInitialize() =>
            (ChangeCustomCoord = NotifySaveCoord).With(NotifyInitialize);
        static void NotifySaveCoord() => PrepareSaveCoord();
        static void NotifyInitialize() =>
            OnCustomInitialize.Try(Plugin.Instance.Log.LogError);
        internal static void CustomChangeCoord(Human human, int coordinateType) => ChangeCustomCoord();
        internal static void ActorChangeCoord(Human human, int coordinateType) =>
            HumanActors.TryGetValue(human, out var actor).Maybe(F.Apply(ActorChangeCoord, actor, coordinateType));
        static void ActorChangeCoord((int, int) index, int coordinateType) =>
            OnActorCoordChange.Apply(index).Apply(coordinateType).Try(Plugin.Instance.Log.LogError);
    }

    internal static partial class ActorExtension<T, U>
    {
        internal static void CoordinateChange((int, int) index, int coordinateType) =>
            Coords[index] = Charas[index].Get(coordinateType);
    }

    #endregion
}