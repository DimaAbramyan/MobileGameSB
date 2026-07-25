using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RadarChartGraphic : Graphic
{
    [Header("Preview")]
    [SerializeField] private RadarChartConfig previewConfig;
    [SerializeField] private int[] previewValues;
    [SerializeField] private RadarChartParameter[] previewParameters;

    [Header("Shape")]
    [Min(0f)]
    [SerializeField] private float padding = 8f;
    [Min(1)]
    [SerializeField] private int gridLevels = 4;
    [Min(0f)]
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private float startAngle = 90f;
    [SerializeField] private bool clockwise = true;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color outlineColor = new Color(0f, 0.9f, 1f, 1f);
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("Labels")]
    [SerializeField] private bool showLabels = true;
    [Min(0f)]
    [SerializeField] private float labelDistance = 18f;
    [SerializeField] private Vector2 labelSize = new Vector2(90f, 24f);
    [SerializeField] private float labelFontSize = 18f;
    [SerializeField] private Color labelColor = Color.white;

    [Header("Animation")]
    [Min(0f)]
    [SerializeField] private float transitionDuration = 0.25f;
    [SerializeField] private bool useUnscaledTime = true;

    private readonly List<string> parameterNames = new();
    private readonly List<float> displayedValues = new();
    private readonly List<float> startValues = new();
    private readonly List<float> targetValues = new();
    private readonly List<TMP_Text> labels = new();

    private float transitionTime;
    private bool isTransitioning;

    public IReadOnlyList<string> ParameterNames => parameterNames;

    protected override void Awake()
    {
        base.Awake();

        if (displayedValues.Count != 0)
            return;

        if (previewConfig != null)
            SetParameters(previewConfig, previewValues, false);
        else if (previewParameters != null)
            SetParameters(previewParameters, false);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        if (Application.isPlaying)
            return;

        if (previewConfig != null)
            SetParameters(previewConfig, previewValues, false);
        else if (previewParameters != null)
            SetParameters(previewParameters, false);
    }
