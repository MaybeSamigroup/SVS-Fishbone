using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using Cysharp.Threading.Tasks;
using ILLGames.Unity.Component;
using Character;
using ILLGames.IO;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppStream = Il2CppSystem.IO.Stream;
using BepInEx.Logging;

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
        /// <summary>
        /// utility function to hook singleton initializer instance setup & destroy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="onSetup"></param>
        /// <param name="onDestroy"></param>
        public static void Hook<T>(Action onSetup, Action onDestroy) where T : SingletonInitializer<T> =>
            SingletonInitializer<T>.WaitUntilSetup(Canceler.Token)
                .ContinueWith(onSetup + AwaitDestroy<T>(onSetup, onDestroy));
        public static void Try(this ManualLogSource log, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                log.LogError(e.StackTrace);
            }
        }
        public static void Try<T>(this ManualLogSource log, Action<T> action, T input) where T : IDisposable
        {
            using (input)
            {
                log.Try(action.Apply(input));
            }
        }
    }
    public delegate void Either(Action a, Action b);
    /// <summary>
    /// functional utilities for favor
    /// </summary>
    public static class FunctionalExtension
    {
        public static T With<T>(this T input, Action<T> sideEffect) => input.With(() => sideEffect(input));
        public static T With<T>(this T input, Action sideEffect)
        {
            sideEffect();
            return input;
        }
        public static Either Either(this bool value) => value ? (_, a2) => a2() : (a1, _) => a1();
        public static void Either(this bool value, Action a1, Action a2) => Either(value)(a1, a2);
        public static void Maybe(this bool value, Action action) => Either(value)(() => { }, action);
        public static Action<V2, V3, V4, V5> Apply<V1, V2, V3, V4, V5>(this Action<V1, V2, V3, V4, V5> action, V1 value) =>
            (v2, v3, v4, v5) => action(value, v2, v3, v4, v5);
        public static Action<V2, V3, V4> Apply<V1, V2, V3, V4>(this Action<V1, V2, V3, V4> action, V1 value) =>
            (v2, v3, v4) => action(value, v2, v3, v4);
        public static Action<V2, V3> Apply<V1, V2, V3>(this Action<V1, V2, V3> action, V1 value) =>
            (v2, v3) => action(value, v2, v3);
        public static Action<V2> Apply<V1, V2>(this Action<V1, V2> action, V1 value) =>
            (v2) => action(value, v2);
        public static Action Apply<V1>(this Action<V1> action, V1 value) =>
            () => action(value);
        public static Func<V2, V3, V4, V5, R> Apply<V1, V2, V3, V4, V5, R>(this Func<V1, V2, V3, V4, V5, R> action, V1 value) =>
            (v2, v3, v4, v5) => action(value, v2, v3, v4, v5);
        public static Func<V2, V3, V4, R> Apply<V1, V2, V3, V4, R>(this Func<V1, V2, V3, V4, R> action, V1 value) =>
            (v2, v3, v4) => action(value, v2, v3, v4);
        public static Func<V2, V3, R> Apply<V1, V2, V3, R>(this Func<V1, V2, V3, R> action, V1 value) =>
            (v2, v3) => action(value, v2, v3);
        public static Func<V2, R> Apply<V1, V2, R>(this Func<V1, V2, R> action, V1 value) =>
            (v2) => action(value, v2);
        public static Func<R> Apply<V1, R>(this Func<V1, R> action, V1 value) =>
            () => action(value);
    }
    /// <summary>
    /// purpose specific portable network graphics encoder
    /// </summary>
    public static class Encode
    {
        private static readonly uint[] TABLE = [.. Enumerable.Range(0, 256)
            .Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];
        public static uint CRC32(this IEnumerable<byte> values) =>
            values.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;
        public static uint FromNetworkOrderBytes(this IEnumerable<byte> values) =>
            ((uint)values.ElementAt(0) << 24) | ((uint)values.ElementAt(1) << 16) | ((uint)values.ElementAt(2) << 8) | values.ElementAt(3);
        public static byte[] ToNetworkOrderBytes(this uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        private static IEnumerable<byte> Suffix(this IEnumerable<byte> values) =>
                values.Concat(CRC32(values).ToNetworkOrderBytes());
        private static IEnumerable<byte> ToChunk(IEnumerable<byte> name, IEnumerable<byte> values) =>
            ((uint)values.Count()).ToNetworkOrderBytes().Concat(Enumerable.Concat(name, values).Suffix());
        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image, IEnumerable<byte> data) =>
            (image.ElementAt(4), image.ElementAt(5), image.ElementAt(6), image.ElementAt(7)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => data.Concat(image),
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => data.Concat(image.Skip((int)size + 12)),
                _ => image.Take((int)size + 12).Concat(image.Skip((int)size + 12).ProcessSize(data))
            };
        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image, IEnumerable<byte> data) =>
            image.FromNetworkOrderBytes().ProcessName(image, data);
        public static byte[] Implant(this IEnumerable<byte> pngData, byte[] data) =>
            [.. pngData.Take(8), .. pngData.Skip(8).ProcessSize(ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data))];
        public static byte[] Implant(this byte[] data) =>
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
            , ..ToChunk([(byte)'I', (byte)'H', (byte)'D', (byte)'R'], [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0])
            , ..ToChunk([(byte)'I', (byte)'D', (byte)'A', (byte)'T'], [])
            , ..ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data)
            , ..ToChunk([(byte)'I', (byte)'E', (byte)'N', (byte)'D'], [])];
    }
    /// <summary>
    /// purpose specific portable network graphics decoder
    /// </summary>
    public static class Decode
    {
        public static byte[] Extract(this IEnumerable<byte> values) =>
            values?.Skip(8)?.ProcessSize()?.ToArray() ?? [];
        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image) =>
            image.Take(4).FromNetworkOrderBytes().ProcessName(image.Skip(4));
        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image) =>
            (image.ElementAt(0), image.ElementAt(1), image.ElementAt(2), image.ElementAt(3)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => [],
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => image.Skip(4).Take((int)size),
                _ => image.Skip((int)size + 8).ProcessSize()
            };
    }
    public static partial class Event
    {
        static void UpdateExtension(this Action<ZipArchive> action, MemoryStream stream) =>
            Plugin.Instance.Log.Try(action, new ZipArchive(stream, ZipArchiveMode.Update));
        static void WriteAllBytes(this byte[] bytes, MemoryStream stream) =>
            stream.Write(bytes.Length > 0 ? bytes : NoExtension);
        static void SeekToBegin(MemoryStream stream) =>
            stream.Position = 0;
        static byte[] UpdateExtension(this byte[] bytes, Action<ZipArchive> action) =>
            new MemoryStream().With(bytes.WriteAllBytes).With(SeekToBegin).With(action.UpdateExtension).ToArray();
        static Action<T> ReferenceExtension<T>(this byte[] bytes, Action<ZipArchive,T> action) =>
            storage => Plugin.Instance.Log.Try(archive => action(archive, storage), new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read));
        static void ReferenceExtension(this byte[] bytes, Action<ZipArchive> action) =>
            Plugin.Instance.Log.Try(action, new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read));
        static void Implant(this HumanData data, byte[] bytes) =>
            data.PngData = data?.PngData?.Implant(bytes) ?? bytes.Implant();
    }
    /// <summary>
    /// struggling trace of coordinate deserialize capturing
    /// </summary>
    static partial class Hooks
    {
        /// <summary>
        /// intercepted png data during character card loading
        /// </summary>
        static byte[] CharaExtension = [];
        static void ReadAllBytes(this Il2CppStream stream, Il2CppBytes buffer) =>
            stream.Read(buffer);
        static Action SeekTo(this Il2CppStream stream, long position) =>
            () => stream.Position = position;
        static void GetPngSizeSkip(Il2CppStream stream, long offset, long length) { }
        static void GetPngSizeProc(Il2CppStream stream, long offset, long length) =>
            Plugin.Instance.Log.Try(() => CharaExtension = new Il2CppBytes(length)
                .With(stream.ReadAllBytes).With(stream.SeekTo(offset)).Extract());
        /// <summary>
        /// action to do when GetPngSize captured
        /// </summary>
        static Action<Il2CppStream, long, long> OnGetPngSize = GetPngSizeProc;
        /// <summary>
        /// capture card load begining
        /// prepare GetPngSize hook when flags lack Png
        /// </summary>
        /// <param name="flags"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void SetGetPngBytesHook(LoadFlags flags) =>
            OnGetPngSize = (flags & LoadFlags.Png) == LoadFlags.None ? GetPngSizeProc : GetPngSizeSkip;
        /// <summary>
        /// capture character card load complete
        /// reset GetPngSize hook to do nothing
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void ResetGetPngBytesHook() =>
            OnGetPngSize = GetPngSizeSkip;
        /// <summary>
        /// capture GetPngSize call and intercept ignored png data if necessary
        /// </summary>
        /// <param name="st"></param>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.GetPngSize), typeof(Il2CppStream))]
        static void GetPngSizePostfix(Il2CppStream st, long __result) =>
            OnGetPngSize(st, st.Position, __result);
        /// <summary>
        /// coordinate load limitation infered from function calling
        /// </summary>
        static CoordLimit CoordLimits = CoordLimit.None;
        /// <summary>
        /// intercepted png data during coordinate card loading
        /// </summary>
        static byte[] CoordExtension = [];
        static Action<CoordLimit> CoordLimitsCheck;
        static void CoordLimitsCheckSkip(CoordLimit _) { }
        static void CoordLimitsCheckProc(CoordLimit limit) =>
            CoordLimits |= limit;
        /// <summary>
        /// capture entrying SkipPng and intercept skipped png data
        /// from ghidra inspection, SkipPng is only called during coordinate loading operation
        /// </summary>
        /// <param name="br"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            Plugin.Instance.Log.Try(() => CoordExtension = PngFile
                .LoadPngBytes(br.With(br.BaseStream.SeekTo(0))).With(br.BaseStream.SeekTo(0)).Extract());
        /// <summary>
        /// capture leaving SkipPng and prepare limitation inference
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPostfix() =>
            CoordLimits = CoordLimit.None;
        /// <summary>
        /// loaded coordinate data is copied completley maybe for verification
        /// so stop limitation inference before HumanData.CopyLimited and HumanDataCoorinate.LoadBytes
        /// </summary>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void DisableCoordinateLimitCheck() =>
            CoordLimitsCheck = CoordLimitsCheckSkip;
        /// <summary>
        /// loaded coordinate data is copied completley maybe for verification
        /// restart limitation inference after HumanData.CopyLimited and HumanDataCoorinate.LoadBytes
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void EnableCoordinateLimitCheck() =>
            CoordLimitsCheck = CoordLimitsCheckProc;
        /// <summary>
        /// capture if limitation meets clothes
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataClothes), nameof(HumanDataClothes.CopyBase))]
        static void HumanDataClothesCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Clothes);
        /// <summary>
        /// capture if limitation meets accessory
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataAccessory), nameof(HumanDataAccessory.Copy))]
        static void HumanDataAccessoryCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Accessory);
        /// <summary>
        /// capture if limitation meets hair
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataHair), nameof(HumanDataHair.Copy))]
        static void HumanDataHairCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Hair);
        /// <summary>
        /// capture if limitation meets face makeup
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataFaceMakeup), nameof(HumanDataFaceMakeup.Copy))]
        static void HumanDataFaceMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.FaceMakeup);
        /// <summary>
        /// capture if limitation meets body makeup
        /// </summary>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataBodyMakeup), nameof(HumanDataBodyMakeup.Copy))]
        static void HumanDataBodyMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.BodyMakeup);
        /// <summary>
        /// capture coordinate deserialize begining
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPrefix(Human __instance) =>
            (CoordLimits != CoordLimit.None)
                .Maybe(Event.NotifyPreCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension));
        /// <summary>
        /// capture coordinate deserialize complete
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPostfix(Human __instance) =>
            (CoordLimits, CoordExtension) = CoordLimits switch
            {
                CoordLimit.None => (CoordLimit.None, []),
                _ => (CoordLimit.None, Array.Empty<byte>())
                    .With(Event.NotifyPostCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension))
            };
    }
    public partial class Plugin : BasePlugin
    {
        public const string Name = "Fishbone";
        public const string Version = "2.0.2";
        internal static Plugin Instance;
    }
}