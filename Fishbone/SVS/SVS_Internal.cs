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
using ILLGames.Extensions;
using SV.CoordeSelectScene;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using CoastalSmell;

namespace Fishbone
{
    #region Load

    static partial class Hooks
    {
        static Hooks()
        {
            HumanDataLoadActions = CustomLoadActions;
        }
        static HumanDataLoadActions CustomLoadActions = (data, flags) => flags
            is (LoadFlags.About | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic)
            or (LoadFlags.About | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Custom | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde)
            or (LoadFlags.About | LoadFlags.Graphic | LoadFlags.Parameter | LoadFlags.GameParam | LoadFlags.Coorde | LoadFlags.Custom)
            ? (GetPngSizeProc(data), Extension.Preprocess) : (GetPngSizeSkip, HumanDataLoadFileSkip);
        static HumanDataLoadActions ActorLoadProc = (data, flags) =>
            (GetPngSizeSkip, Extension.Preprocess);
        static HumanDataLoadActions ActorLoadSkip = (data, flags) =>
            (GetPngSizeSkip, HumanDataLoadFileSkip);
        static HumanDataLoadActions ActorEntryActions = ActorLoadProc;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPrefix() => OnSkipPng = SkipPngSkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        static void CostumeInfoInitFileListPostfix() => OnSkipPng = SkipPngProc;

