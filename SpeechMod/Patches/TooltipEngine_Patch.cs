using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Tooltip.Bricks;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using AiVoiceoverMod.Unity.Extensions;
using UnityEngine;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public static class TooltipEngine_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipEngine), nameof(TooltipEngine.GetBrickView))]
    public static void GetBrickView_Postfix(ref MonoBehaviour __result)
    {
        if (!Main.Enabled)
            return;

        if (__result == null)
            return;

#if DEBUG
        Debug.Log($"{nameof(TooltipEngine)}.{nameof(TooltipEngine.GetBrickView)}:{__result.GetType().Name} @ {__result.transform.GetGameObjectPath()}");
#endif

        // The text in other brick types tends to be split across several TMPs; only the whole-line bricks read well.
        if (__result is not (
                TooltipBrickTextView or
                TooltipBrickEntityHeaderView or
                TooltipBrickIconAndNameView or
                TooltipBrickTitleView or
                TooltipBrickItemFooterView or
                TooltipBrickIconValueStatView or
                TooltipBrickValueStatFormulaView or
                TooltipBrickTimerView or
                TooltipBrickPortraitAndNameView or
                TooltipBrickShortClassDescriptionView or
                TooltipBrickFeatureShortDescriptionView
            ))
            return;

        __result.gameObject.transform.HookupTextToSpeechOnTransform();
    }
}
