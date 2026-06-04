using Kingmaker.Localization;
using Kingmaker.Sound;
using Kingmaker.Visual.Sound;
using UnityEngine;

namespace AiVoiceoverMod.Voice;

/// <summary>
/// Central playback entry point for the ported WOTR UI hooks.
/// Resolution order: blueprint GUID (LocalizedString.Key) -> MinHash fuzzy.
/// The fuzzy fallback lives in <see cref="FuzzyResolver.ResolveAndPlay"/>; this type adds the
/// static "play the real GUID" path, mirroring how <c>VoiceoverShim_Patch</c> feeds the game's own
/// voiceover pipeline by key.
/// </summary>
public static class VoiceResolver
{
    /// <summary>
    /// Plays a localized line statically by its blueprint GUID when the key is present, otherwise
    /// falls back to resolving the supplied display text via fuzzy matching.
    /// </summary>
    public static void PlayLocalized(LocalizedString localizedString, string fallbackText, string kind, GameObject obj = null)
    {
        var key = localizedString?.Key;
        if (!string.IsNullOrWhiteSpace(key))
        {
            PlayByKey(key, obj);
            return;
        }

        PlayByText(fallbackText, kind, obj);
    }

    /// <summary>Statically posts the Wwise event for a known blueprint GUID.</summary>
    public static void PlayByKey(string key, GameObject obj = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (obj == null)
            obj = SoundState.Get2DSoundObject();

#if DEBUG
        Debug.Log($"VoiceResolver (STATIC): evt_{key}");
#endif
        SoundEventsManager.PostEvent("evt_" + key, obj);
    }

    /// <summary>
    /// Resolves a displayed string with no reachable key via MinHash fuzzy matching, handled inside
    /// <see cref="FuzzyResolver.ResolveAndPlay"/>.
    /// </summary>
    public static void PlayByText(string text, string kind, GameObject obj = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (obj == null)
            obj = SoundState.Get2DSoundObject();

        FuzzyResolver.ResolveAndPlay(text, kind, obj);
    }
}
