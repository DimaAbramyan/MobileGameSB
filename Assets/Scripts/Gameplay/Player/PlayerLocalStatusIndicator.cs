using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParentShip))]
public sealed class PlayerLocalStatusIndicator : MonoBehaviour
{
    private const int MinimumSegments = 3;
    private const int MaximumSegments = 128;

    [Header("Visibility")]
    [SerializeField] private bool showIndicator = true;

    [Header("Shared Layout (mirrored)")]
    [Tooltip("Moves both arcs together along the ship's vertical axis.")]
    [SerializeField] private float verticalOffset;
    [Tooltip("Moves the arcs away from the ship's vertical axis while preserving mirror symmetry.")]
    [SerializeField] private float sideOffset;
    [SerializeField, Min(0.01f)] private float radius = 0.7f;
    [Tooltip("The bottom edge of each arc, measured from the top of the ship. 180 reaches the bottom.")]
    [SerializeField, Range(5f, 180f)] private float arcAngle = 150f;
    [Tooltip("Leaves a mirrored angular gap at the top of both arcs.")]
    [SerializeField, Range(0f, 175f)] private float topArcOffset;
    [SerializeField, Range(MinimumSegments, MaximumSegments)] private int segments = 32;
    [SerializeField, Min(0.001f)] private float thickness = 0.04f;
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 999;

