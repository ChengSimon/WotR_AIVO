using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Newtonsoft.Json;
using AiVoiceoverMod.Configuration;
using AiVoiceoverMod.Voice;
using System.Collections.Generic;
using System.IO;

namespace AiVoiceoverMod.Localization;

[HarmonyPatch]
internal class ModLocalizationManager
{
    private static ModLocalizationPack _enPack;

    // Runs when the game initializes localization (a postfix on LocalizationManager.Init), NOT at mod-load
    // time — SettingsRoot/CurrentLocale aren't ready then. Only loads the pack; applying strings is deferred
    // to the OnLocaleChanged postfix below, matching PathfinderTextToSpeechMod.
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.Init))]
    [HarmonyPostfix]
    public static void Init()
    {
        _enPack = LoadPack(Locale.enGB);
        LoadFuzzyIndex();
        ClipCatalog.EnsureLoaded();
    }

    // WOTR has no LocalizationManager.LocaleChanged event (the RT API used .Instance as ILocalizationProvider).
    // Locale changes are observed by patching LocalizationManager.OnLocaleChanged, matching PathfinderTextToSpeechMod.
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.OnLocaleChanged))]
    [HarmonyPostfix]
    public static void OnLocaleChanged_Postfix()
    {
        ApplyLocalization(LocalizationManager.CurrentLocale);
        LoadFuzzyIndex();
    }

    // Builds/loads the fuzzy voiceover index for the current locale. Driven off the same Init/OnLocaleChanged
    // postfixes above because that's when the correct locale becomes known (at mod-load time it isn't), and the
    // OnLocaleChanged postfix is also how the in-game menu language picker is observed. EnsureDatabaseForCurrentLocale
    // is a cheap no-op when the locale is unchanged, so calling it from both hooks is fine.
    private static void LoadFuzzyIndex()
    {
        try
        {
            FuzzyResolver.EnsureDatabaseForCurrentLocale();
        }
        catch (System.Exception ex)
        {
            ModConfigurationManager.Instance?.ModEntry?.Logger?.Error($"Failed to load fuzzy voiceover index: {ex.Message}");
        }
    }

    public static void ApplyLocalization(Locale currentLocale)
    {
        var currentPack = LocalizationManager.CurrentPack;
        if (currentPack == null) return;
        foreach (var entry in _enPack.Strings)
        {
            currentPack.PutString(entry.Key, entry.Value.Text);
        }

        if (currentLocale != Locale.enGB)
        {
            var localized = LoadPack(currentLocale);
            foreach (var entry in localized.Strings)
            {
                currentPack.PutString(entry.Key, entry.Value.Text);
            }
        }
#if DEBUG
        var localizationFolder = Path.Combine(ModConfigurationManager.Instance?.ModEntry?.Path!, "Localization");
        var packFile = Path.Combine(localizationFolder, Locale.enGB + ".json");
        using StreamWriter file = new(packFile);
        using JsonWriter jsonReader = new JsonTextWriter(file);
        JsonSerializer serializer = new();
        serializer.Serialize(jsonReader, _enPack);
#endif
    }

    private static ModLocalizationPack LoadPack(Locale locale)
    {
        var localizationFolder = Path.Combine(ModConfigurationManager.Instance?.ModEntry?.Path!, "Localization");
        var packFile = Path.Combine(localizationFolder, locale + ".json");
        if (File.Exists(packFile))
        {
            try
            {
                using var file = File.OpenText(packFile);
                using JsonReader jsonReader = new JsonTextReader(file);
                JsonSerializer serializer = new();
                var enLocalization = serializer.Deserialize<ModLocalizationPack>(jsonReader);
                return enLocalization;
            }
            catch (System.Exception ex)
            {
                ModConfigurationManager.Instance?.ModEntry?.Logger?.Error($"Failed to read or parse {locale} mod localization pack: {ex.Message}");
            }
        }
        else
        {
            ModConfigurationManager.Instance?.ModEntry?.Logger?.Log($"Missing localization pack for {locale}");
        }
        return new() { Strings = new() };
    }

    public static LocalizedString CreateString(string key, string value)
    {
        if (_enPack.Strings.ContainsKey(key))
        {
            return new LocalizedString { m_ShouldProcess = false, m_Key = key };
        }
        else
        {
            ModConfigurationManager.Instance?.ModEntry?.Logger?.Log($"Missing localization string {key}");
#if DEBUG
            _enPack.Strings[key] = new() { Text = value };
#endif
            return new LocalizedString { m_ShouldProcess = false, m_Key = key };
        }
    }
}

public record ModLocalizationPack
{
    [JsonProperty]
    public Dictionary<string, ModLocalizationEntry> Strings;
}

public struct ModLocalizationEntry
{
    [JsonProperty]
    public string Text;
};