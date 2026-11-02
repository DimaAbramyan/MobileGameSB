using Unity.Cinemachine;
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class BattleCameraViewport : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform topHudBoundary;
    [SerializeField, Tooltip("Нижний HUD целиком. Игровая область начинается выше его верхней границы.")]
    private RectTransform bottomHudBoundary;
    [SerializeField, Tooltip("Учитывать вырезы экрана и системные области устройства.")]
    private bool respectSafeArea = true;
    [SerializeField] private CameraConstantWidth cameraConstantWidth;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [Tooltip("Renders the battle between the configured upper/lower HUD boundaries. "
        + "Use a separate clear camera to fill the non-gameplay area.")]
    [SerializeField] private bool restrictRenderingToGameplayViewport = true;
    [Header("Adaptive Gameplay Viewport")]
    [SerializeField, Tooltip("Fill the available area between HUD boundaries. Preserve the reference world width and height without letterboxing or sprite stretching. Disable to restore legacy fixed-aspect rendering.")]
    private bool fillAvailableGameplayArea = true;
    [Header("Reference Gameplay Format")]
    [SerializeField, Tooltip("Use the reference aspect to preserve the authored world area. With adaptive filling disabled, this restores the legacy fixed-aspect viewport.")]
    private bool useFixedGameplayAspect = true;
    [SerializeField, Min(0.1f)] private float fixedGameplayAspect = 9f / 17f;
    [Header("Cinemachine Lens")]
    [SerializeField, Min(0.01f)] private float lensOrthographicSize = 5.3f;

    private readonly Vector3[] hudCorners = new Vector3[4];
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Rect lastAvailableRect;
    private float lastFixedGameplayAspect;
    private bool lastUsedFixedGameplayAspect;
    private bool lastFilledAvailableGameplayArea;
    private float lastLensOrthographicSize;
    private bool hasAppliedViewport;
    private bool warnedMissingHudBoundary;
    private bool lastRestrictedViewport;

    public Rect GameplayScreenRect => targetCamera != null
        ? targetCamera.pixelRect
        : Rect.zero;

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return targetCamera != null
            && targetCamera.pixelRect.Contains(screenPoint);
    }

    private void Awake()
    {
        ResolveReferences();
        Apply();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Apply();
        Canvas.willRenderCanvases += RefreshEditorViewport;
    }

    private void OnDisable() => Canvas.willRenderCanvases -= RefreshEditorViewport;

    private void RefreshEditorViewport()
    {
        if (!Application.isPlaying)
            RefreshViewport();
    }

    private void LateUpdate() => RefreshViewport();

    private void RefreshViewport()
    {
        if (!ResolveReferences())
            return;

        Rect availableRect = GetAvailableScreenRect();
        if (!NeedsViewportUpdate(availableRect))
            return;

        Apply(availableRect);
    }

    [ContextMenu("Apply Gameplay Viewport")]
    public void Apply()
    {
        if (!ResolveReferences())
            return;

        Apply(GetAvailableScreenRect());
    }

    // Explicit display input lets non-rendering Editor previews use this same calculation
    // without depending on whichever Editor window currently owns Screen's render context.
    public Rect ApplyForDisplay(int screenWidth, int screenHeight, Rect screenSafeArea)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || !ResolveReferences())
            return Rect.zero;

        Apply(GetAvailableScreenRect(screenWidth, screenHeight, screenSafeArea), screenWidth, screenHeight);
        Rect rect = targetCamera.rect;
        return new Rect(rect.x * screenWidth, rect.y * screenHeight, rect.width * screenWidth, rect.height * screenHeight);
    }

    private void Apply(Rect availableRect) => Apply(availableRect, Screen.width, Screen.height);

    private void Apply(Rect availableRect, int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0f || screenHeight <= 0f)
            return;

        Rect gameplayRect = GetGameplayRect(
            screenWidth,
            screenHeight,
            availableRect);
        targetCamera.rect = gameplayRect;
        ApplyCinemachineLens(gameplayRect.width * screenWidth / Mathf.Max(1f, gameplayRect.height * screenHeight));

        if (cinemachineCamera == null && !fillAvailableGameplayArea)
            cameraConstantWidth?.Apply();

        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
        lastAvailableRect = availableRect;
        lastRestrictedViewport = restrictRenderingToGameplayViewport;
        lastFixedGameplayAspect = fixedGameplayAspect;
        lastUsedFixedGameplayAspect = useFixedGameplayAspect;
        lastFilledAvailableGameplayArea = fillAvailableGameplayArea;
        lastLensOrthographicSize = lensOrthographicSize;
        hasAppliedViewport = true;
    }

    // Keep the authored reference rectangle visible; only expose additional world area.
    // Both Runtime and Edit Mode use the same lens and viewport calculations.
    private void ApplyCinemachineLens(float gameplayAspect)
    {
        float size = GetOrthographicSize(gameplayAspect);
        if (cinemachineCamera == null)
        {
            if (fillAvailableGameplayArea)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = size;
            }
            return;
        }

        LensSettings lens = cinemachineCamera.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = size;
        cinemachineCamera.Lens = lens;
        // Cinemachine's Brain does not update the lens in Edit Mode.
        if (!Application.isPlaying)
        {
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = lens.OrthographicSize;
        }
    }

    public float GetOrthographicSize(float gameplayAspect)
    {
        float referenceSize = Mathf.Max(0.01f, lensOrthographicSize);
        if (!fillAvailableGameplayArea || !useFixedGameplayAspect)
            return referenceSize;

        return referenceSize * Mathf.Max(1f, Mathf.Max(0.1f, fixedGameplayAspect) / Mathf.Max(0.01f, gameplayAspect));
    }

    private Rect GetGameplayRect(
        float screenWidth,
        float screenHeight,
        Rect availableRect)
    {
        float availableHeight = availableRect.height;
        if (availableHeight <= 0f)
            return Rect.zero;

        Rect normalizedRect = new Rect(
            availableRect.x / screenWidth, availableRect.y / screenHeight,
            availableRect.width / screenWidth, availableHeight / screenHeight);
        if (fillAvailableGameplayArea || !useFixedGameplayAspect)
            return normalizedRect;

        float targetAspect = Mathf.Max(0.1f, fixedGameplayAspect);
        float availableAspect = availableRect.width / availableHeight;

        if (availableAspect <= targetAspect)
        {
            float viewportHeight = availableRect.width / targetAspect;
            return new Rect(normalizedRect.x, normalizedRect.y,
                normalizedRect.width, viewportHeight / screenHeight);
        }

        float viewportWidth = availableHeight * targetAspect;
        float horizontalPadding = (availableRect.width - viewportWidth) * 0.5f;
        return new Rect(
            (availableRect.x + horizontalPadding) / screenWidth,
            normalizedRect.y,
            viewportWidth / screenWidth,
            normalizedRect.height);
    }

    private bool ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cameraConstantWidth == null)
            cameraConstantWidth = GetComponent<CameraConstantWidth>();

        if (targetCamera == null)
            return false;

        if (!restrictRenderingToGameplayViewport || topHudBoundary != null || bottomHudBoundary != null)
            return true;

        if (!warnedMissingHudBoundary)
        {
            warnedMissingHudBoundary = true;
            Debug.LogWarning(
                $"{nameof(BattleCameraViewport)} on '{name}' requires a Top or Bottom HUD Boundary reference.",
                this);
        }

        return false;
    }

    private Rect GetAvailableScreenRect() => GetAvailableScreenRect(Screen.width, Screen.height, Screen.safeArea);

    private Rect GetAvailableScreenRect(int screenWidth, int screenHeight, Rect screenSafeArea)
    {
        Rect available = respectSafeArea
            ? screenSafeArea
            : new Rect(0f, 0f, screenWidth, screenHeight);
        // Device Simulator can briefly expose the previous device's safe area
        // while the Game View resolution has already changed.
        available = Rect.MinMaxRect(
            Mathf.Clamp(available.xMin, 0f, screenWidth),
            Mathf.Clamp(available.yMin, 0f, screenHeight),
            Mathf.Clamp(available.xMax, 0f, screenWidth),
            Mathf.Clamp(available.yMax, 0f, screenHeight));
        if (!restrictRenderingToGameplayViewport)
            return available;

        float bottom = available.yMin;
        float top = available.yMax;
        if (topHudBoundary != null && topHudBoundary.gameObject.activeInHierarchy)
            top = Mathf.Min(top, GetHudScreenEdge(topHudBoundary, false));
        if (bottomHudBoundary != null && bottomHudBoundary.gameObject.activeInHierarchy)
            bottom = Mathf.Max(bottom, GetHudScreenEdge(bottomHudBoundary, true));
        bottom = Mathf.Clamp(bottom, available.yMin, available.yMax);
        top = Mathf.Clamp(top, bottom, available.yMax);
        return new Rect(available.x, bottom, available.width, top - bottom);
    }

    private float GetHudScreenEdge(RectTransform boundary, bool upperEdge)
    {
        boundary.GetWorldCorners(hudCorners);
        Canvas canvas = boundary.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        float edge = upperEdge ? float.MinValue : float.MaxValue;
        for (int i = 0; i < hudCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                hudCorners[i]);
            edge = upperEdge ? Mathf.Max(edge, screenPoint.y) : Mathf.Min(edge, screenPoint.y);
        }

        return edge;
    }

    private bool NeedsViewportUpdate(Rect availableRect)
    {
        return !hasAppliedViewport
            || Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight
            || availableRect != lastAvailableRect
            || restrictRenderingToGameplayViewport != lastRestrictedViewport
            || !Mathf.Approximately(
                fixedGameplayAspect,
                lastFixedGameplayAspect)
            || useFixedGameplayAspect != lastUsedFixedGameplayAspect
            || fillAvailableGameplayArea != lastFilledAvailableGameplayArea
            || !Mathf.Approximately(
                lensOrthographicSize,
                lastLensOrthographicSize);
    }

    private void OnValidate() => hasAppliedViewport = false;
}
