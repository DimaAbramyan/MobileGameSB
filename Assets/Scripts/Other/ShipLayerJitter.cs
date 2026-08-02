using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ShipVisualJitter : MonoBehaviour
{
    [SerializeField]
    private List<JitterLayer> layers = new();

    [SerializeField]
    private float frequency = 8f;

    [Range(0f, 1f)]
    [SerializeField]
    private float intensity = 1f;

    [SerializeField]
    private bool animationEnabled = true;

    [SerializeField]
    private bool previewInEditor = true;

    public bool AnimationEnabled => animationEnabled;

    public bool PreviewInEditor => previewInEditor;

    private void OnEnable()
    {
        CaptureInitialPositions(false);
    }

    private void OnValidate()
    {
        frequency = Mathf.Max(0f, frequency);
        CaptureInitialPositions(false);

        if (!animationEnabled)
            RestoreInitialPositions();

#if UNITY_EDITOR
        if (!Application.isPlaying && !previewInEditor)
            RestoreInitialPositions();
#endif
    }

    public void CaptureInitialPositions(bool overwriteExisting)
    {
        foreach (var layer in layers)
        {
            if (layer.transform == null)
                continue;

            bool transformChanged = layer.capturedTransform != layer.transform;

            if (!overwriteExisting && layer.hasInitialPosition && !transformChanged)
                continue;

            layer.initialPosition = layer.transform.localPosition;
            layer.hasInitialPosition = true;
            layer.capturedTransform = layer.transform;
        }
    }

    public void RestoreInitialPositions()
    {
        CaptureInitialPositions(false);

        foreach (var layer in layers)
        {
            if (layer.transform == null || !layer.hasInitialPosition)
                continue;

            layer.transform.localPosition = layer.initialPosition;
        }
    }

    public void SetEditorPreview(bool enabled)
    {
        previewInEditor = enabled;

        if (enabled)
            CaptureInitialPositions(false);
        else
            RestoreInitialPositions();
    }

    public void StartJitter()
    {
        animationEnabled = true;
        CaptureInitialPositions(false);
    }

    public void StopJitter()
    {
        animationEnabled = false;
        RestoreInitialPositions();
    }

    public void RandomizeSeeds()
    {
        foreach (var layer in layers)
        {
            layer.seed = Random.insideUnitCircle * 1000f;
        }
    }

    public void CollectDirectChildren(bool includeInactive = true, bool overwriteExisting = false)
    {
        if (overwriteExisting)
            layers.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (!includeInactive && !child.gameObject.activeInHierarchy)
                continue;

            if (ContainsLayer(child))
                continue;

            layers.Add(new JitterLayer
            {
                transform = child,
                seed = Random.insideUnitCircle * 1000f,
                initialPosition = child.localPosition,
                hasInitialPosition = true,
                capturedTransform = child
            });
        }
    }

    private bool ContainsLayer(Transform target)
    {
        foreach (var layer in layers)
        {
            if (layer.transform == target)
                return true;
        }

        return false;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !previewInEditor)
            return;
#endif

        if (!animationEnabled)
            return;

        CaptureInitialPositions(false);

        float time = GetJitterTime();

        foreach (var layer in layers)
        {
            if (layer.transform == null)
                continue;

            Vector3 offset = Vector3.zero;

            if (layer.affectX)
            {
                float x = Mathf.PerlinNoise(
                    layer.seed.x,
                    time * frequency * layer.frequencyMultiplier);

                offset.x =
                    ((x - 0.5f) * 2f) *
                    layer.maxOffsetX *
                    intensity;
            }

            if (layer.affectY)
            {
                float y = Mathf.PerlinNoise(
                    layer.seed.y,
                    time * frequency * layer.frequencyMultiplier);

                offset.y =
                    ((y - 0.5f) * 2f) *
                    layer.maxOffsetY *
                    intensity;
            }

            layer.transform.localPosition =
                layer.initialPosition + offset;
        }
    }

    private void OnDisable()
    {
        RestoreInitialPositions();
    }

    [ContextMenu("Randomize Seeds")]
    private void RandomizeSeedsContextMenu()
    {
        RandomizeSeeds();
    }

    [ContextMenu("Capture Current Positions")]
    private void CaptureCurrentPositionsContextMenu()
    {
        CaptureInitialPositions(true);
    }

    [ContextMenu("Restore Initial Positions")]
    private void RestoreInitialPositionsContextMenu()
    {
        RestoreInitialPositions();
    }

    private float GetJitterTime()
    {
        if (Application.isPlaying)
            return Time.time;

#if UNITY_EDITOR
        return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }
}
