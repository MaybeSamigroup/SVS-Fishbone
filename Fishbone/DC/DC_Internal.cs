using System.IO.Compression;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Character;
using HarmonyLib;
using CoastalSmell;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;
using System;

namespace Fishbone
{
    static partial class Hooks
    {
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanData), nameof(HumanData.SaveFile), typeof(Il2CppWriter), typeof(bool))]
        static void HumanDataSaveFilePrefix(HumanData __instance, Il2CppWriter bw) =>
            bw.Write(__instance.PngData);

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(DigitalCraft.SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPrefix() => CoordLoadTrack.Mode = CoordLoadTrack.Ignore;

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanDataCoordinate), nameof(HumanDataCoordinate.GetProductNo))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitList))]
        [HarmonyPatch(typeof(DigitalCraft.MPCharCtrl.CostumeInfo), nameof(DigitalCraft.MPCharCtrl.CostumeInfo.InitFileList))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Import), typeof(Il2CppReader), typeof(Il2CppSystem.Version))]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(DigitalCraft.SceneDataFile)], [ArgumentType.Normal, ArgumentType.Out])]
        [HarmonyPatch(typeof(DigitalCraft.SceneInfo), nameof(DigitalCraft.SceneInfo.Load),
            [typeof(string), typeof(Il2CppSystem.Version), typeof(bool), typeof(bool)], [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
        static void CostumeInfoInitFileListPostfix() => CoordLoadTrack.Mode = CoordLoadTrack.Aware;
    }
    static partial class Hooks
    {
        static Subject<Human> SaveHuman = new();
        internal static IObservable<Human> OnSaveHuman => SaveHuman.AsObservable();

        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(DigitalCraft.DigitalCraft), nameof(DigitalCraft.DigitalCraft.SaveScene))]
        static void DigitalCraftSaveScenePrefix() => Human.list.Yield().ForEach(SaveHuman.OnNext);
    } 
    public static partial class Extension
    {
        static void Save(Human human, IObserver<(Human, ZipArchive)> observer) =>
            Implant(human.data, ToBinary(Observer.Create<ZipArchive>(archive => observer.OnNext((human, archive)))));
    }
    class CharaCopyTrack : IDisposable
    {
        protected CompositeDisposable Subscription;
        protected IObservable<HumanData> OnDataUpdate;
        internal IObservable<Human> OnResolve;
        HumanData Data;
        CharaCopyTrack() =>
            (OnDataUpdate, OnResolve) = (
                Hooks.OnHumanDataCopy.Where(Match).Select(tuple => tuple.Item2),
                Hooks.OnHumanResolve.Where(Match).FirstAsync());
        internal CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()),
                OnResolve.Subscribe(_ => Dispose()),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData, T) tuple) => Data == tuple.Item1;
        bool Match(Human human) => Data == human.data; 
        void Resolve(HumanData value) => Data = value;
        public void Dispose() => Subscription.Dispose();
    }
    public static partial class Extension
    {
        internal static IObservable<(CharaCopyTrack, HumanData, ZipArchive)> OnTrackChara =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Select(tuple => (new CharaCopyTrack(tuple.Item1), tuple.Item1, tuple.Item2));
    }
    public static partial class Extension<T, U>
    {
        static IObservable<(CharaCopyTrack, HumanData, T)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(Human, T)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Item1.OnResolve.Select(human => (human, tuple.Item3)));
    }
    public static partial class Extension<T>
    {
        static IObservable<(CharaCopyTrack, HumanData, T)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Item1, tuple.Item2, LoadChara(tuple.Item3)));
        internal static IObservable<(Human, T)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Item1.OnResolve.Select(human => (human, tuple.Item3)));
    }
    static partial class Hooks
    {
        static Subject<(Human, int)> ChangeCoordinate = new();
        internal static IObservable<(Human, int)> OnChangeCoordinate => ChangeCoordinate.AsObservable();

        [HarmonyPostfix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCoordinate), nameof(HumanCoordinate.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        static void HumanCoordinateChangeCoordinateTypePostfix(HumanCoordinate __instance, ChaFileDefine.CoordinateType type, bool changeBackCoordinateType) =>
            (changeBackCoordinateType || __instance.human.data.Status.coordinateType != (int)type)
                .Maybe(F.Apply(ChangeCoordinate.OnNext, (__instance.human, (int)type)));
    }

    internal static class HumanExtension<T, U>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new()
        where U : CoordinateExtension<U>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        internal static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());

        internal static U Coord(Human human) =>
            Characters.GetValueOrDefault(human, new T()).Get(human.data.Status.coordinateType);

        internal static void Chara(Human human, T mods) =>
            Characters[human] = mods;

        internal static void Coord(Human human, U mods) =>
            Characters[human] = Chara(human).Merge(human.data.Status.coordinateType, mods);

        internal static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable()
                .Subscribe(F.Apply(Characters.Remove, human).Ignoring().Ignoring<Unit>());

        internal static void LoadCoord(Human human, CoordLimit limit, U value) =>
            Characters[human] = Characters.GetValueOrDefault(human, new()).Merge(human.data.Status.coordinateType, limit, value);
    }

    internal static class HumanExtension<T>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        static readonly Dictionary<Human, T> Characters = new();

        internal static T Chara(Human human) =>
            Characters.GetValueOrDefault(human, new());

        internal static void Chara(Human human, T mods) =>
            Characters[human] = mods;

        internal static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable()
                .Subscribe(F.Apply(Characters.Remove, human).Ignoring().Ignoring<Unit>());
    }

    public static partial class Extension
    {
        internal static void Initialize()
        {
            CharaLoadTrack.Mode = CharaLoadTrack.FlagAware;
#if DEBUG
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara"));
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara:{CharaLoadTrack.Mode.ToString()}"));
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord"));
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load"));
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load"));
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change"));
#endif
        }
    }

}