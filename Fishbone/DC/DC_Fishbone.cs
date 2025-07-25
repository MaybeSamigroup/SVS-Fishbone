using BepInEx.Unity.IL2CPP;
using System;
using System.IO.Compression;
using Character;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
    public static partial class Event
    {
        /// <summary>
        /// Provides readonly access to a human's extension archive.
        /// </summary>
        public static void ReferenceExtension(this Human human, Action<ZipArchive> action) =>
            human.ToExtension().ReferenceExtension(action);

        /// <summary>
        /// Raised before character deserialization.
        /// param1: HumanData being applied
        /// param2: Readonly extension from loading character card
        /// </summary>
        public static event Action<HumanData, ZipArchive> OnPreCharacterDeserialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Pre Character Deserialize: {data?.PngData?.Length ?? 0}");

        /// <summary>
        /// Raised after character deserialization.
        /// param1: Human instance
        /// param2: Readonly extension from loaded character card
        /// </summary>
        public static event Action<Human, ZipArchive> OnPostCharacterDeserialize =
            (human, _) => Plugin.Instance.Log.LogDebug($"Post Character Deserialize: {human.name}, {human.data?.PngData?.Length ?? 0}");

        /// <summary>
        /// Raised before coordinate deserialization.
        /// param1: Human to apply coordinate
        /// param2: HumanDataCoordinate being applied
        /// param3: Coordinate limits
        /// param4: Readonly extension from loading coordinate card
        /// param5: Update extension from applying human
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPreCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Deserialize: {human.name}, {limit}");

        /// <summary>
        /// Raised after coordinate deserialization.
        /// param1: Human to apply coordinate
        /// param2: HumanDataCoordinate being applied
        /// param3: Coordinate limits
        /// param4: Readonly extension from loaded coordinate card
        /// param5: Update extension from applying human
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPostCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Deserialize: {human.name}, {limit}");

        /// <summary>
        /// Raised before coordinate reload.
        /// param1: Human to apply coordinate
        /// param2: Changed to coordinate index
        /// param3: Readonly extension of reloading human
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPreCoordinateReload =
            (human, type, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Reload: {human.name}/{type}");

        /// <summary>
        /// Raised after coordinate reload.
        /// param1: Human with applied coordinate
        /// param2: Changed to coordinate index
        /// param3: Readonly extension of reloaded human
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPostCoordinateReload =
            (human, type, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Reload: {human.name}/{type}");
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}