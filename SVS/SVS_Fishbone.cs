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
            Event.NotifyCoordinateSerialize(__instance, path);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify coordinate serialization to listeners
        /// </summary>
        /// <param name="data">coordinate data to serialize</param>
        /// <param name="path">coordinate card path to serialize</param>
        internal static Action<HumanDataCoordinate, string> NotifyCoordinateSerialize =>
            (data, path) => File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(NoExtension.UpdateExtension(OnCoordinateSerialize.Apply(data))));
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
            Event.NotifyCharacterSerialize(__instance, path);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify character serialize to listeners
        /// </summary>
        /// <param name="data"></param>
        internal static Action<HumanData, string> NotifyCharacterSerialize =>
            (data, path) => File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(CustomExtension.UpdateExtension(OnCharacterSerialize.Apply(data))));
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
        static Action<Human> DoNothing = _ => { };
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
                OnHumanReloading : DoNothing.With(OnHumanReloading.Apply(__instance._human));
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
                OnHumanReloading : DoNothing.With(OnHumanReloading.Apply(__instance.human));
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
            src == HumanCustom.Instance?.DefaultData ? Event.NotifyCharacterInitialize(dst) :
            src == HumanCustom.Instance?.Received?.HumanData ? Event.NotifyActorDeserializeToCharacter(dst) :
            dst == HumanCustom.Instance?.EditHumanData ? DoNothing.With(Event.NotifyCharacterSerializeToActor.Apply(src)) : DoNothing;
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
            OnHumanReloading = Event.NotifyCharacterDeserialize(dst, flags, src.Extract(CharaExtension));
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
        internal static Func<HumanData, CharaLimit, byte[], Action<Human>> NotifyCharacterDeserialize =>
            (data, limits, bytes) => NotifyPostCharacterDeserialize.Apply(limits).Apply(bytes)
                .With(NotifyPreCharacterDeserialize.Apply(data).Apply(limits).Apply(bytes));
        internal static Func<HumanData, Action<Human>> NotifyCharacterInitialize =>
            (data) => NotifyCharacterDeserialize(data, CharaLimit.All, NoExtension);
        internal static Func<HumanData, Action<Human>> NotifyActorDeserializeToCharacter =>
            (data) => NotifyCharacterDeserialize(data, CharaLimit.All, GetHumanCustomTarget.Extract());
        static Action<HumanData, CharaLimit, byte[]> NotifyPreCharacterDeserialize =>
            (data, limits, bytes) => UpdateExtension(bytes.ReferenceExtension(OnPreCharacterDeserialize.Apply(data).Apply(limits)));
        static Action<CharaLimit, byte[], Human> NotifyPostCharacterDeserialize =>
            (limits, bytes, human) => UpdateExtension(bytes.ReferenceExtension(OnPostCharacterDeserialize.Apply(human).Apply(limits)));
        /// <summary>
        /// notify in game actor serialize (reflection) to listeners.
        /// </summary>
        /// <param name="data"></param>
        internal static Action<HumanData> NotifyCharacterSerializeToActor =>
            (data) => GetHumanCustomTarget.UpdateExtension(OnCharacterSerialize.Apply(data), CustomExtension);
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
            Event.NotifyPreCoordinateReload(__instance._human, type);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify begining of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static Action<Human, CoordLimit, byte[]> NotifyPreCoordinateDeserialize =>
            (human, limits, bytes) => bytes.ReferenceExtension(OnPreCoordinateDeserialize.Apply(human).Apply(human.coorde.nowCoordinate).Apply(limits));
        /// <summary>
        /// notify complete of coordinate deserialize to listeners
        /// </summary>
        /// <param name="human"></param>
        /// <param name="limits"></param>
        /// <param name="bytes"></param>
        internal static Action<Human, CoordLimit, byte[]> NotifyPostCoordinateDeserialize =>
            (human, limits, bytes) => bytes.ReferenceExtension(OnPostCoordinateDeserialize.Apply(human).Apply(human.coorde.nowCoordinate).Apply(limits));
        /// <summary>
        /// notify begining of coordinate reload
        /// </summary>
        /// <param name="human"></param>
        internal static Action<Human, int> NotifyPreCoordinateReload =>
            (human, type) => UpdateExtension(OnPreCoordinateReload.Apply(human).Apply(type));
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
                (CoordeSelect.Instance?.IsOpen() ?? false
                    ? Game.Charas?._entries?.First(entry => entry.value.IsPC)?.value : null));
        /// <summary>
        /// actor to extension conversion for listeners
        /// </summary>
        /// <param name="actor">save data actor to exitension restore from</param>
        /// <returns>archive to load extension (readonly)</returns>
        public static void ReferenceExtension(this SaveData.Actor actor, Action<ZipArchive> action) =>
            actor.ToExtension().ReferenceExtension(action);
        /// <summary>
        /// human to extension conversion for listeners
        /// </summary>
        /// <param name="actor">human to exitension restore from</param>
        /// <returns>archive to load extension (readonly)</returns>
        public static void ReferenceExtension(this Human human, Action<ZipArchive> action) =>
            (human.ToActor(out var actor) ? actor.ToExtension() : CustomExtension).ReferenceExtension(action);
        /// <summary>
        /// do extension update operation and store back resulting extension.
        /// </summary>
        /// <param name="data">save data actor hich associated with extension</param>
        /// <param name="action">extension update operation</param>
        static void UpdateExtension(this SaveData.Actor actor, Action<ZipArchive> action) =>
            actor.charFile.Implant(actor.UpdateExtension(action, actor.ToExtension()));
        static byte[] UpdateExtension(this SaveData.Actor actor, Action<ZipArchive> action, byte[] extension) =>
            ActorExtensions[actor.charasGameParam.Index] = extension.UpdateExtension(action);
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
                .Do(entry => Event.NotifyActorDeserialize(entry.value));
        /// <summary>
        /// capture actor entry from character card at Simulation Entry Scene
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(SaveData.Actor __result) =>
            Event.NotifyActorDeserialize(__result);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify actor deserialize to listeners
        /// </summary>
        /// <param name="actor"></param>
        internal static Action<SaveData.Actor> NotifyActorDeserialize =>
            actor => (ActorExtensions[actor.charasGameParam.Index] = actor.Extract()).ReferenceExtension(OnActorDeserialize.Apply(actor));
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
                .Do(entry => Event.NotifyActorSerialize(entry.value));
    }
    public static partial class Event
    {
        /// <summary>
        /// notify actor serialize to listeners
        /// </summary>
        /// <param name="actor"></param>
        internal static Action<SaveData.Actor> NotifyActorSerialize =>
            actor => actor.UpdateExtension(OnActorSerialize.Apply(actor));
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
        static Action<SaveData.CharaData> NotifyPreActorHumanize =>
            charaData => (OnCoordinateTypeChange, OnHumanDataCopy) = (OnCoordinateTypeChangeSkip,
                (dst, src) => DoNothing.With(Event.NotifyPreActorHumanize.Apply(Game.Charas[charaData.charasGameParam.Index]).Apply(dst)));
        /// <summary>
        /// capture bigining of actor binding to low poly human at Simulation Scene
        /// </summary>
        /// <param name="_ai"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Chara.Base), nameof(SV.Chara.Base.SetActive))]
        static void SVCharaBaseSetActivePrefix(SV.Chara.Base __instance) =>
            (__instance.charaData != null).Maybe(NotifyPreActorHumanize.Apply(__instance.charaData));
        /// <summary>
        /// capture bigining of actor binding to high poly human at Simulation Scene
        /// </summary>
        /// <param name="_ai"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadPlayerHighPoly))]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadHighPoly))]
        static void SVTalkTalkTaskBaseLoadHighPolyPrefix(SV.Chara.AI _ai) =>
            (_ai?.charaData?.charasGameParam?.Index == null).Maybe(NotifyPreActorHumanize.Apply(_ai._charaData));
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordeSelect), nameof(CoordeSelect.CreateHiPoly))]
        static void CoordeSelectCreateHiPolyPrefix() =>
            NotifyPreActorHumanize(Game.Charas._entries.First(entry => entry.value.IsPC).value);
        static Action<SaveData.Actor, Human> NotifyPostActorHumanize =>
            (actor, human) => ((OnCoordinateTypeChange, OnHumanDataCopy) =
                (OnCoordinateTypeChangeProc, HumanDataCopySkip)).With(Event.NotifyPostActorHumanize.Apply(actor).Apply(human));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordeSelect), nameof(CoordeSelect.CreateHiPoly))]
        static void CoordeSelectCreateHiPolyPostfix(Human __result) =>
            NotifyPostActorHumanize(Game.Charas._entries.First(entry => entry.value.IsPC).value, __result);
        /// <summary>
        /// capture actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.CharaData), nameof(SaveData.CharaData.SetRoot))]
        static void SaveDataCharaDataSetRootPostfix(SaveData.CharaData __instance) =>
            (__instance.chaCtrl != null).Maybe(NotifyPostActorHumanize.Apply(Game.Charas[__instance.charasGameParam.Index]).Apply(__instance.chaCtrl));
        /// <summary>
        /// capture coordinate reload begining
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateType(HumanCoordinate __instance, ChaFileDefine.CoordinateType type) =>
            OnCoordinateTypeChange(__instance.human, (int)type);
        /// <summary>
        /// capture coordinate reloading complete
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), [])]
        static void HumanReloadCoordinatePostfix(Human __instance) =>
            Event.NotifyPostCoordinateReload(__instance);
    }
    public static partial class Event
    {
        /// <summary>
        /// notify begining of actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="data"></param>
        internal static Action<SaveData.Actor, HumanData> NotifyPreActorHumanize =>
            (actor, data) => actor.ReferenceExtension(OnPreActorHumanize.Apply(actor).Apply(data));
        /// <summary>
        /// notify complete of actor binding to human at Simulation Scene
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="human"></param>
        internal static Action<SaveData.Actor, Human> NotifyPostActorHumanize =>
            (actor, human) => actor.ReferenceExtension(OnPostActorHumanize.Apply(actor).Apply(human));
        /// <summary>
        /// notify complete of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static Action<Human, int> NotifyPreActorCoordinateReload =>
            (human, type) => human.ReferenceExtension(OnPreCoordinateReload.Apply(human).Apply(type));
        /// <summary>
        /// notify complete of coordinate reload to listeners
        /// </summary>
        /// <param name="human"></param>
        internal static Action<Human> NotifyPostCoordinateReload =>
            (human) => human.ReferenceExtension(OnPostCoordinateReload.Apply(human).Apply(human.fileStatus.coordinateType));
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
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Guid = $"{Process}.{Name}";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(() => Instance = this)
                .With(Hooks.InitializeHookSwitch);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}