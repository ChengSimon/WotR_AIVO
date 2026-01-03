using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AiVoiceoverMod.Patches
{
    public class AkSoundEngine_3_Patch
    {
        public static MethodBase TargetMethod()
        {
            foreach (var m in AkSoundEngine_Patch.akSoundEngine.GetMethods())
            {
                if (m.Name == "PostEvent" && m.GetParameters().Length == 7 && m.GetParameters()[0].ParameterType == typeof(string))
                {
                    return m;
                }
            }
            return null;
        }

        //[HarmonyPrefix]
        //public static bool Prefix(string in_pszEventName)
        //{
        //    return AkSoundEngine_Patch.Prefix(in_pszEventName);
        //}

    }
}
