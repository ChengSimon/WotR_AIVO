using System;
using System.Collections;
using Kingmaker;
using Kingmaker.UI.Common;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace AiVoiceoverMod.Unity.Extensions;

public static class UIHelper
{
    public static Coroutine ExecuteLater(this MonoBehaviour behaviour, float delay, Action action)
    {
        return behaviour.StartCoroutine(InternalExecute(delay, action));
    }

    private static IEnumerator InternalExecute(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }

    public static bool IsParentClickable(this Transform transform)
    {
        return transform.GetComponentInParents<ObservablePointerClickTrigger>() != null;
    }

    public static T GetComponentInParents<T>(this Transform transform) where T : Component
    {
        var parent = transform?.parent;
        while (parent != null && parent != transform.root)
        {
            var component = parent.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
            parent = parent.parent;
        }
        return null;
    }

    public static void SetRaycastTarget(this Graphic graphic, bool enable)
    {
        if (graphic == null)
            return;

        graphic.raycastTarget = enable;
    }

    public static Transform TryFind(this Transform transform, string n)
    {
        if (string.IsNullOrWhiteSpace(n) || transform == null)
            return null;

        try
        {
            return transform.Find(n);
        }
        catch
        {
            Debug.Log("TryFind found nothing!");
        }

        return null;
    }

    public static Transform TryFind(string n)
    {
        if (string.IsNullOrWhiteSpace(n))
            return null;

        try
        {
            return GameObject.Find(n)?.transform;
        }
        catch
        {
            Debug.Log("TryFind found nothing!");
        }

        return null;
    }

    public static string GetGameObjectPath(this Transform transform)
    {
        var path = transform?.name;
        while (transform?.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

#nullable enable
    public static T? GetComponentNullable<T>(this Transform transform) where T : Component
    {
        if (transform == null)
            return null;

        if (transform.TryGetComponent(typeof(T), out var component))
        {
            return (T)component;
        }

        return null;
    }

    public static void FixBlockingUi(string path)
    {
        var blockingUi = TryFind(path);
        if (blockingUi != null)
        {
            var image = blockingUi.GetComponentNullable<Image>();
            if (image != null)
                image.raycastTarget = false;
        }
    }

    // --- WOTR canvas lookups (ported from PathfinderTextToSpeechMod) ---
    // WOTR routes static-window UI through Game.Instance.UI.Canvas (or GlobalMapUI on the world map)
    // and transient modals through FadeCanvas, rather than a single GameObject.Find root.

    public static Transform GetUICanvas()
    {
        return UIUtility.IsGlobalMap()
            ? Game.Instance.UI.GlobalMapUI.transform
            : Game.Instance.UI.Canvas.transform;
    }

    public static Transform TryFindInStaticCanvas(string n)
    {
        return TryFindInStaticCanvas(n, n);
    }

    public static Transform TryFindInStaticCanvas(string canvasName, string globalMapName)
    {
        return UIUtility.IsGlobalMap()
            ? Game.Instance.UI.GlobalMapUI.transform.TryFind(globalMapName)
            : Game.Instance.UI.Canvas.transform.TryFind(canvasName);
    }

    public static Transform TryFindInFadeCanvas(string n)
    {
        return Game.Instance.UI.FadeCanvas.transform.TryFind(n);
    }
}