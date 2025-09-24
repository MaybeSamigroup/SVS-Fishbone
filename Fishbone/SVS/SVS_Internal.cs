using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UniRx.Triggers;
using Manager;
using Character;
using CharacterCreation;
using ILLGames.Rigging;
using ILLGames.Extensions;
using HarmonyLib;
using CoastalSmell;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
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
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Save), typeof(string))]
        static void WorldSavePrefix(SaveData.WorldData __instance) =>
            __instance.Charas.Yield()
                .Where(entry => entry != null && entry.Item2 != null)
                .ForEach(entry => Extension.SaveActor(entry.Item2));
    }

    public static partial class Extension
    {
        static void Save(string path, Action<ZipArchive> actions) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), ToBinary(actions)));

        internal static void SaveChara(string path) => Save(path, OnSaveChara);

        internal static void SaveCoord(string path) => Save(path, OnSaveCoord);

        internal static void SaveActor(SaveData.Actor actor) =>
            Implant(actor.charFile, ToBinary(OnSaveActor.Apply(actor)));
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Chara);

        internal static void SaveCoord(ZipArchive archive) =>
            Extension<T, U>.SaveCoord(archive, Coord);
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Chara);
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Save(SaveData.Actor actor, ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Charas.GetValueOrDefault(actor.charasGameParam.Index, new()));
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Save(SaveData.Actor actor, ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Charas.GetValueOrDefault(actor.charasGameParam.Index, new()));
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

        static Action<SaveData.Actor> ActorSetBytesSkip = _ => { };
        static Action<SaveData.Actor> ActorSetBytesProc = Extension.LoadActor;
        static Action<SaveData.Actor> OnActorSetBytes = ActorSetBytesSkip;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePrefix() =>
            HumanDataLoadActions = ActorEntryActions = ActorLoadSkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePostfix() =>
            HumanDataLoadActions = ActorEntryActions = ActorLoadProc;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPrefix() =>
            (OnActorSetBytes, HumanDataLoadActions) = (ActorSetBytesProc, ActorEntryActions);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPostfix() =>
            (OnActorSetBytes, HumanDataLoadActions) = (ActorSetBytesSkip, CustomLoadActions);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController),
            nameof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController.SetActive))]
        static void ListSelectControllerSetActivePostfix(bool active) =>
            (!active).Maybe(SaveDataWorldDataLoadPostfix);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.Actor), nameof(SaveData.Actor.SetBytes))]
        static void ActorSetBytes(SaveData.Actor actor) => OnActorSetBytes(actor);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(SaveData.Actor __result) => Extension.LoadActor(__result);
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

        internal static void LoadActor(SaveData.Actor actor) => OnLoadActor.Apply(actor).Try(Plugin.Instance.Log.LogError);
        internal static event Action<SaveData.Actor> PreLoadActor = actor =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} loaded: {actor.charFile.Pointer}");

        internal static event Action<SaveData.Actor, CharaLimit> PreLoadActorChara = (actor, _) =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} loaded: {actor.charFile.Pointer}");
        internal static void LoadActorChara(Human human, CharaLimit limit) =>
            Resolve(human, out var actor).Maybe(F.Apply(LoadActorChara, actor, human, limit));
        static void LoadActorChara(SaveData.Actor actor, Human human, CharaLimit limit) =>
            (PreLoadActorChara.Apply(actor).Apply(limit) + OnLoadActorChara.Apply(actor).Apply(human)).Try(Plugin.Instance.Log.LogError);

        internal static event Action<SaveData.Actor, CoordLimit> PreLoadActorCoord = (actor, _) =>
            Plugin.Instance.Log.LogDebug($"Simulation coord{actor.charasGameParam.Index} loaded: {actor.charFile.Pointer}");
        internal static void LoadActorCoord(Human human, CoordLimit limit) =>
            Resolve(human, out var actor).Maybe(F.Apply(LoadActorCoord, actor, human, limit));
        static void LoadActorCoord(SaveData.Actor actor, Human human, CoordLimit limit) =>
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

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Initialize() =>
            Current = new();

        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T, U>.Resolve(human.data, Current));

        internal static void LoadCoord(Human human, CoordLimit limit) =>
            Current = Current.Merge(human.data.Status.coordinateType, limit, Extension<T, U>.Resolve(Coord));
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Initialize() =>
            Current = new();

        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T>.Resolve(human.data, Current));
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void LoadActor(SaveData.Actor actor) =>
            Charas[actor.charasGameParam.Index] = Extension<T, U>.Resolve(actor.charFile, new());

        internal static void LoadChara(SaveData.Actor actor, CharaLimit _) =>
            Coords[actor.charasGameParam.Index] =
                Charas[actor.charasGameParam.Index].Get(actor.charFile.Status.coordinateType);

        internal static void LoadCoord(SaveData.Actor actor, CoordLimit limit) =>
            Coords[actor.charasGameParam.Index] =
                Coords[actor.charasGameParam.Index].Merge(limit,
                    Extension<T, U>.Resolve(Coords[actor.charasGameParam.Index]));
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadActor(SaveData.Actor actor) =>
            Charas[actor.charasGameParam.Index] = Extension<T>.Resolve(actor.charFile, new());

        internal static void LoadChara(SaveData.Actor actor, CharaLimit limit) =>
            Charas[actor.charasGameParam.Index] =
                Charas[actor.charasGameParam.Index].Merge(limit,
                    Extension<T>.Resolve(actor.charFile, Charas[actor.charasGameParam.Index]));
    }

    #endregion

    #region Copy Between Actor And Human

    public static partial class Extension
    {
        static int ToActor(HumanData data) =>
            Game.Charas.Yield()
                .Select(entry => entry.Item2)
                .Where(actor => actor.charFile == data)
                .Select(actor => actor.charasGameParam.Index)
                .FirstOrDefault(
                    HumanActors
                        .Where(entry => entry.Key.data == data)
                        .Select(entry => entry.Value)
                        .FirstOrDefault(-1));

        static Dictionary<HumanData, int> TrackActors = new();

        static Dictionary<Human, int> HumanActors = new();

        static void TrackActor(HumanData src, HumanData dst) =>
            (TrackActors.TryGetValue(src, out var value) && TrackActors.Remove(src))
                .Either(F.Apply(TrackActor, ToActor(src), dst), F.Apply(TrackActor, dst, value));

        static void TrackActor(int index, HumanData data) =>
            (index >= 0).Maybe(F.Apply(TrackActor, data, index));

        static void TrackActor(HumanData data, int index) =>
            TrackActors[data] = index;
        static bool Resolve(HumanData data, out int index) =>
            (index = TrackActors.TryGetValue(data, out var value) && TrackActors.Remove(data) ? value : -1) >= 0;

        internal static bool Resolve(Human human, out SaveData.Actor actor) =>
            null != (actor = Resolve(human));

        static SaveData.Actor Resolve(Human human) =>
            (Resolve(human.data, out var track), HumanActors.TryGetValue(human, out var value)) switch
            {
                (true, true) => Game.Charas[Register(human, track)],
                (true, false) => Game.Charas[HumanActors[human] = track],
                (false, true) => Game.Charas[value],
                (false, false) => null
            };

        internal static int Register(Human human, int index) =>
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
        static int GetHumanCustomTarget =>
            ToActor(HumanCustom.Instance.Received.HumanData);
        internal static event Action<int> OnCopyActorToCustom =
            index => Plugin.Instance.Log.LogDebug($"Simulation actor{index} copied to custom.");

        internal static event Action<int> OnCopyCustomToActor =
            index => Plugin.Instance.Log.LogDebug($"Custom copied to simulation actor{index}.");

        internal static event Action OnCustomInitialize =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom initialized.");

        internal static void CustomInitialize() =>
            ChangeCustomCoord = HumanCustom.Instance.Human == null ? PrepareSaveCoord : PrepareInitialize;

        internal static void CopyActorToCustom() =>
            OnCopyActorToCustom.Apply(GetHumanCustomTarget).Try(Plugin.Instance.Log.LogError);

        internal static void CopyCustomToActor() =>
            OnCopyCustomToActor.Apply(GetHumanCustomTarget).Try(Plugin.Instance.Log.LogError);
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy(int index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T, U>.Chara(index));
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy(int index) =>
            Current = Current.Merge(CharaLimit.All, ActorExtension<T>.Chara(index));
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy(int index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T, U>.Chara);
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy(int index) =>
            Charas[index] = Charas[index].Merge(CharaLimit.All, HumanExtension<T>.Chara);
    }

    #endregion

    #region Actor Coordinate Change

    static partial class Extension
    {
        internal static event Action<int, int> OnActorCoordChange = delegate { };
        internal static Action ChangeCustomCoord = PrepareSaveCoord;
        internal static void PrepareInitialize() =>
            (ChangeCustomCoord = PrepareSaveCoord).With(NotifyInitialize);
        static void NotifyInitialize() =>
            OnCustomInitialize.Try(Plugin.Instance.Log.LogError);
        internal static void CustomChangeCoord(Human human, int coordinateType) => ChangeCustomCoord();
        internal static void ActorChangeCoord(Human human, int coordinateType) =>
            HumanActors.TryGetValue(human, out var actor).Maybe(F.Apply(ActorChangeCoord, actor, coordinateType));
        static void ActorChangeCoord(int actor, int coordinateType) =>
            OnActorCoordChange.Apply(actor).Apply(coordinateType).Try(Plugin.Instance.Log.LogError);
    }

    static partial class ActorExtension<T, U>
    {
        internal static void CoordinateChange(int actor, int coordinateType) =>
            Coords[actor] = Charas[actor].Get(coordinateType);
    }

    #endregion
}