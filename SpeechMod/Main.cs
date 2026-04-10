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
        Debug.Log("Warhammer 40K: Rogue Trader Speech Mod Initializing...");

        Logger = modEntry?.Logger;

        Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        Hooks.UpdateHoverColor();

        modEntry!.OnToggle = OnToggle;
        modEntry!.OnGUI = OnGui;
        modEntry!.OnSaveGUI = OnSaveGui;
        //modEntry.Path

        

        var harmony = new Harmony(modEntry.Info?.Id);
        try {

            string soundBanksLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "soundbanks");
            Debug.Log($"Adding {soundBanksLocation} to Wwise");

            // Avoid referencing AK.Wwise.Unity.API -- probably can't redistribute it
            var akSoundEngine = AccessTools.TypeByName("AkSoundEngine");
            var addBasePath = akSoundEngine.GetMethod("AddBasePath", new Type[] { typeof(string) });
            var loadBank = akSoundEngine.GetMethod("LoadBank", new Type[] { typeof(string), typeof(uint).MakeByRefType() });
            var bankPathResult = addBasePath.Invoke(null, new object[] { soundBanksLocation });
            Debug.Log("Bank path: " + bankPathResult);

            foreach (var file in Directory
                .EnumerateFiles(soundBanksLocation, "*.bnk")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var fname = Path.GetFileName(file);
                var bankLoadArgs = new object[] { fname, 0u };
                var bankLoadResult = loadBank.Invoke(null, bankLoadArgs);
                Debug.Log($"Bank loading {fname}: {bankLoadResult}, bank ID: {bankLoadArgs[1]}");
                LoadedBanks.Add(fname);
            }

            harmony.PatchAll(Assembly.GetExecutingAssembly());
        } catch (Exception e) {
            Debug.Log(e.Message);
            Debug.Log(e);
            throw e;
        }

        ModConfigurationManager.Build(harmony, modEntry, Constants.SETTINGS_PREFIX);
        SetUpSettings();
        harmony.CreateClassProcessor(typeof(SettingsUIPatches)).Patch();


        FuzzyResolver.LoadPreprocessedDatabase();

        Debug.Log("Warhammer 40K: Rogue Trader Speech Mod Initialized!");
        m_Loaded = true;
        return true;
    }

    private static void SetUpSettings()
    {
        if (ModConfigurationManager.Instance.GroupedSettings.TryGetValue("main", out _))
            return;

        ModConfigurationManager.Instance.GroupedSettings.Add("main", [new PlaybackStop(), new ToggleBarks()]);
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
