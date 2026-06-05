using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using AiVoiceoverMod.Voice;
using Kingmaker.Localization;
using Kingmaker.UI._ConsoleUI.Overtips;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public class AkSoundEngine_Patch
{
    // WOTR's bundled Harmony has no AccessTools.AllTypes(); TypeByName scans loaded assemblies for the
    // namespace-less AK.Wwise "AkSoundEngine" type (same call Main.cs uses to load the soundbanks).
    //public static Type akSoundEngine = AccessTools.TypeByName("AkSoundEngine");

    //public static MethodBase TargetMethod()
    //{
    //    return akSoundEngine.GetMethod("PostEvent", new Type[] { typeof(string), typeof(GameObject) });
    //}

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AkSoundEngine), nameof(AkSoundEngine.PostEvent), typeof(string), typeof(GameObject))]
    public static bool PostEvent(string in_pszEventName)
    {
        if (in_pszEventName == null)
        {
            return true;
        }
        if (in_pszEventName.StartsWith("evt_") && !in_pszEventName.StartsWith("ev_st")) {
            // Barks disabled: don't play bark/skillcheck clips (their type comes from clips.json, keyed by guid).
            if (ClipCatalog.IsSuppressedBark(in_pszEventName.Substring(4)))
                return false;
            return !BarkExtensions.PlayedRecently(in_pszEventName);
        }
        return true;
    }
}
