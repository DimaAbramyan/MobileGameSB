using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AbilityChargeRingGraphic : MaskableGraphic
{
    [Header("State")]
    [SerializeField, Min(1)] private int segmentCount = 3;
    [SerializeField, Min(0f)] private float filledSegments = 3f;

    [Header("Arc")]
    [SerializeField] private float startAngle;
    [SerializeField] private float endAngle = 360f;
    [SerializeField, Min(1f)] private float thickness = 12f;
    [SerializeField, Min(0f)] private float segmentGapDegrees = 3f;
    [SerializeField, Min(0f)] private float outerPadding;

    [Header("Shape Quality")]
    [SerializeField, Range(1f, 30f)] private float maxDegreesPerQuad = 6f;

    [Header("Colors")]
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private bool drawEmptySegments = true;

    [Header("Editor Preview")]
    [SerializeField, Min(1)] private int previewSegmentCount = 3;
    [SerializeField, Min(0f)] private float previewFilledSegments = 3f;

    public int SegmentCount => segmentCount;
    public float FilledSegments => filledSegments;
    public int PreviewSegmentCount => previewSegmentCount;
    public float PreviewFilledSegments => previewFilledSegments;

    public void SetChargeState(int maxCharges, float currentFilledSegments)
    {
        int newSegmentCount = Mathf.Max(1, maxCharges);
        float newFilledSegments = Mathf.Clamp(currentFilledSegments, 0f, newSegmentCount);

        if (segmentCount == newSegmentCount
            && Mathf.Approximately(filledSegments, newFilledSegments))
        {
            return;
        }

        segmentCount = newSegmentCount;
        filledSegments = newFilledSegments;
        SetVerticesDirty();
    }

    public void ApplyPreviewState()
    {
        SetChargeState(previewSegmentCount, previewFilledSegments);
    }

    public void SetPreviewState(int maxCharges, float currentFilledSegments)
    {
        previewSegmentCount = Mathf.Max(1, maxCharges);
        previewFilledSegments = Mathf.Clamp(currentFilledSegments, 0f, previewSegmentCount);
        ApplyPreviewState();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (segmentCount <= 0 || thickness <= 0f)
            return;

        Rect rect = rectTransform.rect;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f - outerPadding;
        if (outerRadius <= 0f)
            return;

        float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        float totalArc = GetCounterClockwiseArcLength(startAngle, endAngle);
        float segmentArc = totalArc / segmentCount;
        float gap = Mathf.Min(segmentGapDegrees, segmentArc * 0.8f);

        for (int i = 0; i < segmentCount; i++)
            DrawSegment(vh, i, segmentArc, gap, innerRadius, outerRadius);
    }

    private void DrawSegment(
        VertexHelper vh,
        int index,
        float segmentArc,
        float gap,
        float innerRadius,
        float outerRadius)
    {
        float segmentStart = startAngle + index * segmentArc + gap * 0.5f;
        float segmentEnd = startAngle + (index + 1) * segmentArc - gap * 0.5f;

        if (segmentEnd <= segmentStart)
            return;

        if (drawEmptySegments)
            AddRingArc(vh, segmentStart, segmentEnd, innerRadius, outerRadius, emptyColor);

        float fill01 = Mathf.Clamp01(filledSegments - index);
        if (fill01 <= 0f)
            return;

        float filledEnd = Mathf.Lerp(segmentStart, segmentEnd, fill01);
        AddRingArc(vh, segmentStart, filledEnd, innerRadius, outerRadius, filledColor);
    }

    private void AddRingArc(
        VertexHelper vh,
        float fromAngle,
        float toAngle,
        float innerRadius,
        float outerRadius,
        Color segmentColor)
    {
        float arcLength = Mathf.Abs(toAngle - fromAngle);
        int steps = Mathf.Max(1, Mathf.CeilToInt(arcLength / maxDegreesPerQuad));
        Color32 color32 = segmentColor;

        for (int i = 0; i < steps; i++)
        {
            float fromT = (float)i / steps;
            float toT = (float)(i + 1) / steps;
            float angle0 = Mathf.Lerp(fromAngle, toAngle, fromT);
            float angle1 = Mathf.Lerp(fromAngle, toAngle, toT);

            Vector2 outer0 = PointOnCircle(angle0, outerRadius);
            Vector2 outer1 = PointOnCircle(angle1, outerRadius);
            Vector2 inner0 = PointOnCircle(angle0, innerRadius);
            Vector2 inner1 = PointOnCircle(angle1, innerRadius);

            int startIndex = vh.currentVertCount;
            vh.AddVert(outer0, color32, Vector2.zero);
            vh.AddVert(outer1, color32, Vector2.zero);
            vh.AddVert(inner1, color32, Vector2.zero);
            vh.AddVert(inner0, color32, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 1);
            vh.AddTriangle(startIndex, startIndex + 3, startIndex + 2);
        }
    }

    private Vector2 PointOnCircle(float counterClockwiseAngleFromTop, float radius)
    {
        float radians = counterClockwiseAngleFromTop * Mathf.Deg2Rad;
        return new Vector2(
            -Mathf.Sin(radians) * radius,
            Mathf.Cos(radians) * radius);
    }

    private float GetCounterClockwiseArcLength(float fromAngle, float toAngle)
    {
        float arc = toAngle - fromAngle;

        while (arc < 0f)
            arc += 360f;

        while (arc > 360f)
            arc -= 360f;

        return Mathf.Approximately(arc, 0f) ? 360f : arc;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        segmentCount = Mathf.Max(1, segmentCount);
        filledSegments = Mathf.Clamp(filledSegments, 0f, segmentCount);
        previewSegmentCount = Mathf.Max(1, previewSegmentCount);
        previewFilledSegments = Mathf.Clamp(previewFilledSegments, 0f, previewSegmentCount);
        thickness = Mathf.Max(1f, thickness);
        segmentGapDegrees = Mathf.Max(0f, segmentGapDegrees);
        outerPadding = Mathf.Max(0f, outerPadding);
        maxDegreesPerQuad = Mathf.Clamp(maxDegreesPerQuad, 1f, 30f);

        SetVerticesDirty();
    }
#endif
}
