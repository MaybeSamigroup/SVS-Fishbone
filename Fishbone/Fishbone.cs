using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.IO;
using System.IO.Compression;
using Character;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using System.Collections.Generic;

namespace Fishbone
{
    // Extension events and helpers
    public static partial class Extension
    {
        public static event Action<HumanData, ZipArchive> OnPreprocessChara =
            (data, archive) => Plugin.Instance.Log.LogDebug($"Character preprocess:{data.Pointer},{archive.Entries.Count}");

        public static event Action<HumanDataCoordinate, ZipArchive> OnPreprocessCoord =
            (data, archive) => Plugin.Instance.Log.LogDebug($"Coordinate preprocess:{data.Pointer},{archive.Entries.Count}");

        public static event Action<Human> OnReloadChara = delegate { };

        public static event Action<Human> OnReloadCoord = delegate { };
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
        public static T LoadingData(HumanData data) =>
            LoadingCharas.GetValueOrDefault(data, new());
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Action<Stream, U> SerializeCoord =
            Json<U>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, U> DeserializeCoord =
            Json<U>.Load.Apply(Plugin.Instance.Log.LogError);
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
        public static T LoadingData(HumanData data) =>
            LoadingCharas.GetValueOrDefault(data, new());
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
    }

    // Main plugin class
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "Fishbone";
        public const string Version = "3.0.0";

        internal static Plugin Instance;
        private Harmony Patch;

        public override void Load() =>
            (Instance, Patch) = (this, Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks"));

        public override bool Unload() =>
            true.With(Patch.UnpatchSelf) && base.Unload();
    }
}