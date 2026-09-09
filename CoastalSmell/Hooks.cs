using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Startup;
using Il2CppInterop.Runtime.Runtime;
using MonoMod.Utils;
using HarmonyLib;
using Character;
#if Aicomi
using ILLGAMES.Unity;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#else
using ILLGames.Unity;
#endif

namespace CoastalSmell
{
    public static class HumanExtension
    {
        internal static Subject<(Human Human, ChaListDefine.CategoryNo Category, int Id, string Name, GameObject Prefab)> PrefabReady = new();

        public static IObservable<(Human Human, ChaListDefine.CategoryNo Category, int Id, string Name, GameObject Prefab)> OnPrefabReady = PrefabReady.AsObservable();

        internal static Subject<(Human Human, HumanData Data)> ConstructionStart = new();

        public static IObservable<(Human Human, HumanData Data)> OnConstructionStart = ConstructionStart.AsObservable();

        internal static Subject<Human> ConstructionComplete = new();

        public static IObservable<Human> OnConstructionComplete = ConstructionComplete.AsObservable();

        public static IObservable<ChaListControl> OnChaListControlReady =
            Hooks.ChaListControlReady.AsObservable();

        public static void Subscribe(this Human human, IDisposable disposable) =>
            human.onDestroy.Wrap().Subscribe(_ => disposable.Dispose());
    }

    public static class HarmonyExtension
    {
        internal const BindingFlags METHOD_LOOKUP = BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic;

        public static IDisposable AsDisposable(this Harmony hi) =>
            Disposable.Create(hi.UnpatchSelf);

        public static Harmony Prefix(this Harmony hi, Type type, string prefix, params MethodBase[] targets) =>
            hi.With(Prefix(new HarmonyMethod(type.GetMethod(prefix, METHOD_LOOKUP)) { wrapTryCatch = true }, targets));

        public static Harmony Postfix(this Harmony hi, Type type, string postfix, params MethodBase[] targets) =>
            hi.With(Postfix(new HarmonyMethod(type.GetMethod(postfix, METHOD_LOOKUP)) { wrapTryCatch = true }, targets));

        static Action<Harmony> Prefix(HarmonyMethod prefix, MethodBase[] targets) =>
            hi => targets.ForEach(target => hi.Patch(target, prefix: prefix));

        static Action<Harmony> Postfix(HarmonyMethod postfix, MethodBase[] targets) =>
            hi => targets.ForEach(target => hi.Patch(target, postfix: postfix));
    }

    public static partial class Hooks
    {
        static Subject<string> SceneLoad = new();

        public static IObservable<string> OnSceneLoad => SceneLoad.AsObservable();

        static void SceneLoadStartPostfix(Manager.Scene.Data data, ref UniTask __result) =>
            __result = __result.ContinueWith(F.Apply<Action, Action<string>>(F.Try,
                F.Apply(SceneLoad.OnNext, data.LevelName), Plugin.Instance.Log.LogError));

        static void HumanLoadCharaFbxDataPostfix(Human __instance, int category, int id, string createName, GameObject __result) =>
            HumanExtension.PrefabReady
                .With(() => Plugin.Instance.Log.LogInfo($"prefab ready: {category}:{id}:{createName}"))
                .OnNext((__instance, (ChaListDefine.CategoryNo)category, id, createName, __result));

        static void NotifyCoordinateChange() =>
            Plugin.Instance.Log.LogInfo($"coordinate change");

        static void LoadFileLimitedPostfix(HumanDataCoordinate data, string path, HumanDataCoordinate.LoadLimited.Flags flags) =>
            Plugin.Instance.Log.LogInfo($"coordinate load: {data.Pointer}, {path}, {flags}");

        static void HumanCtorPrefix(Human human, HumanData data) =>
            HumanExtension.ConstructionStart.OnNext((human, data));

        static void HumanCtorPostfix(Human human) =>
            HumanExtension.ConstructionComplete.OnNext(human);

        internal static Subject<ChaListControl> ChaListControlReady = new();

        static void LoadListInfoAllPostfix(ChaListControl __instance) =>
            ChaListControlReady.OnNext(__instance);

#if Aicomi
        unsafe delegate IntPtr NativeHumanCtor(IntPtr self, IntPtr data,
            byte hipoly, byte releaseCustomTexture, void* acsSlotLimit, IntPtr useCoordeCaches, Il2CppMethodInfo* method);
#else
        unsafe delegate IntPtr NativeHumanCtor(IntPtr self, IntPtr data, byte hipoly, byte releaseCustomTexture, Il2CppMethodInfo* method);
        unsafe delegate IntPtr NativeChaListControlLoadListInfoAll(IntPtr self, IntPtr* limited, Il2CppMethodInfo* method);
#endif

#if DigitalCraft
        unsafe delegate IntPtr NativeLoadClothesFile(IntPtr self,
            IntPtr* coordinate, byte hair, byte clothes, byte acs, byte face, byte body,
            IntPtr* indexes, IntPtr onBefore, IntPtr onAfter, Il2CppMethodInfo* method);
#endif
        unsafe delegate IntPtr NativeHumanDataLoadFileLimited(IntPtr self, IntPtr path,
            void* sex, HumanDataCoordinate.LoadLimited.Flags flags, Il2CppMethodInfo* method);

#if SamabakeScramble
        unsafe delegate IntPtr NativeHumanLoadCharaFbxData(
            IntPtr self, IntPtr lib, IntPtr disposable, IntPtr components, byte hiPoly,
            ChaListDefine.CategoryNo category, int id, IntPtr createName, byte copyDynamicBone,
            Human.UseDynamicBoneType useDynamicType,
            Human.UseCopyWeightType copyWeightType,
            IntPtr trfParent, int defaultId, byte worldPositionStays, Il2CppMethodInfo* method);
#endif

