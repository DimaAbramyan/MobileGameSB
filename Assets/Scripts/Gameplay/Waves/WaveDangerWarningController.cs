using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum WaveDangerWarningShapeType
{
    Circle,
    Ellipse,
    Parabola,
    Rectangle
}

[System.Serializable]
public sealed class WaveDangerWarningShape
{
    [SerializeField] private WaveDangerWarningShapeType shapeType =
        WaveDangerWarningShapeType.Circle;
    [SerializeField] private Vector2 center;
    [SerializeField] private float rotationDegrees;
    [SerializeField] private Vector2 size = new(2f, 2f);
    [SerializeField, Range(8, 128)] private int segments = 32;
    [SerializeField, Min(0.01f), Tooltip(
        "World-space thickness used by open warning curves.")]
    private float lineThickness = 0.2f;
    [SerializeField, Min(0f), Tooltip(
        "0 creates a straight line. 1 uses the full configured height; values above 1 intensify the bend.")]
    private float parabolaCurvature = 1f;
    [SerializeField, Min(0.01f), Tooltip(
        "Total horizontal span of the parabola in world units.")]
    private float parabolaLength = 2f;
    [SerializeField, Min(0.01f), Tooltip(
        "Range of the parabola's X variable. Values below 1 trim its branches; values above 1 extend them.")]
    private float parabolaXRange = 1f;
    [SerializeField, Min(0.01f), Tooltip(
        "Exponent applied to X. 2 is the standard parabola; higher values make its branches steeper.")]
    private float parabolaPower = 2f;
    [SerializeField, Min(0.01f), Tooltip(
        "Multiplier for the length of each rectangular parabola segment. Values above 1 overlap segments and hide gaps.")]
    private float parabolaSegmentLengthScale = 1f;
    [SerializeField] private bool inverted;
    [SerializeField] private Color color =
        new(0.72506726f, 0f, 0f, 0.42f);

    public WaveDangerWarningShapeType ShapeType => shapeType;
    public Vector2 Center => center;
    public float RotationDegrees => rotationDegrees;
    public Vector2 Size => new Vector2(
        Mathf.Max(0.01f, size.x),
        Mathf.Max(0.01f, size.y));
    public int Segments => Mathf.Clamp(segments, 8, 128);
    public float LineThickness => Mathf.Max(0.01f, lineThickness);
    public float ParabolaCurvature => Mathf.Max(0f, parabolaCurvature);
    public float ParabolaLength => Mathf.Max(0.01f, parabolaLength);
    public float ParabolaXRange => Mathf.Max(0.01f, parabolaXRange);
    public float ParabolaPower => Mathf.Max(0.01f, parabolaPower);
    public float ParabolaSegmentLengthScale => Mathf.Max(0.01f, parabolaSegmentLengthScale);
    public bool IsOpenPath => shapeType == WaveDangerWarningShapeType.Parabola;
    public bool Inverted => inverted;
    public Color Color => color;

    public void Validate()
    {
        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        segments = Mathf.Clamp(segments, 8, 128);
        lineThickness = Mathf.Max(0.01f, lineThickness);
        parabolaCurvature = Mathf.Max(0f, parabolaCurvature);
        parabolaLength = Mathf.Max(0.01f, parabolaLength);
        parabolaXRange = Mathf.Max(0.01f, parabolaXRange);
        parabolaPower = Mathf.Max(0.01f, parabolaPower);
        parabolaSegmentLengthScale = Mathf.Max(0.01f, parabolaSegmentLengthScale);

        if (IsOpenPath)
            inverted = false;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Wave))]
public sealed class WaveDangerWarningController : MonoBehaviour
{
    private const string WarningShaderName = "Game/Wave Danger Warning";

    [Header("Visual Preset")]
    [SerializeField] private WaveDangerWarningVisualPreset visualPreset;

    [Header("Flash Timing")]
    [SerializeField, Min(1)] private int flashCount = 3;
    [SerializeField, Min(0.01f)] private float visibleDuration = 0.16f;
    [SerializeField, Min(0f)] private float hiddenInterval = 0.1f;

    [Header("Alpha Transition")]
    [SerializeField, InspectorName("Use Alpha Transition")]
    private bool useAlphaTransition;
    [SerializeField, Min(0f), InspectorName("Alpha Fade Duration")]
    private float alphaFadeDuration = 0.08f;
    [SerializeField, InspectorName("Alpha Fade Curve")]
    private AnimationCurve alphaFadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Color Override")]
    [SerializeField] private bool useGlobalColor = true;
    [SerializeField] private Color globalColor =
        new(0.72506726f, 0f, 0f, 0.42f);

