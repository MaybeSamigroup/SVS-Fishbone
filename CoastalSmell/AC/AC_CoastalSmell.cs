using BepInEx.Unity.IL2CPP;
using System;
using UnityEngine;
using CharacterCreation;
using Cysharp.Threading.Tasks;
using Il2CppSystem.Threading;
using AC.Scene;
using ThumbnailColor = ILLGAMES.Unity.UI.ColorPicker.ThumbnailColor;
using R3;
using R3.Triggers;

namespace CoastalSmell
{
    public static partial class Util
    {
        public static Action<Action> DoNextFrame =
            action => UniTask.NextFrame().ContinueWith(action); 
        public static Action<Action> OnCustomHumanReady =
            action => DoOnCondition(() => HumanCustom.Instance?.Human != null, action);
    }
    public static class SceneUtil<T> where T : SceneSingleton<T>
    {
        static Action<Action, Action> AwaitStartup = (onStartup, onDestroy) =>
            Util.DoNextFrame.With(onDestroy)(Hook.Apply(onStartup).Apply(onDestroy));

        static Action<Action, Action> AwaitDestroy = (onStartup, onDestroy) =>
            SceneSingleton<T>.Instance.With(onStartup)
                .gameObject.OnDestroyAsObservable()
                .Subscribe(AwaitStartup.Apply(onStartup).Apply(onDestroy).Ignoring<Unit>());

        public static Action<Action, Action> Hook = (onStartup, onDestroy) =>
            SceneSingleton<T>.WaitUntilSetup(CancellationToken.None)
                .ContinueWith(AwaitDestroy.Apply(onStartup).Apply(onDestroy));
    }
    public static partial class UGUI
    {
        public static Action<ThumbnailColor> ThumbnailColor(
            string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => ui => ui.Initialize(HumanCustom.Instance.ColorPicker, name, getColor, setColor.Constant(true), useAlpha, autoOpen);
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "Aicomi";
    }
}
