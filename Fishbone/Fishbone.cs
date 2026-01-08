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
    public static partial class Extension
    {
        public static IObservable<(HumanData Data, ZipArchive Archive)> OnPreprocessChara => CharaLoadTrack.OnLoadComplete;
        public static IObservable<(HumanDataCoordinate Data, ZipArchive Archive)> OnPreprocessCoord => CoordLoadTrack.OnLoadComplete;
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
    public interface CharacterConversion<T> where T : CharacterExtension<T>, CharacterConversion<T>
    {
        T Convert(HumanData data);
    }
    public interface CoordinateExtension<T> where T : CoordinateExtension<T>
    {
        T Merge(CoordLimit limit, T mods);
    }
    public interface CoordinateConversion<T> where T : CoordinateExtension<T>, CoordinateConversion<T>
    {
        T Convert(HumanDataCoordinate data);
    }
    public interface ComplexExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        U Get(int coordinateType);
        T Merge(int coordinateType, U mods);
        sealed T Merge(int coordinateType, CoordLimit limit, U coord) =>
            Merge(coordinateType, Get(coordinateType).Merge(limit, coord));
    }

    public interface SimpleExtension<T>
        where T : ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        virtual T Get() => (T)this;
        sealed T Get(int coordinateType) => Get();
        sealed T Merge(int coordinateType, T mods) => Get();
        sealed T Merge(CoordLimit limit, T mods) => Get();
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

    public interface Storage<T, U, Index>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        T Get(Index index);
        void Set(Index index, T value);
        U GetNowCoordinate(Index index);
        void SetNowCoordinate(Index index, U value);
        sealed U Get(Index index, int coordinateType) =>
            Get(index).Get(coordinateType);
        sealed void Set(Index index, int coordinateType, U value) =>
            Set(index, Get(index).Merge(coordinateType, value));
        public record class Now(Storage<T, U, Index> Storage)
        {
            public U this[Index index]
            {
                get => Storage.GetNowCoordinate(index);
                set => Storage.SetNowCoordinate(index, value);
            }
            public U this[Index index, CoordLimit limit]
            {
                get => new U().Merge(limit, Storage.GetNowCoordinate(index));
                set => Storage.SetNowCoordinate(index, Storage.GetNowCoordinate(index).Merge(limit, value));
            }
        }
        sealed T this[Index index]
        {
            get => Get(index);
            set => Set(index, value);
        }
        sealed T this[Index index, CharaLimit limit]
        {
            get => new T().Merge(limit, Get(index));
            set => Set(index, Get(index).Merge(limit, value));
        }
        sealed U this[Index index, int coordinateType]
        {
            get => Get(index, coordinateType);
            set => Set(index, coordinateType, value);
        }
        sealed Now NowCoordinate => new Now(this);
    }

    // Static extension class for complex extensions
    public static partial class Extension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Action<Stream, U> SerializeCoord =
            Json<U>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, U> DeserializeCoord =
            Json<U>.Load.Apply(Plugin.Instance.Log.LogError);
        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessChara
                .Subscribe(tuple => TryGetEntry(tuple.Archive, path, out var entry).Maybe(F.Apply(Translate, map, tuple.Archive, entry)));
        public static IDisposable Translate<V>(string path, Func<V, U> map) where V : new() =>
            Extension.OnPreprocessCoord
                .Subscribe(tuple => TryGetEntry(tuple.Archive, path, out var entry).Maybe(F.Apply(Translate, map, tuple.Archive, entry)));
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

    public interface Storage<T, Index>
        where T : SimpleExtension<T>, ComplexExtension<T,T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        T Get(Index index);
        void Set(Index index, T value);

        sealed T this[Index index]
        {
            get => Get(index);
            set => Set(index, value);
        }
        sealed T this[Index index, CharaLimit limit]
        {
            get => new T().Merge(limit, Get(index));
            set => Set(index, Get(index).Merge(limit, value));
        }
    }

    // Static extension class for simple extensions
    public static partial class Extension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        public static Action<Stream, T> SerializeChara =
            Json<T>.Save.Apply(Plugin.Instance.Log.LogError);
        public static Func<Stream, T> DeserializeChara =
            Json<T>.Load.Apply(Plugin.Instance.Log.LogError);
        public static IDisposable Translate<V>(string path, Func<V, T> map) where V : new() =>
            Extension.OnPreprocessChara.Subscribe(tuple => 
                TryGetEntry(tuple.Archive, path, out var entry)
                    .Maybe(F.Apply(Translate, map, tuple.Archive, entry)));
    }
    public static partial class Hooks
    {
        internal static IDisposable Initialize() =>
            Disposable.Create(Harmony.CreateAndPatchAll(typeof(Hooks), $"{Plugin.Name}.Hooks").UnpatchSelf);
    }

    // Main plugin class
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        public const string Guid = $"{Process}.{Name}";
        public const string Name = "Fishbone";
        public const string Version = "4.0.0";
        internal static Plugin Instance;
        CompositeDisposable Subscriptions;
        public Plugin() : base() => Instance = this;
        public override void Load() => Subscriptions = [
            .. Extension.Initialize(), Hooks.Initialize()
        ];
        public override bool Unload() =>
            true.With(Subscriptions.Dispose) && base.Unload();
    }
}