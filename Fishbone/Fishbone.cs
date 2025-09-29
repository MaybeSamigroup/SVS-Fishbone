using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using Character;
using HarmonyLib;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
    // Extension events and helpers
    public static partial class Extension
    {
        public static event Action<HumanData, ZipArchive> OnPreprocessChara =
            (data, archive) => Plugin.Instance.Log.LogDebug($"Character preprocess:{data.Pointer},{archive.Entries.Count}");

        public static event Action<HumanDataCoordinate, ZipArchive> OnPreprocessCoord =
            (data, archive) => Plugin.Instance.Log.LogDebug($"Coordinate preprocess:{data.Pointer},{archive.Entries.Count}");

        public static event Action<Human> OnLoadChara =
            (human) => Plugin.Instance.Log.LogDebug($"Character loaded:{human.data.Pointer},{human.data.Status.coordinateType}");

        public static event Action<Human> OnLoadCoord =
            (human) => Plugin.Instance.Log.LogDebug($"Coordinate loaded:{human.data.Pointer},{human.data.Status.coordinateType}");

        public static Dictionary<K, V> Merge<K, V>(this Dictionary<K, V> mods, K index, V mod) =>
            mods == null ? new() { [index] = mod } :
                mods.Where(entry => !index.Equals(entry.Key))
                    .Select(entry => new Tuple<K, V>(entry.Key, entry.Value))
                    .Append(new Tuple<K, V>(index, mod)).ToDictionary();
    }

    // Extension interfaces
    public interface CharacterExtension<T> where T : CharacterExtension<T>
    {
        T Merge(CharaLimit limit, T mods);
    }

    public interface CoordinateExtension<T> where T : CoordinateExtension<T>
    {
        T Merge(CoordLimit limit, T mods);
    }

    public interface ComplexExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        U Get(int coordinateType);
        T Merge(int coordinateType, U mods);

        virtual T Merge(int coordinateType, CoordLimit limit, U coord) =>
            Merge(coordinateType, Get(coordinateType).Merge(limit, coord));
    }

    public interface SimpleExtension<T>
        where T : ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        T Get();

        virtual T Get(int coordinateType) => Get();
        virtual T Merge(int coordinateType, T mods) => Get();
        virtual T Merge(CoordLimit limit, T mods) => Get();
    }

    // Attribute for complex extensions
    [AttributeUsage(AttributeTargets.Class)]
    public class ExtensionAttribute<T, U> : Attribute
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        internal string Path;
        public ExtensionAttribute(params string[] paths) =>
            Path = System.IO.Path.Combine(paths);
    }

    // Static extension class for complex extensions
    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        public static event Action<HumanData, T> OnPreprocessChara = delegate { };
        public static event Action<HumanDataCoordinate, U> OnPreprocessCoord = delegate { };
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Action<Stream, U> SerializeCoord =
            Json<U>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, U> DeserializeCoord =
            Json<U>.Load.Apply(Plugin.Instance.Log.LogError);
        public static Action<HumanData, ZipArchive> Translate<V>(string path, Func<V, T> map) where V : new() =>
            (_, archive) => TryGetEntry(archive, path, out var entry).Maybe(F.Apply(Translate, map, archive, entry));
        public static Action<HumanDataCoordinate, ZipArchive> Translate<V>(string path, Func<V, U> map) where V : new() =>
            (_, archive) => TryGetEntry(archive, path, out var entry).Maybe(F.Apply(Translate, map, archive, entry));
    }

    // Attribute for simple extensions
    [AttributeUsage(AttributeTargets.Class)]
    public class ExtensionAttribute<T> : Attribute
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        internal string Path;
        public ExtensionAttribute(params string[] paths) =>
            Path = System.IO.Path.Combine(paths);
    }

    // Static extension class for simple extensions
    public static partial class Extension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        public static event Action<HumanData, T> OnPreprocessChara = delegate { };

        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
        public static Action<HumanData, ZipArchive> Translate<V>(string path, Func<V, T> map) where V : new() =>
            (_, archive) => TryGetEntry(archive, path, out var entry).Maybe(F.Apply(Translate, map, archive, entry));
    }

    // Main plugin class
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "Fishbone";
        public const string Version = "3.1.2";
        internal static Plugin Instance;
        private Harmony Patch;
        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}