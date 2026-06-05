using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AiVoiceoverMod.Voice;

// Catalog of the pre-rendered clips and their type, loaded from clips.json (guid -> { "t": "<type>" }).
// Used to suppress bark-type lines from being swapped to evt_* while barks are disabled: at the evt_ chokepoint
// we only have a guid, not a call-site "kind", so the line's type has to come from this catalog.
public static class ClipCatalog
{
    // The clips.json "t" values that count as barks for the PlaybackBarks toggle.
    private static readonly HashSet<string> s_BarkTypes =
        new(StringComparer.OrdinalIgnoreCase) { "bark", "skillcheck" };

    private static HashSet<string> s_BarkGuids;
    private static bool s_Loaded;

    private sealed class ClipInfo
    {
        [JsonProperty("t")] public string t { get; set; }
    }

    public static void EnsureLoaded()
    {
        if (s_Loaded) return;
        s_Loaded = true; // Set up front so a parse failure doesn't re-read the file on every event.
        s_BarkGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = Path.Combine(dir, "clips.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"AIVO clip catalog not found at {path}.");
                return;
            }

            var map = JsonConvert.DeserializeObject<Dictionary<string, ClipInfo>>(File.ReadAllText(path));
            if (map == null) return;

            foreach (var pair in map)
            {
                if (pair.Value?.t != null && s_BarkTypes.Contains(pair.Value.t))
                    s_BarkGuids.Add(pair.Key);
            }

            Debug.Log($"AIVO clip catalog loaded: {s_BarkGuids.Count} bark/skillcheck clips of {map.Count} total.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogWarning("AIVO failed to load clip catalog.");
        }
    }

    // True when this guid is a bark-type clip ("bark"/"skillcheck") and barks are currently disabled, meaning
    // the evt_* event for it should not be played. A no-op (false) whenever barks are enabled.
    public static bool IsSuppressedBark(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return false;
        if (Main.Settings == null || Main.Settings.PlaybackBarks)
            return false;
        EnsureLoaded();
        return s_BarkGuids.Contains(guid);
    }
}
