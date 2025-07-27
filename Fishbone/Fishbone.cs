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
                .With(F.Ignoring(value = default))
                && Json<T>.Deserialize.ApplyDisposable(entry.Open())
                    .Try(Plugin.Instance.Log.LogError, out value);

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
        public const string Version = "2.1.2";
        internal static Plugin Instance;
        private Harmony Patch;

        public override void Load() =>
            ((Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"))).With(Hooks.Initialize);

        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}