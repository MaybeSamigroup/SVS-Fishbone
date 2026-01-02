using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Cysharp.Threading.Tasks;
using SV.Title;
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
        static void HumanDataSaveCharaFileBeforeAction(HumanData __instance, string path) => SaveChara.OnNext((__instance, path));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile))]
        static void HumanDataCoordinateSaveFilePostfix(HumanDataCoordinate __instance, string path) => SaveCoord.OnNext((__instance, path));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Save), typeof(string))]
        static void WorldSavePrefix(WorldData __instance) =>
            __instance.Charas.Yield()
                .Where(entry => entry.Value != null)
                .Select(entry => entry.Value).ForEach(SaveActor.OnNext);
    }
    #endregion

    #region Load Chara
    static partial class Hooks
    {
        static Subject<Unit> InitializeActors = new();
        internal static IObservable<Unit> OnInitializeActors => InitializeActors.AsObservable();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(TitleScene), nameof(TitleScene.OnStart))]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Load), typeof(string))]
        static void NotifyInitializeActors() => InitializeActors.OnNext(Unit.Default); 

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPrefix() => CharaLoadTrack.Mode = CharaLoadTrack.FlagIgnore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(WorldData), nameof(WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPostfix() => CharaLoadTrack.Mode = CharaLoadTrack.Ignore;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePrefix() => CharaLoadTrack.Mode = CharaLoadTrack.Ignore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePostfix() => CharaLoadTrack.Mode = CharaLoadTrack.FlagIgnore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController),
            nameof(SV.EntryScene.CharaListView.SelectListUI.ListSelectController.SetActive))]
        static void ListSelectControllerSetActivePostfix(bool active) => (!active).Maybe(SaveDataWorldDataLoadPostfix);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Actor), nameof(Actor.SetBytes))]
        static void ActorSetBytes(Actor actor) => ActorResolve.OnNext(actor);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(Actor __result) => ActorResolve.OnNext(__result);
    }
    #endregion

    #region Load Coord
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateCoordinateInfo), typeof(byte), typeof(bool))]
        [HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateCoordinateInfo), typeof(byte), typeof(bool), typeof(HumanDataCoordinate.LoadFileInfo.Flags))]
        static void CoordinateLoadIgnore() => CoordLoadTrack.Mode = CoordLoadTrack.Ignore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateCoordinateInfo), typeof(byte), typeof(bool))]
        [HarmonyPatch(typeof(Localize.Translate.Manager), nameof(Localize.Translate.Manager.CreateCoordinateInfo), typeof(byte), typeof(bool), typeof(HumanDataCoordinate.LoadFileInfo.Flags))]
        static void CoordinateLoadAware() => CoordLoadTrack.Mode = CoordLoadTrack.Aware;
    }
    #endregion

    #region Change Coordinate
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordinateTypeChange), nameof(CoordinateTypeChange.ChangeType), typeof(int))]
        static void CoordinateTypeChangeChangeTypePrefix(CoordinateTypeChange __instance, int type) =>
            ChangeCustomCoordinate.OnNext((__instance._human, type));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(ChangeActorCoordinate.OnNext, (__instance.human, (int)type)));
    }
    #endregion

    #region Conversion
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.ConvertHumanDataScene), nameof(SV.ConvertHumanDataScene.Start))]
        static void ConvertHumanDataSceneConvertAsyncPrefix(SV.ConvertHumanDataScene __instance) =>
            (InConversion, CharaLoadTrack.Mode, _) = (true, CharaLoadTrack.FlagIgnore,
                __instance.OnDestroyAsObservable()
                    .Subscribe(_ => (InConversion, CharaLoadTrack.Mode) = (false, CharaLoadTrack.Ignore)));
    }
    #endregion
}