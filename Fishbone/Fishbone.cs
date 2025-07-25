using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using CoastalSmell;
using System.Reflection;

namespace Fishbone
{
    /// <summary>
    /// Purpose-specific portable network graphics encoder.
    /// </summary>
    public static partial class Encode
    {
        public static uint CRC32(this IEnumerable<byte> values) =>
            values.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;

        public static uint FromNetworkOrderBytes(this IEnumerable<byte> values) =>
            ((uint)values.ElementAt(0) << 24) | ((uint)values.ElementAt(1) << 16) | ((uint)values.ElementAt(2) << 8) | values.ElementAt(3);

        public static byte[] ToNetworkOrderBytes(this uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        public static byte[] Implant(this IEnumerable<byte> pngData, byte[] data) =>
            [..pngData.Take(8), ..pngData.Skip(8).ProcessSize(ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data))];

        public static byte[] Implant(this byte[] data) =>
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                ..ToChunk([(byte)'I', (byte)'H', (byte)'D', (byte)'R'], [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0]),
                ..ToChunk([(byte)'I', (byte)'D', (byte)'A', (byte)'T'], []),
                ..ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data),
                ..ToChunk([(byte)'I', (byte)'E', (byte)'N', (byte)'D'], [])
            ];
    }

    /// <summary>
    /// Purpose-specific portable network graphics decoder.
    /// </summary>
    public static partial class Decode
    {
        public static byte[] Extract(this IEnumerable<byte> values) =>
            values?.Skip(8)?.ProcessSize()?.ToArray() ?? [];
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class BonesToStuckAttribute : Attribute
    {
        internal string[] Paths;
        public BonesToStuckAttribute(params string[] paths) => Paths = paths;
    }

    public static class BonesToStuck<T>
    {
        static readonly string Path;

        static BonesToStuck()
        {
            Path = typeof(T).GetCustomAttribute(typeof(BonesToStuckAttribute)) is BonesToStuckAttribute bone
                ? System.IO.Path.Combine(bone.Paths)
                : throw new InvalidDataException($"{typeof(T)} is not bones to stuck.");
        }

        static bool TryGetEntry(ZipArchive archive, string path, out ZipArchiveEntry entry) =>
            null != (entry = archive.GetEntry(path));

        static readonly Func<ZipArchive, Action> Cleanup = archive =>
            TryGetEntry(archive, Path, out var entry) ? entry.Delete : F.DoNothing;

        public static bool Load(ZipArchive archive, out T value) =>
            TryGetEntry(archive, Path, out var entry)
                .With(F.Constant(value = default).Ignoring())
                && Json<T>.Deserialize.ApplyDisposable(entry.Open()).Try(Plugin.Instance.Log.LogError, out value);

        public static Action<ZipArchive, T> Save = (archive, data) =>
            Json<T>.Serialize.With(Cleanup(archive)).Apply(data)
                .ApplyDisposable(archive.CreateEntry(Path).Open())
                .Try(Plugin.Instance.Log.LogError);
    }

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "Fishbone";
        public const string Version = "2.1.1";
        internal static Plugin Instance;
        private Harmony Patch;

        public override void Load() =>
            ((Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"))).With(Hooks.Initialize);

        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}