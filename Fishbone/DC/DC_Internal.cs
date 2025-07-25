using HarmonyLib;
using System;
using System.IO;
using System.IO.Compression;
using Character;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Event
    {
        static readonly byte[] NoExtension = new MemoryStream()
            .With(stream =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            }).ToArray();

        static byte[] ToExtension(this Human human) =>
            human.data.PngData.Extract();

        static void UpdateExtension(this Human human, Action<ZipArchive> action) =>
            human.data.PngData = human.ToExtension()
                .UpdateExtension(action)
                .Implant();
    }

    static partial class Hooks
    {
        static int LoadStack = 0;

        #region Save/Load Hooks

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppWriter), typeof(bool))]
        static void HumanDataSaveFilePrefix(HumanData __instance, Il2CppWriter bw) =>
            bw.Write(__instance.PngData);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void LoadCharaFilePostfix(HumanData __instance, LoadFlags flags) =>
            (LoadStack, CharaExtension) = flags switch
            {
                LoadFlags.Craft or LoadFlags.CraftLoad =>
                    (LoadStack + 1, Array.Empty<byte>())
                        .With(Event.NotifyPreDeserialize.Apply(__instance).Apply(CharaExtension)),
                _ => (LoadStack, [])
            };

        #endregion

        #region Human Lifecycle Hooks

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create))]
        static void HumanCreatePostfix(Human __result) =>
            LoadStack = (LoadStack - 1)
                .With(Util.DoNextFrame.Apply(Event.NotifyPostDeserialize.Apply(__result)));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Reload), new Type[0])]
        static void HumanReloadPostfix(Human __instance) =>
            LoadStack = (LoadStack == 0 || __instance.isReloading)
                ? LoadStack
                : (LoadStack - 1)
                    .With(Event.NotifyPostDeserialize.Apply(__instance));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            LoadStack = (LoadStack == 0 || __instance._isReloading)
                ? LoadStack
                : (LoadStack - 1)
                    .With(Event.NotifyPostDeserialize.Apply(__instance._human));

        #endregion

        #region Coordinate Hooks

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateType(HumanCoordinate __instance, ChaFileDefine.CoordinateType type) =>
            (LoadStack == 0)
                .Maybe(Event.NotifyPreCoordinateReload.Apply(__instance.human).Apply((int)type));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), new Type[0])]
        static void HumanReloadCoordinatePostfix(Human __instance) =>
            (LoadStack == 0)
                .Maybe(Event.NotifyPostCoordinateReload.Apply(__instance));

        #endregion

        internal static Action Initialize => InitializeCoordLimits;
    }

    public static partial class Event
    {
        internal static Action<HumanData, byte[]> NotifyPreDeserialize =>
            (data, bytes) => bytes
                .With(data.Implant)
                .ReferenceExtension(OnPreCharacterDeserialize.Apply(data));

        internal static Action<Human> NotifyPostDeserialize =>
            human => human.ReferenceExtension(
                archive => OnPostCharacterDeserialize(human, archive));

        internal static Action<Human, CoordLimit, byte[]> NotifyPreCoordinateDeserialize =>
            (human, limits, bytes) => human.UpdateExtension(
                bytes.ReferenceExtension(
                    OnPreCoordinateDeserialize
                        .Apply(human)
                        .Apply(human.coorde.nowCoordinate)
                        .Apply(limits)));

        internal static Action<Human, CoordLimit, byte[]> NotifyPostCoordinateDeserialize =>
            (human, limits, bytes) => human.UpdateExtension(
                bytes.ReferenceExtension(
                    OnPostCoordinateDeserialize
                        .Apply(human)
                        .Apply(human.coorde.nowCoordinate)
                        .Apply(limits)));

        internal static Action<Human, int> NotifyPreCoordinateReload =>
            (human, type) => human.ReferenceExtension(
                OnPreCoordinateReload.Apply(human).Apply(type));

        internal static Action<Human> NotifyPostCoordinateReload =>
            human => human.ReferenceExtension(
                OnPostCoordinateReload.Apply(human).Apply(human.fileStatus.coordinateType));
    }
}