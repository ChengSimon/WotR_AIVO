using UnityModManagerNet;

namespace AiVoiceoverMod;

public class Settings : UnityModManager.ModSettings
{
    public bool LogVoicedLines = false;

    public bool ColorOnHover = false;
    public float HoverColorR = 0f;
    public float HoverColorG = 0f;
    public float HoverColorB = 0f;
    public float HoverColorA = 1f;

    public bool FontStyleOnHover = false;
    public bool[] FontStyles = [false, false, false, true, false, false, false, false, false, false, false];

    public bool InterruptPlaybackOnPlay = true;
    public bool PlaybackBarks = true;
    public bool PlaybackBarkOnlyIfSilence = false;
    public bool PlaybackBarksInVicinity = true;

    public float SoundDedupTimeout = 4f;

    public override void Save(UnityModManager.ModEntry modEntry)
    {
        Save(this, modEntry);
    }
}
