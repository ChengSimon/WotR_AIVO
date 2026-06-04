using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.Dialog.BookEvent;
using Kingmaker.UI.MVVM._PCView.Dialog.Dialog;
using AiVoiceoverMod.Unity.Extensions;
using UnityEngine;

namespace AiVoiceoverMod.Patches;

[HarmonyPatch(typeof(BookEventView<DialogAnswerPCView>), "SetCues")]
public static class BookEventView_Patch
{
    private const string CANVAS_CUES_BLOCK_PATH = "NestedCanvas1/BookEventPCView/ContentWrapper/Window/Content/CuesBlock";
    private const string GLOBALMAP_CUES_BLOCK_PATH = "BookEventView/ContentWrapper/Window/Content/CuesBlock";

    public static void Postfix()
    {
        if (!Main.Enabled)
            return;

#if DEBUG
        Debug.Log($"{nameof(BookEventView<DialogAnswerPCView>)}_SetCues_Postfix");
#endif

        var cuesBlock = UIHelper.TryFindInStaticCanvas(CANVAS_CUES_BLOCK_PATH, GLOBALMAP_CUES_BLOCK_PATH);
        if (cuesBlock == null)
        {
            Debug.LogWarning("CuesBlock not found!");
            return;
        }

        cuesBlock.HookupTextToSpeechOnTransform();
    }
}
