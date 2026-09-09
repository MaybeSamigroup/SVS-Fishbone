using System;
using System.Reactive.Linq;
using BepInEx.Unity.IL2CPP;

namespace Fishbone
{
    /// <summary>
    /// Platform-level extension entrypoints (save/load/convert hooks) used by plugin registration.
    /// </summary>
    public static partial class Extension
    {
        /// <summary>Register complex character and coordinate extension</summary>
        public static IDisposable[] Register<T, U>()
            where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
            where U : CoordinateExtension<U>, new() => [
            OnSaveActor.Subscribe(Extension<T, U>.SaveActorChara),
            OnSaveChara.Subscribe(Extension<T, U>.SaveCustomChara),
            OnSaveCoord.Subscribe(Extension<T, U>.SaveCustomCoord),
            OnActorsCleanup.Subscribe(Extension<T, U>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T, U>.ClearCustom),
            Extension<T, U>.OnLoadCustomChara.Subscribe(tuple => Extension<T, U>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T, U>.OnLoadActorChara.Subscribe(tuple => Extension<T, U>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T, U>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T, U>.ActorToCustom),
            Extension<T, U>.OnLoadCoordInternal.Subscribe(tuple => Extension<T, U>.Humans.NowCoordinate[tuple.Human, tuple.Limit] = tuple.Value),
            OnActorHumanize.Subscribe(tuple => Extension<T,U>.Indices.NowCoordinate[tuple.Index] = Extension<T, U>.Indices[tuple.Index, tuple.Human.data.Status.coordinateType]),
            OnChangeActorCoord.Subscribe(tuple => Extension<T, U>.Indices.NowCoordinate[tuple.Index] =  Extension<T, U>.Indices[tuple.Index, tuple.CoordinateType])
        ];

        /// <summary>Register simple character extension</summary>
        public static IDisposable[] Register<T>() where T : CharacterExtension<T>, new() => [
            OnSaveActor.Subscribe(Extension<T>.SaveActorChara),
            OnSaveChara.Subscribe(Extension<T>.SaveCustomChara),
            OnActorsCleanup.Subscribe(Extension<T>.ClearActors),
            OnInitializeCustom.Subscribe(Extension<T>.ClearCustom),
            Extension<T>.OnLoadCustomChara.Subscribe(tuple => Extension<T>.Humans[tuple.Human, tuple.Limit] = tuple.Value),
            Extension<T>.OnLoadActorChara.Subscribe(tuple => Extension<T>.Indices[tuple.Index] = tuple.Value),
            OnCopyCustomToActor.Subscribe(Extension<T>.CustomToActor),
            OnCopyActorToCustom.Subscribe(Extension<T>.ActorToCustom)
        ];
    }
    /// <summary>
    /// SamabakeScramble platform plugin marker for Fishbone.
    /// </summary>
    public partial class Plugin : BasePlugin
    {
        /// <summary>supported process name</summary>
        public const string Process = "SamabakeScramble";
    }
}