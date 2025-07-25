using HarmonyLib;
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
using CoastalSmell;

namespace Fishbone
{
    public static partial class Event
    {
        static readonly byte[] NoExtension = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();
        static byte[] CustomExtension = NoExtension;

        static void UpdateCustom(Action<ZipArchive> action) =>
            CustomExtension = CustomExtension.UpdateExtension(action);

        internal static byte[] Extract(this HumanData data, byte[] bytes) =>
            bytes.Length > 0 ? bytes : data?.GameParameter?.imageData?.Extract() ?? NoExtension;

        internal static byte[] Extract(this HumanData data) =>
            data.Extract(data?.PngData?.Extract() ?? []);
    }

    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile))]
        static void HumanDataCoordinateSaveFilePostfix(HumanDataCoordinate __instance, string path) =>
            Event.NotifyCoordinateSerialize(__instance, path);
    }

    public static partial class Event
    {
        internal static Action<HumanDataCoordinate, string> NotifyCoordinateSerialize =>
            (data, path) => File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(NoExtension.UpdateExtension(OnCoordinateSerialize.Apply(data))));
    }

    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveCharaFileBeforeAction))]
        static void HumanDataSaveCharaFileBeforeAction(HumanData __instance, string path) =>
            Event.NotifyCharacterSerialize(__instance, path);
    }

    public static partial class Event
    {
        internal static Action<HumanData, string> NotifyCharacterSerialize =>
            (data, path) => File.WriteAllBytes(path, File.ReadAllBytes(path)
                .Implant(CustomExtension.UpdateExtension(OnCharacterSerialize.Apply(data))));
    }

    static partial class Hooks
    {
        static Action<Human> DoNothing = _ => { };
        static Action<Human> OnHumanReloading = DoNothing;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human.Reloading), nameof(Human.Reloading.Dispose))]
        static void HumanReloadingDisposePostfix(Human.Reloading __instance) =>
            OnHumanReloading = !__instance._isReloading ? OnHumanReloading : DoNothing.With(OnHumanReloading.Apply(__instance._human));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeMouthOpenMax))]
        static void HumanCustomChangeMouthOpenMaxPostfix(float maxValue, HumanFace __instance) =>
            OnHumanReloading = (__instance.human.isReloading || maxValue != 0.0) ? OnHumanReloading : DoNothing.With(OnHumanReloading.Apply(__instance.human));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CheckFlags))]
        static void HumanDataLoadFileLimited(ref HumanData.LoadFileInfo.Flags __result) =>
            __result |= HumanData.LoadFileInfo.Flags.GameParam;

        static Func<HumanData, HumanData, Action<Human>> HumanDataCopySkip = (_, _) => DoNothing;
        static Func<HumanData, HumanData, Action<Human>> HumanDataCopyProc = (dst, src) =>
            src == HumanCustom.Instance?.DefaultData ? Event.NotifyCharacterInitialize(dst) :
            src == HumanCustom.Instance?.Received?.HumanData ? Event.NotifyActorDeserializeToCharacter(dst) :
            dst == HumanCustom.Instance?.EditHumanData ? DoNothing.With(Event.NotifyCharacterSerializeToActor.Apply(src)) : DoNothing;
        static Func<HumanData, HumanData, Action<Human>> OnHumanDataCopy = HumanDataCopySkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyPostfix(HumanData dst, HumanData src) =>
            OnHumanReloading = OnHumanDataCopy(dst, src);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        static void HumanDataCopyLimitedPostfix(HumanData dst, HumanData src, CharaLimit flags) =>
            OnHumanReloading = Event.NotifyCharacterDeserialize(dst, flags, src.Extract(CharaExtension));

        static Action SwitchToCustom = () =>
            (OnHumanDataCopy, OnCoordinateTypeChange, OnCoordinateTypeChangeProc) =
            (HumanDataCopyProc, OnCoordinateTypeChangeSkip, OnCoordinateTypeChangeSkip);

        static Action SwitchToSimulation = () =>
            (OnHumanDataCopy, OnCoordinateTypeChange, OnCoordinateTypeChangeProc) =
            (HumanDataCopySkip, Event.NotifyPreActorCoordinateReload, Event.NotifyPreActorCoordinateReload);

        internal static Action Initialize =>
            InitializeCoordLimits + InitializeHookSwitch;

        static Action InitializeHookSwitch =
            () => Util<HumanCustom>.Hook(SwitchToCustom, SwitchToSimulation);
    }

    public static partial class Event
    {
        static void ResetMotionIK() =>
            HumanCustom.Instance._motionIK = new ILLGames.Rigging.MotionIK(HumanCustom.Instance.Human, HumanCustom.Instance._motionIK._data);

        static void ResetAnimation() =>
            HumanCustom.Instance.LoadPlayAnimation(HumanCustom.Instance.NowPose, new() { value = 0.0f });

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

        static Action<HumanData, CharaLimit, byte[]> NotifyPreCharacterDeserialize =
            (data, limits, bytes) =>
                UpdateCustom(bytes.ReferenceExtension(OnPreCharacterDeserialize.Apply(data).Apply(limits)));

        static Action<CharaLimit, byte[], Human> NotifyPostCharacterDeserialize =
            (limits, bytes, human) =>
                UpdateCustom(bytes.ReferenceExtension(OnPostCharacterDeserialize.Apply(human).Apply(limits)));

        internal static Action<HumanData> NotifyCharacterSerializeToActor =>
            data => GetHumanCustomTarget.UpdateActor(OnCharacterSerialize.Apply(data), CustomExtension);
    }

    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordinateTypeChange), nameof(CoordinateTypeChange.ChangeType))]
        static void CoordinateTypeChangeChangeTypePrefix(CoordinateTypeChange __instance, int type) =>
            Event.NotifyPreCoordinateReload(__instance._human, type);
    }

    public static partial class Event
    {
        internal static Action<Human, CoordLimit, byte[]> NotifyPreCoordinateDeserialize =
            (human, limits, bytes) =>
                human.UpdateHuman(
                    bytes.ReferenceExtension(
                        OnPreCoordinateDeserialize.Apply(human)
                            .Apply(human.coorde.nowCoordinate)
                            .Apply(limits)
                    )
                );

        internal static Action<Human, CoordLimit, byte[]> NotifyPostCoordinateDeserialize =
            (human, limits, bytes) =>
                human.UpdateHuman(
                    bytes.ReferenceExtension(
                        OnPostCoordinateDeserialize.Apply(human)
                            .Apply(human.coorde.nowCoordinate)
                            .Apply(limits)
                    )
                );

        internal static Action<Human, int> NotifyPreCoordinateReload =
            (human, type) => UpdateCustom(OnPreCoordinateReload.Apply(human).Apply(type));
    }

    public static partial class Event
    {
        static Dictionary<int, byte[]> ActorExtensions = new();

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

        internal static byte[] Extract(this SaveData.Actor actor, byte[] bytes) =>
            bytes.Length > 0 ? bytes : actor?.gameParameter?.imageData?.Extract() ?? NoExtension;

        internal static byte[] Extract(this SaveData.Actor actor) =>
            actor.Extract(actor.charFile.Extract());

        internal static bool ToActor(this Human human, out SaveData.Actor actor) =>
            null != (actor =
                TaskCharaInfos.Where(info => info?.chaCtrl == human)
                    .Select(info => info.actor).FirstOrDefault() ??
                Game.Charas?._entries
                    ?.Where(entry => entry?.value?.chaCtrl == human)
                    ?.Select(entry => entry.value)?.FirstOrDefault() ??
                (CoordeSelect.Instance?.IsOpen() ?? false
                    ? Game.Charas?._entries?.First(entry => entry.value.IsPC)?.value : null));
        static void UpdateActor(this SaveData.Actor actor, Action<ZipArchive> action) =>
            actor.charFile.Implant(actor.UpdateActor(action, actor.ToExtension()));
        static byte[] UpdateActor(this SaveData.Actor actor, Action<ZipArchive> action, byte[] extension) =>
            ActorExtensions[actor.charasGameParam.Index] = extension.UpdateExtension(action);
        static void UpdateHuman(this Human human, Action<ZipArchive> action) =>
            human.ToActor(out var actor).Either(F.Apply(UpdateCustom, action), F.Apply(actor.UpdateActor, action));
    }

    static partial class Hooks
    {
        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        static void WorldLoadPostfix(SaveData.WorldData __result) =>
            __result.Charas._entries
                .Where(entry => entry != null && entry.value != null)
                .ForEach(entry => Event.NotifyActorDeserialize(entry.value));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(SaveData.Actor __result) =>
            Event.NotifyActorDeserialize(__result);
    }

    public static partial class Event
    {
        internal static Action<SaveData.Actor> NotifyActorDeserialize =>
            actor => (ActorExtensions[actor.charasGameParam.Index] = actor.Extract()).ReferenceExtension(OnActorDeserialize.Apply(actor));
    }

    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Save), typeof(string))]
        static void WorldSavePrefix(SaveData.WorldData __instance) =>
            __instance.Charas
                ._entries.Where(entry => entry != null && entry.value != null)
                .ForEach(entry => Event.NotifyActorSerialize(entry.value));
    }

    public static partial class Event
    {
        internal static Action<SaveData.Actor> NotifyActorSerialize =>
            actor => actor.UpdateActor(OnActorSerialize.Apply(actor));
    }

    static partial class Hooks
    {
        static Action<Human, int> OnCoordinateTypeChangeSkip = (_, _) => { };
        static Action<Human, int> OnCoordinateTypeChangeProc = Event.NotifyPreActorCoordinateReload;
        static Action<Human, int> OnCoordinateTypeChange = Event.NotifyPreActorCoordinateReload;

        static Action<SaveData.CharaData> NotifyPreActorHumanize =>
            charaData => (OnCoordinateTypeChange, OnHumanDataCopy) = (OnCoordinateTypeChangeSkip,
                (dst, src) => DoNothing.With(Event.NotifyPreActorHumanize.Apply(Game.Charas[charaData.charasGameParam.Index]).Apply(dst)));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Chara.Base), nameof(SV.Chara.Base.SetActive))]
        static void SVCharaBaseSetActivePrefix(SV.Chara.Base __instance) =>
            (__instance.charaData != null).Maybe(NotifyPreActorHumanize.Apply(__instance.charaData));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadPlayerHighPoly))]
        [HarmonyPatch(typeof(SV.Talk.TalkTaskBase), nameof(SV.Talk.TalkTaskBase.LoadHighPoly))]
        static void SVTalkTalkTaskBaseLoadHighPolyPrefix(SV.Chara.AI _ai) =>
            (_ai?.charaData?.charasGameParam?.Index != null).Maybe(NotifyPreActorHumanize.Apply(_ai?._charaData));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordeSelect), nameof(CoordeSelect.CreateHiPoly))]
        static void CoordeSelectCreateHiPolyPrefix() =>
            NotifyPreActorHumanize(Game.Charas._entries.First(entry => entry.value.IsPC).value);

        static Action<SaveData.Actor, Human> NotifyPostActorHumanize =>
            (actor, human) => ((OnCoordinateTypeChange, OnHumanDataCopy) =
                (OnCoordinateTypeChangeProc, HumanDataCopySkip))
                    .With(Event.NotifyPostActorHumanize.Apply(actor).Apply(human));

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(CoordeSelect), nameof(CoordeSelect.CreateHiPoly))]
        static void CoordeSelectCreateHiPolyPostfix(Human __result) =>
            NotifyPostActorHumanize(Game.Charas._entries.First(entry => entry.value.IsPC).value, __result);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.CharaData), nameof(SaveData.CharaData.SetRoot))]
        static void SaveDataCharaDataSetRootPostfix(SaveData.CharaData __instance) =>
            (__instance.chaCtrl != null).Maybe(Util.DoNextFrame.Apply(NotifyPostActorHumanize
                .Apply(Game.Charas[__instance.charasGameParam.Index]).Apply(__instance.chaCtrl)));

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateType(HumanCoordinate __instance, ChaFileDefine.CoordinateType type) =>
            OnCoordinateTypeChange(__instance.human, (int)type);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), [])]
        static void HumanReloadCoordinatePostfix(Human __instance) =>
            Event.NotifyPostCoordinateReload(__instance);
    }

    public static partial class Event
    {
        internal static Action<SaveData.Actor, HumanData> NotifyPreActorHumanize =>
            (actor, data) => actor.ReferenceExtension(OnPreActorHumanize.Apply(actor).Apply(data));

        internal static Action<SaveData.Actor, Human> NotifyPostActorHumanize =>
            (actor, human) => actor.ReferenceExtension(OnPostActorHumanize.Apply(actor).Apply(human));

        internal static Action<Human, int> NotifyPreActorCoordinateReload =>
            (human, type) => human.ReferenceExtension(OnPreCoordinateReload.Apply(human).Apply(type));

        internal static Action<Human> NotifyPostCoordinateReload =>
            (human) => human.ReferenceExtension(OnPostCoordinateReload.Apply(human).Apply(human.fileStatus.coordinateType));
    }
}