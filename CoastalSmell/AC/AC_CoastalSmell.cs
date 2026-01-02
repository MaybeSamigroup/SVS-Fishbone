using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Il2CppSystem.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using CharacterCreation;
using AC.Scene;
using ThumbnailColor = ILLGAMES.Unity.UI.ColorPicker.ThumbnailColor;

namespace CoastalSmell
{
    public static partial class UGUI
    {
        public static UIAction ThumbnailColor(
            string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => Component<ThumbnailColor>(ui =>ui.Initialize(HumanCustom.Instance.ColorPicker, name, getColor, setColor.Constant(true), useAlpha, autoOpen));
    }
    public static class SceneSingletonExtension<T> where T : SceneSingleton<T>
    {
        public static IObservable<T> OnStartup =>
            Startup.AsObservable().Select(_ => SceneSingleton<T>.Instance);
        public static IObservable<Unit> OnDestroy => Destroy.AsObservable();
        static Subject<Unit> Startup = new();
        static Subject<Unit> Destroy = new();
        static Action Wait = () => SceneSingleton<T>
            .WaitUntilSetup(CancellationToken.None)
            .ContinueWith(F.Apply(Startup.OnNext, Unit.Default));
        static SceneSingletonExtension() {
            OnDestroy.Subscribe(_ => UniTask.NextFrame().ContinueWith(Wait));
            OnStartup.Subscribe(cmp => cmp.OnDestroyAsObservable().Subscribe(Destroy.OnNext));
            Wait();
        }
    }
    public partial class Plugin
    {
        public const string Process = "Aicomi";
    }
}