    [Header("Health — left arc")]
    [SerializeField] private Color healthColor = new(1f, 0.25f, 0.2f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float healthAlpha = 1f;

    [Header("Shield — right arc")]
    [SerializeField] private Color shieldColor = new(0.15f, 0.8f, 1f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float shieldAlpha = 1f;

    [Header("Empty Arc")]
    [SerializeField, Range(0f, 1f)] private float emptyArcOpacity = 0.2f;

    [Header("Editor Preview")]
    [SerializeField, Range(0f, 1f)] private float previewHealthFraction = 1f;
    [SerializeField, Range(0f, 1f)] private float previewShieldFraction = 1f;

    private ParentShip ship;
    private Material material;
    private LineRenderer healthBackground;
    private LineRenderer healthFill;
    private LineRenderer shieldBackground;
    private LineRenderer shieldFill;
    private Vector3[] leftPoints = System.Array.Empty<Vector3>();
    private Vector3[] rightPoints = System.Array.Empty<Vector3>();
    private Vector3[] healthFillPoints = System.Array.Empty<Vector3>();
    private Vector3[] shieldFillPoints = System.Array.Empty<Vector3>();
    private float healthFraction;
    private float shieldFraction;
    private bool isSubscribed;
    private bool wasVisible;

#if UNITY_EDITOR
    private bool isEditorPreview;
#endif

    private void Awake()
    {
        ship = GetComponent<ParentShip>();
        Subscribe();
        RefreshState();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshState();
    }

    private void LateUpdate()
    {
        SetLinesVisible(showIndicator && ship != null && ship.IsVisible);
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetLinesVisible(false);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        ClearEditorPreview();
#endif

        if (material != null)
            Destroy(material);
    }

#if UNITY_EDITOR
    public bool CanPreviewInEditor => !Application.isPlaying
        && !UnityEditor.EditorUtility.IsPersistent(gameObject);

    public void SetEditorPreview(bool shouldShow)
    {
        if (!CanPreviewInEditor)
            return;

        if (!shouldShow)
        {
            ClearEditorPreview();
            return;
        }

        isEditorPreview = true;
        RefreshEditorPreview();
    }

    public void RefreshEditorPreview()
    {
        if (!isEditorPreview || !CanPreviewInEditor)
            return;

        healthFraction = previewHealthFraction;
        shieldFraction = previewShieldFraction;
        RefreshVisual();
        SetLinesVisible(true);
    }

    private void ClearEditorPreview()
    {
        if (!isEditorPreview)
            return;

        DestroyEditorLine(ref healthBackground);
        DestroyEditorLine(ref healthFill);
        DestroyEditorLine(ref shieldBackground);
        DestroyEditorLine(ref shieldFill);

        if (material != null)
        {
            DestroyImmediate(material);
            material = null;
        }

        wasVisible = false;
        isEditorPreview = false;
    }

    private static void DestroyEditorLine(ref LineRenderer line)
    {
        if (line != null)
            DestroyImmediate(line.gameObject);

        line = null;
    }
#endif

    private void OnValidate()
    {
        sideOffset = Mathf.Max(-9999f, sideOffset);
        radius = Mathf.Max(0.01f, radius);
        arcAngle = Mathf.Clamp(arcAngle, 5f, 180f);
        topArcOffset = Mathf.Clamp(topArcOffset, 0f, arcAngle - 5f);
        segments = Mathf.Clamp(segments, MinimumSegments, MaximumSegments);
        thickness = Mathf.Max(0.001f, thickness);
        healthAlpha = Mathf.Clamp01(healthAlpha);
        shieldAlpha = Mathf.Clamp01(shieldAlpha);
        emptyArcOpacity = Mathf.Clamp01(emptyArcOpacity);
        previewHealthFraction = Mathf.Clamp01(previewHealthFraction);
        previewShieldFraction = Mathf.Clamp01(previewShieldFraction);
    }

    private void Subscribe()
    {
        if (isSubscribed)
            return;

        if (ship == null)
            ship = GetComponent<ParentShip>();

        if (ship == null)
            return;

        ship.OnHealthChanged += OnHealthChanged;
        ship.OnShieldChanged += OnShieldChanged;
        ship.OnMaxHealthChanged += OnMaxHealthChanged;
        ship.OnMaxShieldChanged += OnMaxShieldChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || ship == null)
            return;

        ship.OnHealthChanged -= OnHealthChanged;
        ship.OnShieldChanged -= OnShieldChanged;
        ship.OnMaxHealthChanged -= OnMaxHealthChanged;
        ship.OnMaxShieldChanged -= OnMaxShieldChanged;
        isSubscribed = false;
    }

    private void OnHealthChanged(float currentHealth)
    {
        healthFraction = GetFraction(currentHealth, ship.MaximumHealthPoints);
        RefreshHealthFill();
    }

    private void OnShieldChanged(float currentShield)
    {
        shieldFraction = GetFraction(currentShield, ship.MaximumShieldPoints);
        RefreshShieldFill();
    }

    private void OnMaxHealthChanged(float maximumHealth)
    {
        healthFraction = GetFraction(ship.CurrentHealthPoints, maximumHealth);
        RefreshHealthFill();
    }

    private void OnMaxShieldChanged(float maximumShield)
    {
        shieldFraction = GetFraction(ship.CurrentShieldPoints, maximumShield);
        RefreshShieldFill();
    }

    private void RefreshState()
    {
        if (ship == null)
            return;

        healthFraction = GetFraction(ship.CurrentHealthPoints, ship.MaximumHealthPoints);
        shieldFraction = GetFraction(ship.CurrentShieldPoints, ship.MaximumShieldPoints);
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (!EnsureLines())
            return;

        BuildArcPoints();
        ApplyBackground(healthBackground, leftPoints, GetHealthColor());
        ApplyBackground(shieldBackground, rightPoints, GetShieldColor());
        SetLinesVisible(false);
        RefreshHealthFill();
        RefreshShieldFill();
    }

    private void RefreshHealthFill()
    {
        RefreshFill(
            healthFill,
            leftPoints,
            healthFillPoints,
            healthFraction,
            GetHealthColor());
    }

    private void RefreshShieldFill()
    {
        RefreshFill(
            shieldFill,
            rightPoints,
            shieldFillPoints,
            shieldFraction,
            GetShieldColor());
    }

    private bool EnsureLines()
    {
        if (!EnsureMaterial())
            return false;

        if (healthBackground == null)
            healthBackground = CreateLine("Health Arc Empty");
        if (healthFill == null)
            healthFill = CreateLine("Health Arc Fill");
        if (shieldBackground == null)
            shieldBackground = CreateLine("Shield Arc Empty");
        if (shieldFill == null)
            shieldFill = CreateLine("Shield Arc Fill");

        return true;
    }

    private bool EnsureMaterial()
    {
        if (material != null)
            return true;

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning(
                $"{nameof(PlayerLocalStatusIndicator)} could not find a shader for the status indicator.",
                this);
            return false;
        }

        material = new Material(shader)
        {
            name = "Runtime Player Local Status Indicator Material"
        };

#if UNITY_EDITOR
        if (isEditorPreview)
            material.hideFlags = HideFlags.DontSaveInEditor;
#endif

        return true;
    }

