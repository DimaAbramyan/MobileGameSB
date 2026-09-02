using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemyShieldModifier : MonoBehaviour
{
    [SerializeField] private bool startWithFullShield = true;
    [SerializeField] private bool shieldEnabled = true;

    [Header("Shield Visual")]
    [SerializeField] private bool showShieldVisual = true;
    [SerializeField] private Vector3 shieldVisualLocalOffset;
    [SerializeField, Min(0.01f)] private float shieldVisualRadius = 0.75f;
    [SerializeField] private Vector2 shieldVisualScale = Vector2.one;
    [SerializeField, Range(8, 128)] private int shieldVisualSegments = 32;
    [SerializeField, Min(0.001f)] private float shieldVisualLineWidth = 0.04f;
    [SerializeField] private Color shieldVisualColor =
        new(0.15f, 0.8f, 1f, 0.85f);
    [SerializeField] private Color shieldVisualDepletedColor =
        new(1f, 0.25f, 0.15f, 0.85f);
    [SerializeField] private string shieldVisualSortingLayer = "Default";
    [SerializeField] private int shieldVisualSortingOrder = 1;

    private float currentShieldPoints;
    private Enemy enemy;
    private LineRenderer shieldLineRenderer;
    private Vector3[] shieldVisualPoints = System.Array.Empty<Vector3>();
    private Material runtimeShieldVisualMaterial;

    public event Action<float, float> OnShieldChanged;
    public event Action OnShieldDepleted;

    public float MaxShieldPoints => enemy != null
        ? enemy.ShieldPoints
        : 0f;
    public float CurrentShieldPoints => Mathf.Max(0f, currentShieldPoints);
    public bool IsShieldEnabled => shieldEnabled;
    public bool IsShieldActive => shieldEnabled && CurrentShieldPoints > 0f;
    public bool ShowShieldVisual => showShieldVisual;
    public Vector3 ShieldVisualLocalOffset => shieldVisualLocalOffset;
    public float ShieldVisualRadius => Mathf.Max(0.01f, shieldVisualRadius);
    public Vector2 ShieldVisualScale => new Vector2(
        Mathf.Max(0.01f, shieldVisualScale.x),
        Mathf.Max(0.01f, shieldVisualScale.y));
    public Color ShieldVisualColor => shieldVisualColor;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        currentShieldPoints = startWithFullShield
            ? MaxShieldPoints
            : Mathf.Min(currentShieldPoints, MaxShieldPoints);
        RefreshVisual();
    }

    public float AbsorbDamage(float incomingDamage)
    {
        return AbsorbDamage(incomingDamage, 1f);
    }

    public float AbsorbDamage(
        float incomingDamage,
        float shieldDamageMultiplier)
    {
        if (incomingDamage <= 0f || !IsShieldActive)
            return incomingDamage;

        float safeMultiplier = Mathf.Max(0.0001f, shieldDamageMultiplier);
        float shieldDamage = incomingDamage * safeMultiplier;
        float absorbedShieldDamage = Mathf.Min(shieldDamage, currentShieldPoints);
        currentShieldPoints -= absorbedShieldDamage;
        OnShieldChanged?.Invoke(CurrentShieldPoints, MaxShieldPoints);
        RefreshVisual();

        if (currentShieldPoints <= 0f)
            OnShieldDepleted?.Invoke();

        return incomingDamage - absorbedShieldDamage / safeMultiplier;
    }

    public void SetShieldEnabled(bool isEnabled)
    {
        if (shieldEnabled == isEnabled)
            return;

        shieldEnabled = isEnabled;
        OnShieldChanged?.Invoke(CurrentShieldPoints, MaxShieldPoints);
        RefreshVisual();
    }

    public void SetShieldPoints(float value)
    {
        float nextValue = Mathf.Clamp(value, 0f, MaxShieldPoints);
        if (Mathf.Approximately(currentShieldPoints, nextValue))
            return;

        bool wasActive = IsShieldActive;
        currentShieldPoints = nextValue;
        OnShieldChanged?.Invoke(CurrentShieldPoints, MaxShieldPoints);
        RefreshVisual();
        if (wasActive && currentShieldPoints <= 0f)
            OnShieldDepleted?.Invoke();
    }

    public void RestoreFullShield()
    {
        SetShieldPoints(MaxShieldPoints);
    }

    public void MultiplyShieldHealth(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        enemy?.MultiplyShieldPoints(safeMultiplier);
        currentShieldPoints *= safeMultiplier;
        currentShieldPoints = Mathf.Min(currentShieldPoints, MaxShieldPoints);
        OnShieldChanged?.Invoke(CurrentShieldPoints, MaxShieldPoints);
        RefreshVisual();
    }

    private void OnValidate()
    {
        shieldVisualRadius = Mathf.Max(0.01f, shieldVisualRadius);
        shieldVisualScale.x = Mathf.Max(0.01f, shieldVisualScale.x);
        shieldVisualScale.y = Mathf.Max(0.01f, shieldVisualScale.y);
        shieldVisualSegments = Mathf.Clamp(shieldVisualSegments, 8, 128);
        shieldVisualLineWidth = Mathf.Max(0.001f, shieldVisualLineWidth);

        if (Application.isPlaying)
            RefreshVisual();
    }

    private void OnDisable()
    {
        SetVisualVisible(false);
    }

    private void OnDestroy()
    {
        if (runtimeShieldVisualMaterial != null)
            Destroy(runtimeShieldVisualMaterial);
    }

    private void RefreshVisual()
    {
        if (!showShieldVisual || !IsShieldActive)
        {
            SetVisualVisible(false);
            return;
        }

        EnsureVisual();
        if (shieldLineRenderer == null)
            return;

        int segmentCount = Mathf.Clamp(shieldVisualSegments, 8, 128);
        EnsureVisualPointCapacity(segmentCount);
        Vector2 scale = ShieldVisualScale;
        float radius = ShieldVisualRadius;
        for (int index = 0; index < segmentCount; index++)
        {
            float angle = Mathf.PI * 2f * index / segmentCount;
            shieldVisualPoints[index] = shieldVisualLocalOffset
                + new Vector3(
                    Mathf.Cos(angle) * radius * scale.x,
                    Mathf.Sin(angle) * radius * scale.y,
                    0f);
        }

        shieldLineRenderer.positionCount = segmentCount;
        shieldLineRenderer.SetPositions(shieldVisualPoints);
        shieldLineRenderer.widthMultiplier = shieldVisualLineWidth;
        Color visualColor = GetShieldVisualColor();
        shieldLineRenderer.startColor = visualColor;
        shieldLineRenderer.endColor = visualColor;
        shieldLineRenderer.sortingLayerName = shieldVisualSortingLayer;
        shieldLineRenderer.sortingOrder = shieldVisualSortingOrder;
        shieldLineRenderer.enabled = true;
    }

    private void EnsureVisual()
    {
        if (shieldLineRenderer != null)
            return;

        var visualObject = new GameObject("Enemy Shield Visual");
        visualObject.transform.SetParent(transform, false);
        shieldLineRenderer = visualObject.AddComponent<LineRenderer>();
        shieldLineRenderer.useWorldSpace = false;
        shieldLineRenderer.loop = true;
        shieldLineRenderer.alignment = LineAlignment.TransformZ;
        shieldLineRenderer.textureMode = LineTextureMode.Stretch;
        shieldLineRenderer.numCapVertices = 2;
        shieldLineRenderer.numCornerVertices = 2;

        Material material = GetShieldVisualMaterial();
        if (material != null)
            shieldLineRenderer.sharedMaterial = material;
    }

    private Material GetShieldVisualMaterial()
    {
        if (runtimeShieldVisualMaterial != null)
            return runtimeShieldVisualMaterial;

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning(
                $"{nameof(EnemyShieldModifier)} could not find a shader for the shield visual.",
                this);
            return null;
        }

        runtimeShieldVisualMaterial = new Material(shader)
        {
            name = "Runtime Enemy Shield Visual Material"
        };
        return runtimeShieldVisualMaterial;
    }

    private void SetVisualVisible(bool isVisible)
    {
        if (shieldLineRenderer != null)
            shieldLineRenderer.enabled = isVisible;
    }

    private Color GetShieldVisualColor()
    {
        float shieldFraction = MaxShieldPoints <= 0f
            ? 0f
            : Mathf.Clamp01(CurrentShieldPoints / MaxShieldPoints);
        return Color.Lerp(
            shieldVisualDepletedColor,
            shieldVisualColor,
            shieldFraction);
    }

    private void EnsureVisualPointCapacity(int pointCount)
    {
        if (shieldVisualPoints.Length >= pointCount)
            return;

        shieldVisualPoints = new Vector3[pointCount];
    }
}