    [Header("Playable Area")]
    [Tooltip("Local center of the area that may become dangerous when a shape is inverted.")]
    [SerializeField] private Vector2 playfieldCenter;
    [Tooltip("Local world-space size of the playable area. Inverted shapes cover this area outside the shape.")]
    [SerializeField] private Vector2 playfieldSize = new(5.625f, 10f);

    [Header("Rendering")]
    [Tooltip("Optional material template. The warning shader is applied automatically to prevent alpha stacking in intersections.")]
    [SerializeField] private Material warningMaterial;
    [SerializeField, Range(1, 255)] private int stencilReference = 177;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private float zOffset = -0.1f;

    [Header("Danger Shapes")]
    [SerializeField] private List<WaveDangerWarningShape> warningShapes = new();

    private readonly List<WarningVisual> activeVisuals = new();
    private readonly List<Vector3> gizmoPoints = new(132);
    private MaterialPropertyBlock materialPropertyBlock;
    private Material runtimeMaterial;

    private sealed class WarningVisual
    {
        public WarningVisual(
            GameObject gameObject,
            MeshRenderer renderer,
            Color baseColor)
        {
            GameObject = gameObject;
            Renderer = renderer;
            BaseColor = baseColor;
        }

        public GameObject GameObject { get; }
        public MeshRenderer Renderer { get; }
        public Color BaseColor { get; }
    }

    private void Awake()
    {
        materialPropertyBlock = new MaterialPropertyBlock();
    }

    public bool HasConfiguredWarnings
    {
        get
        {
            if (warningShapes == null)
                return false;

            for (int i = 0; i < warningShapes.Count; i++)
            {
                if (warningShapes[i] != null)
                    return true;
            }

            return false;
        }
    }

    public float WarningDuration
    {
        get
        {
            if (!HasConfiguredWarnings)
                return 0f;

            int safeFlashCount = ResolvedFlashCount;
            return safeFlashCount * ResolvedVisibleDuration
                + Mathf.Max(0, safeFlashCount - 1)
                * ResolvedHiddenInterval;
        }
    }

    public bool ShouldPlayWarning => enabled
        && (!Application.isPlaying || gameObject.activeInHierarchy)
        && HasConfiguredWarnings;

    public int ShapeCount => warningShapes != null ? warningShapes.Count : 0;
    public bool UsesVisualPreset => visualPreset != null;
    public Vector2 PlayfieldCenter => playfieldCenter;
    public Vector2 PlayfieldSize => new Vector2(
        Mathf.Max(0.01f, playfieldSize.x),
        Mathf.Max(0.01f, playfieldSize.y));

    public WaveDangerWarningShape GetShape(int index)
    {
        if (warningShapes == null
            || index < 0
            || index >= warningShapes.Count)
        {
            return null;
        }

        return warningShapes[index];
    }

    public Color GetShapeColor(int shapeIndex)
    {
        WaveDangerWarningShape shape = GetShape(shapeIndex);
        return shape == null ? Color.clear : GetResolvedShapeColor(shape.Color);
    }

    public void CopyResolvedVisualSettingsTo(
        WaveDangerWarningVisualPreset preset)
    {
        if (preset == null)
            return;

        preset.SetSettings(
            ResolvedFlashCount,
            ResolvedVisibleDuration,
            ResolvedHiddenInterval,
            ResolvedUseAlphaTransition,
            ResolvedAlphaFadeDuration,
            ResolvedAlphaFadeCurve);
    }

    public void UseVisualPresetAsLocalSettings()
    {
        if (visualPreset == null)
            return;

        flashCount = visualPreset.FlashCount;
        visibleDuration = visualPreset.VisibleDuration;
        hiddenInterval = visualPreset.HiddenInterval;
        useAlphaTransition = visualPreset.UseAlphaTransition;
        alphaFadeDuration = visualPreset.AlphaFadeDuration;
        alphaFadeCurve = CopyCurve(visualPreset.AlphaFadeCurve);
        visualPreset = null;
    }

