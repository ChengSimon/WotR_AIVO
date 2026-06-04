using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Visual.Sound;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AiVoiceoverMod.Patches
{
    //[HarmonyPatch]
    //public class PlayVoiceOver_Patch
    //{

    //    [HarmonyPatch(typeof(VoiceOverPlayer), nameof(VoiceOverPlayer.PlayVoiceOver), typeof(string), typeof(GameObject))]
    //    [HarmonyPrefix]
    //    public static void Prefix(string voiceOverSound, GameObject target)
    //    {
    //        Debug.Log($"PlayVoiceOver {voiceOverSound}!!!!");
    //        if (string.IsNullOrEmpty(voiceOverSound))
    //        {
    //            Debug.Log("VO is null!");
    //        }

    //        if (target == null)
    //        {
    //            target = SoundState.Get2DSoundObject();
    //            if (target)
    //            {
    //                Debug.Log("Emitting sound from 2DSoundPosition" + target.transform.position);
    //            }
    //            else
    //            {
    //                Debug.Log("target is null, falling back to 2D, but 2DSoundObject is NULL!");
    //            }
    //            return;
    //        }
    //        //GameObject gameObject = target.gameObject;
    //        if (target)
    //        {
    //            Debug.Log("Emitting sound from " + target.transform.position);
    //        }
    //    }
    //}
}
