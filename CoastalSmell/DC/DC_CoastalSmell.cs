using System;
using System.Reactive;
using System.Reactive.Linq;
using UnityEngine;
using ILLGames.Unity.UI.ColorPicker;

namespace CoastalSmell
{
    public static partial class UGUI
    {
        static Action<Unit> ColorPaletteSetup(string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha, bool autoOpen) =>
            _ => DigitalCraft.ColorPalette.Instance.Setup(name, getColor(), setColor, useAlpha, autoOpen);
        public static UIDesign ThumbnailColor(string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => Component<ThumbnailColor>(ui => ui._button.OnClickAsObservable()
            .Subscribe(ColorPaletteSetup(name, getColor, ui.SetGraphic + setColor, useAlpha, autoOpen)));
    }

    #region Plugin
    public partial class Plugin
    {
        public const string Process = "DigitalCraft";
    }
    #endregion
}
