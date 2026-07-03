using System;
using UnityEngine;

namespace AiVoiceoverMod.Unity;

public static class MenuGUI
{

    private static GUIStyle m_WarningStyle;
    private static GUIStyle m_InfoStyle;

    public static void OnGui()
    {
        if (Main.LoadedBanks.Count == 0)
        {
            m_WarningStyle ??= new GUIStyle(GUI.skin.box)
            {
                normal = { textColor = Color.yellow },
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 10, 10)
            };
            GUILayout.Label("WARNING: No soundbanks loaded! Put AI voiceover .bnk files into the soundbanks/ folder.", m_WarningStyle);
        }
        else
        {
            m_InfoStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };
            GUILayout.Label("Loaded soundbanks: " + string.Join(", ", Main.LoadedBanks), m_InfoStyle);
        }

#if DEBUG
        GUILayout.BeginVertical("", GUI.skin.box);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Log speech", GUILayout.ExpandWidth(false));
        Main.Settings.LogVoicedLines = GUILayout.Toggle(Main.Settings.LogVoicedLines, "Enabled");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
#endif

        AddHeader("Playback Settings");

        
        GUILayout.BeginVertical("", GUI.skin.box);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Playback barks", GUILayout.ExpandWidth(false));
        GUILayout.Space(10);
        Main.Settings.PlaybackBarks = GUILayout.Toggle(Main.Settings.PlaybackBarks, "Enabled");
        GUILayout.EndHorizontal();

        if (Main.Settings.PlaybackBarks)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Only playback barks if there's no dialogue", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Main.Settings.PlaybackBarkOnlyIfSilence = GUILayout.Toggle(Main.Settings.PlaybackBarkOnlyIfSilence, "Enabled");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Playback vicinity and cutscene triggered barks", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Main.Settings.PlaybackBarksInVicinity = GUILayout.Toggle(Main.Settings.PlaybackBarksInVicinity, "Enabled");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Deduplicate barks withing {Math.Ceiling(Main.Settings.SoundDedupTimeout)}s (0 to disable)", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            Main.Settings.SoundDedupTimeout = GUILayout.HorizontalSlider(Main.Settings.SoundDedupTimeout, 0, 10);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Accelerated speed (default key is ]) {Main.Settings.AcceleratedPlaybackSpeed.ToString("F2")}", GUILayout.ExpandWidth(false));
        GUILayout.Space(10);
        Main.Settings.AcceleratedPlaybackSpeed = GUILayout.HorizontalSlider(Main.Settings.AcceleratedPlaybackSpeed, 0, 100);
        // Applied via the "Hold to apply playback speed" hotkey (default ]); see PlaybackSpeedHold / Main.OnUpdate.
        GUILayout.EndHorizontal();
		
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Accelerated pitch correction {Main.Settings.AcceleratedPitchCorrection:F0}", GUILayout.ExpandWidth(false));
		GUILayout.Space(10);
		Main.Settings.AcceleratedPitchCorrection =
			GUILayout.HorizontalSlider(Main.Settings.AcceleratedPitchCorrection, -2400f, 0f);
		GUILayout.EndHorizontal();
		
		GUILayout.BeginHorizontal();
        GUILayout.Label($"Default Play back speed {Main.Settings.DefaultAcceleratedPlaybackSpeed.ToString("F2")}", GUILayout.ExpandWidth(false));
        GUILayout.Space(10);
        Main.Settings.DefaultAcceleratedPlaybackSpeed = GUILayout.HorizontalSlider(Main.Settings.DefaultAcceleratedPlaybackSpeed, 0, 100);
        GUILayout.EndHorizontal();
		
		GUILayout.BeginHorizontal();
		GUILayout.Label($"Default pitch correction {Main.Settings.DefaultPitchCorrection:F0}", GUILayout.ExpandWidth(false));
		GUILayout.Space(10);
		Main.Settings.DefaultPitchCorrection =
			GUILayout.HorizontalSlider(Main.Settings.DefaultPitchCorrection, -2400f, 0f);
		GUILayout.EndHorizontal();

        AddColorPicker("Color on text hover", ref Main.Settings.ColorOnHover, "Hover color", ref Main.Settings.HoverColorR, ref Main.Settings.HoverColorG, ref Main.Settings.HoverColorB, ref Main.Settings.HoverColorA);

        GUILayout.BeginVertical("", GUI.skin.box);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Font style on text hover", GUILayout.ExpandWidth(false));
        Main.Settings.FontStyleOnHover = GUILayout.Toggle(Main.Settings.FontStyleOnHover, "Enabled");
        GUILayout.EndHorizontal();

        if (Main.Settings.FontStyleOnHover)
        {
            GUILayout.BeginHorizontal();
            for (var i = 0; i < Main.Settings.FontStyles.Length; ++i)
            {
                Main.Settings.FontStyles[i] = GUILayout.Toggle(Main.Settings.FontStyles[i], Main.FontStyleNames[i], GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();



        GUILayout.EndVertical();
    }

    
    private static void AddColorPicker(string enableLabel, ref bool enabledBool, string colorLabel, ref float r, ref float g, ref float b)
    {
        float a = 1;
        AddColorPicker(enableLabel, ref enabledBool, colorLabel, ref r, ref g, ref b, ref a, false);
    }

    private static void AddColorPicker(string enableLabel, ref bool enabledBool, string colorLabel, ref float r, ref float g, ref float b, ref float a, bool useAlpha = true)
    {
        GUILayout.BeginVertical("", GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(enableLabel, GUILayout.ExpandWidth(false));
        enabledBool = GUILayout.Toggle(enabledBool, "Enabled");
        GUILayout.EndHorizontal();

        if (enabledBool)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(colorLabel, GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            GUILayout.Label("R ", GUILayout.ExpandWidth(false));
            r = GUILayout.HorizontalSlider(r, 0, 1);
            GUILayout.Space(10);
            GUILayout.Label("G", GUILayout.ExpandWidth(false));
            g = GUILayout.HorizontalSlider(g, 0, 1);
            GUILayout.Space(10);
            GUILayout.Label("B", GUILayout.ExpandWidth(false));
            b = GUILayout.HorizontalSlider(b, 0, 1);
            GUILayout.Space(10);
            if (useAlpha)
            {
                GUILayout.Label("A", GUILayout.ExpandWidth(false));
                a = GUILayout.HorizontalSlider(a, 0, 1);
                GUILayout.Space(10);
            }
            else
            {
                a = 1;
            }
            GUILayout.Box(GetColorPreview(ref r, ref g, ref b, ref a), GUILayout.Width(20));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private static void AddHeader(string text)
    {
        GUILayout.BeginVertical(text, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private static Texture2D GetColorPreview(ref float r, ref float g, ref float b, ref float a)
    {
        var texture = new Texture2D(20, 20);
        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, new Color(r, g, b, a));
            }
        }
        texture.Apply();
        return texture;
    }
}