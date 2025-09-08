using BepInEx.Unity.IL2CPP;
using System;
using System.IO.Compression;
using System.Collections.Generic;
using Character;
using CharacterCreation;
using CoastalSmell;

namespace Fishbone
{
    public static partial class Extension
    {
        public static bool ToActor(this Human human, out SaveData.Actor actor) =>
            (actor = ToActor(human)) is not null;

        public static T Chara<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ToActor(human, out var actor)
                ? ActorExtension<T, U>.Chara(actor)
                : HumanExtension<T, U>.Chara;

        public static U Coord<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            ToActor(human, out var actor)
                ? ActorExtension<T, U>.Coord(actor)
                : HumanExtension<T, U>.Coord;

        public static T Chara<T>(Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            ToActor(human, out var actor)
                ? ActorExtension<T>.Chara(actor)
                : HumanExtension<T>.Chara;
        public static void HumanCustomReload()
        {
            var custom = HumanCustom.Instance;
            custom.Human.Load();
            custom._motionIK = new ILLGames.Rigging.MotionIK(custom.Human, custom._motionIK._data);
            custom.LoadPlayAnimation(custom.NowPose, new() { value = 0.0f });
            custom.Human.Reload();
        }
        public static event Action PrepareSaveChara =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom character save.");
        public static event Action PrepareSaveCoord =
            F.Apply(Plugin.Instance.Log.LogDebug, "Custom coordinate save.");
        public static event Action<SaveData.Actor> PrepareSaveActor =
            actor => Plugin.Instance.Log.LogDebug($"Simulation actor{actor.charasGameParam.Index} save.");
        public static event Action<ZipArchive> OnSaveChara = PrepareSaveChara.Ignoring<ZipArchive>();
        public static event Action<ZipArchive> OnSaveCoord = PrepareSaveCoord.Ignoring<ZipArchive>();
        public static event Action<SaveData.Actor, ZipArchive> OnSaveActor = (actor, _) => PrepareSaveActor(actor); 
        public static event Action<Human> OnReloadCustomChara = delegate { };
        public static event Action<Human> OnReloadCustomCoord = delegate { };
        public static event Action<SaveData.Actor, Human> OnReloadActorChara = delegate { };
        public static event Action<SaveData.Actor, Human> OnReloadActorCoord = delegate { };

        static Extension()
        {
            PreReloadChara += ForkReloadChara;
            PreReloadCoord += ForkReloadCoord;
        }

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            RegisterInternal<T, U>();
            OnSaveChara += HumanExtension<T, U>.SaveChara;
            OnSaveCoord += HumanExtension<T, U>.SaveCoord;
            OnSaveActor += ActorExtension<T, U>.Save;
            PreReloadCustomChara += HumanExtension<T, U>.LoadChara;
            PreReloadCustomCoord += HumanExtension<T, U>.LoadCoord;
            PreReloadActorChara += ActorExtension<T, U>.LoadChara;
            PreReloadActorCoord += ActorExtension<T, U>.LoadCoord;
            OnCopyCustomToActor += ActorExtension<T, U>.OnCopy;
            OnCopyActorToCustom += HumanExtension<T, U>.OnCopy;
            OnChangeActorCoord += ActorExtension<T, U>.CoordinateChange;
        }

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            RegisterInternal<T>();
            OnSaveChara += HumanExtension<T>.SaveChara;
            OnSaveActor += ActorExtension<T>.Save;
            PreReloadCustomChara += HumanExtension<T>.LoadChara;
            PreReloadActorChara += ActorExtension<T>.LoadChara;
            OnCopyCustomToActor += ActorExtension<T>.OnCopy;
            OnCopyActorToCustom += HumanExtension<T>.OnCopy;
        }
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
    }
}