        internal static IDisposable[] Initialize() => [
            Harmony.CreateAndPatchAll(typeof(Hooks), $"Hooks.{Plugin.Name}")
                .Postfix(typeof(Hooks), nameof(SceneLoadStartPostfix),
                    typeof(Manager.Scene).GetMethod(nameof(Manager.Scene.LoadStart)))
#if SamabakeScramble
#else
                .Postfix(typeof(Hooks), nameof(HumanLoadCharaFbxDataPostfix),
                    typeof(Human).GetMethod(nameof(Human.LoadCharaFbxData)))
#endif
#if Aicomi
                .Postfix(typeof(Hooks), nameof(LoadListInfoAllPostfix),
                    typeof(ChaListControl).GetMethod(nameof(ChaListControl.LoadListInfoAll), 0, []))
#endif
                .AsDisposable(),
            CreateDetour(
#if Aicomi
                typeof(Human).GetField("NativeMethodInfoPtr__ctor_Public_Void_HumanData_Boolean_Boolean_Nullable_1_Int32_Il2CppStructArray_1_CoordinateType_0", HarmonyExtension.METHOD_LOOKUP),
                typeof(Human).GetConstructor([typeof(HumanData), typeof(bool), typeof(bool), typeof(Il2CppSystem.Nullable<int>), typeof(Il2CppStructArray<ChaFileDefine.CoordinateType>)]),
#else
                typeof(Human).GetField("NativeMethodInfoPtr__ctor_Public_Void_HumanData_Boolean_Boolean_0", HarmonyExtension.METHOD_LOOKUP),
                typeof(Human).GetConstructor([typeof(HumanData), typeof(bool), typeof(bool)]),
#endif
                typeof(NativeHumanCtor),
                (ilg, names) => {
                    ilg.Emit(OpCodes.Ldarg_S, names["self"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(Human)));
                    ilg.Emit(OpCodes.Ldarg_S, names["data"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(HumanData)));
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(HumanCtorPrefix), HarmonyExtension.METHOD_LOOKUP));
                },
                (ilg, names) => {
                    ilg.Emit(OpCodes.Ldarg_S, names["self"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(Human)));
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(HumanCtorPostfix), HarmonyExtension.METHOD_LOOKUP));
                }
            ),
#if DigitalCraft
            CreateDetour(
                typeof(DigitalCraft.OCIChar).GetMethod(nameof(DigitalCraft.OCIChar.LoadClothesFile)),
                typeof(NativeLoadClothesFile),
                (ilg, names) => {
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(NotifyCoordinateChange), HarmonyExtension.METHOD_LOOKUP));
                 },
                (ilg, names) => { }
            ),
#endif
            CreateDetour(
                typeof(HumanDataCoordinate).GetMethod(nameof(HumanDataCoordinate.LoadFileLimited)),
                typeof(NativeHumanDataLoadFileLimited),
                (ilg, names) => {
                    ilg.Emit(OpCodes.Ldarg_S, names["self"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(HumanDataCoordinate)));
                    ilg.Emit(OpCodes.Ldarg_S, names["path"]);
                    ilg.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.Il2CppStringToManaged)));
                    ilg.Emit(OpCodes.Ldarg_S, names["flags"]);
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(LoadFileLimitedPostfix), HarmonyExtension.METHOD_LOOKUP));
                },
                (ilg, names) => { }
            ),
#if Aicomi
#else
            CreateDetour(
                typeof(ChaListControl).GetMethod(nameof(ChaListControl.LoadListInfoAll), 0, [typeof(BundleLib.CheckBundleLimited).MakeByRefType()]),
                typeof(NativeChaListControlLoadListInfoAll),
                (ilg, names) => { },
                (ilg, names) => {
                    ilg.Emit(OpCodes.Ldarg_S, names["self"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(ChaListControl)));
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(LoadListInfoAllPostfix), HarmonyExtension.METHOD_LOOKUP));
                }
            ),