#endif

    private void Update()
    {
        if (!isTransitioning)
            return;

        if (transitionDuration <= 0f)
        {
            CopyValues(targetValues, displayedValues);
            isTransitioning = false;
            SetVerticesDirty();
            return;
        }

        transitionTime += useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        float t = Mathf.Clamp01(transitionTime / transitionDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        EnsureSize(displayedValues, targetValues.Count);
        for (int i = 0; i < targetValues.Count; i++)
        {
            float startValue = i < startValues.Count ? startValues[i] : 0f;
            displayedValues[i] = Mathf.Lerp(startValue, targetValues[i], smoothT);
        }

        if (t >= 1f)
        {
            CopyValues(targetValues, displayedValues);
            isTransitioning = false;
        }

        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateLabels();
    }

    public void SetParameters(
        IReadOnlyList<RadarChartParameter> parameters,
        bool animate = true)
    {
        parameterNames.Clear();
        targetValues.Clear();

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                RadarChartParameter parameter = parameters[i];
                if (parameter == null)
                    continue;

                parameterNames.Add(parameter.Name);
                targetValues.Add(parameter.NormalizedValue);
            }
        }

        StartTransitionToTargetValues(animate);
        UpdateLabels();
    }

    public void SetParameters(
        RadarChartConfig config,
        IReadOnlyList<int> values,
        bool animate = true)
    {
        parameterNames.Clear();
        targetValues.Clear();

        if (config != null)
        {
            for (int i = 0; i < config.ParameterNames.Count; i++)
            {
                parameterNames.Add(config.ParameterNames[i]);

                int value = values != null && i < values.Count
                    ? values[i]
                    : 0;
                targetValues.Add(NormalizeValue(value, config.MaxValue));
            }
        }

        StartTransitionToTargetValues(animate);
        UpdateLabels();
    }

    public void SetColors(
        Color fill,
        Color outline,
        Color grid,
        Color axis)
    {
        fillColor = fill;
        outlineColor = outline;
        gridColor = grid;
        axisColor = axis;
        SetVerticesDirty();
    }

    public void SetLabelColor(Color color)
    {
        labelColor = color;
        UpdateLabels();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        int axisCount = displayedValues.Count;
        if (axisCount < 3)
            return;

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Max(
            0f,
            Mathf.Min(rect.width, rect.height) * 0.5f - padding);

        if (radius <= 0f)
            return;

        DrawGrid(vh, center, radius, axisCount);
        DrawAxes(vh, center, radius, axisCount);
        DrawFilledValue(vh, center, radius, axisCount);
        DrawOutline(vh, center, radius, axisCount);
    }

    private void DrawGrid(
        VertexHelper vh,
        Vector2 center,
        float radius,
        int axisCount)
    {
        for (int level = 1; level <= gridLevels; level++)
        {
            float levelRadius = radius * level / gridLevels;
            for (int i = 0; i < axisCount; i++)
            {
                Vector2 from = GetPoint(center, levelRadius, i, axisCount);
                Vector2 to = GetPoint(
                    center,
                    levelRadius,
                    (i + 1) % axisCount,
                    axisCount);

                AddLine(vh, from, to, lineThickness, gridColor);
            }
        }
    }

    private void DrawAxes(
        VertexHelper vh,
        Vector2 center,
        float radius,
        int axisCount)
    {
        for (int i = 0; i < axisCount; i++)
            AddLine(
                vh,
                center,
                GetPoint(center, radius, i, axisCount),
                lineThickness,
                axisColor);
    }

    private void DrawFilledValue(
        VertexHelper vh,
        Vector2 center,
        float radius,
        int axisCount)
    {
        int centerIndex = vh.currentVertCount;
        AddVertex(vh, center, fillColor);

        for (int i = 0; i < axisCount; i++)
        {
            float valueRadius = radius * displayedValues[i];
            AddVertex(
                vh,
                GetPoint(center, valueRadius, i, axisCount),
                fillColor);
        }

        for (int i = 0; i < axisCount; i++)
        {
            int from = centerIndex + 1 + i;
            int to = centerIndex + 1 + ((i + 1) % axisCount);
            vh.AddTriangle(centerIndex, from, to);
        }
    }

    private void DrawOutline(
        VertexHelper vh,
        Vector2 center,
        float radius,
        int axisCount)
    {
        for (int i = 0; i < axisCount; i++)
        {
            Vector2 from = GetPoint(
                center,
                radius * displayedValues[i],
                i,
                axisCount);
            Vector2 to = GetPoint(
                center,
                radius * displayedValues[(i + 1) % axisCount],
                (i + 1) % axisCount,
                axisCount);

            AddLine(vh, from, to, lineThickness, outlineColor);
        }
    }

    private Vector2 GetPoint(
        Vector2 center,
        float radius,
        int index,
        int axisCount)
    {
        float direction = clockwise ? -1f : 1f;
        float angle = startAngle + direction * 360f * index / axisCount;
        float radians = angle * Mathf.Deg2Rad;

        return center + new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)) * radius;
    }

    private void UpdateLabels()
    {
        EnsureLabelsCount(showLabels ? parameterNames.Count : 0);

        if (!showLabels || parameterNames.Count == 0)
            return;

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Max(
            0f,
            Mathf.Min(rect.width, rect.height) * 0.5f - padding);
        float labelRadius = radius + labelDistance;

        for (int i = 0; i < parameterNames.Count; i++)
        {
            TMP_Text label = labels[i];
            label.text = string.IsNullOrWhiteSpace(parameterNames[i])
                ? $"Parameter {i + 1}"
                : parameterNames[i];
            label.color = labelColor;
            label.fontSize = labelFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            RectTransform labelTransform = label.rectTransform;
            labelTransform.sizeDelta = labelSize;
            labelTransform.anchoredPosition =
                GetPoint(center, labelRadius, i, parameterNames.Count);
        }
    }

    private void EnsureLabelsCount(int count)
    {
        while (labels.Count < count)
        {
            var labelObject = new GameObject(
                $"Radar Label {labels.Count + 1}",
                typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            labels.Add(label);
        }

        for (int i = 0; i < labels.Count; i++)
            labels[i].gameObject.SetActive(i < count);
    }

    private static void AddLine(
        VertexHelper vh,
        Vector2 from,
        Vector2 to,
        float thickness,
        Color color)
    {
        if (thickness <= 0f)
            return;

        Vector2 direction = to - from;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        Vector2 offset = normal * thickness * 0.5f;

        int startIndex = vh.currentVertCount;
        AddVertex(vh, from - offset, color);
        AddVertex(vh, from + offset, color);
        AddVertex(vh, to + offset, color);
        AddVertex(vh, to - offset, color);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void AddVertex(
        VertexHelper vh,
        Vector2 position,
        Color color)
    {
        vh.AddVert(position, color, Vector2.zero);
    }

    private static void CopyValues(
        IReadOnlyList<float> source,
        List<float> destination)
    {
        destination.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            destination.Add(source[i]);
    }

    private void StartTransitionToTargetValues(bool animate)
    {
        if (!animate || displayedValues.Count == 0)
        {
            CopyValues(targetValues, displayedValues);
            isTransitioning = false;
            SetVerticesDirty();
            return;
        }

        CopyValues(displayedValues, startValues);
        EnsureSize(startValues, targetValues.Count);
        EnsureSize(displayedValues, targetValues.Count);

        transitionTime = 0f;
        isTransitioning = true;
        SetVerticesDirty();
    }

    private static float NormalizeValue(int value, int maxValue)
    {
        if (maxValue <= 0)
            return 0f;

        return Mathf.Clamp01((float)value / maxValue);
    }

    private static void EnsureSize(List<float> values, int size)
    {
        while (values.Count < size)
            values.Add(0f);

        while (values.Count > size)
            values.RemoveAt(values.Count - 1);
    }
}
