using BepInEx.Unity.IL2CPP;
using System;
using UnityEngine;
using CharacterCreation;
using ThumbnailColor = ILLGames.Unity.UI.ColorPicker.ThumbnailColor;

namespace CoastalSmell
{
    public static partial class Util
    {
        public static Action<Action> OnCustomHumanReady =
            action => (HumanCustom.Instance.Human == null).Either(action, F.Apply(DoNextFrame, OnCustomHumanReady.Apply(action)));
    }
    public static partial class UGUI
    {
        public static Action<ThumbnailColor> ThumbnailColor(
            string name, Func<Color> getColor, Action<Color> setColor, bool useAlpha = true, bool autoOpen = true
        ) => ui => ui.Initialize(HumanCustom.Instance.ColorPicker, name, getColor, setColor.Constant(true), useAlpha, autoOpen);
    }
    public partial class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
    }
}
