using BepInEx.Unity.IL2CPP;
using System;
using System.IO.Compression;
using System.Collections.Generic;
using Character;

namespace Fishbone
{
    public static partial class Extension
    {
        public static event Action<Human, ZipArchive> OnSaveChara = delegate { };

        public static T Chara<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Chara(human);

        public static U Coord<T, U>(Human human)
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() =>
            HumanExtension<T, U>.Coord(human);

        public static void Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new()
        {
            RegisterInternal<T, U>();
            OnSaveChara += HumanExtension<T, U>.SaveChara;
            PreReloadChara += HumanExtension<T, U>.LoadChara;
            PreReloadCoord += HumanExtension<T, U>.LoadCoord;
        }

        public static T Chara<T>(Human human)
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new() =>
            HumanExtension<T>.Chara(human);

        public static void Register<T>()
            where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
        {
            RegisterInternal<T>();
            OnSaveChara += HumanExtension<T>.SaveChara;
            PreReloadChara += HumanExtension<T>.LoadChara;
        }
    }

    public partial class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        public static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());

        public static U Coord(Human human) =>
            Characters.GetValueOrDefault(human, new T()).Get(human.data.Status.coordinateType);
    }

    public partial class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        public static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());
    }

    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}