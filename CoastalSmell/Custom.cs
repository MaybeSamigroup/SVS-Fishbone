using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Character;
using CharacterCreation;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;

namespace CoastalSmell
{
    public static class HumanCustomExtension
    {
        static Dictionary<(string, string), Subject<GameObject>> UIPrefab = new ();
        static Subject<GameObject> UIPrefabSubject(string bundle, string asset) =>
            UIPrefab[(bundle, asset)] = UIPrefab.GetValueOrDefault((bundle, asset), new());
        public static IObservable<GameObject> UIPrefabObservable(string bundle, string asset) =>
            UIPrefabSubject(bundle, asset).AsObservable();
        static void NotifyUIPrefabLoad(string bundle, string asset, GameObject go) =>
            UIPrefabSubject(bundle, asset).OnNext(go);
        static void NotifyUIPrefabLoad(string asset, GameObject go) =>
            NotifyUIPrefabLoad(CategoryView.GetBundlePath(HumanCustom.Instance.SelectionTop._openIndex, asset), asset, go);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadObject))]
        static void HumanCustomLoadObjectPostfix(string bundle, string asset, GameObject __result) =>
            NotifyUIPrefabLoad(bundle, asset, __result);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CategoryViewBinderBase), nameof(CategoryViewBinderBase.SetResult))]
        static void CategoryViewBinderBaseSetResultPostfix(CategoryViewBinderBase __instance, GameObject o) =>
            NotifyUIPrefabLoad(__instance.GetFile(), o);

        public static IObservable<Human> Human => HumanSubject.AsObservable();
        static Subject<Human> HumanSubject = new();
        static HumanCustomExtension() =>
            SingletonInitializerExtension<HumanCustom>.OnStartup.Subscribe(custom =>
                UniTask.WaitUntil((Func<bool>)(() => custom.Human != null),
                    PlayerLoopTiming.Initialization,
                    Il2CppSystem.Threading.CancellationToken.None, false)
                        .ContinueWith((Action)(() => HumanSubject.OnNext(custom.Human))));
    }

    #region Plugin

    public partial class Plugin : BasePlugin
    {
        Harmony Patch;
        public override void Load()
        {
            Patch = Harmony.CreateAndPatchAll(typeof(HumanCustomExtension), $"{Name}.Hooks");
            Logger = Log;
            Sprites.Initialize();
            UGUI.Initialize();
        }
        public override bool Unload() => true.With(Patch.UnpatchSelf) && base.Unload();
    }

    #endregion

}
