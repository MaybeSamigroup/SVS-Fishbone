using System.IO.Compression;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using Character;
using HarmonyLib;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;

namespace Fishbone
{
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppWriter), typeof(bool))]
        static void HumanDataSaveFilePrefix(HumanData __instance, Il2CppWriter bw) =>
            bw.Write(__instance.PngData);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(DigitalCraft.SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPrefix() => CoordLoadHook = CoordLoadHook.Skip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(DigitalCraft.SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPostfix() => CoordLoadHook = new CoordLoadWait();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(DigitalCraft.DigitalCraft), nameof(DigitalCraft.DigitalCraft.SaveScene))]
        static void DigitalCraftSaveScenePrefix() =>
            Extension.SaveScene();
    }

    public static partial class Extension
    {
        internal static void SaveScene() =>
            Human.list.Yield().ForEach(Save);

        internal static void Save(Human human) =>
            Implant(human.data, ToBinary(OnSaveChara.Apply(human)));
    }
    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type).Maybe(F.Apply(Extension.LoadCoord, __instance.human));
    }

    internal static class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        internal static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());

        internal static U Coord(Human human) =>
            Characters.GetValueOrDefault(human, new T()).Get(human.data.Status.coordinateType);

        internal static void Chara(Human human, T mods) =>
            Characters[human] = mods;

        internal static void Coord(Human human, U mods) =>
            Characters[human] = Chara(human).Merge(human.data.Status.coordinateType, mods);

        internal static void SaveChara(Human human, ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Characters.GetValueOrDefault(human, new()));

        internal static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable()
                .Subscribe(F.Apply(Characters.Remove, human).Ignoring().Ignoring<Unit>());

        internal static void LoadChara(Human human, CharaLimit limit, T value) =>
            Characters[human] = Characters.GetValueOrDefault(human, new()).Merge(limit, value);

        internal static void LoadCoord(Human human, CoordLimit limit, U value) =>
            Characters[human] = Characters.GetValueOrDefault(human, new()).Merge(human.data.Status.coordinateType, limit, value);

        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveHuman += (human, limit) => LoadChara(human, limit, value);

        internal static void JoinLimitTrack(CoordTrack track, U value) =>
            track.OnResolve += (human, limit) => LoadCoord(human, limit, value);
    }

    internal static class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        internal static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());

        internal static void Chara(Human human, T mods) =>
            Characters[human] = mods;

        internal static void SaveChara(Human human, ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Characters.GetValueOrDefault(human, new()));

        internal static void LoadChara(Human human, CharaLimit limit, T value) =>
            Characters[human] = Characters.GetValueOrDefault(human, new()).Merge(limit, value);

        internal static void JoinCopyTrack(CopyTrack track, T value) =>
            track.OnResolveHuman += (human, limit) => LoadChara(human, limit, value);
    }
}