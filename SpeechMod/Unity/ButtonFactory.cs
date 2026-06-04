using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Owlcat.Runtime.UI.Controls.Button;
using AiVoiceoverMod.Unity.Extensions;
using AiVoiceoverMod.Voice;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Kingmaker.Visual.Sound;
using GameObject = UnityEngine.GameObject;
using Object = UnityEngine.Object;

namespace AiVoiceoverMod.Unity;

public static class ButtonFactory
{
    // WOTR play button: clone the dialog's own arrow ("ButtonEdge"). The RT source (ConvertButton / asset
    // 6dda9b69...) does not exist in WOTR, which is why no button appeared. On the global map the dialog UI
    // isn't present, so clone the combat log's ButtonEdge instead. Transform.Find locates these even while
    // their windows are inactive, so cloning works in non-dialog windows (encyclopedia, journal) too.
    private const string ARROW_BUTTON_PATH = "NestedCanvas1/DialogPCView/Body/View/Scroll View/ButtonEdge";
    private const string ARROW_BUTTON_MAP_PATH = "CombatLog_New/Panel/ButtonEdge";
    private const string MIRROR_MAP_PATH = "BookEventView/ContentWrapper/Window/Mirror/Mirror";
    private const string MIRROR_STATIC_CANVAS_PATH = "NestedCanvas1/BookEventPCView/ContentWrapper/Window/Mirror/Mirror";

    private static GameObject CreatePlayButton(Transform parent, UnityAction action, string text, string toolTip)
    {
        GameObject arrowButton;

        if (UIUtility.IsGlobalMap())
        {
            arrowButton = UIHelper.TryFindInStaticCanvas(ARROW_BUTTON_MAP_PATH)?.gameObject;
            FixMirrorRaycastTarget(UIHelper.TryFindInStaticCanvas(MIRROR_MAP_PATH));
        }
        else
        {
            arrowButton = UIHelper.TryFindInStaticCanvas(ARROW_BUTTON_PATH)?.gameObject;
            FixMirrorRaycastTarget(UIHelper.TryFindInStaticCanvas(MIRROR_STATIC_CANVAS_PATH));
        }

        if (arrowButton == null)
        {
            Debug.LogWarning("Arrow button (ButtonEdge) source not found; cannot create play button.");
            return null;
        }

        var buttonGameObject = Object.Instantiate(arrowButton, parent);
        SetupOwlcatButton(buttonGameObject, action, text, toolTip);

        return buttonGameObject;
    }

    private static void FixMirrorRaycastTarget(Transform mirror)
    {
        // The "Mirror" overlay sits above the cue text and would otherwise eat clicks on the new button.
        var image = mirror?.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = false;
    }

    private static void SetupOwlcatButton(GameObject buttonGameObject, UnityAction action, string text, string toolTip)
    {
        if (buttonGameObject == null)
            return;

        var button = buttonGameObject.GetComponent<OwlcatButton>();
        if (button == null)
        {
            Debug.LogWarning("Cloned ButtonEdge has no OwlcatButton; cannot wire click.");
            return;
        }

        button.OnLeftClick.RemoveAllListeners();

        // The dialog ButtonEdge ships with a persistent listener (advance dialog); disable it on our clone.
        if (button.OnLeftClick.GetPersistentEventCount() > 0)
            button.OnLeftClick.SetPersistentListenerState(0, UnityEventCallState.Off);

        button.OnLeftClick.AddListener(action);

        if (!string.IsNullOrWhiteSpace(text))
            button.SetTooltip(new TooltipTemplateSimple(text, toolTip));
    }

    public static GameObject TryCreatePlayButton(Transform parent, UnityAction action, string text = null, string tooltip = null)
    {
        return CreatePlayButton(parent, action, text, tooltip);
    }

    public static GameObject TryAddButtonToTextMeshPro(this TextMeshProUGUI textMeshPro, string buttonName, Vector2? anchoredPosition = null, Vector3? scale = null, TextMeshProUGUI[] textMeshProUguis = null)
    {
        var transform = textMeshPro?.transform;
        var tmpButton = transform.TryFind(buttonName)?.gameObject;
        if (tmpButton != null)
            return null;

#if DEBUG
        Debug.Log($"Adding playbutton to {textMeshPro?.name}...");
#endif

        var button = TryCreatePlayButton(transform, () =>
        {
            var text = textMeshPro?.text;
            if (textMeshProUguis != null)
            {
                text = textMeshProUguis.Where(textOverride => textOverride != null).Select(to => to.text).Aggregate("", (previous, current) => $"{previous}, {current}");
            }
            FuzzyResolver.ResolveAndPlay(text, "PlayBtn", SoundState.Get2DSoundObject());
        });

        if (button == null || button.transform == null)
            return null;

        button.name = buttonName;
        button.RectAlignTopLeft(anchoredPosition);

        if (scale.HasValue)
            button.transform!.localScale = scale.Value;

        button.SetActive(true);
        return button;
    }
}
