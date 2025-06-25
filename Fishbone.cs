using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Cysharp.Threading.Tasks;
using Character;
using ILLGames.IO;
using LoadFlags = Character.HumanData.LoadFileInfo.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppBytes = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppStream = Il2CppSystem.IO.Stream;
using CoastalSmell;
using System.Reflection;

namespace Fishbone
{
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
        static Func<Action<ZipArchive>, Action<MemoryStream>> ForUpdate =
            action => stream => action.ApplyDisposable(new ZipArchive(stream, ZipArchiveMode.Update)).Try(Plugin.Instance.Log.LogError);
        static Action<byte[], MemoryStream> WriteAllBytes =
            (bytes, stream) => stream.Write(bytes.Length > 0 ? bytes : NoExtension);
        static Action<MemoryStream> SeekToBegin =
            stream => stream.Position = 0;
        static byte[] UpdateExtension(this byte[] bytes, Action<ZipArchive> action) =>
            new MemoryStream().With(WriteAllBytes.Apply(bytes)).With(SeekToBegin).With(ForUpdate(action)).ToArray();
        static Action<ZipArchive> ReferenceExtension(this byte[] bytes, Action<ZipArchive, ZipArchive> action) =>
            action.ApplyDisposable(new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read));
        static void ReferenceExtension(this byte[] bytes, Action<ZipArchive> action) =>
            action.ApplyDisposable(new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read)).Invoke();
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
        static Action<Il2CppStream, long, long> GetPngSizeSkip = (stream, offset, length) => { };
        static Action<Il2CppStream, long, long> GetPngSizeProc = (stream, offset, length) =>
        {
            var buffer = new Il2CppBytes(length);
            stream.Read(buffer);
            stream.Seek(offset, Il2CppSystem.IO.SeekOrigin.Begin);
            CharaExtension = buffer.Extract();
        };
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
            OnGetPngSize.Apply(st).Apply(st.Position).Apply(__result).Try(Plugin.Instance.Log.LogError);
        /// <summary>
        /// coordinate load limitation infered from function calling
        /// </summary>
        static CoordLimit CoordLimits = CoordLimit.None;
        /// <summary>
        /// intercepted png data during coordinate card loading
        /// </summary>
        static byte[] CoordExtension = [];
        static Action<Il2CppReader> SkipPngProc = (reader) =>
        {
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
            CoordExtension = PngFile.LoadPngBytes(reader).Extract();
            reader.BaseStream.Seek(0, Il2CppSystem.IO.SeekOrigin.Begin);
        };
        static Action<CoordLimit> CoordLimitsCheck;
        static Action<CoordLimit> CoordLimitsCheckSkip => _ => { };
        static Action<CoordLimit> CoordLimitsCheckProc => limit => CoordLimits |= limit;
        /// <summary>
        /// capture entrying SkipPng and intercept skipped png data
        /// from ghidra inspection, SkipPng is only called during coordinate loading operation
        /// </summary>
        /// <param name="br"></param>
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            SkipPngProc.Apply(br).Try(Plugin.Instance.Log.LogError);
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
            (CoordLimits is not CoordLimit.None)
                .Maybe(Event.NotifyPreCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension));
        /// <summary>
        /// capture coordinate deserialize complete
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPostfix(Human __instance) =>
            (CoordLimits is not CoordLimit.None)
                .Maybe(Event.NotifyPostCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension));
        static Action InitializeCoordLimits =
            () => Event.OnPostCoordinateDeserialize += delegate { CoordLimits = CoordLimit.None; };
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class BonesToStuckAttribute : Attribute
    {
        internal string[] Paths;
        public BonesToStuckAttribute(params string[] paths) => Paths = paths;
    }
    public static class BonesToStuck<T>
    {
        static string Path;
        static BonesToStuck() => Path = typeof(T)
            .GetCustomAttribute(typeof(BonesToStuckAttribute)) is BonesToStuckAttribute bone
                ? System.IO.Path.Combine(bone.Paths) : throw new InvalidDataException($"{typeof(T)} is not bone to stuck.");
        static readonly JsonSerializerOptions JsonOpts = new()
        {
            NumberHandling =
                JsonNumberHandling.WriteAsString |
                JsonNumberHandling.AllowReadingFromString |
                JsonNumberHandling.AllowNamedFloatingPointLiterals,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        static bool TryGetEntry(ZipArchive archive, string path, out ZipArchiveEntry entry) =>
            null != (entry = archive.GetEntry(path));
        static Func<ZipArchive, Action> Cleanup =
            (archive) => TryGetEntry(archive, Path, out var entry) ? entry.Delete : F.DoNothing;
        static Func<Stream, T> FromJson =
            (stream) => JsonSerializer.Deserialize<T>(stream, JsonOpts);
        static Action<T, Stream> ToJson =
            (data, stream) => JsonSerializer.Serialize(stream, data, JsonOpts);
        public static bool Load(ZipArchive archive, out T value) =>
            TryGetEntry(archive, Path, out var entry).With(F.Constant(value = default).Ignoring())
                && FromJson.ApplyDisposable(entry.Open()).Try(Plugin.Instance.Log.LogError, out value);
        public static Action<ZipArchive, T> Save =
            (archive, data) => ToJson.With(Cleanup(archive)).Apply(data).
                ApplyDisposable(archive.CreateEntry(Path).Open()).Try(Plugin.Instance.Log.LogError);
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "Fishbone";
        public const string Version = "2.0.2";
        internal static Plugin Instance;
        private Harmony Patch;
        public override void Load() =>
            ((Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"))).With(Hooks.Initialize);
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}