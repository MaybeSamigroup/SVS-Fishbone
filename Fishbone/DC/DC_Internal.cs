using System.IO.Compression;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Character;
using HarmonyLib;
using CoastalSmell;
using Il2CppReader = Il2CppSystem.IO.BinaryReader;
using Il2CppWriter = Il2CppSystem.IO.BinaryWriter;
using System;

namespace Fishbone
{
    class HumansStorage<T,U> : Storage<T, U, Human>
        where T : ComplexExtension<T, U>, CharacterExtension<T>, new() where U : CoordinateExtension<U>, new()
    {
        Dictionary<Human, T> Humans = new(Il2CppEquals.Instance);
        public T Get(Human human) => Humans.GetValueOrDefault(human, new ());
        public void Set(Human human, T value) => Humans[human] = value; 
        public U GetNowCoordinate(Human human) => Get(human).Get(human.data.Status.coordinateType);
        public void SetNowCoordinate(Human human, U value) => Humans[human] = Get(human).Merge(human.data.Status.coordinateType, value);
        internal void Remove(Human human) => Humans.Remove(human);
    }
    class HumansStorage<T> : Storage<T, Human>
        where T : SimpleExtension<T>, ComplexExtension<T, T>, CharacterExtension<T>, CoordinateExtension<T>, new()
    {
        Dictionary<Human, T> Humans = new(Il2CppEquals.Instance);
        public T Get(Human human) => Humans.GetValueOrDefault(human, new());
        public void Set(Human human, T value) => Humans[human] = value; 
        internal void Remove(Human human) => Humans.Remove(human);
    }

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
        [HarmonyPrefix, HarmonyWrapSafe]
        [HarmonyPatch(typeof(DigitalCraft.DigitalCraft), nameof(DigitalCraft.DigitalCraft.SaveScene))]
        static void DigitalCraftSaveScenePrefix() => Human.list.Yield().ForEach(Extension.Save);
    } 
    public static partial class Extension
    {
        static Subject<Human> PrepareSaveChara = new();
        static Subject<(ZipArchive, Human)> SaveChara = new();
        internal static void Save(Human human) =>
            Implant(human.data, ToBinary(SaveChara, human.With(PrepareSaveChara.OnNext)));
    }
    public static partial class Extension<T, U>
    {
        internal static void SaveChara((ZipArchive Archive, Human Human) tuple) =>
            SaveChara(tuple.Archive, Humans[tuple.Human]);
    }
    public static partial class Extension<T>
    {
        internal static void SaveChara((ZipArchive Archive, Human Human) tuple) =>
            SaveChara(tuple.Archive, Values[tuple.Human]);
    }

    class CharaCopyTrack : IDisposable
    {
        protected CompositeDisposable Subscription;
        protected IObservable<HumanData> OnDataUpdate;
        internal IObservable<Human> OnResolve;
        HumanData Data;
        CharaCopyTrack() =>
            (OnDataUpdate, OnResolve) = (
                Hooks.OnHumanDataCopy.Where(Match).Select(tuple => tuple.Dst),
                Hooks.OnHumanResolve.Where(Match).FirstAsync());
        internal CharaCopyTrack(HumanData data) : this() =>
            (Data, Subscription) = (data, [
                CharaLoadTrack.OnModeUpdate.Subscribe(_ => Dispose()),
                OnResolve.Subscribe(_ => Dispose()),
                OnDataUpdate.Subscribe(Resolve),
            ]);
        bool Match<T>((HumanData Data, T Value) tuple) => Il2CppEquals.Apply(Data, tuple.Data);
        bool Match(Human human) => Il2CppEquals.Apply(Data, human.data); 
        void Resolve(HumanData value) => Il2CppEquals.Apply(Data, value);
        public void Dispose() => Subscription.Dispose();
    }
    public static partial class Extension
    {
        internal static IObservable<(CharaCopyTrack Track, HumanData Data, ZipArchive Value)> OnTrackChara =>
            OnPreprocessChara.Where(_ => CharaLoadTrack.Mode == CharaLoadTrack.FlagAware)
                .Select(tuple => (new CharaCopyTrack(tuple.Data), tuple.Data, tuple.Archive));
    }
    public static partial class Extension<T, U>
    {
        static IObservable<(CharaCopyTrack Track, HumanData Data, T Value)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, T Value)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve.Select(human => (human, tuple.Value)));
        internal static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable().Select(_ => human).Subscribe(Storage.Remove);
    }
    public static partial class Extension<T>
    {
        static IObservable<(CharaCopyTrack Track, HumanData Data, T Value)> OnTrackChara =>
            Extension.OnTrackChara.Select(tuple => (tuple.Track, tuple.Data, LoadChara(tuple.Value)));
        internal static IObservable<(Human Human, T Value)> OnLoadChara =>
            OnTrackChara.SelectMany(tuple => tuple.Track.OnResolve.Select(human => (human, tuple.Value)));
        internal static void Prepare(Human human) =>
            human.component.OnDestroyAsObservable().Select(_ => human).Subscribe(Storage.Remove);
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

    public static partial class Extension
    {
        internal static IDisposable[] Initialize() => [
#if DEBUG
            OnPrepareSaveChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("prepare save chara")),
            OnPreprocessChara.Subscribe(_ => Plugin.Instance.Log.LogDebug($"preprocess chara")),
            OnPreprocessCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("preprocess coord")),
            OnLoadChara.Subscribe(_ => Plugin.Instance.Log.LogDebug("chara load")),
            OnLoadCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coord load")),
            OnChangeCoord.Subscribe(_ => Plugin.Instance.Log.LogDebug("coordinate change"))
#endif
        ];
    }

}