        static Action<SaveData.Actor> ActorSetBytesSkip = _ => { };
        static Action<SaveData.Actor> ActorSetBytesProc = Extension.LoadActor;
        static Action<SaveData.Actor> OnActorSetBytes = ActorSetBytesSkip;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePrefix() =>
            HumanDataLoadActions = ActorEntryActions = ActorLoadSkip;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Initialize))]
        static void EntryFileListSelecterInitializePostfix() =>
            HumanDataLoadActions = ActorEntryActions = ActorLoadProc;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPrefix() =>
            (OnActorSetBytes, HumanDataLoadActions) = (ActorSetBytesProc, ActorEntryActions);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        [HarmonyPatch(typeof(SV.EntryScene.EntryFileListSelecter), nameof(SV.EntryScene.EntryFileListSelecter.Execute))]
        static void SaveDataWorldDataLoadPostfix() =>
            (OnActorSetBytes, HumanDataLoadActions) = (ActorSetBytesSkip, CustomLoadActions);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.Actor), nameof(SaveData.Actor.SetBytes))]
        static void ActorSetBytes(SaveData.Actor actor) => OnActorSetBytes(actor);

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry))]
        static void CharaEntryPostfix(SaveData.Actor __result) => Extension.LoadActor(__result);
    }

    public static partial class Extension
    {
        static TalkManager.TaskCharaInfo[] TaskCharaInfos =>
            TalkManager.Instance == null ? Array.Empty<TalkManager.TaskCharaInfo>() :
            [
                TalkManager.Instance?.playerCharaInfo,
                TalkManager.Instance?.npcCharaInfo1,
                TalkManager.Instance?.npcCharaInfo2,
                TalkManager.Instance?.npcCharaInfo3
            ];

        static SaveData.Actor ToActor(Human human) =>
            TaskCharaInfos.Where(info => info?.chaCtrl == human)
                .Select(info => info.actor).FirstOrDefault() ??
            Game.Charas?._entries
                ?.Where(entry => entry?.value?.chaCtrl == human)
                ?.Select(entry => entry.value)?.FirstOrDefault() ??
            (CoordeSelect.Instance?.IsOpen() ?? false
                ? Game.Charas?._entries?.First(entry => entry.value.IsPC)?.value : null);

        internal static event Action<SaveData.Actor> PreLoadActor =
            actor => Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} loaded: {actor.charFile.Pointer}");
        internal static event Action<Human, CharaLimit> PreReloadCustomChara = delegate { };
        internal static event Action<Human, CoordLimit> PreReloadCustomCoord = delegate { };
        internal static event Action<SaveData.Actor, CharaLimit> PreReloadActorChara = delegate { };
        internal static event Action<SaveData.Actor, CoordLimit> PreReloadActorCoord = delegate { };
        internal static void LoadActor(SaveData.Actor actor) =>
            PreReloadActorChara(actor, CharaLimit.All);
        internal static void ForkReloadChara(Human human, CharaLimit limit) =>
            ToActor(human, out var actor).Either(
                PreReloadCustomChara.Apply(human).Apply(limit) + OnReloadCustomChara.Apply(human),
                PreReloadActorChara.Apply(actor).Apply(limit) + OnReloadActorChara.Apply(actor).Apply(human));
        internal static void ForkReloadCoord(Human human, CoordLimit limit) =>
            ToActor(human, out var actor).Either(
                PreReloadCustomCoord.Apply(human).Apply(limit) + OnReloadCustomCoord.Apply(human),
                PreReloadActorCoord.Apply(actor).Apply(limit) + OnReloadActorCoord.Apply(actor).Apply(human)); 
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T, U>.Resolve(human.data, Current));

        internal static void LoadCoord(Human human, CoordLimit limit) =>
            Current = Current.Merge(human.data.Status.coordinateType, limit, Extension<T, U>.Resolve(Coord));
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadChara(Human human, CharaLimit limit) =>
            Current = Current.Merge(limit, Extension<T>.Resolve(human.data, Current));
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void LoadChara(SaveData.Actor actor, CharaLimit limit) =>
            Charas[actor.charasGameParam.Index] = Charas.TryGetValue(actor.charasGameParam.Index, out var current) ?
                current.Merge(limit, Extension<T, U>.Resolve(actor.charFile, current)) : Extension<T, U>.Resolve(actor.charFile, new());
        internal static void LoadCoord(SaveData.Actor actor, CoordLimit limit) =>
            Coords[actor.charasGameParam.Index] =
                Coords[actor.charasGameParam.Index].Merge(limit,
                    Extension<T, U>.Resolve(Coords[actor.charasGameParam.Index]));
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void LoadChara(SaveData.Actor actor, CharaLimit limit) =>
            Charas[actor.charasGameParam.Index] =
                Charas[actor.charasGameParam.Index].Merge(limit,
                    Extension<T>.Resolve(actor.charFile, Charas[actor.charasGameParam.Index]));
    }

    #endregion

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
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Save), typeof(string))]
        static void WorldSavePrefix(SaveData.WorldData __instance) =>
            __instance.Charas.Yield()
                .Where(entry => entry != null && entry.Item2 != null)
                .ForEach(entry => Extension.SaveActor(entry.Item2));
    }

    public static partial class Extension
    {
        static byte[] Save(MemoryStream stream, Action<ZipArchive> actions) =>
            stream.With(actions.ApplyDisposable(new ZipArchive(stream, ZipArchiveMode.Create))).ToArray();

        static void Save(string path, Action<ZipArchive> actions) =>
            File.WriteAllBytes(path, Encode.Implant(File.ReadAllBytes(path), Save(new MemoryStream(), actions)));

        internal static void SaveChara(string path) => Save(path, OnSaveChara);

        internal static void SaveCoord(string path) => Save(path, OnSaveCoord);

        internal static void SaveActor(SaveData.Actor actor) =>
            Implant(actor.charFile, Save(new MemoryStream(), OnSaveActor.Apply(actor)));
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Chara);

        internal static void SaveCoord(ZipArchive archive) =>
            Extension<T, U>.SaveCoord(archive, Coord);
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void SaveChara(ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Chara);
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void Save(SaveData.Actor actor, ZipArchive archive) =>
            Extension<T, U>.SaveChara(archive, Charas.GetValueOrDefault(actor.charasGameParam.Index, new()));
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void Save(SaveData.Actor actor, ZipArchive archive) =>
            Extension<T>.SaveChara(archive, Charas.GetValueOrDefault(actor.charasGameParam.Index, new()));
    }

    #endregion

    #region Copy Between Actor And Custom

    static partial class Hooks
    {
        static Action OnCopyAction(HumanCustom custom, HumanData dst, HumanData src) =>
            src == custom?.Received?.HumanData ? Extension.CopyActorToCustom :
            dst == custom?.EditHumanData ? Extension.CopyCustomToActor : F.DoNothing;

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy))]
        static void HumanDataCopyLimitedPrefix(HumanData dst, HumanData src) =>
            OnCopyAction(HumanCustom.Instance, dst, src).Invoke();
    }

    public static partial class Extension
    {
        static int GetHumanCustomTarget =>
            Game.Charas.Yield()
                .Select(entry => entry.Item2)
                .Where(actor => actor.charFile == HumanCustom.Instance.Received.HumanData)
                .Select(actor => actor.charasGameParam.Index).FirstOrDefault(-1);

        internal static event Action<int> OnCopyActorToCustom =
            index => Plugin.Instance.Log.LogDebug($"Simulation actor{index} copied to custom.");

        internal static event Action<int> OnCopyCustomToActor =
            index => Plugin.Instance.Log.LogDebug($"Custom copied to simulation actor{index}.");

        internal static void CopyActorToCustom() =>
            OnCopyActorToCustom(GetHumanCustomTarget);

        internal static void CopyCustomToActor() =>
            OnCopyCustomToActor(GetHumanCustomTarget);
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy(int index) =>
            Current = ActorExtension<T, U>.Chara(index);
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy(int index) =>
            Current = ActorExtension<T>.Chara(index);
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal static void OnCopy(int index) =>
            (Charas[index], Coords[index]) = (HumanExtension<T, U>.Chara, HumanExtension<T, U>.Coord);
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal static void OnCopy(int index) =>
            Charas[index] = HumanExtension<T>.Chara;
    }

    #endregion

    #region Actor Coordinate Change

    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePrefix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(Extension.ChangeCoordinate, __instance.human, (int)type));
    }

    static partial class Extension
    {
        internal static event Action<SaveData.Actor, int> OnChangeActorCoord =
            (actor, coordinateType) => Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} coordinate type changed: {coordinateType}");
        internal static void ChangeCoordinate(Human human, int coordinateType) =>
            human.ToActor(out var actor).Either(PrepareSaveCoord, F.Apply(OnChangeActorCoord, actor, coordinateType));
    }

    static partial class ActorExtension<T, U>
    {
        internal static void CoordinateChange(SaveData.Actor actor, int coordinateType) =>
            Coords[actor.charasGameParam.Index] = Charas[actor.charasGameParam.Index].Get(coordinateType);
    }

    #endregion
}