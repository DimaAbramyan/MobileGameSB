using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastScreenSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool hasAppliedLayout;

    public Rect ScreenSafeArea => Screen.safeArea;
    public event Action<Rect> OnScreenSafeAreaChanged;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (NeedsLayoutUpdate())
            Apply();
    }

    [ContextMenu("Apply Safe Area")]
    public void Apply()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null || rectTransform.parent is not RectTransform)
        {
            Debug.LogWarning(
                $"{nameof(SafeArea)} on '{name}' must be a child of a UI RectTransform.",
                this);
            return;
        }

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (screenWidth <= 0 || screenHeight <= 0)
            return;

        Rect screenSafeArea = Screen.safeArea;
        Vector2 anchorMin = new Vector2(
            screenSafeArea.xMin / screenWidth,
            0f);
        Vector2 anchorMax = new Vector2(
            screenSafeArea.xMax / screenWidth,
            screenSafeArea.yMax / screenHeight);

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        bool changed = !hasAppliedLayout
            || screenWidth != lastScreenWidth
            || screenHeight != lastScreenHeight
            || screenSafeArea != lastScreenSafeArea;

        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
        lastScreenSafeArea = screenSafeArea;
        hasAppliedLayout = true;

        if (changed)
            OnScreenSafeAreaChanged?.Invoke(screenSafeArea);
    }

    private bool NeedsLayoutUpdate()
    {
        return !hasAppliedLayout
            || Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight
            || Screen.safeArea != lastScreenSafeArea;
    }
}
