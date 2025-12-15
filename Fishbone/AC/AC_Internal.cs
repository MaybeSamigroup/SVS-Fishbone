using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Cysharp.Threading.Tasks;
using AC.User;
using AC.Scene.Home.UI;
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
        static void HumanDataSaveCharaFileBeforeAction(string path) => SaveCustomChara.OnNext(path);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile))]
        static void HumanDataCoordinateSaveFilePostfix(string path) => SaveCustomCoord.OnNext(path);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.SerializeHumanData))]
        static void ActorSerializeHumanDataPrefix(ActorData __instance) => SaveActor.OnNext(__instance);
    }
    #endregion

    #region Load Chara
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Load))]
        static void HumanLoadPrefix(Human __instance) => HumanResolve.OnNext(__instance);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData), nameof(SaveData.Load))]
        static void SaveDataLoadPrefix() => Plugin.Instance.Log.LogInfo("save data load");

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.DeserializeHumanData))]
        static void ActorDataDeserializeHumanDataPrefix() => CharaLoadTrack.Mode = CharaLoadTrack.FlagIgnore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.DeserializeHumanData))]
        static void ActorDataDeserializeHumanDataPostfix(ActorData __instance) =>
            (CharaLoadTrack.Mode = CharaLoadTrack.Ignore).With(F.Apply(ActorResolve.OnNext, __instance));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanSelectUI), nameof(HumanSelectUI.Open))]
        static void HumanSelectUIOpenPostfix() => CharaLoadTrack.Mode = CharaLoadTrack.FlagIgnore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanSelectUI), nameof(HumanSelectUI.Close))]
        static void HumanSelectUIClosePrefix() => CharaLoadTrack.Mode = CharaLoadTrack.Ignore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(ActorData), nameof(ActorData.HumanData), MethodType.Setter)]
        static void ActorDataHumanDataSetPostfix(ActorData __instance) => ActorResolve.OnNext(__instance);
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
        [HarmonyPatch(typeof(HumanCloth.ClothesCoordeCacheCommand), nameof(HumanCloth.ClothesCoordeCacheCommand.ChangeAll))]
        static void ClothesCoordeCacheCommandChangeAll(HumanCloth.ClothesCoordeCacheCommand __instance) =>
            ChangeActorCoordinate.OnNext((__instance._cloth._human, __instance._cloth._human.data.Status.coordinateType));
    }
    #endregion

    #region Conversion
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(AC.Scene.ConvertHumanDataScene), nameof(AC.Scene.ConvertHumanDataScene.ConvertAsync))]
        static void ConvertHumanDataSceneConvertAsyncPrefix() => EnterConversion.OnNext(Unit.Default);

        [HarmonyPatch(typeof(AC.Scene.ConvertHumanDataScene), nameof(AC.Scene.ConvertHumanDataScene.ConvertAsync))]
        static void ConvertHumanDataSceneConvertAsyncPostfix(ref UniTask __result) =>
            __result = __result.ContinueWith(F.Apply(LeaveConversion.OnNext, Unit.Default));
    }
    #endregion

    #region Swap Actor
    static partial class Hooks
    {
        static Subject<((int, int), (int, int))> SwapActor = new();
        internal static IObservable<((int, int), (int, int))> OnSwapActor => SwapActor.AsObservable();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(RegistrationUI), nameof(RegistrationUI.Swap))]
        static void RegistrationUISwapPostfix(RegistrationUI.SwapData from, RegistrationUI.SwapData to) =>
            SwapActor.OnNext(((from.Group, from.DataIndex), (to.Group, to.DataIndex)));
    }
    internal static partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void SwapChara(((int, int), (int, int)) tuple) =>
            Charas.Remove(tuple.Item1, out var chara).Maybe(() => Charas[tuple.Item2] = chara);
        internal static void SwapCoord(((int, int), (int, int)) tuple) =>
            Coords.Remove(tuple.Item1, out var coord).Maybe(() => Coords[tuple.Item2] = coord);
        internal static void Initialize() =>
            Enumerable.Range(1, 3).ForEach(index => Coords[(-1, index)] = (Charas[(-1, index)] = new()).Get(0));
    }
    internal static partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Swap(((int, int), (int, int)) tuple) =>
            Charas.Remove(tuple.Item1, out var chara).Maybe(() => Charas[tuple.Item2] = chara);
        internal static void Initialize() =>
            Enumerable.Range(1, 3).ForEach(index => Charas[(-1, index)] = new());
    }
    #endregion
}