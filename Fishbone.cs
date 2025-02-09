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
using CharaSaveFlgs = Character.HumanData.SaveFileInfo.Flags;
using CharaLoadFlgs = Character.HumanData.LoadFileInfo.Flags;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;

namespace Fishbone
{
    public static class Util
    {
        static readonly Il2CppSystem.Threading.CancellationTokenSource Canceler = new();
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
        public static byte[] Implant(this Il2CppBytes image, IEnumerable<byte> data) =>
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
        internal static CoordLimit CoordLimit => HSceneSelectCoordinateCard._togglesParts
                .Where(item => item.isOn)
                .Select(item => item.name switch
                {
                    "Toggle Hair" => CoordLimit.Hair,
                    "Toggle Codenate" => CoordLimit.Clothes,
                    "Toggle Accessory" => CoordLimit.Accessory,
                    _ => CoordLimit.None
                }).Aggregate(CoordLimit.None, (x, y) => x | y);
        static Action ListenCoordLoad => () =>
            CoordLoad.onClick.AddListener(CoordinateLoad);
        static Action ListenCoordReset => () =>
            CoordReset.onClick.AddListener(CoordinateReset);
        static Action IgnoreCoordLoad => () =>
            CoordLoad.onClick.AddListener(CoordinateLoad);
        static Action IgnoreCoordReset => () =>
            CoordReset.onClick.AddListener(CoordinateReset);
        static Action CoordinateLoad => () =>
            CurrentTarget.NotifyCoordinateDeserialize(CurrentSource, CoordLimit);
        static Action CoordinateReset => () =>
            CurrentTarget.NotifyCoordinateInitialize();
        internal static void Initialize() => Util.Hook<SV.H.HScene>(ListenCoordLoad + ListenCoordReset, IgnoreCoordLoad + IgnoreCoordReset);
    }
    internal static class UIRef
    {
        static string CurrentSource =>
            HumanCustom.Instance.CustomCoordinateFile._listCtrl.GetSelectTopInfo().FullPath;
        static Button CoordinateLoadButton =>
            HumanCustom.Instance.CustomCoordinateFile._fileWindow._btnLoad;
        static CoordLimit CoordLimit =>
            HumanCustom.Instance.CustomCoordinateFile._fileWindow
                ._coordinateCategory.ToggleGroup.onList._items
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
        public static event Action<Human, ZipArchive> OnCoordinateInitialize =
            static delegate { Plugin.Instance.Log.LogDebug("Coordinate Initialize"); };
        public static event Action<SaveData.Actor, ZipArchive> OnActorSerialize =
            static delegate { Plugin.Instance.Log.LogDebug("Actor Serialize"); };
        public static event Action<SaveData.Actor, Human, ZipArchive> OnActorDeserialize =
            static delegate { Plugin.Instance.Log.LogDebug("Actor Deserialize"); };
        static readonly byte[] EmptyBytes = new MemoryStream()
            .With(stream => new ZipArchive(stream, ZipArchiveMode.Create).Dispose()).ToArray();
        static ZipArchive EmptyArchive => new(new MemoryStream(EmptyBytes), ZipArchiveMode.Read);
        static ZipArchive ToArchive(this byte[] bytes) =>
            bytes.Length > 0 ? new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read) : EmptyArchive;
        internal static void NotifyCharacterCreationSerialize(this HumanData data) =>
            data.GameParameter.imageData = data.GameParameter.imageData
                .Implant(new MemoryStream()
                    .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                    .With(archive => OnCharacterCreationSerialize.Invoke(archive)).Dispose()).ToArray());
        internal static void NotifyCharacterCreationDeserialize(this HumanData data, Human human, CharaLimit limit) =>
            OnCharacterCreationDeserialize.Invoke(limit, data.GameParameter.imageData.Extract().ToArchive());
        internal static void NotifyCoordinateDeserialize(this Human human, string path, CoordLimit limit) =>
            OnCoordinateDeserialize(human, limit, File.ReadAllBytes(path).Extract().ToArchive());
        internal static void NotifyCoordinateInitialize(this Human human) =>
            OnCoordinateInitialize(human, human.data.GameParameter.imageData.Extract().ToArchive());
        internal static void NotifyCoordinateSerialize(this HumanDataCoordinate data) =>
            data.PngData.Implant(new MemoryStream()
                .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                .With(archive => OnCoordinateSerialize.Invoke(archive)).Dispose()).ToArray());
        internal static void NotifyActorSerialize(this SaveData.Actor actor) =>
            Actors.ContainsKey(actor).Maybe(() => actor.gameParameter.imageData = actor.gameParameter.imageData
                .Implant(new MemoryStream()
                    .With(stream => new ZipArchive(stream, ZipArchiveMode.Create)
                    .With(archive => OnActorSerialize.Invoke(actor, archive)).Dispose()).ToArray()));
        internal static void NotifyActorDeserialize(this Human human) =>
            Game.Charas._entries.Select(item => item.value)
                .Where(actor => actor?.gameParameter?.imageData == human.data?.GameParameter?.imageData)
                .Where(actor => !Humans.ContainsKey(human))
                .Do(actor => OnActorDeserialize(actor, human, Humans[human] = Actors[actor] = human.data.GameParameter.imageData.Extract().ToArchive()));
        internal static readonly Dictionary<Human, ZipArchive> Humans = [];
        internal static readonly Dictionary<SaveData.Actor, ZipArchive> Actors = [];
        internal static void Initialize() => Util.Hook<SV.SimulationScene>((Action) Humans.Clear + Actors.Clear, (Action) Humans.Clear + Actors.Clear);
    }
    static class Hooks
    {
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppSystem.IO.BinaryWriter), typeof(CharaSaveFlgs))]
        static void HumanDataSaveFilePrefix(HumanData __instance, CharaSaveFlgs flags) =>
            ((Scene.NowData.LevelName, flags) switch
            {
                (_, CharaSaveFlgs.EditCheck) => false,
                ("CustomScene", _) => true,
                _ => false
            }).Maybe(__instance.NotifyCharacterCreationSerialize);
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
        static Action<CharaLimit> OnCopy(HumanData dst, HumanData src) =>
            (dst == HumanCustom.Instance?.HumanData, src == HumanCustom.Instance?.HumanData) switch
            {
                (true, false) => (limit) => src.NotifyCharacterCreationDeserialize(HumanCustom.Instance.Human, limit),
                (false, true) => (limit) => src.NotifyCharacterCreationSerialize(),
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
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.SaveFile), typeof(string), typeof(byte))]
        static void HumanDataCoordinateSaveFilePrefix(HumanDataCoordinate __instance) => __instance.NotifyCoordinateSerialize();
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(SaveData.Actor), nameof(SaveData.Actor.GetBytes), typeof(SaveData.Actor))]
        static void ActorSavePrefix(SaveData.Actor heroine) =>
            Scene.NowData.LevelName.Equals(SceneNames.Simulation, StringComparison.Ordinal).Maybe(heroine.NotifyActorSerialize);
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.SetCreateTexture), typeof(CustomTextureCreate), typeof(bool),
             typeof(ChaListDefine.CategoryNo), typeof(int), typeof(ChaListDefine.KeyType), typeof(ChaListDefine.KeyType), typeof(int))]
        static void SetCreateTexturePostfix(Human  __instance) =>
            Scene.NowData.LevelName.Equals(SceneNames.Simulation, StringComparison.Ordinal).Maybe(__instance.NotifyActorDeserialize);
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Process = "SamabakeScramble";
        public const string Name = "Fishbone";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.0.0";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(Event.Initialize)
                .With(UIRef.Initialize)
                .With(UIRefHScene.Initialize)
                .With(() => Instance = this);
        public override bool Unload() => true.With(Patch.UnpatchSelf) && base.Unload();
    }
}