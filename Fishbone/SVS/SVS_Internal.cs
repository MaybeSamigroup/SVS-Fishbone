using System.Linq;
using UniRx;
using SaveData;
using Character;
using CharacterCreation;
using HarmonyLib;
using CoastalSmell;

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
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Save), typeof(string))]
        static void WorldSavePrefix(WorldData __instance) =>
            __instance.Charas.Yield()
                .Where(entry => entry != null && entry.Item2 != null)
                .ForEach(entry => Extension.SaveActor(entry.Item2));
    }
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordinateTypeChange), nameof(CoordinateTypeChange.ChangeType), typeof(int))]
        static void CoordinateTypeChangeChangeTypePrefix(CoordinateTypeChange __instance, int type) =>
            Extension.CustomChangeCoord(__instance._human, type);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(Extension.ActorChangeCoord, __instance.human, (int)type));
    }

    #endregion

    #region Load Custom
    static partial class Hooks
    {
        internal static void OnEnterCustom() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.CustomFlagResolver;
        internal static void OnLeaveCustom() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePrefix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePostfix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.EnabledResolver;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPrefix() => CoordLoadHook = CoordLoadHook.Skip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPostfix() => CoordLoadHook = new CoordLoadWait();
    }
    #endregion

    #region Load Actor
    static partial class Hooks
    {

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPrefix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.EnabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPostfix() =>
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.DisabledResolver;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController),
            nameof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController.SetActive))]
        static void ListSelectControllerSetActivePostfix(bool active) => (!active).Maybe(SaveDataWorldDataLoadPostfix);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Actor), nameof(Actor.SetBytes))]
        static void ActorSetBytes(Actor actor) => Extension.ResolveCopy(actor);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(Actor __result) => Extension.ResolveCopy(__result);
    }
    #endregion
}