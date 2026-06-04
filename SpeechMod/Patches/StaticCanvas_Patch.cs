using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.Visual.Sound;
using AiVoiceoverMod.Unity;
using AiVoiceoverMod.Unity.Extensions;
using AiVoiceoverMod.Voice;
using UnityEngine;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch(typeof(StaticCanvas), "Initialize")]
public static class StaticCanvas_Patch
{
    private const string SCROLL_VIEW_PATH = "NestedCanvas1/DialogPCView/Body/View/Scroll View";
    private const string SPEECH_BUTTON_NAME = "SpeechMod_DialogButton";

    public static void Postfix()
    {
        if (!Main.Enabled)
            return;

#if DEBUG
        Debug.Log($"{nameof(StaticCanvas)}_Initialize_Postfix");
#endif

        AddDialogSpeechButton();
    }

    private static void AddDialogSpeechButton()
    {
        var parent = UIHelper.TryFindInStaticCanvas(SCROLL_VIEW_PATH);
        if (parent == null)
        {
            Debug.LogWarning("Dialog Scroll View not found!");
            return;
        }

        if (parent.TryFind(SPEECH_BUTTON_NAME) != null)
            return;

        var buttonGameObject = ButtonFactory.TryCreatePlayButton(parent, () =>
        {
            var cue = Game.Instance?.DialogController?.CurrentCue;
            // Static-by-GUID: the cue carries its blueprint key; fall back to its display text otherwise.
            VoiceResolver.PlayLocalized(cue?.Text, cue?.DisplayText, "DlgBtn", SoundState.Get2DSoundObject());
        });

        if (buttonGameObject == null)
            return;

        buttonGameObject.name = SPEECH_BUTTON_NAME;
        buttonGameObject.transform.localPosition = new Vector3(-493, 164, 0);
        buttonGameObject.transform.localRotation = Quaternion.Euler(0, 0, 90);
        buttonGameObject.SetActive(true);
    }
}