#endif
#if SamabakeScramble
            CreateDetour(
                typeof(Human).GetMethod(nameof(Human.LoadCharaFbxData)),
                typeof(NativeHumanLoadCharaFbxData),
                (ilg, names) => { },
                (ilg, names) => {
                    ilg.Emit(OpCodes.Ldarg_S, names["self"]);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(Human)));
                    ilg.Emit(OpCodes.Ldarg_S, names["category"]);
                    ilg.Emit(OpCodes.Ldarg_S, names["id"]);
                    ilg.Emit(OpCodes.Ldarg_S, names["createName"]);
                    ilg.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.Il2CppStringToManaged)));
                    ilg.Emit(OpCodes.Ldloc_2);
                    ilg.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get)).MakeGenericMethod(typeof(GameObject)));
                    ilg.Emit(OpCodes.Call, typeof(Hooks).GetMethod(nameof(HumanLoadCharaFbxDataPostfix), HarmonyExtension.METHOD_LOOKUP));
                }
            )
#endif
        ];
        static OpCode OpCodeByParamType(Type type) =>
            type == typeof(IntPtr) || type == typeof(IntPtr*) || type == typeof(IntPtr**) ? OpCodes.Ldarg_S : OpCodes.Ldarga_S;

        public static IDisposable CreateDetour(
            MethodBase target, Type native,
            Action<ILGenerator, Dictionary<string, int>> prefix,
            Action<ILGenerator, Dictionary<string, int>> postfix
        ) => CreateDetour(Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target), target, native, prefix, postfix);

        public static unsafe IDisposable CreateDetour(
            FieldInfo cppptr, MethodBase target, Type native,
            Action<ILGenerator, Dictionary<string, int>> prefix,
            Action<ILGenerator, Dictionary<string, int>> postfix)
        {
            var cppOrg = UnityVersionHandler.Wrap((Il2CppMethodInfo*)(IntPtr)cppptr.GetValue(null));
            var cppMod = UnityVersionHandler.NewMethod();
            Buffer.MemoryCopy(
                cppOrg.Pointer.ToPointer(),
                cppMod.Pointer.ToPointer(),
                UnityVersionHandler.MethodSize(),
                UnityVersionHandler.MethodSize());
            var paramTypes = native.GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
            var paramNames = native.GetMethod("Invoke").GetParameters().Select((p, index) => (p.Name, index)).ToDictionary();
            var (selfCode, paramCodes) = target.IsStatic
                ? (OpCodes.Ldc_I4_0,
                    Enumerable.Range(0, paramTypes.Count() - 1)
                        .Select(index => (OpCodeByParamType(paramTypes[index]), index)).ToArray())
                : (OpCodes.Ldarg_0,
                    Enumerable.Range(1, paramTypes.Count() - 2)
                        .Select(index => (OpCodeByParamType(paramTypes[index]), index)).ToArray());
            var dmd = new DynamicMethodDefinition($"(Detour){target.Name}", typeof(IntPtr), paramTypes);
            var ilg = dmd.GetILGenerator();
            ilg.DeclareLocal(typeof(IntPtr*));
            ilg.DeclareLocal(typeof(IntPtr));
            ilg.DeclareLocal(typeof(IntPtr));
            prefix(ilg, paramNames);
            ilg.Emit(OpCodes.Ldc_I4_S, paramCodes.Count());
            ilg.Emit(OpCodes.Conv_U);
            ilg.Emit(OpCodes.Sizeof, typeof(IntPtr));
            ilg.Emit(OpCodes.Mul_Ovf_Un);
            ilg.Emit(OpCodes.Localloc);
            ilg.Emit(OpCodes.Stloc_0);
            paramCodes.ForEachIndex((entry, index) =>
            {
                ilg.Emit(OpCodes.Ldloc_0);
                ilg.Emit(OpCodes.Ldc_I4, index);
                ilg.Emit(OpCodes.Conv_U);
                ilg.Emit(OpCodes.Sizeof, typeof(IntPtr));
                ilg.Emit(OpCodes.Mul_Ovf_Un);
                ilg.Emit(OpCodes.Add);
                ilg.Emit(entry.Item1, entry.Item2);
                ilg.Emit(OpCodes.Stind_I);
            });
            ilg.Emit(OpCodes.Ldc_I8, cppMod.Pointer.ToInt64());
            ilg.Emit(OpCodes.Conv_I);
            ilg.Emit(selfCode);
            ilg.Emit(OpCodes.Ldloc_0);
            ilg.Emit(OpCodes.Ldloca_S, 1);
            ilg.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_runtime_invoke)));
            ilg.Emit(OpCodes.Stloc_2);
            ilg.Emit(OpCodes.Ldloc_1);
            ilg.Emit(OpCodes.Call, typeof(Il2CppInterop.Runtime.Il2CppException)
                .GetMethod(nameof(Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary)));
            postfix(ilg, paramNames);
            ilg.Emit(OpCodes.Ldloc_2);
            ilg.Emit(OpCodes.Ret);
            var nativeDelegate = dmd.Generate().CreateDelegate(native);
            var nativeDetour = Il2CppInteropRuntime.Instance.DetourProvider.Create(cppOrg.MethodPointer, nativeDelegate);
            nativeDetour.Apply();
            cppMod.MethodPointer = nativeDetour.OriginalTrampoline;
            return nativeDetour;
        }

    }
}