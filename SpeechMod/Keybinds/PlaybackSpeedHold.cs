using AiVoiceoverMod.Configuration.Settings;
using UnityEngine;

namespace AiVoiceoverMod.KeyBinds;

public class PlaybackSpeedHold : ModHotkeySettingEntry
{
    private const string KEY = "playback.speed.hold";
    private const string TITLE = "Hold to apply playback speed";
    private const string TOOLTIP = "While this key is held, TTS plays at the configured Accelerated speed; released, it plays at normal speed.";
    // Format: Binding1;Binding2;GameModesGroup;TriggerOnHold. Bindings use the KeyCode enum name,
    // optionally prefixed with a modifier (e.g. "%S" for Ctrl+S). RightBracket is "]".
    private const string DEFAULT_VALUE = "RightBracket;;All;false";

    public static PlaybackSpeedHold Instance { get; private set; }

    public PlaybackSpeedHold() : base(KEY, TITLE, TOOLTIP, DEFAULT_VALUE)
    {
        Instance = this;
    }

    // We poll the bound key ourselves (see Main.OnUpdate) so we can react to both press and
    // release, which the press-only action binding can't express. Registering the keybind keeps
    // it visible/rebindable in the controls UI and feeding into conflict detection.
    public override SettingStatus TryEnable()
    {
        RegisterKeybind();
        return SettingStatus.WORKING;
    }

    /// <summary>True while the currently bound key (read live, so rebinds take effect immediately) is held down.</summary>
    public bool IsHeld()
    {
        var pair = SettingEntity.GetValue();
        return IsDown(pair.Binding1.Key) || IsDown(pair.Binding2.Key);
    }

    private static bool IsDown(KeyCode key) => key != KeyCode.None && Input.GetKey(key);
}
