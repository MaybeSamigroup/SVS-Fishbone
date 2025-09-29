using System;
using System.IO.Compression;
using AC.User;
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
            () => Plugin.Instance.Log.LogDebug($"Custom coordinate {HumanCustom.Instance.Human.data.Status.coordinateType} save.");
        public static event Action<ZipArchive> OnSaveCoord =
            archive => PrepareSaveCoord.Try(Plugin.Instance.Log.LogError);

        public static event Action<ActorData> PrepareSaveActor = actor =>
            Plugin.Instance.Log.LogDebug($"Simulation actor{actor.ToIndex()} save.");
        public static event Action<ActorData, ZipArchive> OnSaveActor =
            (actor, _) => PrepareSaveActor.Apply(actor).Try(Plugin.Instance.Log.LogError);

        public static event Action<Human> OnLoadCustomChara = delegate { };
        public static event Action<Human> OnLoadCustomCoord = delegate { };

        public static event Action<ActorData> OnLoadActor = delegate { };
        public static event Action<ActorData, Human> OnLoadActorChara = delegate { };
        public static event Action<ActorData, Human> OnLoadActorCoord = delegate { };

        public static T Chara<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Chara(index)
                : HumanExtension<T, U>.Chara();

        public static U Coord<T, U>(this Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T, U>.Coord(index)
                : HumanExtension<T, U>.Coord();

        public static void Chara<T, U>(this Human human, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Chara<T, U>, mods), F.Apply(ActorExtension<T, U>.Chara, index, mods));

        public static void Coord<T, U>(this Human human, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Coord<T, U>, mods), F.Apply(ActorExtension<T, U>.Coord, index, mods));

        public static T Chara<T, U>(this ActorData actor)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Chara(actor.ToIndex());

        public static U Coord<T, U>(this ActorData actor)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Coord(actor.ToIndex());

        public static void Chara<T, U>(this ActorData actor, T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Chara(actor.ToIndex(), mods);

        public static void Coord<T, U>(this ActorData actor, U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ActorExtension<T, U>.Coord(actor.ToIndex(), mods);

        public static T Chara<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara();

        public static U Coord<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord();

        public static void Chara<T, U>(T mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(mods);

        public static void Coord<T, U>(U mods)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(mods);

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            RegisterInternal<T, U>();
            OnSaveChara += HumanExtension<T, U>.SaveChara;
            OnSaveCoord += HumanExtension<T, U>.SaveCoord;
            OnSaveActor += ActorExtension<T, U>.Save;
            OnEnterCustom += HumanExtension<T, U>.EnterCustom;
            OnLeaveCustom += HumanExtension<T, U>.LeaveCustom;
            OnEnterCustom += ActorExtension<T, U>.EnterCustom;
            OnLeaveCustom += ActorExtension<T, U>.LeaveCustom;
            OnCopyCustomToActor += ActorExtension<T, U>.Copy;
            OnCopyActorToCustom += HumanExtension<T, U>.Copy;
            OnCustomInitialize += HumanExtension<T, U>.Initialize;
            OnSwapIndex = ActorExtension<T, U>.SwapIndex;
            HumanExtension<T, U>.LeaveCustom();
            ActorExtension<T, U>.LeaveCustom();
            ActorExtension<T, U>.Initialize();
            Plugin.Instance.Log.LogDebug($"ComplexExtension<{typeof(T)},{typeof(U)}> registered.");
        }

        public static T Chara<T>(this Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                ? ActorExtension<T>.Chara(index)
                : HumanExtension<T>.Chara();

        public static void Chara<T>(this Human human, T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanToActors.TryGetValue(human, out var index)
                .Either(F.Apply(Chara, mods), F.Apply(ActorExtension<T>.Chara, index, mods));

        public static T Chara<T>(this ActorData actor)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            ActorExtension<T>.Chara(actor.ToIndex());

        public static void Chara<T>(this ActorData actor, T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            ActorExtension<T>.Chara(actor.ToIndex(), mods);

        public static T Chara<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara();

        public static void Chara<T>(T mods)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara(mods);

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            RegisterInternal<T>();
            OnSaveChara += HumanExtension<T>.SaveChara;
            OnSaveActor += ActorExtension<T>.Save;
            OnEnterCustom += HumanExtension<T>.EnterCustom;
            OnLeaveCustom += HumanExtension<T>.LeaveCustom;
            OnEnterCustom += ActorExtension<T>.EnterCustom;
            OnLeaveCustom += ActorExtension<T>.LeaveCustom;
            OnCopyCustomToActor += ActorExtension<T>.Copy;
            OnCopyActorToCustom += HumanExtension<T>.Copy;
            OnCustomInitialize += HumanExtension<T>.Initialize;
            OnSwapIndex = ActorExtension<T>.SwapIndex;
            HumanExtension<T>.LeaveCustom();
            ActorExtension<T>.LeaveCustom();
            ActorExtension<T>.Initialize();
            Plugin.Instance.Log.LogDebug($"SimpleExtension<{typeof(T)}> registered.");
        }
        public static void HumanCustomReload() => HumanCustomReload(HumanCustom.Instance);
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "Aicomi";
        public override void Load()
        {
            Instance = this;
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            Extension.Initialize();
        }
    }
}