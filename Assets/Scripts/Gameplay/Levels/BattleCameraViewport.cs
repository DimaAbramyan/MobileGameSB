using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class BattleCameraViewport : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform topHudBoundary;
    [SerializeField] private CameraConstantWidth cameraConstantWidth;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [Tooltip("Renders the battle only below the upper ship HUD. "
        + "Use a separate clear camera to fill the non-gameplay area.")]
    [SerializeField] private bool restrictRenderingToGameplayViewport = true;
    [Header("Fixed Gameplay Format")]
    [SerializeField] private bool useFixedGameplayAspect = true;
    [SerializeField, Min(0.1f)] private float fixedGameplayAspect = 9f / 17f;
    [Header("Cinemachine Lens")]
    [SerializeField, Min(0.01f)] private float lensOrthographicSize = 5.3f;

    private readonly Vector3[] hudCorners = new Vector3[4];
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastHudBottom;
    private float lastFixedGameplayAspect;
    private bool lastUsedFixedGameplayAspect;
    private float lastLensOrthographicSize;
    private bool hasAppliedViewport;
    private bool warnedMissingHudBoundary;

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
    }

    private void LateUpdate()
    {
        if (!ResolveReferences())
            return;

        float hudBottom = GetHudBottomScreenY();
        if (!NeedsViewportUpdate(hudBottom))
            return;

        Apply(hudBottom);
    }

    [ContextMenu("Apply Gameplay Viewport")]
    public void Apply()
    {
        if (!ResolveReferences())
            return;

        Apply(GetHudBottomScreenY());
    }

    private void Apply(float hudBottom)
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        if (screenWidth <= 0f || screenHeight <= 0f)
            return;

        float availableHeight = restrictRenderingToGameplayViewport
            ? Mathf.Clamp(hudBottom, 0f, screenHeight)
            : screenHeight;
        Rect gameplayRect = GetGameplayRect(
            screenWidth,
            screenHeight,
            availableHeight);
        targetCamera.rect = gameplayRect;
        ApplyCinemachineLens();

        if (cinemachineCamera == null)
            cameraConstantWidth?.Apply();

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastHudBottom = hudBottom;
        lastFixedGameplayAspect = fixedGameplayAspect;
        lastUsedFixedGameplayAspect = useFixedGameplayAspect;
        lastLensOrthographicSize = lensOrthographicSize;
        hasAppliedViewport = true;
    }

    private void ApplyCinemachineLens()
    {
        if (cinemachineCamera == null)
            return;

        LensSettings lens = cinemachineCamera.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = Mathf.Max(0.01f, lensOrthographicSize);
        cinemachineCamera.Lens = lens;
    }

    private Rect GetGameplayRect(
        float screenWidth,
        float screenHeight,
        float availableHeight)
    {
        if (availableHeight <= 0f)
            return Rect.zero;

        float normalizedAvailableHeight = availableHeight / screenHeight;
        if (!useFixedGameplayAspect)
            return new Rect(0f, 0f, 1f, normalizedAvailableHeight);

        float targetAspect = Mathf.Max(0.1f, fixedGameplayAspect);
        float availableAspect = screenWidth / availableHeight;

        if (availableAspect <= targetAspect)
        {
            float viewportHeight = screenWidth / targetAspect;
            return new Rect(0f, 0f, 1f, viewportHeight / screenHeight);
        }

        float viewportWidth = availableHeight * targetAspect;
        float horizontalPadding = (screenWidth - viewportWidth) * 0.5f;
        return new Rect(
            horizontalPadding / screenWidth,
            0f,
            viewportWidth / screenWidth,
            normalizedAvailableHeight);
    }

    private bool ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cameraConstantWidth == null)
            cameraConstantWidth = GetComponent<CameraConstantWidth>();

        if (targetCamera == null)
            return false;

        if (topHudBoundary != null)
            return true;

        if (!warnedMissingHudBoundary)
        {
            warnedMissingHudBoundary = true;
            Debug.LogWarning(
                $"{nameof(BattleCameraViewport)} on '{name}' requires a Top HUD Boundary reference.",
                this);
        }

        return false;
    }

    private float GetHudBottomScreenY()
    {
        topHudBoundary.GetWorldCorners(hudCorners);

        float bottom = float.MaxValue;
        for (int i = 0; i < hudCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                hudCorners[i]);
            bottom = Mathf.Min(bottom, screenPoint.y);
        }

        return bottom;
    }

    private bool NeedsViewportUpdate(float hudBottom)
    {
        return !hasAppliedViewport
            || Screen.width != lastScreenWidth
            || Screen.height != lastScreenHeight
            || !Mathf.Approximately(hudBottom, lastHudBottom)
            || !Mathf.Approximately(
                fixedGameplayAspect,
                lastFixedGameplayAspect)
            || useFixedGameplayAspect != lastUsedFixedGameplayAspect
            || !Mathf.Approximately(
                lensOrthographicSize,
                lastLensOrthographicSize);
    }
}
