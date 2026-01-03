using AiVoiceoverMod.Voice;
using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Sound.Base;
using Kingmaker.Visual.Sound;
using UnityEngine;

namespace AiVoiceoverMod.Patches
{
    [HarmonyPatch]
    public class VoiceoverShim_Patch
    {
        [HarmonyPatch(typeof(LocalizedString), nameof(LocalizedString.GetVoiceOverSound))]
        [HarmonyPostfix]
        public static void GetVoiceOverSoundPostfix(LocalizedString __instance, ref string __result) {
            if (__result == "")
            {
                if (__instance.Key == "")
                {
                    ResolveResult res = FuzzyResolver.Singleton.Query(__instance.Text);
                    Debug.Log("FIXING (FUZZY): " + res.Best.Id + ": " + __instance.Text);
                    __result = "ev_" + res.Best.Id;
                }
                else
                {
                    //Debug.Log("FIXING (Static): " + __instance.Key + ": " + __instance.Text);
                    __result = "ev_" + __instance.Key;
                }
                if (BarkExtensions.PlayedRecently(__result))
                {
                    __result = "";
                }
            }
        }

        [HarmonyPatch(typeof(SoundState), nameof(SoundState.StopDialog))]
        [HarmonyPostfix]
        public static void StopDialogPostfix()
        {
            //Debug.Log("Stopping AIVO");
            SoundEventsManager.PostEvent("ev_stop_aivo", null);
        }

    }
}
