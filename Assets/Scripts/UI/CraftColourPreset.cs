using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class CraftColourPreset : MonoBehaviour
{
    private static readonly int PrimaryColorId = Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int WhiteReferenceId = Shader.PropertyToID("_WhiteReference");
    private static readonly int GrayReferenceId = Shader.PropertyToID("_GrayReference");
    private static readonly int BlueReferenceId = Shader.PropertyToID("_BlueReference");
    private static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");

    [Header("Preset")]
    [SerializeField] private string presetName;
    [SerializeField] private HullContentDefinition targetHull;

    [Header("Palette")]
    [SerializeField] private Color primaryColor = Color.white;
    [SerializeField] private Color secondaryColor = new(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color accentColor = new(0.25f, 0.75f, 1f, 1f);

    [Header("Shader Mask Positions")]
    [SerializeField] private Color whiteReference = Color.white;
    [SerializeField] private Color grayReference = new(0.245f, 0.245f, 0.245f, 1f);
    [SerializeField] private Color blueReference = new(0.198f, 0.473f, 1f, 1f);
    [SerializeField, Min(0f)] private float tolerance = 0.05f;

    [Header("UI")]
    [SerializeField] private CraftUIButton craftButton;
    [SerializeField] private Button button;
    [SerializeField] private Image shipVisual;
    [SerializeField] private Sprite icon;
    [SerializeField] private Material previewMaterial;

    public event Action<CraftColourPreset> Clicked;

    public string PresetName => presetName;
    public HullContentDefinition TargetHull => targetHull;
    public ShipColorPalette Palette => new()
    {
        primary = primaryColor,
        secondary = secondaryColor,
        accent = accentColor
    };

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("Craft colour preset is not fully configured.", this);
            return;
        }

        button.onClick.AddListener(InvokeClick);
        RefreshPreview();
    }

    private void OnEnable()
    {
        RefreshPreview();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(InvokeClick);
    }

    private void OnValidate()
    {
        tolerance = Mathf.Max(0f, tolerance);
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        if (!HasRequiredReferences())
            return;

        craftButton.SetShip(presetName, icon);
        shipVisual.sprite = icon;
        shipVisual.material = previewMaterial;
        ApplyShaderPalette();
    }

    public bool Supports(HullContentDefinition hull)
    {
        return targetHull == null || targetHull == hull;
    }

    public bool Matches(ShipColorPalette palette)
    {
        return palette != null
            && AreColorsEqual(primaryColor, palette.primary)
            && AreColorsEqual(secondaryColor, palette.secondary)
            && AreColorsEqual(accentColor, palette.accent);
    }

    public void SetAvailable(bool isAvailable)
    {
        if (craftButton == null)
            return;

        craftButton.SetAvailability(isAvailable);
        craftButton.SetInteractable(isAvailable);
    }

    public void SetSelected(bool isSelected)
    {
        craftButton?.SetSelected(isSelected);
    }

    private void InvokeClick()
    {
        Clicked?.Invoke(this);
    }

    private void ApplyShaderPalette()
    {
        if (previewMaterial == null)
            return;

        SetColorIfPresent(PrimaryColorId, primaryColor);
        SetColorIfPresent(SecondaryColorId, secondaryColor);
        SetColorIfPresent(AccentColorId, accentColor);
        SetColorIfPresent(WhiteReferenceId, whiteReference);
        SetColorIfPresent(GrayReferenceId, grayReference);
        SetColorIfPresent(BlueReferenceId, blueReference);

        if (previewMaterial.HasProperty(ToleranceId))
            previewMaterial.SetFloat(ToleranceId, tolerance);
    }

    private void SetColorIfPresent(int propertyId, Color color)
    {
        if (previewMaterial.HasProperty(propertyId))
            previewMaterial.SetColor(propertyId, color);
    }

    private bool HasRequiredReferences()
    {
        return craftButton != null
            && button != null
            && shipVisual != null
            && icon != null
            && previewMaterial != null;
    }

    private static bool AreColorsEqual(Color first, Color second)
    {
        const float tolerance = 0.001f;
        return Mathf.Abs(first.r - second.r) < tolerance
            && Mathf.Abs(first.g - second.g) < tolerance
            && Mathf.Abs(first.b - second.b) < tolerance
            && Mathf.Abs(first.a - second.a) < tolerance;
    }
}
