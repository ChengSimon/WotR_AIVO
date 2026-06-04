using HarmonyLib;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.Localization;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.Visual.Sound;
using AiVoiceoverMod.Voice;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public static class BarkPlayer_Patch
{
    // WOTR routes overhead barks through EntityOvertipVM.ShowBark, not BarkPlayer.Bark (which is the RT path).
    // VoiceOverStatus is supplied by the game when a real (pre-rendered) line already exists for the bark.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityOvertipVM), nameof(EntityOvertipVM.ShowBark), typeof(string), typeof(float), typeof(VoiceOverStatus))]
    public static void ShowBark_Postfix(EntityOvertipVM __instance, string text, float duration, VoiceOverStatus voiceOverStatus)
    {
        if (!BarkExtensions.PlayBark())
            return;

        // The game already has a real voiceover queued for this bark; don't speak over it.
        if (voiceOverStatus != null)
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

#if DEBUG
        Debug.Log($"{nameof(EntityOvertipVM)}_ShowBark_Postfix");
#endif

        // 2D emitter, consistent with the rest of the mod's playback (Get2DSoundObject is confirmed in WOTR).
        FuzzyResolver.ResolveAndPlay(text, "Bark", SoundState.Get2DSoundObject());
    }
}

public static class BarkExtensions
{
    public static bool PlayBark()
    {
        if (!Main.Enabled)
            return false;

        if (!Main.Settings!.PlaybackBarks)
            return false;

        // Don't play barks while a dialog is up.
        if (Game.Instance == null || Game.Instance.IsModeActive(GameModeType.Dialog))
            return false;

        if (Main.Settings.PlaybackBarkOnlyIfSilence && Game.Instance.DialogController?.CurrentCue != null)
            return false;

        if (!Main.Settings.PlaybackBarksInVicinity)
        {
            var stackTrace = Environment.StackTrace;
            if (stackTrace.Contains("UnitsProximityController.TickOnUnit") ||
                stackTrace.Contains("Cutscenes.Commands.CommandBark"))
                return false;
        }

        return true;
    }

    private static readonly Dictionary<string, DateTime> _lastSeen = new();
    private static readonly TimeSpan _threshold = TimeSpan.FromSeconds(4);

    // Allocate once
    private static readonly List<string> _keysToRemove = new();

    // Used by VoiceoverShim_Patch to suppress duplicate Wwise events fired in quick succession.
    public static bool PlayedRecently(string value)
    {
        if (Main.Settings.SoundDedupTimeout <= 0)
        {
            Debug.LogWarning("Never played recently");
            return false;
        }
        var threshold = TimeSpan.FromSeconds(Main.Settings.SoundDedupTimeout);

        var now = DateTime.UtcNow;

        if (_lastSeen.TryGetValue(value, out var lastTime) && (now - lastTime) <= threshold)
        {
            _lastSeen[value] = now;
            Debug.Log("Blocking " + value);
            return true;
        }
        _lastSeen[value] = now;

        _keysToRemove.Clear();
        // Cleanup
        foreach (var kvp in _lastSeen)
        {
            if (now - kvp.Value > _threshold)
                _keysToRemove.Add(kvp.Key);
        }

        foreach (var key in _keysToRemove)
            _lastSeen.Remove(key);

        Debug.Log("Allowing " + value);
        return false;
    }
}
