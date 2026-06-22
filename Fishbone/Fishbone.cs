using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.IO.Compression;
using System.Collections.Generic;
using Character;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;

namespace Fishbone
{
    // Extension events and helpers
    /// <summary>
    /// Top-level extension utilities and events.
    /// Consumers can subscribe to preprocess events to translate or inject extension data.
    /// </summary>
    public static partial class Extension
    {
        /// <summary>Raised when a character's data has been loaded and an extension archive is available.</summary>
        public static IObservable<(HumanData Data, ZipArchive Archive)> OnPreprocessChara => CharaLoadTrack.OnLoadComplete;

        /// <summary>Raised when coordinate data has been loaded and an extension archive is available.</summary>
        public static IObservable<(HumanDataCoordinate Data, ZipArchive Archive)> OnPreprocessCoord => CoordLoadTrack.OnLoadComplete;

        /// <summary>Return a new dictionary which merges an entry into <paramref name="mods"/>.</summary>
        /// <typeparam name="K">Dictionary key type.</typeparam>
        /// <typeparam name="V">Dictionary value type.</typeparam>
        /// <param name="mods">Original dictionary (may be null).</param>
        /// <param name="index">Key to insert or replace.</param>
        /// <param name="mod">Value to insert.</param>
        /// <returns>New dictionary containing the merged entries.</returns>
        public static Dictionary<K, V> Merge<K, V>(this Dictionary<K, V> mods, K index, V mod) =>
            mods == null ? new() { [index] = mod } :
                mods.Where(entry => !index.Equals(entry.Key))
                    .Select(entry => new Tuple<K, V>(entry.Key, entry.Value))
                    .Append(new Tuple<K, V>(index, mod)).ToDictionary();
    }
    // Extension interfaces
    /// <summary>
    /// Implemented by character-bound extension types to support merge semantics.
    /// </summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CharacterExtension<T> where T : CharacterExtension<T>, new ()
    {
        /// <summary>Merge character-limited modifications into this instance.</summary>
        T Merge(CharaLimit limit, T mods);
    }
    /// <summary>Implement for coordinate-bound extension types to support merge semantics.</summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CoordinateExtension<T> where T : CoordinateExtension<T>, new ()
    {
        /// <summary>Merge coordinate-limited modifications into this instance.</summary>
        T Merge(CoordLimit limit, T mods);
    }
    /// <summary>Conversion helper to produce a character extension from raw <see cref="HumanData"/>.</summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CharacterConversion<T> where T : CharacterExtension<T>, CharacterConversion<T>, new ()
    {
        /// <summary>Convert the provided <see cref="HumanData"/> into an extension instance.</summary>
        T Convert(HumanData data);
    }
    /// <summary>Conversion helper to produce a coordinate extension from raw <see cref="HumanDataCoordinate"/>.</summary>
    /// <typeparam name="T">Concrete extension type.</typeparam>
    public interface CoordinateConversion<T> where T : CoordinateExtension<T>, CoordinateConversion<T>, new ()
    {
        /// <summary>Convert the provided <see cref="HumanDataCoordinate"/> into an extension instance.</summary>
        T Convert(HumanDataCoordinate data);
    }
    /// <summary>
    /// Represents an extension that has both character and coordinate components.
    /// Implementations provide access to coordinate-specific data and merging logic.
    /// </summary>
    public interface ComplexExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        /// <summary>Return the coordinate extension instance for the given coordinate type.</summary>
        U Get(int coordinateType);
        /// <summary>Merge the provided coordinate modifications into a copy of this extension for the given type.</summary>
        T Merge(int coordinateType, U mods);
        /// <summary>Merge the provided coordinate modifications into a copy of this extension for the given type according to the limit.</summary>
        sealed T Merge(int coordinateType, CoordLimit limit, U coord) =>
            Merge(coordinateType, Get(coordinateType).Merge(limit, coord));
    }
    /// <summary>Attribute used to specify a storage path for extension data within the zip archive.</summary>
    /// <summary>Base attribute that specifies a relative storage path inside the extension zip archive.</summary>
    public class PathAttribute : Attribute
    {
        internal readonly string Path;
        /// <summary>Constructs a PathAttribute by combining the provided path components.</summary>
        /// <param name="paths">Path segments to combine into the archive path.</param>
        public PathAttribute(params string[] paths) =>
            Path = System.IO.Path.Combine(paths);
    }

    /// <summary>Marks a complex extension type and declares its storage path components.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ExtensionAttribute<T, U> : PathAttribute
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        /// <summary>Creates the attribute and records path segments used for zip entry naming.</summary>
        public ExtensionAttribute(params string[] paths) : base(paths) {}
    }
    /// <summary>Minimal storage abstraction for mapping an index to a value.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <typeparam name="Index">Index/key type.</typeparam>
    public interface ValueStorage<T, Index> where T : new()
    {
        /// <summary>Retrieve the value for <paramref name="index"/>.</summary>
        T Get(Index index);
        /// <summary>Set the value for <paramref name="index"/>.</summary>
        void Set(Index index, T value);

        /// <summary>Indexer proxy to <see cref="Get"/> and <see cref="Set"/>.</summary>
        sealed T this[Index index]
        {
            get => Get(index);
            set => Set(index, value);
        }
    }
    /// <summary>
    /// Storage that supports mapping from another index type <typeparamref name="U"/> to the primary index.
    /// </summary>
    public interface MapStorage<T,U,Index> : ValueStorage<T, Index> where T: new ()
    {
        /// <summary>Map an alternate index to the storage index.</summary>
        Index Map(U index);
        /// <summary>Indexer that accepts the alternate index type.</summary>
        sealed T this[U index]
        {
            get => Get(Map(index));
            set => Set(Map(index), value);
        }
    }
    /// <summary>
    /// Rich storage abstraction for complex extensions that also exposes coordinate-aware accessors.
    /// </summary>
    public interface Storage<T, U, Index> : ValueStorage<T, Index>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new() 
    {
        /// <summary>Get the current coordinate extension value for the specified character.</summary>
        U GetNowCoordinate(Index index);
        /// <summary>Set the current coordinate extension value for the specified character.</summary>
        void SetNowCoordinate(Index index, U value);
        /// <summary>Get coordinate value for the specified character and type</summary>
        sealed U Get(Index index, int coordinateType) =>
            Get(index).Get(coordinateType);
        /// <summary>Set coordinate value for the specified character and type</summary>
        sealed void Set(Index index, int coordinateType, U value) =>
            Set(index, Get(index).Merge(coordinateType, value));
        /// <summary>
        /// Helper record that enables indexer-style access to the current coordinate values.
        /// </summary>
        public record class Now(Storage<T, U, Index> Storage)
        {
            /// <summary>Provide access for the specified character's now coordinate</summary>
            public U this[Index index]
            {
                get => Storage.GetNowCoordinate(index);
                set => Storage.SetNowCoordinate(index, value);
            }
            /// <summary>Provide access for the specified character's now coordinate, according to the limit</summary>
            public U this[Index index, CoordLimit limit]
            {
                get => new U().Merge(limit, Storage.GetNowCoordinate(index));
                set => Storage.SetNowCoordinate(index, Storage.GetNowCoordinate(index).Merge(limit, value));
            }
        }
        /// <summary>Provide access for the specified character's extension value, according to the limit</summary>
        sealed T this[Index index, CharaLimit limit]
        {
            get => new T().Merge(limit, Get(index));
            set => Set(index, Get(index).Merge(limit, value));
        }
        /// <summary>Provide access for the specified character's coordinate extension value</summary>
        sealed U this[Index index, int coordinateType]
        {
            get => Get(index, coordinateType);
            set => Set(index, coordinateType, value);
        }
        /// <summary>Provide access for each character's now coordinate</summary>
        sealed Now NowCoordinate => new Now(this);
    }
    // Static extension class for complex extensions
    /// <summary>Utilities for complex (character+coordinate) extensions.</summary>
    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        /// <summary>Serializer for character extension instances.</summary>
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Serializer for coordinate extension instances.</summary>
        public static Action<Stream, U> SerializeCoord =
            Json<U>.Save.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Deserializer for character extension instances.</summary>
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Deserializer for coordinate extension instances.</summary>
        public static Func<Stream, U> DeserializeCoord =
            Json<U>.Load.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Helper to convert extension data path and type.</summary>
        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessChara
                .Subscribe(tuple => tuple.Archive.TryGetEntry(path, out var entry).Maybe(F.Apply(Translate, map, tuple.Archive, entry)));

        /// <summary>Helper to convert extension data path and type.</summary>
        public static IDisposable Translate<V>(string path, Func<V, U> map) where V : new() =>
            Extension.OnPreprocessCoord
                .Subscribe(tuple => tuple.Archive.TryGetEntry(path, out var entry).Maybe(F.Apply(Translate, map, tuple.Archive, entry)));
    }

    /// <summary>Marks a simple character extension type and declares its storage path components.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ExtensionAttribute<T> : PathAttribute where T : CharacterExtension<T>, new()
    {
        /// <summary>Creates the attribute and records path segments used for zip entry naming.</summary>
        public ExtensionAttribute(params string[] paths) : base(paths) {}
    }

    /// <summary>
    /// Storage abstraction for simple character extensions that supports character-limited accessors.
    /// </summary>
    public interface Storage<T, Index> : ValueStorage<T, Index> where T : CharacterExtension<T>, new()
    {
        /// <summary>Indexer that returns a copy merged with the provided character limit.</summary>
        sealed T this[Index index, CharaLimit limit]
        {
            get => new T().Merge(limit, Get(index));
            set => Set(index, Get(index).Merge(limit, value));
        }
    }

    // Static extension class for simple extensions
    public static partial class Extension<T> where T : CharacterExtension<T>, new()
    {
        /// <summary>Serializer for character extension instances.</summary>
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Deserializer for character extension instances.</summary>
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);

        /// <summary>Helper to convert extension data path and type.</summary>
        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessChara.Subscribe(tuple => 
                tuple.Archive.TryGetEntry(path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry)));
    }
    static partial class Hooks
    {
        /// <summary>Initialize Harmony patches for the plugin.</summary>
        internal static IDisposable Initialize() =>
            Disposable.Create(Harmony.CreateAndPatchAll(typeof(Hooks), $"{Plugin.Name}.Hooks").UnpatchSelf);
    }

    // Main plugin class
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        /// <summary>Plugin GUID composed of process and name.</summary>
        public const string Guid = $"{Process}.{Name}";

        /// <summary>Human-readable plugin name.</summary>
        public const string Name = "Fishbone";

        /// <summary>Plugin version string.</summary>
        public const string Version = "4.1.0";

        /// <summary>Global singleton instance of the plugin.</summary>
        internal static Plugin Instance;

        CompositeDisposable Subscriptions;

        /// <summary>Create plugin instance and set global singleton.</summary>
        public Plugin() : base() => Instance = this;

        /// <summary>Called by BepInEx when the plugin is loaded; sets up subscriptions and patches.</summary>
        public override void Load() => Subscriptions = [
            .. Extension.Initialize(), Hooks.Initialize()
        ];

        /// <summary>Called by BepInEx to unload the plugin; disposes subscriptions and returns success.</summary>
        public override bool Unload() =>
            true.With(Subscriptions.Dispose) && base.Unload();
    }
}