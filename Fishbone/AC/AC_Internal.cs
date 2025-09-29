using System;
using System.Linq;
using AC.User;
using AC.Scene.Home.UI;
using Character;
using CharacterCreation;
using HarmonyLib;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Extension
    {
        internal static event Action<(int, int), (int, int)> OnSwapIndex =
            (src, dst) => Plugin.Instance.Log.LogDebug($"Actor swapped {src} => ${dst}");
        internal static void SwapIndex((int, int) src, (int, int) dst) =>
            OnSwapIndex.Apply(src).Apply(dst).Try(Plugin.Instance.Log.LogError);
        internal static void StartAllActorTrack() =>
            AllActors(Manager.Game.Instance.SaveData)
                .Where(actor => actor != null)
                .Where(actor => !HumanToActors.Values.Contains(actor.ToIndex()))
                .ForEach(actor => StartActorTrack(actor.ToHumanData(), actor.ToIndex()));
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void SwapIndex((int, int) src, (int, int) dst)
        {
            Charas.Remove(src, out var chara).Maybe(() => Charas[dst] = chara);
            Coords.Remove(src, out var coord).Maybe(() => Coords[dst] = coord);
        }
        internal static void Initialize() =>
            Enumerable.Range(1, 3).ForEach(index => Coords[(-1, index)] = (Charas[(-1, index)] = new()).Get(0));
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void SwapIndex((int, int) src, (int, int) dst) =>
            Charas.Remove(src, out var chara).Maybe(() => Charas[dst] = chara);
        internal static void Initialize() =>
            Enumerable.Range(1, 3).ForEach(index => Charas[(-1, index)] = new());
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
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.SerializeHumanData))]
        static void ActorSerializeHumanDataPrefix(ActorData __instance) =>
            Extension.SaveActor(__instance);
    }
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordinateTypeChange), nameof(CoordinateTypeChange.ChangeType), typeof(int))]
        static void CoordinateTypeChangeChangeTypePrefix(CoordinateTypeChange __instance, int type) =>
            Extension.CustomChangeCoord(__instance._human, type);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateTypeAndReload), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance._human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(Extension.ActorChangeCoord, __instance._human, (int)type));
    }

    #endregion

    #region Load

    static partial class Hooks
    {
        internal static void OnEnterCustom() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.CustomFlagResolver;
        internal static void OnLeaveCustom() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPrefix() => CoordLoadHook = CoordLoadHook.Skip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPostfix() => CoordLoadHook = new CoordLoadWait();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.DeserializeHumanData))]
        static void ActorDataDeserializeHumanDataPrefix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.EnabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.DeserializeHumanData))]
        static void ActorDataDeserializeHumanDataPostfix(ActorData __instance) =>
            (CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver).With(F.Apply(Extension.ResolveCopy, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanSelectUI), nameof(HumanSelectUI.Open))]
        static void HumanSelectUIOpenPostfix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.EnabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanSelectUI), nameof(HumanSelectUI.Close))]
        static void HumanSelectUIClosePrefix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.HumanData), MethodType.Setter)]
        static void ActorDataHumanDataSetPostfix(ActorData __instance) =>
            Extension.ResolveCopy(__instance);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Seat), nameof(Seat.SetData))]
        static void SeatSetData(ActorData data) =>
            Extension.StartActorTrack(data.ToHumanData(), data.ToIndex());

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(RegistrationUI), nameof(RegistrationUI.Swap))]
        static void RegistrationUISwapPostfix(RegistrationUI.SwapData from, RegistrationUI.SwapData to) =>
            Extension.SwapIndex((from.Group, from.DataIndex), (to.Group, to.DataIndex));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(AC.Scene.HomeScene), nameof(AC.Scene.HomeScene.RunTutorialSequence))]
        [HarmonyPatch(typeof(AC.Scene.HomeScene), nameof(AC.Scene.HomeScene.RunFestivalSequence))]
        [HarmonyPatch(typeof(AC.Scene.HomeScene), nameof(AC.Scene.HomeScene.RunLazySundaySequence))]
        [HarmonyPatch(typeof(AC.Scene.HomeScene), nameof(AC.Scene.HomeScene.RunDateScenarioSequence))]
        [HarmonyPatch(typeof(AC.Scene.HomeScene), nameof(AC.Scene.HomeScene.RunDefaultScenarioSequence))]
        static void HomeSceneRunADVPrefix() => Extension.StartAllActorTrack();
    }
    #endregion
}