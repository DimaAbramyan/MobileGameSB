using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class ChainLightningVisual : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.08f;
    [SerializeField] private LineRenderer lineRenderer;

    private Vector3[] positions = System.Array.Empty<Vector3>();
    private float hideTime;
    private bool isVisible;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = 0.08f;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
    }

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (isVisible && Time.time >= hideTime)
            Clear();
    }

    public void Play(Vector3 sourcePosition, IReadOnlyList<Vector3> targets)
    {
        if (lineRenderer == null || targets == null || targets.Count == 0)
            return;

        int positionCount = targets.Count + 1;
        EnsurePositionCapacity(positionCount);

        positions[0] = sourcePosition;
        for (int index = 0; index < targets.Count; index++)
            positions[index + 1] = targets[index];

        lineRenderer.positionCount = positionCount;
        for (int index = 0; index < positionCount; index++)
            lineRenderer.SetPosition(index, positions[index]);

        lineRenderer.enabled = true;
        isVisible = true;
        hideTime = Time.time + duration;
    }

    public void Prepare(int maxTargetCount)
    {
        EnsurePositionCapacity(Mathf.Max(1, maxTargetCount) + 1);
    }

    public void Clear()
    {
        isVisible = false;

        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    private void OnDisable()
    {
        Clear();
    }

    private void EnsurePositionCapacity(int positionCount)
    {
        if (positions.Length >= positionCount)
            return;

        positions = new Vector3[positionCount];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
    }
#endif
}
