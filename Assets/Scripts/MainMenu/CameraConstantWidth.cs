using UnityEngine;

/// <summary>
/// Keeps an orthographic camera's visible world width constant across devices.
/// Can be placed either on the Camera itself or on a scene object that should
/// apply the setting to Camera.main during gameplay.
/// </summary>
public sealed class CameraConstantWidth : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.1f)] private float targetWorldWidth = 5.6f;
    [SerializeField] private bool useMainCameraIfEmpty = true;

    [Header("Update")]
    [SerializeField] private bool updateWhenResolutionChanges = true;

    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private bool warnedMissingCamera;

    public float TargetWorldWidth
    {
        get => targetWorldWidth;
        set
        {
            targetWorldWidth = Mathf.Max(0.1f, value);
            Apply();
        }
    }

    private void Awake()
    {
        ResolveCamera();
        Apply();
    }

    private void OnEnable()
    {
        ResolveCamera();
        Apply();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            ResolveCamera();

        if (targetCamera == null)
            return;

        if (!updateWhenResolutionChanges && lastScreenWidth > 0 && lastScreenHeight > 0)
            return;

        if (Screen.width == lastScreenWidth
            && Screen.height == lastScreenHeight
            && Mathf.Approximately(lastCameraAspect, targetCamera.aspect))
        {
            return;
        }

        Apply();
    }

    private void OnValidate()
    {
        targetWorldWidth = Mathf.Max(0.1f, targetWorldWidth);

        if (!Application.isPlaying)
            targetCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();
    }

    public void Apply()
    {
        if (targetCamera == null)
            ResolveCamera();

        if (targetCamera == null)
        {
            WarnMissingCamera();
            return;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogWarning(
                $"{nameof(CameraConstantWidth)} on {name} expects an orthographic camera. "
                + $"Camera '{targetCamera.name}' is perspective, so fixed width was not applied.",
                this);
            return;
        }

        float aspect = Mathf.Max(0.01f, targetCamera.aspect);
        targetCamera.orthographicSize = targetWorldWidth / (2f * aspect);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraAspect = targetCamera.aspect;
    }

    private void ResolveCamera()
    {
        if (targetCamera != null)
            return;

        targetCamera = GetComponent<Camera>();

        if (targetCamera == null && useMainCameraIfEmpty)
            targetCamera = Camera.main;
    }

    private void WarnMissingCamera()
    {
        if (warnedMissingCamera)
            return;

        warnedMissingCamera = true;
        Debug.LogWarning(
            $"{nameof(CameraConstantWidth)} on {name} could not find a camera. "
            + "Assign Target Camera or make sure a camera is tagged MainCamera.",
            this);
    }
}