    public bool IsWarningVisibleAt(float elapsed)
    {
        if (!ShouldPlayWarning || elapsed < 0f || elapsed >= WarningDuration)
            return false;

        float safeVisibleDuration = ResolvedVisibleDuration;
        float safeHiddenInterval = ResolvedHiddenInterval;
        float flashCycleDuration = safeVisibleDuration + safeHiddenInterval;
        int completedFlashes = Mathf.FloorToInt(elapsed / flashCycleDuration);
        if (completedFlashes >= ResolvedFlashCount)
            return false;

        float flashElapsed = elapsed - completedFlashes * flashCycleDuration;
        return flashElapsed < safeVisibleDuration;
    }

    public float GetWarningAlphaAt(float elapsed)
    {
        if (!IsWarningVisibleAt(elapsed))
            return 0f;

        float safeVisibleDuration = ResolvedVisibleDuration;
        float flashCycleDuration = safeVisibleDuration
            + ResolvedHiddenInterval;
        float flashElapsed = elapsed
            - Mathf.FloorToInt(elapsed / flashCycleDuration)
            * flashCycleDuration;
        return GetFlashAlpha(flashElapsed, safeVisibleDuration);
    }

    public IEnumerator PlayWarning()
    {
        if (!ShouldPlayWarning)
            yield break;

        CreateWarningVisuals();

        int safeFlashCount = ResolvedFlashCount;
        float safeVisibleDuration = ResolvedVisibleDuration;
        float safeHiddenInterval = ResolvedHiddenInterval;
        for (int i = 0; i < safeFlashCount; i++)
        {
            SetVisualsVisible(true);
            yield return PlayVisibleFlash(safeVisibleDuration);

            SetVisualsVisible(false);
            if (i < safeFlashCount - 1 && safeHiddenInterval > 0f)
                yield return new WaitForSeconds(safeHiddenInterval);
        }

        ClearWarningVisuals();
    }