    private LineRenderer CreateLine(string lineName)
    {
        var lineObject = new GameObject(lineName);

#if UNITY_EDITOR
        if (isEditorPreview)
            lineObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
#endif

        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.widthMultiplier = thickness;
        line.sortingLayerName = sortingLayer;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }

    private void BuildArcPoints()
    {
        int pointCount = segments + 1;
        EnsurePointCapacity(pointCount);

        for (int index = 0; index < pointCount; index++)
        {
            float t = (float)index / segments;
            float angleRadians = Mathf.Lerp(topArcOffset, arcAngle, t) * Mathf.Deg2Rad;
            float rightX = sideOffset + Mathf.Sin(angleRadians) * radius;
            float y = verticalOffset + Mathf.Cos(angleRadians) * radius;

            rightPoints[index] = new Vector3(rightX, y, 0f);
            leftPoints[index] = new Vector3(-rightX, y, 0f);
        }
    }

    private void ApplyBackground(LineRenderer line, Vector3[] points, Color color)
    {
        Color emptyColor = color;
        emptyColor.a *= emptyArcOpacity;

        line.widthMultiplier = thickness;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startColor = emptyColor;
        line.endColor = emptyColor;
        line.sortingLayerName = sortingLayer;
        line.sortingOrder = sortingOrder;
    }

    private void RefreshFill(
        LineRenderer line,
        Vector3[] sourcePoints,
        Vector3[] fillPoints,
        float fillFraction,
        Color color)
    {
        if (line == null || sourcePoints.Length < 2)
            return;

        float clampedFraction = Mathf.Clamp01(fillFraction);
        if (clampedFraction <= 0f)
        {
            line.enabled = false;
            return;
        }

        int fillPointCount = Mathf.Clamp(
            Mathf.CeilToInt(clampedFraction * segments) + 1,
            2,
            sourcePoints.Length);
        for (int index = 0; index < fillPointCount; index++)
        {
            float t = GetFillPointFraction(index, fillPointCount, clampedFraction);
            fillPoints[index] = GetPointAt(sourcePoints, t);
        }

        line.widthMultiplier = thickness;
        line.positionCount = fillPointCount;
        line.SetPositions(fillPoints);
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = sortingLayer;
        line.sortingOrder = sortingOrder + 1;
        line.enabled = wasVisible;
    }

    private Vector3 GetPointAt(Vector3[] points, float t)
    {
        float scaled = Mathf.Clamp01(t) * segments;
        int fromIndex = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
        float fraction = scaled - fromIndex;
        return Vector3.Lerp(points[fromIndex], points[fromIndex + 1], fraction);
    }

    private void SetLinesVisible(bool isVisible)
    {
        if (wasVisible == isVisible)
            return;

        wasVisible = isVisible;
        SetLineVisible(healthBackground, isVisible);
        SetLineVisible(shieldBackground, isVisible);
        SetLineVisible(healthFill, isVisible && healthFraction > 0f);
        SetLineVisible(shieldFill, isVisible && shieldFraction > 0f);
    }

    private static void SetLineVisible(LineRenderer line, bool isVisible)
    {
        if (line != null)
            line.enabled = isVisible;
    }

    private void EnsurePointCapacity(int pointCount)
    {
        if (leftPoints.Length >= pointCount)
            return;

        leftPoints = new Vector3[pointCount];
        rightPoints = new Vector3[pointCount];
        healthFillPoints = new Vector3[pointCount];
        shieldFillPoints = new Vector3[pointCount];
    }

    private static float GetFraction(float current, float maximum)
    {
        return maximum <= 0f ? 0f : Mathf.Clamp01(current / maximum);
    }

    private float GetFillPointFraction(
        int pointIndex,
        int pointCount,
        float fillFraction)
    {
        if (pointIndex == pointCount - 1)
            return 1f;

        return 1f - fillFraction + (float)pointIndex / segments;
    }

    private Color GetHealthColor()
    {
        return ApplyAlpha(healthColor, healthAlpha);
    }

    private Color GetShieldColor()
    {
        return ApplyAlpha(shieldColor, shieldAlpha);
    }

    private static Color ApplyAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

}
