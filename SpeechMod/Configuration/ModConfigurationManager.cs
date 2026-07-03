using HarmonyLib;
using Kingmaker;
using Kingmaker.Settings;
using AiVoiceoverMod.Configuration.Settings;
using AiVoiceoverMod.Configuration.UI;
using AiVoiceoverMod.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnityModManagerNet.UnityModManager;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UI;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AiVoiceoverMod.Configuration;

public class ModConfigurationManager
{
    public Dictionary<string, List<ModSettingEntry>> GroupedSettings = new();
    public Harmony HarmonyInstance { get; protected set; }
    public ModEntry ModEntry { get; protected set; }
    public string SettingsPrefix = Guid.NewGuid().ToString();

    private ModConfigurationManager() { }

    public static void Build(Harmony harmonyInstance, ModEntry modEntry, string settingsPrefix)
    {
        Instance.HarmonyInstance = harmonyInstance;
        Instance.ModEntry = modEntry;
        Instance.SettingsPrefix = settingsPrefix;
        // ModLocalizationManager.Init is a Harmony postfix on LocalizationManager.Init now; it must not be
        // called here at mod-load time, when SettingsRoot/CurrentLocale aren't initialized yet (NRE).
    }

    private bool Initialized = false;

    public void Initialize()
    {
        if (Initialized) return;
        Initialized = true;

        foreach (var setting in GroupedSettings.SelectMany(settings => settings.Value))
        {
            setting.BuildUIAndLink();
            setting.TryEnable();
        }

        if (ModHotkeySettingEntry.ReSavingRequired)
        {
            SettingsController.SaveAll();
            Instance.ModEntry.Logger.Log("Hotkey settings were migrated");
        }
    }

    public static ModConfigurationManager Instance { get; } = new();
}

[HarmonyPatch]
public static class SettingsUIPatches
{
    [HarmonyPatch(typeof(UISettingsManager), nameof(UISettingsManager.Initialize))]
    [HarmonyPostfix]
    static void AddSettingsGroup()
    {
        if (Game.Instance.UISettingsManager.m_SoundSettingsList.Any(group => group.name?.StartsWith(ModConfigurationManager.Instance.SettingsPrefix) ?? false))
        {
            return;
        }

        ModConfigurationManager.Instance?.Initialize();

        foreach (var settings in ModConfigurationManager.Instance.GroupedSettings)
        {
            Game.Instance.UISettingsManager.m_SoundSettingsList?.Add(
                OwlcatUITools.MakeSettingsGroup($"{ModConfigurationManager.Instance.SettingsPrefix}.group.{settings.Key}", "Speech Mod",
                    settings.Value?.Select(x => x.GetUISettings()).ToArray()
                ));
        }


        try
        {

            string soundBanksLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "soundbanks");
            Debug.Log($"Adding {soundBanksLocation} to Wwise");

            var bankPathResult = AkSoundEngine.AddBasePath(soundBanksLocation);

            foreach (var file in Directory
                .EnumerateFiles(soundBanksLocation, "*.bnk")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var fname = Path.GetFileName(file);
                //var bankLoadArgs = new object[] { fname, 0u };
                uint bankId;
                var bankLoadResult = AkSoundEngine.LoadBank(fname, out bankId);
                Debug.Log($"Bank loading {fname}: {bankLoadResult}, bank ID: {bankId}");
                Main.LoadedBanks.Add(fname);
            }
            AkSoundEngine.SetRTPCValue("AivoPlaybackSpeed", Main.Settings.DefaultAcceleratedPlaybackSpeed);
            AkSoundEngine.SetRTPCValue("AivoPitch", Main.Settings.DefaultPitchCorrection);
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            Debug.Log(e);
            throw e;
        }
    }

    [HarmonyPatch(typeof(KeyboardAccess), nameof(KeyboardAccess.CanBeRegistered))]
    [HarmonyPrefix]
    public static bool CanRegisterAnything(ref bool __result, string name)
    {
        if (name == null || !name.StartsWith(ModConfigurationManager.Instance.SettingsPrefix))
        {
            return true;
        }
        __result = true;
        return false;
    }
}