using System;
using System.IO.Compression;
using Character;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Extension
    {
        public static event Action<Human> PrepareSaveChara =
            _ => Plugin.Instance.Log.LogDebug("Custom character save.");

        public static event Action<Human, ZipArchive> OnSaveChara =
            (human, _) => PrepareSaveChara.Apply(human).Try(Plugin.Instance.Log.LogError);

        public static T Chara<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(human);

        public static U Coord<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(human);

        public static void Chara<T, U>(this Human human, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(human, mods);

        public static void Coord<T, U>(this Human human, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(human, mods);

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            RegisterInternal<T, U>();
            OnSaveChara += HumanExtension<T, U>.SaveChara;
            Extension<T, U>.OnCopyTrackStart += HumanExtension<T, U>.JoinCopyTrack;
            Extension<T, U>.OnCoordTrackStart += HumanExtension<T, U>.JoinLimitTrack;
            Plugin.Instance.Log.LogDebug($"ComplexExtension<{typeof(T)},{typeof(U)}> registered.");
        }

        public static T Chara<T>(this Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara(human);

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T,T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            RegisterInternal<T>();
            OnSaveChara += HumanExtension<T>.SaveChara;
            Extension<T>.OnCopyTrackStart += HumanExtension<T>.JoinCopyTrack;
            Plugin.Instance.Log.LogDebug($"SimpleExtension<{typeof(T)}> registered.");
        }
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
        public override void Load()
        {
            Instance = this;
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            CharaLoadHook.LoadFlagResolver = CharaLoadHook.CraftFlagResolver;
        }
    }
}