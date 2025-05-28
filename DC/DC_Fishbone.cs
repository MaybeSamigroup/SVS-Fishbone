using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using Character;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;

namespace Fishbone
{
    public static partial class Event
    {
        /// <summary>
        /// default data for character card without extension
        /// </summary>
        static readonly byte[] NoExtension = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();
        public static ZipArchive ToArchive(this Human human) =>
            human.ToExtension().ToArchive();
        static byte[] ToExtension(this Human human) =>
            human.data.PngData.Extract();
        static void UpdateExtension(this Human human, Action<ZipArchive> action) =>
            human.data.PngData = human.ToExtension().UpdateExtension(action).Implant();
    }
    static partial class Hooks
    {
        /// <summary>
        /// force to write png data on character serialize
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="bw"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppWriter), typeof(bool))]
        static void HumanDataSaveFilePrefix(HumanData __instance, Il2CppWriter bw) =>
            bw.Write(__instance.PngData);
        static int LoadStack = 0;
        /// <summary>
        /// capture character deserialize begining from card and duplication
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="flags"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void LoadCharaFilePostfix(HumanData __instance, LoadFlags flags) =>
            (LoadStack, CharaExtension) = (LoadStack + 1, flags switch
            {
                LoadFlags.Craft or LoadFlags.CraftLoad => Array.Empty<byte>()
                    .With(CharaExtension.Curry(__instance.NotifyDeserialize)),
                _ => Array.Empty<byte>()
            });
        /// <summary>
        /// chapture character deserialize complete from card
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.Create))]
        static void HumanCreatePostfix(Human __result) =>
            LoadStack = (LoadStack - 1).With(__result.NotifyDeserialize);
        /// <summary>
        /// capture human reloading complete.
        /// only notified to listeners when human data updated.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            LoadStack = LoadStack == 0 || __instance._isReloading ?
                LoadStack : (LoadStack - 1).With(__instance._human.NotifyDeserialize);
        /// <summary>
        /// capture coordinate reload begining
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateType(HumanCoordinate __instance, ChaFileDefine.CoordinateType type) =>
            (LoadStack == 0).Maybe(() => __instance.human.NotifyPreCoordinateReload((int)type));
    }
    public static partial class Event
    {
        /// <summary>
        /// notify begining of character deserialize to listeners
        /// </summary>
        /// <param name="data"></param>
        /// <param name="bytes"></param>
        internal static void NotifyDeserialize(this HumanData data, byte[] bytes) =>
            OnPreCharacterDeserialize(data, bytes.With(data.Implant).ToArchive());
        /// <summary>
        /// notify complete of character deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyDeserialize(this Human human) =>
            OnPostCharacterDeserialize(human, human.ToArchive());
        /// <summary>
        /// notify begining of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static void NotifyPreCoordinateDeserialize(this Human human, CoordLimit limits, byte[] bytes) =>
            human.UpdateExtension(storage => OnPreCoordinateDeserialize(human, human.coorde.nowCoordinate, limits, bytes.ToArchive(), storage));
        /// <summary>
        /// notify complete of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static void NotifyPostCoordinateDeserialize(this Human human, CoordLimit limits, byte[] bytes) =>
            human.UpdateExtension(storage => OnPostCoordinateDeserialize(human, human.coorde.nowCoordinate, limits, bytes.ToArchive(), storage));
        /// <summary>
        /// notify begining of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyPreCoordinateReload(this Human human, int type) =>
            OnPreCoordinateReload(human, type, human.ToArchive());
        /// <summary>
        /// notify complete of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyPostCoordinateReload(this Human human) =>
            OnPostCoordinateReload(human, human.fileStatus.coordinateType, human.ToArchive());
        /// <summary>
        /// character deserialize begining event.
        /// param1: human data applying to human
        /// param2: readonly mode extension from loading character card
        public static event Action<HumanData, ZipArchive> OnPreCharacterDeserialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Pre Character Deserialize: {data?.PngData?.Length ?? 0}");
        /// <summary>
        /// character deserialize complete event.
        /// param1: human data applied to human
        /// param2: readonly mode extension from loaded character card
        /// </summary>
        public static event Action<Human, ZipArchive> OnPostCharacterDeserialize =
            (human, _) => Plugin.Instance.Log.LogDebug($"Post Character Deserialize: {human.name}, {human.data?.PngData?.Length ?? 0}");
        /// <summary>
        /// coodinate deserialize begining event
        /// param1: human to apply coordinate
        /// param2: human data coordinate to applying
        /// param3: coordinate limits
        /// param4: readonly mode extension from loading coordinate card
        /// param5: update mode extension from applying human
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPreCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Deserialize: {human.name}, {limit}");
        /// <summary>
        /// coordinate deserialize complete event
        /// param1: human to apply coordinate
        /// param2: human data coordinate to applying
        /// param3: coordinate limits
        /// param4: readonly mode extension from loaded coordinate card
        /// param5: update mode extension from applying human
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPostCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Deserialize: {human.name}, {limit}");
        /// <summary>
        ///  coordinate reload begining event
        /// param1: human to apply coordinate
        /// param2: chenged to coordinate index
        /// param3: readonly mode extension of reloading human
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPreCoordinateReload =
            (human, type, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Reload: {human.name}/{type}");
        /// <summary>
        /// coordinate reload complete event
        /// param1: coordinate applied human
        /// param2: chenged to coordinate index
        /// param3: readonly mode extension of reloaded human
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPostCoordinateReload =
            (human, type, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Reload: {human.name}/{type}");
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "DigitalCraft";
        public const string Name = "Fishbone";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.0.0";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks").With(() => Instance = this);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}