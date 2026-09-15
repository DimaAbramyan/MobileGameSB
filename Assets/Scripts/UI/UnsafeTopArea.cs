using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UnsafeTopArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastScreenSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private bool hasAppliedLayout;

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

    [ContextMenu("Apply Unsafe Top Area")]
    public void Apply()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null || rectTransform.parent is not RectTransform)
        {
            Debug.LogWarning(
                $"{nameof(UnsafeTopArea)} on '{name}' must be a child of a UI RectTransform.",
                this);
            return;
        }

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (screenWidth <= 0 || screenHeight <= 0)
            return;

        Rect screenSafeArea = Screen.safeArea;
        float safeTop = Mathf.Clamp01(screenSafeArea.yMax / screenHeight);

        rectTransform.anchorMin = new Vector2(0f, safeTop);
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
        lastScreenSafeArea = screenSafeArea;
        hasAppliedLayout = true;
    }

    private bool NeedsLayoutUpdate()
    {
        return !hasAppliedLayout
            || Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight
            || Screen.safeArea != lastScreenSafeArea;
    }
}
