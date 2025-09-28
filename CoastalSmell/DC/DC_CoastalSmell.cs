using BepInEx.Unity.IL2CPP;
using System;
using UnityEngine;
using ILLGames.Unity.UI.ColorPicker;
using UniRx;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;

namespace CoastalSmell
{
    public static partial class Util
    {
        public static Action<Action> DoNextFrame =
            action => Observable.NextFrame().Subscribe(action.Ignoring<Unit>());

        public static unsafe Span<byte> AsSpan(this Il2CppStructArray<byte> array) =>
            new Span<byte>(IntPtr.Add(array.Pointer, sizeof(Il2CppObject) + sizeof(void*) + sizeof(nuint)).ToPointer(), array.Length);
    }

    public static partial class UGUI
    {
        static Action<Unit> ColorPaletteSetup(string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha, bool autoOpen) =>
            _ => DigitalCraft.ColorPalette.Instance.Setup(name, getColor(), setColor, useAlpha, autoOpen);
        public static Action<ThumbnailColor> ThumbnailColor(
            string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => ui => ui._button.OnClickAsObservable().Subscribe(ColorPaletteSetup(name, getColor, ui.SetGraphic + setColor, useAlpha, autoOpen));
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "DigitalCraft";
    }
}
