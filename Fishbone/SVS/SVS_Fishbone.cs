using System;
using System.IO.Compression;
using System.Collections.Generic;
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Extension
    {
        public static event Action PrepareSaveChara =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom character save.");
        public static event Action<ZipArchive> OnSaveChara =
            archive => PrepareSaveChara.Try(Plugin.Instance.Log.LogError);

        public static event Action PrepareSaveCoord =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom coordinate save.");
        public static event Action<ZipArchive> OnSaveCoord =
            archive => PrepareSaveCoord.Try(Plugin.Instance.Log.LogError);

        public static event Action<SaveData.Actor> PrepareSaveActor = actor =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} save.");
        public static event Action<SaveData.Actor, ZipArchive> OnSaveActor =
            (actor, _) => PrepareSaveActor.Apply(actor).Try(Plugin.Instance.Log.LogError); 

        public static event Action<Human> OnLoadCustomChara = delegate { };
        public static event Action<Human> OnLoadCustomCoord = delegate { };

        public static event Action<SaveData.Actor> OnLoadActor =
            actor => PreLoadActor.Apply(actor).Try(Plugin.Instance.Log.LogError); 
        public static event Action<SaveData.Actor, Human> OnLoadActorChara = delegate { };
        public static event Action<SaveData.Actor, Human> OnLoadActorCoord = delegate { };

        public static T Chara<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Chara(index)
                : HumanExtension<T, U>.Chara;

        public static U Coord<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Coord(index)
                : HumanExtension<T, U>.Coord;

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            RegisterInternal<T, U>();
            OnSaveChara += HumanExtension<T, U>.SaveChara;
            OnSaveCoord += HumanExtension<T, U>.SaveCoord;
            OnSaveActor += ActorExtension<T, U>.Save;
            PreLoadCustomChara += HumanExtension<T, U>.LoadChara;
            PreLoadCustomCoord += HumanExtension<T, U>.LoadCoord;
            PreLoadActor += ActorExtension<T, U>.LoadActor;
            PreLoadActorChara += ActorExtension<T, U>.LoadChara;
            PreLoadActorCoord += ActorExtension<T, U>.LoadCoord;
            OnCopyCustomToActor += ActorExtension<T, U>.OnCopy;
            OnCopyActorToCustom += HumanExtension<T, U>.OnCopy;
            OnActorCoordChange += ActorExtension<T, U>.CoordinateChange;
            OnEnterCustom += Extension<T, U>.Initialize;
            OnLeaveCustom += Extension<T, U>.Initialize;
            OnEnterCustom += HumanExtension<T, U>.Initialize;
            OnLeaveCustom += HumanExtension<T, U>.Initialize;
            OnCustomInitialize += HumanExtension<T, U>.Initialize;
            Plugin.Instance.Log.LogDebug($"ComplexExtension<{typeof(T)},{typeof(U)}> regiistered.");
        }

        public static T Chara<T>(Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanActors.TryGetValue(human, out var index)
                ? ActorExtension<T>.Chara(index)
                : HumanExtension<T>.Chara;

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            RegisterInternal<T>();
            OnSaveChara += HumanExtension<T>.SaveChara;
            OnSaveActor += ActorExtension<T>.Save;
            PreLoadCustomChara += HumanExtension<T>.LoadChara;
            PreLoadActor += ActorExtension<T>.LoadActor;
            PreLoadActorChara += ActorExtension<T>.LoadChara;
            OnCopyCustomToActor += ActorExtension<T>.OnCopy;
            OnCopyActorToCustom += HumanExtension<T>.OnCopy;
            OnEnterCustom += Extension<T>.Initialize;
            OnLeaveCustom += Extension<T>.Initialize;
            OnEnterCustom += HumanExtension<T>.Initialize;
            OnLeaveCustom += HumanExtension<T>.Initialize;
            OnCustomInitialize += HumanExtension<T>.Initialize;
            Plugin.Instance.Log.LogDebug($"SimpleExtension<{typeof(T)}> regiistered.");
        }
        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static T Current = new();

        public static T Chara => Current;

        public static U Coord =>
            Current.Get(HumanCustom.Instance?.Human?.data?.Status.coordinateType ?? 0) ?? new ();
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static T Current = new();

        public static T Chara => Current;
    }

    public partial class ActorExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly Dictionary<int, T> Charas = new();
        static readonly Dictionary<int, U> Coords = new();

        public static T Chara(SaveData.Actor actor) =>
            Chara(actor.charasGameParam.Index);

        public static U Coord(SaveData.Actor actor) =>
            Coord(actor.charasGameParam.Index);

        public static T Chara(int index) =>
            Charas.GetValueOrDefault(index, new());

        public static U Coord(int index) =>
            Coords.GetValueOrDefault(index, new());
    }

    public partial class ActorExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<int, T> Charas = new();

        public static T Chara(SaveData.Actor actor) =>
            Chara(actor.charasGameParam.Index);

        public static T Chara(int index) =>
            Charas.GetValueOrDefault(index, new());
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public override void Load()
        {
            Instance = this;
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            Extension.Initialize();
        }
    }
}