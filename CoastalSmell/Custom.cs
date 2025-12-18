using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Character;
using CharacterCreation;
using HarmonyLib;

namespace CoastalSmell
{
    public static class HumanCustomExtension
    {
        public static IObservable<GameObject> OnUIPrefab(string bundle, string asset) =>
            Hooks.UIPrefab.AsObservable().Where(tuple => (tuple.Item1, tuple.Item2) == (bundle, asset)).Select(tuple => tuple.Item3);
        public static IObservable<HumanBody> OnBodyChange => Hooks.BodyChange.AsObservable();
        public static IObservable<HumanFace> OnFaceChange => Hooks.FaceChange.AsObservable();
        public static IObservable<(HumanHair, int)> OnHairChange => Hooks.HairChange.AsObservable();
        public static IObservable<(HumanCloth, int)> OnClothesChange => Hooks.ClothesChange.AsObservable();
        public static IObservable<(HumanAccessory, int)> OnAccessoryChange => Hooks.AccessoryChange.AsObservable();
    }

    public static partial class Hooks {
        internal static Subject<(string, string, GameObject)> UIPrefab = new ();

        static Hooks() => UIPrefab.Subscribe(tuple => Plugin.Instance.Log.LogInfo($"{tuple.Item1},{tuple.Item2}"));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadObject))]
        static void NotifyUIPrefabLoad(string bundle, string asset, GameObject __result) =>
            UIPrefab.OnNext((bundle, asset, __result));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(CategoryViewBinderBase), nameof(CategoryViewBinderBase.SetResult))]
        static void NotifyUIPrefabBind(CategoryViewBinderBase __instance, GameObject o) =>
            UIPrefab.OnNext((CategoryView.GetBundlePath(HumanCustom.Instance.SelectionTop._openIndex, __instance.GetFile()), __instance.GetFile(), o));

        internal static Subject<HumanBody> BodyChange = new ();
        internal static Subject<HumanFace> FaceChange = new ();
        internal static Subject<(HumanHair, int)> HairChange = new ();
        internal static Subject<(HumanCloth, int)> ClothesChange = new ();
        internal static Subject<(HumanAccessory, int)> AccessoryChange = new ();

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.CreateBodyTexture))]
        [HarmonyPatch(typeof(HumanBody), nameof(HumanBody.InitBaseCustomTextureBody))]
        internal static void InitBaseCustomTextureBodyPostfix(HumanBody __instance) => BodyChange.OnNext(__instance);

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanFace), nameof(HumanFace.ChangeHead), typeof(int), typeof(bool))]
        static void ChangeHeadPostfix(HumanFace __instance) => FaceChange.OnNext(__instance);
            
        [HarmonyPostfix]
        [HarmonyWrapSafe]
#if Aicomi
        [HarmonyPatch(typeof(HumanHair), nameof(HumanHair.ChangeHair), typeof(ChaFileDefine.HairKind), typeof(int), typeof(bool))]
#else
        [HarmonyPatch(typeof(HumanHair), nameof(HumanHair.ChangeHair), typeof(int), typeof(int), typeof(bool))]
#endif
        static void ChangeHairPostfix(HumanHair __instance, int kind) => HairChange.OnNext((__instance, kind));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBot), typeof(int), typeof(bool))]
        static void ChangeClothesBotPostfix(HumanCloth __instance) => ClothesChange.OnNext((__instance, 1));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesGloves), typeof(int), typeof(bool))]
        static void ChangeClothesGlovesPostfix(HumanCloth __instance) => ClothesChange.OnNext((__instance, 4));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesPanst), typeof(int), typeof(bool))]
        static void ChangeClothesPanstPostfix(HumanCloth __instance) => ClothesChange.OnNext((__instance, 5));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesSocks), typeof(int), typeof(bool))]
        static void ChangeClothesSocksPostfix(HumanCloth __instance) => ClothesChange.OnNext((__instance, 6));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShoes), typeof(int), typeof(bool))]
        static void ChangeClothesShoesPostfix(HumanCloth __instance) => ClothesChange.OnNext((__instance, 7));

        static readonly int[] BraOnly = [2];
        static readonly int[] ShortsOnly = [3];
        static readonly int[] BraAndShorts = [2, 3];

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBra), typeof(int), typeof(bool))]
        static void ChangeClothesBraPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notShorts;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesBra), typeof(int), typeof(bool))]
        static void ChangeClothesBraPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notShorts) ? BraOnly : BraAndShorts)
                .ForEach(kind => ClothesChange.OnNext((__instance, kind)));

        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShorts), typeof(int), typeof(bool))]
        static void ChangeClothesShortsPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notBra;

        [HarmonyPostfix]
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesShorts), typeof(int), typeof(bool))]
        static void ChangeClothesShortsPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notShorts) ? ShortsOnly : BraAndShorts)
                .ForEach(kind => ClothesChange.OnNext((__instance, kind)));
        static readonly int[] TopOnly = [0];
        static readonly int[] TopAndBot = [0, 1];

        [HarmonyPrefix]
        [HarmonyWrapSafe]
#if Aicomi
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(bool), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
#else
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(HumanCloth.TopResultData), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
#endif
        static void ChangeClothesTopPrefix(HumanCloth __instance, ref bool __state) =>
            __state = __instance.notBot;

        [HarmonyPostfix]
#if Aicomi
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(bool), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
#else
        [HarmonyWrapSafe]
        [HarmonyPatch(typeof(HumanCloth), nameof(HumanCloth.ChangeClothesTop),
            [typeof(HumanCloth.TopResultData), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool)],
            [ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal])]
#endif
        static void ChangeClothesTopPostfix(HumanCloth __instance, bool __state) =>
            ((__state == __instance.notBot) ? TopOnly : TopAndBot).ForEach(kind => ClothesChange.OnNext((__instance, kind)));

        [HarmonyPostfix]
        [HarmonyWrapSafe]
#if Aicomi
        [HarmonyPatch(typeof(HumanAccessory), nameof(HumanAccessory.ChangeAccessory),
            typeof(int), typeof(ChaListDefine.CategoryNo), typeof(int), typeof(ChaAccessoryDefine.AccessoryParentKey), typeof(bool))]
#else
        [HarmonyPatch(typeof(HumanAccessory), nameof(HumanAccessory.ChangeAccessory),
            typeof(int), typeof(int), typeof(int), typeof(ChaAccessoryDefine.AccessoryParentKey), typeof(bool))]
#endif
        static void ChangeAccessoryPostfix(HumanAccessory __instance, int slotNo) =>
            AccessoryChange.OnNext((__instance, slotNo));
    }
}
