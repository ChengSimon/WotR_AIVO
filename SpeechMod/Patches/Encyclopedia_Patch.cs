using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Encyclopedia;
using AiVoiceoverMod.Unity;
using AiVoiceoverMod.Unity.Extensions;
using AiVoiceoverMod.Voice;
using System.Linq;
using TMPro;
using UnityEngine;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch]
public static class Encyclopedia_Patch
{
    private const string ButtonName = "EncyclopediaSpeechButton";

    private const string BODY_GROUP_PATH = "ServiceWindowsPCView/Background/Windows/EncyclopediaPCView/EncyclopediaPageView/BodyGroup";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EncyclopediaPageBaseView), "Initialize")]
    public static void Initialize_Postfix(EncyclopediaPageBaseView __instance)
    {
        if (!Main.Enabled)
            return;

        __instance.m_Title.HookupTextToSpeech();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EncyclopediaPagePCView), "UpdateView")]
    public static void UpdateView_Postfix()
    {
        if (!Main.Enabled)
            return;

#if DEBUG
        Debug.Log($"{nameof(EncyclopediaPagePCView)}_UpdateView_Postfix");
#endif

        var bodyGroup = UIHelper.TryFindInStaticCanvas(BODY_GROUP_PATH);
        if (bodyGroup == null)
        {
#if DEBUG
            Debug.Log("Couldn't find BodyGroup...");
#endif
            return;
        }

        var content = bodyGroup.TryFind("ObjectivesGroup/StandardScrollView/Viewport/Content");
        if (content == null)
            return;

        // Only get the texts that are not in the unit view.
        var allTexts = content.gameObject?.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Where(t => t.transform.name.Equals("Text")).ToArray();
        if (allTexts == null || allTexts.Length == 0)
            return;

        foreach (var textMeshPro in allTexts)
        {
            var parent = textMeshPro.transform;
            var button = parent.TryFind(ButtonName)?.gameObject;

            if (button != null)
            {
                button.transform.localRotation = Quaternion.Euler(0, 0, 90);
                button.RectAlignTopLeft();
                button.transform.localPosition = new Vector3(-36, -26, 0);
                continue;
            }

            button = ButtonFactory.TryCreatePlayButton(parent, () =>
            {
                VoiceResolver.PlayByText(textMeshPro.text, "Encyclopedia");
            });
            if (button == null)
                continue;

            button.name = ButtonName;
            button.transform.localRotation = Quaternion.Euler(0, 0, 90);
            button.RectAlignTopLeft();
            button.transform.localPosition = new Vector3(-36, -26, 0);
            button.SetActive(true);
        }
    }
}
