using HarmonyLib;
using Kingmaker;
using AiVoiceoverMod.Configuration.Settings;
using System;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.Sound;


#if DEBUG
using UnityEngine;
#endif

namespace AiVoiceoverMod.KeyBinds;

public class PlaybackStop() : ModHotkeySettingEntry(KEY, TITLE, TOOLTIP, DEFAULT_VALUE)
{
    private const string KEY = "playback.stop";
    private const string TITLE = "Stop playback";
    private const string TOOLTIP = "Stops playback of SpeechMod TTS.";
    private const string DEFAULT_VALUE = "%S;;All;false";
    private const string BIND_NAME = $"{Constants.SETTINGS_PREFIX}.newcontrols.ui.{KEY}";

    public override SettingStatus TryEnable() => TryEnableAndPatch(typeof(Patches));

    [HarmonyPatch]
    private static class Patches
    {
        private static string _playbackStoppedText = "SpeechMod: Playback stopped!";
        private static IDisposable _disposableBinding;

        [HarmonyPatch(typeof(CommonPCView), nameof(CommonPCView.BindViewImplementation))]
        [HarmonyPostfix]
        private static void AddStopPlaybackHotkey(CommonPCView __instance)
        {
#if DEBUG
            Debug.Log($"{nameof(CommonPCView)}_{nameof(CommonPCView.BindViewImplementation)}_Postfix : {BIND_NAME}");
#endif
            //if (!LocalizationManager.CurrentPack!.TryGetText("pas2k.speechmod.feature.playback.stop.notification", out var text, false))
            //    _playbackStoppedText = text;

            if (Game.Instance.Keyboard.m_Bindings.Exists(binding => binding.Name.Equals(BIND_NAME)))
            {
#if DEBUG
                Debug.Log($"Binding {BIND_NAME} already exists! Disposing of binding...");
#endif
                _disposableBinding.Dispose();
            }

            _disposableBinding = Game.Instance!.Keyboard!.Bind(BIND_NAME, () => StopPlayback(__instance));
            __instance?.AddDisposable(_disposableBinding);
        }

        private static void StopPlayback(CommonPCView instance)
        {
            SoundEventsManager.PostEvent("ev_stop_aivo", null);
        }
    }
}
