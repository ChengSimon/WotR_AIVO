using HarmonyLib;
using AiVoiceoverMod.Configuration;
using AiVoiceoverMod.KeyBinds;
using AiVoiceoverMod.Unity;
using AiVoiceoverMod.Unity.Extensions;
using AiVoiceoverMod.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityModManagerNet;
using System.IO;
using Kingmaker.Sound;

namespace AiVoiceoverMod;

#if DEBUG
[EnableReloading]
#endif
public static class Main
{
    public static UnityModManager.ModEntry.ModLogger Logger;
    public static Settings Settings;
    public static bool Enabled;
    public static string[] FontStyleNames = Enum.GetNames(typeof(FontStyles));
    public static List<string> LoadedBanks = new();

    private static bool m_Loaded = false;

    private static bool Load(UnityModManager.ModEntry modEntry)
    {
        Debug.Log("WOTR AIVO Mod Initializing...");

        Logger = modEntry?.Logger;

        Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        Hooks.UpdateHoverColor();

        modEntry!.OnToggle = OnToggle;
        modEntry!.OnGUI = OnGui;
        modEntry!.OnSaveGUI = OnSaveGui;
        modEntry!.OnUpdate = OnUpdate;
        //modEntry.Path

        

        var harmony = new Harmony(modEntry.Info?.Id);
        try
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            Debug.Log(e);
            throw e;
        }

        ModConfigurationManager.Build(harmony, modEntry, Constants.SETTINGS_PREFIX);
        SetUpSettings();
        harmony.CreateClassProcessor(typeof(SettingsUIPatches)).Patch();


        FuzzyResolver.LoadPreprocessedDatabase();

        Debug.Log("WOTR AIVO Mod Initialized!");
        m_Loaded = true;
        return true;
    }

    private static void SetUpSettings()
    {
        if (ModConfigurationManager.Instance.GroupedSettings.TryGetValue("main", out _))
            return;

        ModConfigurationManager.Instance.GroupedSettings.Add("main", [new PlaybackStop(), new ToggleBarks(), new PlaybackSpeedHold()]);
    }

    private static bool m_PlaybackSpeedApplied = false;

    private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
    {
        if (!m_Loaded || !Enabled)
            return;

        var hold = PlaybackSpeedHold.Instance;
        if (hold == null)
            return;

        if (hold.IsHeld())
        {
            // Push live so adjusting the slider while held takes effect immediately.
            AkSoundEngine.SetRTPCValue("AivoPlaybackSpeed", Settings.AcceleratedPlaybackSpeed);
            m_PlaybackSpeedApplied = true;
        }
        else if (m_PlaybackSpeedApplied)
        {
            AkSoundEngine.SetRTPCValue("AivoPlaybackSpeed", 0f);
            m_PlaybackSpeedApplied = false;
        }
    }


    private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
    {
        Enabled = value;
        return true;
    }

    private static void OnGui(UnityModManager.ModEntry modEntry)
    {
        if (m_Loaded)
            MenuGUI.OnGui();
    }

    private static void OnSaveGui(UnityModManager.ModEntry modEntry)
    {
        Hooks.UpdateHoverColor();
        Settings?.Save(modEntry);
    }
}
