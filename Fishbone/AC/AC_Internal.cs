using System;
using System.Linq;
using AC.User;
using AC.Scene.Home.UI;
using Character;
using CharacterCreation;
using HarmonyLib;
using CoastalSmell;
using Cysharp.Threading.Tasks;

namespace Fishbone
{
    public static partial class Extension
    {
        internal static event Action<(int, int), (int, int)> OnSwapIndex =
            (src, dst) => Plugin.Instance.Log.LogDebug($"Actor swapped {src} => ${dst}");
        internal static void SwapIndex((int, int) src, (int, int) dst) =>
            OnSwapIndex.Apply(src).Apply(dst).Try(Plugin.Instance.Log.LogError);
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

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth.ClothesCoordeCacheCommand), nameof(HumanCloth.ClothesCoordeCacheCommand.ChangeAll))]
        static void ClothesCoordeCacheCommandChangeAll(HumanCloth.ClothesCoordeCacheCommand __instance) =>
            Extension.ActorChangeCoord(__instance._cloth._human, __instance._cloth._human.data.Status.coordinateType);
    }

    #endregion

    #region Load
    static partial class Hooks
    {
        static event Action<Human> HumanLoadAction = HumanLoadForActors;
        static void HumanLoadForCustom(Human _) { }
        static void HumanLoadForActors(Human human) =>
            Extension.ToActorIndex(human.data, out var index)
                .Maybe(F.Apply(Extension.ResolveHumanToActor, human, index)); 
        internal static void OnEnterCustom() =>
            (CharaLoadHook.LoadFlagResolver, HumanLoadAction) = (CharaLoadHook.CustomFlagResolver, HumanLoadForCustom);
        internal static void OnLeaveCustom() =>
            (CharaLoadHook.LoadFlagResolver, HumanLoadAction) = (CharaLoadHook.DisabledResolver, HumanLoadForActors);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(AC.Scene.ConvertHumanDataScene), nameof(AC.Scene.ConvertHumanDataScene.ConvertAsync))]
        static void ConvertHumanDataSceneConvertAsyncPrefix() => Extension.EnterConversion();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(AC.Scene.ConvertHumanDataScene), nameof(AC.Scene.ConvertHumanDataScene.ConvertAsync))]
        static void ConvertHumanDataSceneConvertAsyncPostfix(ref UniTask __result) =>
            __result = __result.ContinueWith((Action)Extension.LeaveConversion);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Load))]
        static void HumanLoadPrefix(Human __instance) => HumanLoadAction(__instance);

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
        [HarmonyPatch(typeof(RegistrationUI), nameof(RegistrationUI.Swap))]
        static void RegistrationUISwapPostfix(RegistrationUI.SwapData from, RegistrationUI.SwapData to) =>
            Extension.SwapIndex((from.Group, from.DataIndex), (to.Group, to.DataIndex));
    }
    #endregion
}