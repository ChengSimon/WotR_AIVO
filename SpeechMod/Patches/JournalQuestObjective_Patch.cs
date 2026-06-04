using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Journal;
using AiVoiceoverMod.Unity;
using AiVoiceoverMod.Unity.Extensions;
using AiVoiceoverMod.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch(typeof(JournalQuestObjectivePCView), "BindViewImplementation")]
public static class JournalQuestObjective_Patch
{
    private const string ButtonName = "JQSpeechButton";

    private const string BODY_GROUP_PATH = "ServiceWindowsPCView/Background/Windows/JournalPCView/JournalQuestView/BodyGroup";

    public static void Postfix()
    {
        if (!Main.Enabled)
            return;

#if DEBUG
        Debug.Log($"{nameof(JournalQuestObjectivePCView)}_BindViewImplementation_Postfix");
#endif

        var bodyGroup = UIHelper.TryFindInStaticCanvas(BODY_GROUP_PATH);
        if (bodyGroup == null)
        {
            Debug.Log("Couldn't find BodyGroup...");
            return;
        }

        var allTexts = bodyGroup.gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (allTexts == null || allTexts.Length == 0)
        {
            Debug.Log("Couldn't find any TextMeshProUGUI...");
            return;
        }

        var isFirst = true;
        foreach (var textMeshPro in allTexts)
        {
            var tmpTransform = textMeshPro.transform;
            if (!ShouldAddButton(tmpTransform))
                continue;

            var button = tmpTransform.TryFind(ButtonName)?.gameObject;

            if (button != null)
            {
                button.transform.localRotation = Quaternion.Euler(0, 0, 90);
                tmpTransform.gameObject.RectAlignTopLeft();
                button.RectAlignTopLeft();
                SetNewPosition(tmpTransform, button.transform, ref isFirst);
                button.SetActive(true);
                continue;
            }

            button = ButtonFactory.TryCreatePlayButton(tmpTransform, () =>
            {
                VoiceResolver.PlayByText(textMeshPro.text, "Journal");
            });
            if (button == null)
                continue;

            button.name = ButtonName;
            button.transform.localRotation = Quaternion.Euler(0, 0, 90);
            tmpTransform.gameObject.RectAlignTopLeft();
            button.RectAlignTopLeft();
            SetNewPosition(tmpTransform, button.transform, ref isFirst);
            button.SetActive(true);
        }

        // Move the line back behind our buttons.
        var allImages = bodyGroup.GetComponentsInChildren<Image>();
        foreach (var image in allImages)
        {
            if (image.gameObject.name.Equals("LeftVerticalBorderImage"))
                image.transform.SetAsFirstSibling();
        }
    }

    private static bool ShouldAddButton(Transform transform)
    {
        switch (transform.name)
        {
            case "LastChapterLabel":
            case "DescriptionLabel":
            case "Label":
                return true;
            default:
                return false;
        }
    }

    private static void SetNewPosition(Transform tmpTransform, Transform transform, ref bool isFirst)
    {
        switch (tmpTransform.name)
        {
            case "LastChapterLabel":
                transform.localPosition = new Vector3(-72, -35, 0);
                break;
            case "TitleLabel":
                transform.localPosition = new Vector3(0, -42, 0);
                break;
            case "DescriptionLabel":
                if (isFirst)
                {
                    isFirst = false;
                    transform.localPosition = new Vector3(-10, -24, 0);
                    break;
                }
                transform.localPosition = new Vector3(-35, -24, 0);
                break;
            case "Label":
                var ipi = tmpTransform.parent.TryFind("InProgressImage").gameObject;
                transform.localPosition = new Vector3(-82, ipi.transform.InverseTransformPoint(ipi.transform.position).y - 26, 0);
                break;
            default:
                transform.localPosition = Vector3.zero;
                break;
        }
    }
}