    private IEnumerator PlayVisibleFlash(float duration)
    {
        if (!UsesAlphaTransition)
        {
            SetVisualsAlpha(1f);
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            SetVisualsAlpha(GetFlashAlpha(elapsed, duration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetVisualsAlpha(GetFlashAlpha(duration, duration));
    }

    public void GetShapeLocalPolygon(
        int shapeIndex,
        List<Vector3> output)
    {
        if (output == null)
            return;

        output.Clear();
        WaveDangerWarningShape shape = GetShape(shapeIndex);
        if (shape == null)
            return;

        BuildShapePolygon(shape, output);
    }

    private void CreateWarningVisuals()
    {
        ClearWarningVisuals();

        for (int i = 0; i < ShapeCount; i++)
        {
            WaveDangerWarningShape shape = GetShape(i);
            if (shape == null)
                continue;

            WarningVisual visual = CreateWarningVisual(shape, i);
            if (visual != null)
                activeVisuals.Add(visual);
        }
    }

    private WarningVisual CreateWarningVisual(
        WaveDangerWarningShape shape,
        int shapeIndex)
    {
        var points = new List<Vector3>(shape.Segments + 4);
        BuildShapePolygon(shape, points);
        if (points.Count < (shape.IsOpenPath ? 2 : 3))
            return null;

        Mesh mesh = shape.IsOpenPath
            ? BuildOpenPathMesh(points, shape)
            : shape.Inverted
                ? BuildInvertedMesh(shape, points)
                : BuildFilledMesh(points);
        if (mesh == null)
            return null;

        var visualObject = new GameObject($"Danger Warning {shapeIndex + 1}");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = new Vector3(0f, 0f, zOffset);

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visualObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = GetWarningMaterial();
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
        Color resolvedColor = GetResolvedShapeColor(shape.Color);
        ApplyColor(meshRenderer, resolvedColor);
        return new WarningVisual(visualObject, meshRenderer, resolvedColor);
    }

    private Mesh BuildFilledMesh(List<Vector3> polygon)
    {
        int vertexCount = polygon.Count + 1;
        var vertices = new Vector3[vertexCount];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            vertices[i + 1] = polygon[i];
            center += polygon[i];
        }

        vertices[0] = center / polygon.Count;

        var triangles = new int[polygon.Count * 3];
        int triangleIndex = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            int next = (i + 1) % polygon.Count;
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i + 1;
            triangles[triangleIndex++] = next + 1;
        }

        var mesh = new Mesh
        {
            name = "Wave Danger Warning Mesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildOpenPathMesh(
        List<Vector3> path,
        WaveDangerWarningShape shape)
    {
        if (path == null || path.Count < 2)
            return null;

        int segmentCount = path.Count - 1;
        var vertices = new Vector3[segmentCount * 4];
        var triangles = new int[segmentCount * 6];
        int triangleIndex = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 from = path[i];
            Vector3 to = path[i + 1];
            Vector3 tangent = to - from;
            if (tangent.sqrMagnitude <= 0.000001f)
                continue;

            Vector3 segmentCenter = (from + to) * 0.5f;
            Vector3 halfTangent = tangent * shape.ParabolaSegmentLengthScale * 0.5f;
            from = segmentCenter - halfTangent;
            to = segmentCenter + halfTangent;
            float halfThickness = shape.LineThickness * 0.5f;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f)
                .normalized * halfThickness;
            int vertexIndex = i * 4;
            vertices[vertexIndex] = from + normal;
            vertices[vertexIndex + 1] = from - normal;
            vertices[vertexIndex + 2] = to - normal;
            vertices[vertexIndex + 3] = to + normal;

            triangles[triangleIndex++] = vertexIndex;
            triangles[triangleIndex++] = vertexIndex + 1;
            triangles[triangleIndex++] = vertexIndex + 2;
            triangles[triangleIndex++] = vertexIndex;
            triangles[triangleIndex++] = vertexIndex + 2;
            triangles[triangleIndex++] = vertexIndex + 3;
        }

        var mesh = new Mesh
        {
            name = "Wave Danger Warning Path Mesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildInvertedMesh(
        WaveDangerWarningShape shape,
        List<Vector3> polygon)
    {
        int pointCount = polygon.Count;
        var vertices = new Vector3[pointCount * 2];
        Vector2 pivot = CalculatePolygonCenter(polygon);
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 point = polygon[i];
            Vector2 direction = new Vector2(point.x - pivot.x, point.y - pivot.y);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;

            vertices[i] = point;
            Vector2 outerPoint = GetOuterPlayfieldPoint(
                pivot,
                direction.normalized);
            vertices[pointCount + i] = new Vector3(
                outerPoint.x,
                outerPoint.y,
                point.z);
        }

        var triangles = new int[pointCount * 6];
        int triangleIndex = 0;
        for (int i = 0; i < pointCount; i++)
        {
            int next = (i + 1) % pointCount;
            int innerCurrent = i;
            int innerNext = next;
            int outerCurrent = pointCount + i;
            int outerNext = pointCount + next;

            triangles[triangleIndex++] = innerCurrent;
            triangles[triangleIndex++] = outerCurrent;
            triangles[triangleIndex++] = outerNext;
            triangles[triangleIndex++] = innerCurrent;
            triangles[triangleIndex++] = outerNext;
            triangles[triangleIndex++] = innerNext;
        }

        var mesh = new Mesh
        {
            name = $"Inverted {shape.ShapeType} Danger Warning Mesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector2 GetOuterPlayfieldPoint(
        Vector2 origin,
        Vector2 direction)
    {
        Vector2 halfSize = PlayfieldSize * 0.5f;
        float minX = playfieldCenter.x - halfSize.x;
        float maxX = playfieldCenter.x + halfSize.x;
        float minY = playfieldCenter.y - halfSize.y;
        float maxY = playfieldCenter.y + halfSize.y;

        float xDistance = direction.x > 0f
            ? maxX - origin.x
            : origin.x - minX;
        float yDistance = direction.y > 0f
            ? maxY - origin.y
            : origin.y - minY;
        float xScale = Mathf.Abs(direction.x) > 0.0001f
            ? xDistance / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float yScale = Mathf.Abs(direction.y) > 0.0001f
            ? yDistance / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        float scale = Mathf.Min(xScale, yScale);
        return origin + direction * Mathf.Max(0f, scale);
    }

    private static Vector2 CalculatePolygonCenter(List<Vector3> polygon)
    {
        Vector2 center = Vector2.zero;
        for (int i = 0; i < polygon.Count; i++)
            center += new Vector2(polygon[i].x, polygon[i].y);

        return center / polygon.Count;
    }

    private static void BuildShapePolygon(
        WaveDangerWarningShape shape,
        List<Vector3> output)
    {
        if (shape.ShapeType == WaveDangerWarningShapeType.Parabola)
        {
            BuildParabolaPath(shape, output);
            return;
        }

        Vector2 halfSize = shape.Size * 0.5f;
        if (shape.ShapeType == WaveDangerWarningShapeType.Rectangle)
        {
            output.Add(ToLocalPoint(shape, new Vector2(-halfSize.x, -halfSize.y)));
            output.Add(ToLocalPoint(shape, new Vector2(halfSize.x, -halfSize.y)));
            output.Add(ToLocalPoint(shape, new Vector2(halfSize.x, halfSize.y)));
            output.Add(ToLocalPoint(shape, new Vector2(-halfSize.x, halfSize.y)));
            return;
        }

        int segments = shape.Segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector2 point = shape.ShapeType == WaveDangerWarningShapeType.Circle
                ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * halfSize.x
                : new Vector2(
                    Mathf.Cos(angle) * halfSize.x,
                    Mathf.Sin(angle) * halfSize.y);
            output.Add(ToLocalPoint(shape, point));
        }
    }

