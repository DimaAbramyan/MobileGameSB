using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UnsafeTopArea : MonoBehaviour
{
    [Tooltip("Upper edge of the player ship HUD. When assigned, the unsafe area fills everything above this boundary.")]
    [SerializeField] private RectTransform playerShipHudBoundary;

    private RectTransform rectTransform;
    private readonly Vector3[] boundaryCorners = new Vector3[4];
    private Rect lastScreenSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastBoundaryTop;
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
        float unsafeAreaBottom = GetUnsafeAreaBottom(screenSafeArea, screenHeight);

        rectTransform.anchorMin = new Vector2(0f, unsafeAreaBottom);
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
        lastScreenSafeArea = screenSafeArea;
        lastBoundaryTop = GetPlayerShipHudTop();
        hasAppliedLayout = true;
    }

    private float GetUnsafeAreaBottom(Rect screenSafeArea, int screenHeight)
    {
        if (playerShipHudBoundary == null)
            return Mathf.Clamp01(screenSafeArea.yMax / screenHeight);

        return Mathf.Clamp01(GetPlayerShipHudTop() / screenHeight);
    }

    private float GetPlayerShipHudTop()
    {
        if (playerShipHudBoundary == null)
            return 0f;

        playerShipHudBoundary.GetWorldCorners(boundaryCorners);

        float top = float.MinValue;
        for (int i = 0; i < boundaryCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                boundaryCorners[i]);
            top = Mathf.Max(top, screenPoint.y);
        }

        return top;
    }

    private bool NeedsLayoutUpdate()
    {
        return !hasAppliedLayout
            || Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight
            || Screen.safeArea != lastScreenSafeArea
            || !Mathf.Approximately(GetPlayerShipHudTop(), lastBoundaryTop);
    }
}
