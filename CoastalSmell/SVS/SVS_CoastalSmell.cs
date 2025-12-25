using System;
using UnityEngine;
using CharacterCreation;
using ThumbnailColor = ILLGames.Unity.UI.ColorPicker.ThumbnailColor;

namespace CoastalSmell
{
    public static partial class UGUI
    {
        public static UIDesign ThumbnailColor(
            string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => Component<ThumbnailColor>(ui =>ui.Initialize(HumanCustom.Instance.ColorPicker, name, getColor, setColor.Constant(true), useAlpha, autoOpen));
    }
    public partial class Plugin
    {
        public const string Process = "SamabakeScramble";
    }
}
