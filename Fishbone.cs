using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using ILLGames.Unity.Component;
using Manager;
using Character;
using CharacterCreation;
using CharaLoadFlgs = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;

namespace Fishbone
{
    public static class Util
    {
        internal static readonly Il2CppSystem.Threading.CancellationTokenSource Canceler = new();
        static Action AwaitDestroy<T>(Action onSetup, Action onDestroy) where T : SingletonInitializer<T> =>
            () => SingletonInitializer<T>.Instance.gameObject
                    .GetComponentInChildren<ObservableDestroyTrigger>()
                    .AddDisposableOnDestroy(Disposable.Create(onDestroy + AwaitSetup<T>(onSetup, onDestroy)));
        static Action AwaitSetup<T>(Action onSetup, Action onDestroy) where T : SingletonInitializer<T> =>
            () => UniTask.NextFrame().ContinueWith((Action)(() => Hook<T>(onSetup, onDestroy)));
        public static void Hook<T>(Action onSetup, Action onDestroy) where T : SingletonInitializer<T> =>
            SingletonInitializer<T>.WaitUntilSetup(Canceler.Token)
                .ContinueWith(onSetup + AwaitDestroy<T>(onSetup, onDestroy));
    }
    public delegate void Either(Action a, Action b);
    public static class FunctionalExtension
    {
        public static Either Either(bool value) => value ? (left, right) => right() : (left, right) => left();
        public static void Either(this bool value, Action left, Action right) => Either(value)(left, right);
        public static void Maybe(this bool value, Action maybe) => value.Either(() => { }, maybe);
        public static T With<T>(this T input, Action sideEffect)
        {
            sideEffect();
            return input;
        }
        public static T With<T>(this T input, Action<T> sideEffect)
        {
            sideEffect(input);
            return input;
        }
    }
    public static class Codec
    {
        private static readonly uint[] TABLE = [.. Enumerable.Range(0, 256).Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];
        public static uint FromNetworkOrderBytes(this IEnumerable<byte> values) =>
            ((uint)values.ElementAt(0) << 24) | ((uint)values.ElementAt(1) << 16) | ((uint)values.ElementAt(2) << 8) | values.ElementAt(3);
        public static byte[] ToNetworkOrderBytes(this uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        public static uint CRC32(this IEnumerable<byte> values) =>
            values.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;
        private static IEnumerable<byte> Compose(this IEnumerable<byte> values) =>
                values.Concat(CRC32(values).ToNetworkOrderBytes());
        private static IEnumerable<byte> ToChunk(this IEnumerable<byte> values) =>
            ((uint)values.Count()).ToNetworkOrderBytes().Concat(Enumerable.Concat([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], values).Compose());
        public static byte[] Extract(this IEnumerable<byte> values) => values?.Skip(8)?.ProcessSize()?.ToArray() ?? [];
        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image) =>
            image.Take(4).FromNetworkOrderBytes().ProcessName(image.Skip(4));
        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image) =>
            (image.ElementAt(0), image.ElementAt(1), image.ElementAt(2), image.ElementAt(3)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => [],
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => image.Skip(4).Take((int)size),
                _ => image.Skip((int)size + 8).ProcessSize()
            };
        public static byte[] Implant(this IEnumerable<byte> image, IEnumerable<byte> data) =>
            [.. image.Take(8), .. image.Skip(8).ProcessSize(data.ToChunk())];
        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image, IEnumerable<byte> data) =>
            image.FromNetworkOrderBytes().ProcessName(image, data);
        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image, IEnumerable<byte> data) =>
            (image.ElementAt(4), image.ElementAt(5), image.ElementAt(6), image.ElementAt(7)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => data.Concat(image),
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => data.Concat(image.Skip((int)size + 12)),
                _ => image.Take((int)size + 12).Concat(image.Skip((int)size + 12).ProcessSize(data))
            };
    }
    internal static class UIRefHScene
    {
        internal static SV.H.UI.ClothesSettingMenu.SelectCoodinateCard HSceneSelectCoordinateCard =>
            SV.H.HScene.Instance.ClothesSlideSetting.SelectCoodinateCard;
        internal static Human CurrentTarget => HSceneSelectCoordinateCard._actor.Human;
        internal static string CurrentSource => HSceneSelectCoordinateCard._filename;
        internal static Button CoordReset => HSceneSelectCoordinateCard._beforeCoode;
        internal static Button CoordLoad => HSceneSelectCoordinateCard._decide;
        internal static Button End => SV.H.HScene.Instance.option._btnExit;
        internal static CoordLimit CoordLimit => HSceneSelectCoordinateCard._togglesParts
                .Where(item => item.isOn)
                .Select(item => item.name switch
                {
                    "Toggle Hair" => CoordLimit.Hair,
                    "Toggle Codenate" => CoordLimit.Clothes,
                    "Toggle Accessory" => CoordLimit.Accessory,
                    _ => CoordLimit.None
                }).Aggregate(CoordLimit.None, (x, y) => x | y);
        static Action Listen => (Action)
            (() => CoordLoad.onClick.AddListener(CoordinateLoad)) +
            (() => CoordReset.onClick.AddListener(CoordinateReset)) +
            (() => End.onClick.AddListener(CoordinateResetAll));
        static Action Ignore => (Action)
            (() => CoordLoad.onClick.RemoveListener(CoordinateLoad)) +
            (() => CoordReset.onClick.RemoveListener(CoordinateReset)) +
            (() => End.onClick.RemoveListener(CoordinateResetAll));
        static Action CoordinateLoad => () =>
            CurrentTarget.NotifyCoordinateDeserialize(CurrentSource, CoordLimit);
        static Action CoordinateReset => () =>
            CurrentTarget.NotifyCoordinateInitialize();
        static Action CoordinateResetAll => () =>
            SV.H.HScene.Instance.Actors.Do(actor => actor.Human.NotifyCoordinateInitialize());
        internal static void Initialize() => Util.Hook<SV.H.HScene>(Listen, Ignore);
    }
    internal static class UIRef
    {
        static string CurrentSource =>
            HumanCustom.Instance.CustomCoordinateFile._listCtrl.GetSelectTopInfo().FullPath;
        static Button CoordinateLoadButton =>
            HumanCustom.Instance.CustomCoordinateFile._fileWindow._btnLoad;
        static CoordLimit CoordLimit =>
            HumanCustom.Instance.CustomCoordinateFile._fileWindow
                ._coordinateCategory._toggles
                .Where(item => item.isOn)
                .Select(item => item.name switch
                {
                    "tglItem01" => CoordLimit.Clothes,
                    "tglItem02" => CoordLimit.Accessory,
                    "tglItem03" => CoordLimit.Hair,
                    "tglItem04" => CoordLimit.FaceMakeup,
                    "tglItem05" => CoordLimit.BodyMakeup,
                    _ => CoordLimit.None,
                }).Aggregate(CoordLimit.None, static (x, y) => x | y);
        static Action CoordinateLoad => () =>
            HumanCustom.Instance.Human.NotifyCoordinateDeserialize(CurrentSource, CoordLimit);
        static Action ListenCoordinateLoad => () =>
            CoordinateLoadButton.onClick.AddListener(CoordinateLoad);
        static Action IgnoreCoordinateLoad => () =>
            CoordinateLoadButton.onClick.RemoveListener(CoordinateLoad);
        internal static void Initialize() => Util.Hook<HumanCustom>(ListenCoordinateLoad, IgnoreCoordinateLoad);
    }
    public static class Event
    {
        public static event Action<ZipArchive> OnCharacterCreationSerialize =
            static delegate { Plugin.Instance.Log.LogDebug("Character Serialize"); };
        public static event Action<CharaLimit, ZipArchive> OnCharacterCreationDeserialize =
            static delegate { Plugin.Instance.Log.LogDebug("Character Deserialize"); };
        public static event Action<ZipArchive> OnCoordinateSerialize =
            static delegate { Plugin.Instance.Log.LogDebug("Coordinate Serialize"); };
        public static event Action<Human, CoordLimit, ZipArchive> OnCoordinateDeserialize =
            static delegate { Plugin.Instance.Log.LogDebug("Coordinate Deserialize"); };
        public static event Action<int, ZipArchive> OnCoordinateInitialize =
            static delegate { Plugin.Instance.Log.LogDebug("Coordinate Initialize"); };
        public static event Action<int, ZipArchive> OnActorSerialize =
            static delegate { Plugin.Instance.Log.LogDebug("Actor Serialize"); };
        public static event Action<int, ZipArchive> OnActorDeserialize =
            static delegate { Plugin.Instance.Log.LogDebug("Actor Deserialize"); };
        public static int GetActorIndex(this Il2CppBytes imageData) =>
            Enumerable.Range(0, 24).Where(Game.saveData.Charas.ContainsKey)
                .Where(index => Game.saveData.Charas[index].gameParameter.imageData == imageData).FirstOrDefault(-1);
        public static int GetActorIndex(this Human human) => human?.fileGameParam?.imageData?.GetActorIndex() ?? -1;
        public static int GetActorIndex(this HumanData data) => data?.GameParameter?.imageData?.GetActorIndex() ?? -1;
        public static int GetActorIndex(this SaveData.Actor actor) => actor?.gameParameter?.imageData?.GetActorIndex() ?? -1;
        static readonly byte[] EmptyBytes = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();
        static ZipArchive EmptyArchive => new(new MemoryStream(EmptyBytes), ZipArchiveMode.Read);
        static ZipArchive ToArchive(this byte[] bytes) =>
            bytes.Length > 0 ? new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read) : EmptyArchive;
        internal static Il2CppBytes NotifyCharacterCreationSerialize(this HumanData data) =>
            data.GameParameter.imageData = data.GameParameter.imageData
                .Implant(new MemoryStream()
                    .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                    .With(archive => OnCharacterCreationSerialize.Invoke(archive)).Dispose()).ToArray());
        internal static void NotifyCharacterCreationSerialize(this HumanData data, int index) =>
            data.NotifyCharacterCreationSerialize()
                .With(imageData => (index > 0).Maybe(() => UpdateImageData(index, imageData)));
        internal static void NotifyCharacterCreationDeserialize(this HumanData data, CharaLimit limit) =>
            OnCharacterCreationDeserialize.Invoke(limit, data.GameParameter.imageData.Extract().ToArchive());
        internal static void NotifyCoordinateDeserialize(this Human human, string path, CoordLimit limit) =>
            OnCoordinateDeserialize.Invoke(human, limit, File.ReadAllBytes(path).Extract().ToArchive());
        internal static void NotifyCoordinateInitialize(this Human human) =>
            OnCoordinateInitialize.Invoke(human.GetActorIndex(), human.data.GameParameter.imageData.Extract().ToArchive());
        internal static void NotifyCoordinateSerialize(this string path) =>
            File.WriteAllBytes(path, File.ReadAllBytes(path).Implant(new MemoryStream() 
                .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                .With(archive => OnCoordinateSerialize.Invoke(archive)).Dispose()).ToArray()));
        internal static void NotifyActorSerialize(this SaveData.Actor actor, int index) =>
            (actor.gameParameter.imageData = actor.gameParameter.imageData 
                .Implant(new MemoryStream()
                    .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                    .With(archive => OnActorSerialize.Invoke(index, archive)).Dispose()).ToArray()))
                    .With(ApplyToHuman(actor.chaCtrl));
        private static void UpdateImageData(int index, Il2CppBytes imageData) =>
            Game.saveData.Charas[index].gameParameter.imageData = imageData;
        private static Action<Il2CppBytes> ApplyToHuman(Human human) =>
            imageData => (human != null).Maybe(() => human.data.GameParameter.imageData = imageData);
        internal static void NotifyActorDeserialize(this SaveData.Actor actor, int index) =>
            OnActorDeserialize(index, actor.gameParameter.imageData.Extract().ToArchive());
    }
    static class Hooks
    {
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.H.HScene), nameof(SV.H.HScene.End), [])]
        static void HSceneEndPrefix() => 
            SV.H.HScene.Instance.Actors.Do(actor => actor.Human.NotifyCoordinateInitialize());
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveCharaFileBeforeAction), typeof(string), typeof(Il2CppSystem.Action))]
        static void HumanDataSaveFilePrefix(HumanData __instance) => __instance.NotifyCharacterCreationSerialize();
        static Action<CharaLimit> OnCopy(HumanData dst, HumanData src) =>
            (dst == HumanCustom.Instance?.HumanData, src == HumanCustom.Instance?.HumanData) switch
            {
                (true, false) => src.NotifyCharacterCreationDeserialize,
                (false, true) => _ => src.NotifyCharacterCreationSerialize(dst.GetActorIndex()),
                _ => (limit) => { }
            };
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.Copy), typeof(HumanData), typeof(HumanData))]
        static void HumanDataCopyPrefix(HumanData dst, HumanData src) => OnCopy(dst, src)(CharaLimit.All);
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited), typeof(bool), typeof(HumanData), typeof(HumanData), typeof(CharaLimit))]
        static void HumanDataCopyLimitedPrefix(HumanData dst, HumanData src, CharaLimit flags) => OnCopy(dst, src)(flags);
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppSystem.IO.BinaryReader), typeof(CharaLoadFlgs))]
        static void HumanDataLoadFilePostfix(ref CharaLoadFlgs flags) =>
            flags = flags switch
            {
                CharaLoadFlgs.FileView => flags,
                CharaLoadFlgs.FileViewUploader => flags,
                _ => flags | CharaLoadFlgs.GameParam
            };
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile), typeof(string), typeof(byte))]
        static void HumanDataCoordinateSaveFilePrefix(string path) => path.NotifyCoordinateSerialize();
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Save), typeof(string))]
        static void WorldSavePrefix(SaveData.WorldData __instance) =>
            Enumerable.Range(0, 24).Do(index => __instance.Charas.ContainsKey(index)
                .Maybe(() => __instance.Charas[index].NotifyActorSerialize(index)));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.WorldData), nameof(SaveData.WorldData.Load), typeof(string))]
        static void WorldLoadPostfix(SaveData.WorldData __result) =>
            Enumerable.Range(0, 24).Do(index => __result.Charas.ContainsKey(index)
                .Maybe(() => __result.Charas[index].NotifyActorDeserialize(index)));
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SV.EntryScene.CharaEntry), nameof(SV.EntryScene.CharaEntry.Entry), typeof(int), typeof(HumanData), typeof(bool))]
        static void CharaEntryPostfix(SaveData.Actor __result) => __result.NotifyActorDeserialize(__result.GetActorIndex());
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "SamabakeScramble";
        public const string Name = "Fishbone";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.1.1";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(UIRef.Initialize)
                .With(UIRefHScene.Initialize)
                .With(() => Instance = this);
        public override bool Unload() => true.With(Patch.UnpatchSelf) && base.Unload();
    }
}