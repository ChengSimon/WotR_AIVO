using HarmonyLib;
using AiVoiceoverMod.Unity.Extensions;
using Kingmaker.UI.MVVM._PCView.MainMenu;

#if DEBUG
using UnityEngine;
#endif

namespace AiVoiceoverMod.Patches;

[HarmonyPatch(typeof(MainMenuPCView), nameof(MainMenuPCView.BindViewImplementation))]
public class MainMenuPCView_Patch
{
    private const string MAIN_MENU_WELCOME_TEXT_PATH = "/MainMenuPCView(Clone)/UICanvas/WelcomeWindowPCView/Background/ScrollContainer/ServiceWindowStandardScrollView/Viewport/Content/text";

    public static void Postfix()
    {
        if (!Main.Enabled)
            return;

#if DEBUG
        Debug.Log($"{nameof(MainMenuPCView)}_BindViewImplementation_Postfix");
#endif

        Hooks.HookUpTextToSpeechOnTransformWithPath(MAIN_MENU_WELCOME_TEXT_PATH);
    }
}