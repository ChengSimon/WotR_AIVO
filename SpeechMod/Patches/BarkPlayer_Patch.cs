using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Base;
using Kingmaker.Code.UI.MVVM.VM.Bark;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.GameModes;
using Kingmaker.Mechanics.Entities;
using AiVoiceoverMod.Voice;
using System;
using UnityEngine;
using Kingmaker.Sound;
using Kingmaker.Sound.Base;
using Kingmaker.Visual.Sound;
using Kingmaker.AreaLogic.Cutscenes.Commands;
using Kingmaker.AreaLogic.Cutscenes;
using RewiredConsts;
using UnityEngine.UI;
using Kingmaker.Signals;
using Kingmaker.UI.Common;
using Kingmaker.Localization;
using Kingmaker.Networking;
using System.Collections.Generic;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public class BarkPlayer_Patch
{
    //[HarmonyPatch(typeof(CommandBarkEntity), nameof(CommandBarkEntity.OnRun), typeof(CutscenePlayerData), typeof(bool))]
    //[HarmonyPostfix]
    //public static void CommandBarkEntity_OnRun(CommandBarkEntity __instance)
    //{
    //    Debug.Log($"CommandBarkEntity_OnRun. IsSubText: {__instance.IsSubText}, SharedText: {__instance.SharedText}, ONIL: {__instance.OverrideNameInLog}");
    //}


    [HarmonyPatch(typeof(BarkPlayer), nameof(BarkPlayer.Bark), typeof(Entity), typeof(string), typeof(float), typeof(string), typeof(BaseUnitEntity), typeof(bool), typeof(string), typeof(Color))]
    [HarmonyPostfix]
    public static void Bark(Entity entity, string text, float duration = -1f, string voiceOver = null, BaseUnitEntity interactUser = null, bool synced = true, string overrideName = null, Color overrideNameColor = default(Color))
    {
        if (!BarkExtensions.PlayBark())
            return;

#if DEBUG
        Debug.Log($"{nameof(BarkPlayer)}_Bark_Postfix");
#endif

        BarkExtensions.DoBark(entity, text, voiceOver);
    }

    [HarmonyPatch(typeof(BarkPlayer), nameof(BarkPlayer.BarkExploration), typeof(Entity), typeof(string), typeof(float), typeof(string))]
    [HarmonyPostfix]
    public static void BarkExploration_1(Entity entity, string text, float duration = -1f, string voiceOver = null)
    {
        if (!BarkExtensions.PlayBark())
            return;

#if DEBUG
        Debug.Log($"{nameof(BarkPlayer)}_BarkExploration_1_Postfix");
#endif

        BarkExtensions.DoBark(entity, text, voiceOver);
    }

    [HarmonyPatch(typeof(BarkPlayer), nameof(BarkPlayer.BarkExploration), typeof(Entity), typeof(string), typeof(string), typeof(float), typeof(string))]
    [HarmonyPostfix]
    public static void BarkExploration_2(Entity entity, string text, string encyclopediaLink, float duration = -1f, string voiceOver = null)
    {
        if (!BarkExtensions.PlayBark())
            return;

#if DEBUG
        Debug.Log($"{nameof(BarkPlayer)}_BarkExploration_2_Postfix");
#endif

        BarkExtensions.DoBark(entity, text, voiceOver);
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

        // Don't play barks if we are in a dialog.
        if (Game.Instance == null || Game.Instance.IsModeActive(GameModeType.Dialog))
            return false;

        switch (Main.Settings.PlaybackBarkOnlyIfSilence)
        {
            case true when Game.Instance.DialogController?.CurrentCue != null:
                return false;
        }

        if (!Main.Settings.PlaybackBarksInVicinity)
        {
            var stackTrace = Environment.StackTrace;
            if (stackTrace.Contains("UnitsProximityController.TickOnUnit") ||
                stackTrace.Contains("Cutscenes.Commands.CommandBark"))
                return false;
        }

        return true;
    }

    public static void DoBark(Entity entity, string text, string voiceOver)
    {
        if (Game.Instance!.IsModeActive(GameModeType.StarSystem))
        {
            // Force null speaker in space
            SpeakBark(text, null);
        }
        if (!string.IsNullOrWhiteSpace(voiceOver))
            return;

        SpeakBark(text, entity);
    }

    private static readonly Dictionary<string, DateTime> _lastSeen = new();
    private static readonly TimeSpan _threshold = TimeSpan.FromSeconds(4);

    // Allocate once
    private static readonly List<string> _keysToRemove = new();

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

    public static void SpeakBark(string text, Entity entity)
    {
        GameObject obj = entity?.View?.GO;
        if (obj == null)
        {
            obj = SoundState.Get2DSoundObject();
        }
        if (obj != null)
        {
            FuzzyResolver.ResolveAndPlay(text, "Bark", obj);
        }
    }

}