    private static void BuildParabolaPath(
        WaveDangerWarningShape shape,
        List<Vector3> output)
    {
        int curveSteps = Mathf.Max(4, shape.Segments);
        Vector2 halfSize = shape.Size * 0.5f;
        float halfLength = shape.ParabolaLength * 0.5f;

        for (int i = 0; i <= curveSteps; i++)
        {
            float t = i / (float)curveSteps;
            float normalizedX = Mathf.Lerp(
                -shape.ParabolaXRange,
                shape.ParabolaXRange,
                t);
            float x = normalizedX * halfLength;
            float y = -halfSize.y
                + shape.Size.y
                * shape.ParabolaCurvature
                * (1f - Mathf.Pow(
                    Mathf.Abs(normalizedX),
                    shape.ParabolaPower));
            output.Add(ToLocalPoint(shape, new Vector2(x, y)));
        }
    }

    private static Vector3 ToLocalPoint(
        WaveDangerWarningShape shape,
        Vector2 point)
    {
        float radians = shape.RotationDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        Vector2 rotated = new Vector2(
            point.x * cosine - point.y * sine,
            point.x * sine + point.y * cosine);
        return shape.Center + rotated;
    }

    private Material GetWarningMaterial()
    {
        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find(WarningShaderName);
        if (shader == null)
        {
            Debug.LogError(
                $"{nameof(WaveDangerWarningController)} could not find "
                + $"the '{WarningShaderName}' shader.",
                this);
            return null;
        }

        runtimeMaterial = warningMaterial != null
            ? new Material(warningMaterial)
            : new Material(shader);
        runtimeMaterial.shader = shader;
        runtimeMaterial.name = "Runtime Wave Danger Warning Material";
        runtimeMaterial.SetColor("_BaseColor", Color.white);
        runtimeMaterial.SetFloat(
            "_StencilRef",
            Mathf.Clamp(stencilReference, 1, 255));
        return runtimeMaterial;
    }

    private void ApplyColor(
        MeshRenderer renderer,
        Color color,
        float alphaMultiplier = 1f)
    {
        if (renderer == null)
            return;

        color.a *= Mathf.Clamp01(alphaMultiplier);
        materialPropertyBlock ??= new MaterialPropertyBlock();
        materialPropertyBlock.Clear();
        renderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(materialPropertyBlock);
    }

    private void SetVisualsVisible(bool visible)
    {
        for (int i = 0; i < activeVisuals.Count; i++)
        {
            GameObject visualObject = activeVisuals[i].GameObject;
            if (visualObject != null)
                visualObject.SetActive(visible);
        }
    }

    private void SetVisualsAlpha(float alphaMultiplier)
    {
        for (int i = 0; i < activeVisuals.Count; i++)
        {
            WarningVisual visual = activeVisuals[i];
            ApplyColor(visual.Renderer, visual.BaseColor, alphaMultiplier);
        }
    }

    private void ClearWarningVisuals()
    {
        for (int i = 0; i < activeVisuals.Count; i++)
        {
            GameObject visualObject = activeVisuals[i].GameObject;
            if (visualObject != null)
                Destroy(visualObject);
        }

        activeVisuals.Clear();
    }

    private void OnDisable()
    {
        ClearWarningVisuals();
    }

