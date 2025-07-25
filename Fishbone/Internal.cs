using HarmonyLib;
using System;
using System.IO;
using System.IO.Compression;
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

namespace Fishbone
{
    #region Encode/Decode
    public static partial class Encode
    {
        private static readonly uint[] TABLE = [.. Enumerable.Range(0, 256)
            .Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];

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
    }

    public static partial class Decode
    {
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
    #endregion

    #region Event Extension Methods
    public static partial class Event
    {
        static Func<Action<ZipArchive>, Action<MemoryStream>> ForUpdate =
            action => stream => action.ApplyDisposable(new ZipArchive(stream, ZipArchiveMode.Update)).Try(Plugin.Instance.Log.LogError);

        static Action<byte[], MemoryStream> WriteAllBytes =
            (bytes, stream) => stream.Write(bytes.Length > 0 ? bytes : NoExtension);

        static Action<MemoryStream> SeekToBegin = stream => stream.Position = 0;

        static byte[] UpdateExtension(this byte[] bytes, Action<ZipArchive> action) =>
            new MemoryStream()
                .With(WriteAllBytes.Apply(bytes))
                .With(SeekToBegin)
                .With(ForUpdate(action))
                .ToArray();

        static Action<ZipArchive> ReferenceExtension(this byte[] bytes, Action<ZipArchive, ZipArchive> action) =>
            action.ApplyDisposable(new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read));

        static void ReferenceExtension(this byte[] bytes, Action<ZipArchive> action) =>
            action.ApplyDisposable(new ZipArchive(new MemoryStream(bytes.Length > 0 ? bytes : NoExtension), ZipArchiveMode.Read)).Invoke();

        static void Implant(this HumanData data, byte[] bytes) =>
            data.PngData = data?.PngData?.Implant(bytes) ?? bytes.Implant();
    }
    #endregion

    #region Hooks
    static partial class Hooks
    {
        // PNG extension capture for character card loading
        static byte[] CharaExtension = [];
        static Action<Il2CppStream, long, long> GetPngSizeSkip = (stream, offset, length) => { };
        static Action<Il2CppStream, long, long> GetPngSizeProc = (stream, offset, length) =>
        {
            var buffer = new Il2CppBytes(length);
            stream.Read(buffer);
            stream.Seek(offset, Il2CppSystem.IO.SeekOrigin.Begin);
            CharaExtension = buffer.Extract();
        };
        static Action<Il2CppStream, long, long> OnGetPngSize = GetPngSizeProc;

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void SetGetPngBytesHook(LoadFlags flags) =>
            OnGetPngSize = (flags & LoadFlags.Png) == LoadFlags.None ? GetPngSizeProc : GetPngSizeSkip;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.LoadFile), typeof(Il2CppReader), typeof(LoadFlags))]
        static void ResetGetPngBytesHook() =>
            OnGetPngSize = GetPngSizeSkip;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.GetPngSize), typeof(Il2CppStream))]
        static void GetPngSizePostfix(Il2CppStream st, long __result) =>
            OnGetPngSize.Apply(st).Apply(st.Position).Apply(__result).Try(Plugin.Instance.Log.LogError);

        // Coordinate load limitation inference
        static CoordLimit CoordLimits = CoordLimit.None;
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

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPrefix(Il2CppReader br) =>
            SkipPngProc.Apply(br).Try(Plugin.Instance.Log.LogError);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(PngFile), nameof(PngFile.SkipPng), typeof(Il2CppReader))]
        static void SkipPngPostfix() =>
            CoordLimits = CoordLimit.None;

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void DisableCoordinateLimitCheck() =>
            CoordLimitsCheck = CoordLimitsCheckSkip;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.CopyLimited))]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.LoadBytes), typeof(Il2CppBytes), typeof(Il2CppSystem.Version))]
        static void EnableCoordinateLimitCheck() =>
            CoordLimitsCheck = CoordLimitsCheckProc;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataClothes), nameof(HumanDataClothes.CopyBase))]
        static void HumanDataClothesCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Clothes);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataAccessory), nameof(HumanDataAccessory.Copy))]
        static void HumanDataAccessoryCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Accessory);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataHair), nameof(HumanDataHair.Copy))]
        static void HumanDataHairCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.Hair);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataFaceMakeup), nameof(HumanDataFaceMakeup.Copy))]
        static void HumanDataFaceMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.FaceMakeup);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataBodyMakeup), nameof(HumanDataBodyMakeup.Copy))]
        static void HumanDataBodyMakeupCopyPostfix() =>
            CoordLimitsCheck(CoordLimit.BodyMakeup);

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPrefix(Human __instance) =>
            (CoordLimits is not CoordLimit.None)
                .Maybe(Event.NotifyPreCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(Human), nameof(Human.ReloadCoordinate), typeof(Human.ReloadFlags))]
        static void HumanReloadCoordinateWithFlagsPostfix(Human __instance) =>
            (CoordLimits is not CoordLimit.None)
                .Maybe(Event.NotifyPostCoordinateDeserialize.Apply(__instance).Apply(CoordLimits).Apply(CoordExtension));

        static Action InitializeCoordLimits =
            () => Event.OnPostCoordinateDeserialize += delegate { CoordLimits = CoordLimit.None; };
    }
    #endregion
}