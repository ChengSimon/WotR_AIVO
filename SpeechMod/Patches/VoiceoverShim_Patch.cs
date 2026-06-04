using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Sound;
using Kingmaker.Visual.Sound;
using UnityEngine;

namespace AiVoiceoverMod.Patches
{
    /// <summary>
    /// WOTR voiceover shim.
    ///
    /// Rogue Trader exposed <c>LocalizedString.GetVoiceOverSound</c>, which returned the event-name
    /// string the game was about to play; the RT shim rewrote that string to <c>ev_&lt;Key&gt;</c> when
    /// it came back empty (i.e. the line had no shipped voiceover).
    ///
    /// WOTR has no such method. Localized voiceover is played through the instance methods
    /// <c>LocalizedString.PlayVoiceOver</c> / <c>PlayCueVoiceOver</c>, which resolve and play internally
    /// and return a <see cref="VoiceOverStatus"/>. We postfix both: when nothing real was played
    /// (<c>PlayingSoundId == 0</c> — the WOTR analogue of RT's empty result), we post our pre-rendered
    /// bank event <c>ev_&lt;Key&gt;</c> for the same emitter. This is true GUID (Text.Key) resolution and
    /// covers every voiced surface, dialog cues included. Duplicate suppression is handled downstream by
    /// <c>AkSoundEngine_Patch</c>.
    /// </summary>
    [HarmonyPatch]
    public class VoiceoverShim_Patch
    {
        [HarmonyPatch(typeof(LocalizedString), nameof(LocalizedString.PlayVoiceOver))]
        [HarmonyPostfix]
        public static void PlayVoiceOver_Postfix(LocalizedString __instance, MonoBehaviour target, VoiceOverStatus __result)
        {
            TryPlayBankEvent(__instance, target, __result);
        }

        [HarmonyPatch(typeof(LocalizedString), nameof(LocalizedString.PlayCueVoiceOver))]
        [HarmonyPostfix]
        public static void PlayCueVoiceOver_Postfix(LocalizedString __instance, MonoBehaviour target, VoiceOverStatus __result)
        {
            TryPlayBankEvent(__instance, target, __result);
        }

        private static void TryPlayBankEvent(LocalizedString localizedString, MonoBehaviour target, VoiceOverStatus result)
        {
            if (!Main.Enabled)
                return;

            // The game already played a real (shipped) voiceover for this line; don't talk over it.
            if (result != null && result.PlayingSoundId != 0)
                return;

            var key = localizedString?.Key;
            if (string.IsNullOrEmpty(key))
                return;

            var obj = target != null ? target.gameObject : SoundState.Get2DSoundObject();

#if DEBUG
            Debug.Log("FIXING (Static): ev_" + key);
#endif
            SoundEventsManager.PostEvent("evt_" + key, obj);
        }

        [HarmonyPatch(typeof(SoundState), nameof(SoundState.StopDialog))]
        [HarmonyPostfix]
        public static void StopDialogPostfix()
        {
            SoundEventsManager.PostEvent("ev_stop_aivo", null);
        }
    }
}