    private void OnDestroy()
    {
        ClearWarningVisuals();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void OnValidate()
    {
        flashCount = Mathf.Max(1, flashCount);
        visibleDuration = Mathf.Max(0.01f, visibleDuration);
        hiddenInterval = Mathf.Max(0f, hiddenInterval);
        alphaFadeDuration = Mathf.Max(0f, alphaFadeDuration);
        stencilReference = Mathf.Clamp(stencilReference, 1, 255);
        playfieldSize.x = Mathf.Max(0.01f, playfieldSize.x);
        playfieldSize.y = Mathf.Max(0.01f, playfieldSize.y);

        if (alphaFadeCurve == null || alphaFadeCurve.length == 0)
            alphaFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (warningShapes == null)
            return;

        for (int i = 0; i < warningShapes.Count; i++)
            warningShapes[i]?.Validate();
    }

    private void OnDrawGizmosSelected()
    {
        if (warningShapes == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        for (int i = 0; i < warningShapes.Count; i++)
        {
            WaveDangerWarningShape shape = warningShapes[i];
            if (shape == null)
                continue;

            gizmoPoints.Clear();
            BuildShapePolygon(shape, gizmoPoints);
            if (gizmoPoints.Count < 2)
                continue;

            Color color = GetResolvedShapeColor(shape.Color);
            color.a = 0.9f;
            Gizmos.color = color;
            int lineCount = shape.IsOpenPath
                ? gizmoPoints.Count - 1
                : gizmoPoints.Count;
            for (int pointIndex = 0; pointIndex < lineCount; pointIndex++)
            {
                Vector3 from = gizmoPoints[pointIndex];
                Vector3 to = gizmoPoints[
                    (pointIndex + 1) % gizmoPoints.Count];
                Gizmos.DrawLine(from, to);
            }

            if (!shape.Inverted)
                continue;

            Vector2 halfSize = PlayfieldSize * 0.5f;
            Vector3 rectangleCenter = playfieldCenter;
            Gizmos.DrawWireCube(
                rectangleCenter,
                new Vector3(halfSize.x * 2f, halfSize.y * 2f, 0f));
        }

        Gizmos.matrix = previousMatrix;
    }

    private int ResolvedFlashCount => visualPreset != null
        ? visualPreset.FlashCount
        : Mathf.Max(1, flashCount);

    private float ResolvedVisibleDuration => visualPreset != null
        ? visualPreset.VisibleDuration
        : Mathf.Max(0.01f, visibleDuration);

    private float ResolvedHiddenInterval => visualPreset != null
        ? visualPreset.HiddenInterval
        : Mathf.Max(0f, hiddenInterval);

    private bool ResolvedUseAlphaTransition => visualPreset != null
        ? visualPreset.UseAlphaTransition
        : useAlphaTransition;

    private float ResolvedAlphaFadeDuration => visualPreset != null
        ? visualPreset.AlphaFadeDuration
        : Mathf.Max(0f, alphaFadeDuration);

    private AnimationCurve ResolvedAlphaFadeCurve => visualPreset != null
        ? visualPreset.AlphaFadeCurve
        : alphaFadeCurve;

    private bool UsesAlphaTransition => ResolvedUseAlphaTransition
        && ResolvedAlphaFadeDuration > 0.0001f;

    private float GetFlashAlpha(float elapsed, float duration)
    {
        if (!UsesAlphaTransition)
            return 1f;

        float fadeDuration = Mathf.Min(
            ResolvedAlphaFadeDuration,
            Mathf.Max(0f, duration) * 0.5f);
        if (fadeDuration <= 0.0001f)
            return 1f;

        if (elapsed < fadeDuration)
            return EvaluateAlphaCurve(elapsed / fadeDuration);

        float fadeOutStart = duration - fadeDuration;
        if (elapsed > fadeOutStart)
            return EvaluateAlphaCurve((duration - elapsed) / fadeDuration);

        return 1f;
    }

    private float EvaluateAlphaCurve(float normalizedTime)
    {
        return ResolvedAlphaFadeCurve != null
            ? Mathf.Clamp01(ResolvedAlphaFadeCurve.Evaluate(Mathf.Clamp01(normalizedTime)))
            : Mathf.Clamp01(normalizedTime);
    }

    private Color GetResolvedShapeColor(Color shapeColor)
    {
        return useGlobalColor ? globalColor : shapeColor;
    }

    private static AnimationCurve CopyCurve(AnimationCurve source)
    {
        return source != null && source.length > 0
            ? new AnimationCurve(source.keys)
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
