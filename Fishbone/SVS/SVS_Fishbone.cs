using BepInEx.Unity.IL2CPP;
using System;
using System.IO.Compression;
using Character;
using CharacterCreation;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Event
    {
        public static void HumanCustomReload() =>
            NotifyCharacterDeserialize(HumanCustom.Instance.Human.data, CharaLimit.All, CustomExtension)
                .With(HumanCustom.Instance.Human.Load)
                .With(ResetMotionIK)
                .With(ResetAnimation)(HumanCustom.Instance.Human);

        /// <summary>
        /// Actor to extension conversion for listeners.
        /// </summary>
        public static void ReferenceExtension(this SaveData.Actor actor, Action<ZipArchive> action) =>
            actor.ToExtension().ReferenceExtension(action);

        /// <summary>
        /// Human to extension conversion for listeners.
        /// </summary>
        public static void ReferenceExtension(this Human human, Action<ZipArchive> action) =>
            (human.ToActor(out var actor) ? actor.ToExtension() : CustomExtension).ReferenceExtension(action);

        /// <summary>
        /// Coordinate serialize event.
        /// param1: Serializing coordinate.
        /// param2: Readonly extension from Character Creation storage.
        /// param3: Update mode empty archive.
        /// </summary>
        public static event Action<HumanDataCoordinate, ZipArchive> OnCoordinateSerialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Coordinate Serialize: {data.CoordinateName}");

        /// <summary>
        /// Character serialize event.
        /// param1: Serializing human data.
        /// param2: Update mode extension from Character Creation storage.
        /// </summary>
        public static event Action<HumanData, ZipArchive> OnCharacterSerialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Character Serialize: {data.CharaFileName}");

        /// <summary>
        /// Character deserialize beginning event.
        /// param1: Human data being applied.
        /// param2: Character limits.
        /// param3: Readonly extension from loading character card.
        /// param4: Update mode extension from Character Creation storage.
        /// </summary>
        public static event Action<HumanData, CharaLimit, ZipArchive, ZipArchive> OnPreCharacterDeserialize =
            (_, limit, _, _) => Plugin.Instance.Log.LogDebug($"Pre Character Deserialize: {limit}");

        /// <summary>
        /// Character deserialize complete event.
        /// param1: Human data applied to human.
        /// param2: Character limits.
        /// param3: Readonly extension from loaded character card.
        /// param4: Update mode extension from Character Creation storage.
        /// </summary>
        public static event Action<Human, CharaLimit, ZipArchive, ZipArchive> OnPostCharacterDeserialize =
            (_, limit, _, _) => Plugin.Instance.Log.LogDebug($"Post Character Deserialize: {limit}");

        /// <summary>
        /// Coordinate deserialize beginning event.
        /// param1: Human to apply coordinate.
        /// param2: HumanDataCoordinate being applied.
        /// param3: Coordinate limits.
        /// param4: Readonly extension from loading coordinate card.
        /// param5: Update mode extension from applying human.
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPreCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Deserialize: {human.name}, {limit}");

        /// <summary>
        /// Coordinate deserialize complete event.
        /// param1: Human to apply coordinate.
        /// param2: HumanDataCoordinate being applied.
        /// param3: Coordinate limits.
        /// param4: Readonly extension from loaded coordinate card.
        /// param5: Update mode extension from applying human.
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive, ZipArchive> OnPostCoordinateDeserialize =
            (human, _, limit, _, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Deserialize: {human.name}, {limit}");

        /// <summary>
        /// Coordinate reload beginning event.
        /// param1: Human to apply coordinate.
        /// param2: Changed to coordinate index.
        /// param3: (In Character Creation) update mode extension from Character Creation storage,
        ///         (In Other Scenes) readonly extension of reloading Actor.
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPreCoordinateReload =
            (human, _, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Reload: {human.name}");

        /// <summary>
        /// Coordinate reload complete event.
        /// param1: Human with applied coordinate.
        /// param2: Changed to coordinate index.
        /// param3: Readonly extension of reloaded human.
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPostCoordinateReload =
            (human, _, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Reload: {human.name}");

        /// <summary>
        /// Actor deserialize event.
        /// param1: Actor to deserialize.
        /// param2: Archive to load extension (readonly mode).
        /// </summary>
        public static event Action<SaveData.Actor, ZipArchive> OnActorDeserialize =
            (actor, _) => Plugin.Instance.Log.LogDebug($"Actor Deserialize: {actor.charasGameParam.Index}");

        /// <summary>
        /// Actor serialize event.
        /// param1: Actor to serialize.
        /// param2: Archive to save extension (update mode).
        /// </summary>
        public static event Action<SaveData.Actor, ZipArchive> OnActorSerialize =
            (actor, _) => Plugin.Instance.Log.LogDebug($"Actor Serialize: {actor.charasGameParam.Index}");

        /// <summary>
        /// Actor binding to human event (pre).
        /// param1: Binding actor.
        /// param2: Archive to load extension.
        /// </summary>
        public static event Action<SaveData.Actor, HumanData, ZipArchive> OnPreActorHumanize =
            (actor, _, _) => Plugin.Instance.Log.LogDebug($"Pre Actor Humanized: {actor.charasGameParam.Index}");

        /// <summary>
        /// Actor binding to human event (post).
        /// param1: Binding actor.
        /// param2: Bound human.
        /// param3: Archive to load extension.
        /// </summary>
        public static event Action<SaveData.Actor, Human, ZipArchive> OnPostActorHumanize =
            (actor, human, _) => Plugin.Instance.Log.LogDebug($"Post Actor Humanized: {actor.charasGameParam.Index}, {human.name}");
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
    }
}