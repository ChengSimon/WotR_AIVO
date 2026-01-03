using HarmonyLib;
using Kingmaker.Code.UI.MVVM.VM.Bark;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.EntitySystem.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kingmaker.Sound.Base;
using UnityEngine;
using System.Reflection;
using Kingmaker.UI.Sound;
using Kingmaker.UI.MVVM.View.ShipCustomization.ShipPosts;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public class AkSoundEngine_Patch
{
    static Type resolveType()
    {
        foreach (Type t in AccessTools.AllTypes())
        {
            if (t.FullName == "AkSoundEngine")
            {
                return t;
            }
        }
        return null;
    }

    public static Type akSoundEngine = resolveType();

    public static MethodBase TargetMethod()
    {
        return akSoundEngine.GetMethod("PostEvent", new Type[] { typeof(string), typeof(GameObject) });
    }

    [HarmonyPrefix]
    public static bool Prefix(string in_pszEventName)
    {
        if (in_pszEventName.StartsWith("ev_") && !in_pszEventName.StartsWith("ev_st")) {
            return !BarkExtensions.PlayedRecently(in_pszEventName);
        }
        return true;
    }
}
