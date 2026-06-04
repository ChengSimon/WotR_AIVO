using AiVoiceoverMod.Localization;
using UnityEngine;
using Kingmaker.UI.SettingsUI;

namespace AiVoiceoverMod.Configuration.UI;

public static class OwlcatUITools
{
    public static UISettingsGroup MakeSettingsGroup(string key, string name, params UISettingsEntityBase[] settings)
    {
        var group = ScriptableObject.CreateInstance<UISettingsGroup>();
        group.name = key;
        group.Title = ModLocalizationManager.CreateString(key, name);

        group.SettingsList = settings;

        return group;
    }
}
