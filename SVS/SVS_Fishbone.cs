using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Manager;
using Character;
using CharacterCreation;
using SV.CoordeSelectScene;
using ILLGames.Extensions;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
    public static partial class Event
    {
        /// <summary>
        /// default data for character card without extension
        /// </summary>
        static readonly byte[] NoExtension = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();
        /// <value>
        /// static extension storage during Character Creation
        /// </value>
        static byte[] CustomExtension = NoExtension;
        static void UpdateExtension(Action<ZipArchive> action) =>
            CustomExtension = CustomExtension.UpdateExtension(action);
        /// <summary>
        /// extract extension data from character card.
        /// if failes, falls back to legacy game parameter image extraction.
        /// </summary>
        /// <param name="data">human data which associated with character card</param>
        /// <param name="bytes">intercepted png data during human data loading sequence</param>
        /// <returns>extension data</returns>
        internal static byte[] Extract(this HumanData data, byte[] bytes) =>
            bytes.Length > 0 ? bytes : data?.GameParameter?.imageData?.Extract() ?? NoExtension;
        internal static byte[] Extract(this HumanData data) =>
            data.Extract(data?.PngData?.Extract() ?? []);
    }
    static partial class Hooks
    {
        /// <summary>
        /// capture coordinate save operation and overrite extension data
        /// </summary>
        /// <param name="coordinate"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile))]
        static void HumanDataCoordinateSaveFilePostfix(HumanDataCoordinate __instance, string path) =>
            __instance.NotifyCoordinateSerialize(path);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify coordinate serialization to listeners
        /// </summary>
        /// <param name="data">coordinate data to serialize</param>
        /// <param name="path">coordinate card path to serialize</param>
        internal static void NotifyCoordinateSerialize(this HumanDataCoordinate data, string path) =>
            File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(NoExtension.UpdateExtension(archive => OnCoordinateSerialize(data, archive))));
        /// <summary>
        /// coordinate serialize event:
        /// param1: serializing coordinate
        /// param2: readonly mode extension from Character Creation storage
        /// param3: update mode empty archive
        /// </summary>
        public static event Action<HumanDataCoordinate, ZipArchive> OnCoordinateSerialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Coordinate Serialize: {data.CoordinateName}");
    }
    static partial class Hooks
    {
        /// <summary>
        /// capture character save operation and implant extension data
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveCharaFileBeforeAction))]
        static void HumanDataSaveCharaFileBeforeAction(HumanData __instance, string path) =>
            __instance.NotifyCharacterSerialize(path);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify character serialize to listeners
        /// </summary>
        /// <param name="data"></param>
        internal static void NotifyCharacterSerialize(this HumanData data, string path) =>
            File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(CustomExtension.UpdateExtension(archive => OnCharacterSerialize(data, archive))));
        /// <summary>
        /// character serialize event
        /// param1: serializing human data
        /// param2: update mode extension from Character Creation storage
        /// </summary>
        public static event Action<HumanData, ZipArchive> OnCharacterSerialize =
            (data, _) => Plugin.Instance.Log.LogDebug($"Character Serialize: {data.CharaFileName}");
    }
    static partial class Hooks
    {
        internal static Action<Human> DoNothing = _ => { };
        static Action<Human> OnHumanReloading = DoNothing;
        /// <summary>
        /// capture human reloading complete.
        /// only notified to listeners when human data updated.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            OnHumanReloading = !__instance._isReloading ?
                OnHumanReloading : DoNothing.With(__instance._human.Curry(OnHumanReloading));
        /// <summary>
        /// capture initial loading complete.
        /// only notified to listeners when Character Creation scene loaded and human ready.
        /// </summary>
        /// <param name="maxValue"></param>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeMouthOpenMax))]
        static void HumanCustomChangeMouthOpenMaxPostfix(float maxValue, HumanFace __instance) =>
            OnHumanReloading = (__instance.human.isReloading || maxValue != 0.0) ?
                OnHumanReloading : DoNothing.With(__instance.human.Curry(OnHumanReloading));
        /// <summary>
        /// override load flag to force game parameter image loading.
        /// remains for legacy format character card.
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CheckFlags))]
        static void HumanDataLoadFileLimited(ref HumanData.LoadFileInfo.Flags __result) =>
            __result |= HumanData.LoadFileInfo.Flags.GameParam;
        /// <summary>
        /// human data copy action except Character Creation.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        /// <returns>action to do when reloading complete</returns>
        static Func<HumanData, HumanData, Action<Human>> HumanDataCopySkip = (_, _) => DoNothing;
        /// <summary>
        /// human data copy action during Character Creation.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        /// <returns>action to do when reloading complete</returns>
        static Action<Human> HumanDataCopyProc(HumanData dst, HumanData src) =>
            src == HumanCustom.Instance?.DefaultData ? dst.NotifyCharacterInitialize() :
            src == HumanCustom.Instance?.Received?.HumanData ? dst.NotifyActorDeserializeToCharacter() :
            dst == HumanCustom.Instance?.EditHumanData ? DoNothing.With(src.NotifyCharacterSerializeToActor) : DoNothing;
        static Func<HumanData, HumanData, Action<Human>> OnHumanDataCopy = HumanDataCopySkip;
        /// <summary>
        /// capture character card loading caused by file selection.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        /// <param name="flags"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        static void HumanDataCopyLimitedPrefix(HumanData dst, HumanData src, CharaLimit flags) =>
            OnHumanReloading = dst.NotifyPreCharacterDeserialize(flags, src.Extract(CharaExtension));
        /// <summary>
        /// capture character card loading caused by Character Creation scene loading or resetting.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyPostfix(HumanData dst, HumanData src) =>
            OnHumanReloading = OnHumanDataCopy(dst, src);
        /// <summary>
        /// switching human data copy actions
        /// </summary>
        internal static void InitializeHookSwitch() =>
            Util.Hook<HumanCustom>(
                () => (OnHumanDataCopy, OnCoordinateTypeChange, OnCoordinateTypeChangeProc) =
                        (HumanDataCopyProc, OnCoordinateTypeChangeSkip, OnCoordinateTypeChangeSkip),
                () => (OnHumanDataCopy, OnCoordinateTypeChange, OnCoordinateTypeChangeProc) =
                        (HumanDataCopySkip, Event.NotifyPreActorCoordinateReload, Event.NotifyPreActorCoordinateReload));
    }
    public static partial class Event
    {
        /// <summary>
        /// storage in game actor edited in Character Creation
        /// </summary>
        static SaveData.Actor GetHumanCustomTarget =>
            Game.Charas._entries.Select(entry => entry.value)
                .First(actor => actor.charFile == HumanCustom.Instance.Received.HumanData);
        /// <summary>
        /// notify complete of character deserialize to listeners.
        /// </summary>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        /// <returns>action to do when deserialize complete</returns>
        internal static Action<Human> NotifyPreCharacterDeserialize(this HumanData data, CharaLimit limits, byte[] bytes) =>
            NotifyPostCharacterDeserialize(limits, bytes)
                .With(data.NotifyPreCharacterDeserialize(limits, bytes.ToArchive()).Curry(UpdateExtension));
        static Action<Human> NotifyPostCharacterDeserialize(CharaLimit limits, byte[] bytes) =>
            human => UpdateExtension(storage => OnPostCharacterDeserialize(human, limits, bytes.ToArchive(), storage));
        static Action<ZipArchive> NotifyPreCharacterDeserialize(this HumanData data, CharaLimit limits, ZipArchive archive) =>
            storage => OnPreCharacterDeserialize(data, limits, archive, storage);
        /// <summary>
        /// notify begining of default character loading (initail or resetting) to listeners.
        /// </summary>
        /// <param name="data"></param>
        /// <returns>action to do when deserialize complete</returns>
        internal static Action<Human> NotifyCharacterInitialize(this HumanData data) =>
            NotifyPostCharacterDeserialize(CharaLimit.All, NoExtension)
                .With(data.NotifyPreCharacterDeserialize(CharaLimit.All, NoExtension.ToArchive()).Curry(UpdateExtension));
        /// <summary>
        /// notify begining of in game actor deserialize (initial or resetting) to listeners.
        /// </summary>
        /// <param name="data"></param>
        /// <returns>action to do when deserialize complete</returns>
        internal static Action<Human> NotifyActorDeserializeToCharacter(this HumanData data) =>
            GetHumanCustomTarget.NotifyActorDeserializeToCharacter(data);
        static Action<ZipArchive> NotifyActorDeserializeToCharacter(this SaveData.Actor actor, HumanData data, ZipArchive archive) =>
            storage => OnActorDeserializeToCharacter(actor, data, archive, storage);
        static Action<Human> NotifyActorDeserializeToCharacter(this SaveData.Actor actor, HumanData data) =>
            NotifyPostCharacterDeserialize(CharaLimit.All, actor.ToExtension())
                .With(actor.NotifyActorDeserializeToCharacter(data, actor.ToArchive()).Curry(UpdateExtension));
        /// <summary>
        /// notify in game actor serialize (reflection) to listeners.
        /// </summary>
        /// <param name="data"></param>
        internal static void NotifyCharacterSerializeToActor(this HumanData data) =>
            GetHumanCustomTarget.NotifyCharacterSerializeToActor(data);
        static void NotifyCharacterSerializeToActor(this SaveData.Actor actor, HumanData data) =>
            actor.UpdateExtension(storage => OnCharacterSerializeToActor(actor, data, actor.ToArchive(), storage));
        /// <summary>
        /// character deserialize begining event.
        /// param1: human data applying to human
        /// param2: character limits
        /// param3: readonly mode extension from loading character card
        /// param4: update mode extension from Character Creation storage
        /// </summary>
        public static event Action<HumanData, CharaLimit, ZipArchive, ZipArchive> OnPreCharacterDeserialize =
            (_, limit, _, _) => Plugin.Instance.Log.LogDebug($"Pre Character Deserialize: {limit}");
        /// <summary>
        /// character deserialize complete event.
        /// param1: human data applied to human
        /// param2: character limits
        /// param3: readonly mode extension from loaded character card
        /// param4: update mode extension from Character Creation storage
        /// </summary>
        public static event Action<Human, CharaLimit, ZipArchive, ZipArchive> OnPostCharacterDeserialize =
            (_, limit, _, _) => Plugin.Instance.Log.LogDebug($"Post Character Deserialize: {limit}");
        /// <summary>
        /// actor deserialize begining event.
        /// param1: save data actor deserialized from
        /// param2: human data deserialized to
        /// param3: readonly mode extension from loading character card
        /// param4: update mode extension from Character Creation storage
        /// </summary>
        public static event Action<SaveData.Actor, HumanData, ZipArchive, ZipArchive> OnActorDeserializeToCharacter =
            (actor, _, _, _) => Plugin.Instance.Log.LogDebug($"Actor Deserialzie To Character: {actor.charasGameParam.Index}");
        /// <summary>
        /// actor serialize event.
        /// param1: save data actor serialized to
        /// param2: human data serialized from
        /// param3: readonly mode original actor extension
        /// param4: update mode extension from Simulation Scene storage
        /// </summary>
        public static event Action<SaveData.Actor, HumanData, ZipArchive, ZipArchive> OnCharacterSerializeToActor =
            (actor, _, _, _) => Plugin.Instance.Log.LogDebug($"Character Serialize To Actor: {actor.charasGameParam.Index}");
    }
    static partial class Hooks
    {
        /// <summary>
        /// capture coordinate reload begining during Character Creation
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordinateTypeChange), nameof(CoordinateTypeChange.ChangeType))]
        static void CoordinateTypeChangeChangeTypePrefix(CoordinateTypeChange __instance, int type) =>
            __instance._human.NotifyPreCoordinateReload(type);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify begining of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static void NotifyPreCoordinateDeserialize(this Human human, CoordLimit limits, byte[] bytes) =>
            OnPreCoordinateDeserialize(human, human.coorde.nowCoordinate, limits, bytes.ToArchive());
        /// <summary>
        /// notify complete of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static void NotifyPostCoordinateDeserialize(this Human human, CoordLimit limits, byte[] bytes) =>
            OnPostCoordinateDeserialize(human, human.coorde.nowCoordinate, limits, bytes.ToArchive());
        /// <summary>
        /// notify begining of coordinate reload
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyPreCoordinateReload(this Human human, int type) =>
            UpdateExtension(archive => OnPreCoordinateReload(human, type, archive));
        /// <summary>
        /// coodinate deserialize begining event
        /// param1: human to apply coordinate
        /// param2: human data coordinate to applying
        /// param3: coordinate limits
        /// param4: readonly mode extension from loading coordinate card
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive> OnPreCoordinateDeserialize =
            (human, _, limit, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Deserialize: {human.name}, {limit}");
        /// <summary>
        /// coordinate deserialize complete event
        /// param1: human to apply coordinate
        /// param2: human data coordinate to applying
        /// param3: coordinate limits
        /// param4: readonly mode extension from loaded coordinate card
        /// </summary>
        public static event Action<Human, HumanDataCoordinate, CoordLimit, ZipArchive> OnPostCoordinateDeserialize =
            (human, _, limit, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Deserialize: {human.name}, {limit}");
        /// <summary>
        ///  coordinate reload begining event
        /// param1: human to apply coordinate
        /// param2: chenged to coordinate index
        /// param3: (In Character Creation) update mode extension from Character Creation storage
        ///         (In Other Scenes) readonly mode extension of reloading Actor
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPreCoordinateReload =
            (human, _, _) => Plugin.Instance.Log.LogDebug($"Pre Coordinate Reload: {human.name}");
    }
    public static partial class Event
    {
        /// <summary>
        /// static extension storage during Simulation Scene
        /// </summary>
        static Dictionary<int, byte[]> ActorExtensions = new();
        /// <summary>
        /// extract extension data from character card.
        /// if failes, falls back to legacy game parameter image extraction.
        /// </summary>
        /// <param name="actor">save data actor which associated with character card</param>
        /// <param name="bytes">intercepted png data during human data loading sequence</param>
        /// <returns>extension data</returns>
        internal static byte[] Extract(this SaveData.Actor actor, byte[] bytes) =>
            bytes.Length > 0 ? bytes : actor?.gameParameter?.imageData?.Extract() ?? NoExtension;
        internal static byte[] Extract(this SaveData.Actor actor) =>
            actor.Extract(actor.charFile.Extract());
        /// <summary>
        ///  actor to extension transform memoized by static storage.
        /// </summary>
        /// <param name="actor">save data actor to exitension restore from</param>
        /// <returns>extension data</returns>
        static byte[] ToExtension(this SaveData.Actor actor) =>
            ActorExtensions.TryGetValue(actor.charasGameParam.Index, out var bytes)
                ? bytes : ActorExtensions[actor.charasGameParam.Index] = actor.Extract();
        static TalkManager.TaskCharaInfo[] TaskCharaInfos =>
            TalkManager.Instance == null ? [] : [
                TalkManager.Instance?.playerCharaInfo,
                TalkManager.Instance?.npcCharaInfo1,
                TalkManager.Instance?.npcCharaInfo2,
                TalkManager.Instance?.npcCharaInfo3
            ];
        /// <summary>
        /// human to actor transform
        /// lookup for high poly human then falls back to low poly human.
        /// </summary>
        internal static bool ToActor(this Human human, out SaveData.Actor actor) =>
            null != (actor =
                TaskCharaInfos.Where(info => info?.chaCtrl == human)
                    .Select(info => info.actor).FirstOrDefault() ??
                Game.Charas?._entries
                    ?.Where(entry => entry?.value?.chaCtrl == human)
                    ?.Select(entry => entry.value)?.FirstOrDefault() ??
                Game.Charas?._entries?.First(entry => entry.value.IsPC)?.value);
        /// <summary>
        /// actor to extension conversion for listeners
        /// </summary>
        /// <param name="actor">save data actor to exitension restore from</param>
        /// <returns>archive to load extension (readonly)</returns>
        public static ZipArchive ToArchive(this SaveData.Actor actor) =>
            actor.ToExtension().ToArchive();
        /// <summary>
        /// human to extension conversion for listeners
        /// </summary>
        /// <param name="actor">human to exitension restore from</param>
        /// <returns>archive to load extension (readonly)</returns>
        public static ZipArchive ToArchive(this Human human) =>
            human.ToActor(out var actor) ? actor.ToArchive() : CustomExtension.ToArchive();
        /// <summary>
        /// do extension update operation and store back resulting extension.
        /// </summary>
        /// <param name="data">save data actor hich associated with extension</param>
        /// <param name="action">extension update operation</param>
        static void UpdateExtension(this SaveData.Actor actor, Action<ZipArchive> action) =>
             actor.charFile.Implant(ActorExtensions[actor.charasGameParam.Index] = actor.ToExtension().UpdateExtension(action));
    }
    static partial class Hooks
    {
        /// <summary>
        /// capture actor deserialize at Simulation Entry Scene
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        static void WorldLoadPostfix(SaveData.WorldData __result) =>
            __result.Charas._entries
                .Where(entry => entry != null && entry.value != null)
                .Do(entry => entry.value.NotifyActorDeserialize());
        /// <summary>
        /// capture actor entry from character card at Simulation Entry Scene
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(SaveData.Actor __result) =>
            __result.NotifyActorDeserialize();
    }
    public static partial class Event
    {
        /// <summary>
        /// notify actor deserialize to listeners
        /// </summary>
        /// <param name="actor"></param>
        internal static void NotifyActorDeserialize(this SaveData.Actor actor) =>
            OnActorDeserialize(actor, (ActorExtensions[actor.charasGameParam.Index] = actor.Extract()).ToArchive());
        /// <summary>
        /// actor deserialize event
        /// param1: actor to deserialize
        /// param2: archive to load extension (readonly mode)
        /// </summary>
        public static event Action<SaveData.Actor, ZipArchive> OnActorDeserialize =
            (actor, _) => Plugin.Instance.Log.LogDebug($"Actor Deserialize: {actor.charasGameParam.Index}");
    }
    static partial class Hooks
    {
        /// <summary>
        /// capture actor serialize at Simulation Entry Scene
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Save), typeof(string))]
        static void WorldSavePrefix(SaveData.WorldData __instance) =>
            __instance.Charas
                ._entries.Where(entry => entry != null && entry.value != null)
                .Do(entry => entry.value.NotifyActorSerialize());
    }
    public static partial class Event
    {
        /// <summary>
        /// notify actor serialize to listeners
        /// </summary>
        /// <param name="actor"></param>
        internal static void NotifyActorSerialize(this SaveData.Actor actor) =>
            actor.UpdateExtension(archive => OnActorSerialize(actor, archive));
        /// <summary>
        /// actor serialize event
        /// param1: actor to serialize
        /// param2: arichive to save extension (update mode)
        /// </summary>
        public static event Action<SaveData.Actor, ZipArchive> OnActorSerialize =
            (actor, _) => Plugin.Instance.Log.LogDebug($"Actor Serialize: {actor.charasGameParam.Index}");
    }
    static partial class Hooks
    {
        static Action<Human, int> OnCoordinateTypeChangeSkip = (_, _) => { };
        static Action<Human, int> OnCoordinateTypeChangeProc = Event.NotifyPreActorCoordinateReload;
        static Action<Human, int> OnCoordinateTypeChange = Event.NotifyPreActorCoordinateReload;
        /// <summary>
        /// capture bigining of actor binding to low poly human at Simulation Scene
        /// </summary>
        /// <param name="_ai"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Chara.Base), nameof(SV.Chara.Base.SetActive))]
        static void SVCharaBaseSetActivePrefix(SV.Chara.Base __instance) =>
            (OnCoordinateTypeChange, OnHumanDataCopy) = __instance.charaData == null
                ? (OnCoordinateTypeChangeProc, HumanDataCopySkip)
                : (OnCoordinateTypeChangeSkip, __instance.charaData.NotifyPreActorHumanize);
        /// <summary>
        /// capture bigining of actor binding to high poly human at Simulation Scene
        /// </summary>
        /// <param name="_ai"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadPlayerHighPoly))]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadHighPoly))]
        static void SVTalkTalkTaskBaseLoadHighPolyPrefix(SV.Chara.AI _ai) =>
            (OnCoordinateTypeChange, OnHumanDataCopy) = _ai?.charaData?.charasGameParam?.Index == null
             ? (OnCoordinateTypeChangeProc, HumanDataCopySkip) :
               (OnCoordinateTypeChangeSkip, _ai.charaData.NotifyPreActorHumanize);
        static Action<Human> NotifyPreActorHumanize(this SaveData.CharaData charaData, HumanData dst, HumanData src) =>
            DoNothing.With(dst.Curry(Game.Charas[charaData.charasGameParam.Index].NotifyPreActorHumanize));
        /// <summary>
        /// capture actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.CharaData), nameof(SaveData.CharaData.SetRoot))]
        static void SaveDataCharaDataSetRootPostfix(SaveData.CharaData __instance) =>
            (__instance.chaCtrl != null).Maybe(__instance.NotifyPostActorHumanize);
        static void NotifyPostActorHumanize(this SaveData.CharaData charaData) =>
            ((OnCoordinateTypeChange, OnHumanDataCopy) = (OnCoordinateTypeChangeProc, HumanDataCopySkip))
                .With(charaData.chaCtrl.Curry(Game.Charas[charaData.charasGameParam.Index].NotifyPostActorHumanize));
        /// <summary>
        /// capture coordinate reload begining
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateType(HumanCoordinate __instance, ChaFileDefine.CoordinateType type) =>
            OnCoordinateTypeChange(__instance.human, (int)type);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordeSelect), nameof(CoordeSelect.PlayAnimation))]
        static void CoordeSelectPlayAnimationPostfix(CoordeSelect __instance) =>
            __instance._hiPoly.NotifyPostCoordinateReload();
    }
    public static partial class Event
    {
        /// <summary>
        /// notify begining of actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="data"></param>
        internal static void NotifyPreActorHumanize(this SaveData.Actor actor, HumanData data) =>
            OnPreActorHumanize(actor, data, actor.ToArchive());
        /// <summary>
        /// notify complete of actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="human"></param>
        internal static void NotifyPostActorHumanize(this SaveData.Actor actor, Human human) =>
            OnPostActorHumanize(actor, human, actor.ToArchive());
        /// <summary>
        /// notify complete of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyPreActorCoordinateReload(this Human human, int type) =>
            OnPreCoordinateReload(human, type, human.ToArchive());
        /// <summary>
        /// notify complete of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static void NotifyPostCoordinateReload(this Human human) =>
            OnPostCoordinateReload(human, human.fileStatus.coordinateType, human.ToArchive());
        /// <summary>
        /// actor binding to human event
        /// param1: binding actor
        /// param2: archive to load extension
        /// </summary>
        public static event Action<SaveData.Actor, HumanData, ZipArchive> OnPreActorHumanize =
            (actor, _, _) => Plugin.Instance.Log.LogDebug($"Pre Actor Humanized: {actor.charasGameParam.Index}");
        /// <summary>
        /// actor binding to human event
        /// param1: binding actor
        /// param2: bound human
        /// param3: archive to load extension
        /// </summary>
        public static event Action<SaveData.Actor, Human, ZipArchive> OnPostActorHumanize =
            (actor, human, _) => Plugin.Instance.Log.LogDebug($"Post Actor Humanized: {actor.charasGameParam.Index}, {human.name}");
        /// <summary>
        /// coordinate reload complete event
        /// param1: coordinate applied human
        /// param2: chenged to coordinate index
        /// param3: readonly mode extension of reloaded human
        /// </summary>
        public static event Action<Human, int, ZipArchive> OnPostCoordinateReload =
            (human, _, _) => Plugin.Instance.Log.LogDebug($"Post Coordinate Reload: {human.name}");
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "SamabakeScramble";
        public const string Name = "Fishbone";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.0.0";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(Hooks.InitializeHookSwitch);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}