using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShipColorPaletteSelectionController : MonoBehaviour
{
    public enum ColorChannel
    {
        Primary,
        Secondary,
        Accent
    }

    [Header("Data")]
    [SerializeField] private ShipColorPaletteConfig colorPaletteConfig;

    [Header("Color Button Content")]
    [SerializeField] private RectTransform swatchRoot;
    [SerializeField] private ShipColorPaletteButton colorButtonPrefab;

    [Header("Color Button Visuals")]
    [SerializeField] private Image previewImage;
    [SerializeField] private Sprite fallbackSwatchSprite;
    [SerializeField] private Color selectedOutlineColor = new(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Min(0f)] private float outlineDistance = 4f;

    [Header("Channel Buttons")]
    [SerializeField] private Button primaryChannelButton;
    [SerializeField] private Image primaryChannelColorImage;
    [SerializeField] private Button secondaryChannelButton;
    [SerializeField] private Image secondaryChannelColorImage;
    [SerializeField] private Button accentChannelButton;
    [SerializeField] private Image accentChannelColorImage;

    [Header("Ship Preview")]
    [SerializeField] private ShipColorMaterialApplier shipPreview;

    [Header("State")]
    [SerializeField] private ColorChannel selectedChannel = ColorChannel.Primary;
    [SerializeField] private ShipColorPalette palette = new ShipColorPalette();

    private readonly List<ShipColorPaletteButton> swatches = new();
    private int selectedColorNumber = -1;

    public event Action<ShipColorPalette> PaletteChanged;
    public event Action PaletteEditCompleted;
    public event Action<ColorChannel, Color> SelectedColorChanged;

    public ShipColorPalette Palette => palette != null ? palette.Clone() : new ShipColorPalette();
    public ColorChannel SelectedChannel => selectedChannel;

    private void Awake()
    {
        palette ??= new ShipColorPalette();
        RegisterChannelButtons();
        RefreshAvailableColors();
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        UnregisterChannelButtons();
        ClearSwatches();
    }

    public void SetPalette(ShipColorPalette sourcePalette)
    {
        palette = sourcePalette != null ? sourcePalette.Clone() : new ShipColorPalette();
        selectedColorNumber = FindColorNumber(GetSelectedColor());
        RefreshVisuals();
    }

    public void ApplyPalette(ShipColorPalette sourcePalette)
    {
        SetPalette(sourcePalette);
        PaletteChanged?.Invoke(Palette);
        PaletteEditCompleted?.Invoke();
    }

    public void SelectPrimary() => SelectChannel(ColorChannel.Primary);
    public void SelectSecondary() => SelectChannel(ColorChannel.Secondary);
    public void SelectAccent() => SelectChannel(ColorChannel.Accent);

    public void SelectChannel(ColorChannel channel)
    {
        selectedChannel = channel;
        selectedColorNumber = FindColorNumber(GetSelectedColor());
        RefreshVisuals();
    }

    public void RefreshAvailableColors()
    {
        ClearSwatches();

        if (colorPaletteConfig == null || swatchRoot == null || colorButtonPrefab == null)
        {
            Debug.LogError(
                "Ship color palette selector requires a color config, content root, and button prefab.",
                this);
            return;
        }

        IReadOnlyList<ShipColorPaletteColor> colors = colorPaletteConfig.Colors;
        HashSet<int> colorNumbers = new();
        for (int i = 0; i < colors.Count; i++)
        {
            ShipColorPaletteColor color = colors[i];
            if (color == null)
                continue;

            if (!colorNumbers.Add(color.ColorNumber))
            {
                Debug.LogError(
                    $"Color palette contains duplicate color number {color.ColorNumber}.",
                    colorPaletteConfig);
                continue;
            }

            CreateSwatch(color);
        }
    }

    private void CreateSwatch(ShipColorPaletteColor color)
    {
        ShipColorPaletteButton colorButton = Instantiate(colorButtonPrefab, swatchRoot);
        colorButton.gameObject.name = $"Color {color.ColorNumber}";
        colorButton.gameObject.layer = swatchRoot.gameObject.layer;
        colorButton.Initialize(
            color,
            fallbackSwatchSprite,
            selectedOutlineColor,
            outlineDistance,
            SelectColor);
        swatches.Add(colorButton);
    }

    private void SelectColor(ShipColorPaletteColor color)
    {
        if (color == null)
            return;

        Color selectedColor = color.Color;
        selectedColor.a = 1f;
        SetSelectedColor(selectedColor);
        selectedColorNumber = color.ColorNumber;

        RefreshVisuals();
        SelectedColorChanged?.Invoke(selectedChannel, selectedColor);
        PaletteChanged?.Invoke(Palette);
        PaletteEditCompleted?.Invoke();
    }

    private void SetSelectedColor(Color color)
    {
        switch (selectedChannel)
        {
            case ColorChannel.Secondary:
                palette.secondary = color;
                break;
            case ColorChannel.Accent:
                palette.accent = color;
                break;
            default:
                palette.primary = color;
                break;
        }
    }

    private Color GetSelectedColor()
    {
        return selectedChannel switch
        {
            ColorChannel.Secondary => palette.secondary,
            ColorChannel.Accent => palette.accent,
            _ => palette.primary
        };
    }

    private int FindColorNumber(Color targetColor)
    {
        if (colorPaletteConfig == null)
            return -1;

        IReadOnlyList<ShipColorPaletteColor> colors = colorPaletteConfig.Colors;
        for (int i = 0; i < colors.Count; i++)
        {
            ShipColorPaletteColor color = colors[i];
            if (color != null && AreColorsEqual(color.Color, targetColor))
                return color.ColorNumber;
        }

        return -1;
    }

    private void RefreshVisuals()
    {
        if (palette == null)
            return;

        Color selectedColor = GetSelectedColor();
        if (previewImage != null)
            previewImage.color = selectedColor;

        if (shipPreview != null)
            shipPreview.Apply(palette);

        SetButtonColor(primaryChannelButton, primaryChannelColorImage, palette.primary);
        SetButtonColor(secondaryChannelButton, secondaryChannelColorImage, palette.secondary);
        SetButtonColor(accentChannelButton, accentChannelColorImage, palette.accent);

        for (int i = 0; i < swatches.Count; i++)
        {
            ShipColorPaletteButton swatch = swatches[i];
            if (swatch != null)
                swatch.SetSelected(swatch.ColorNumber == selectedColorNumber);
        }
    }

    private void RegisterChannelButtons()
    {
        if (primaryChannelButton != null)
            primaryChannelButton.onClick.AddListener(SelectPrimary);

        if (secondaryChannelButton != null)
            secondaryChannelButton.onClick.AddListener(SelectSecondary);

        if (accentChannelButton != null)
            accentChannelButton.onClick.AddListener(SelectAccent);
    }

    private void UnregisterChannelButtons()
    {
        if (primaryChannelButton != null)
            primaryChannelButton.onClick.RemoveListener(SelectPrimary);

        if (secondaryChannelButton != null)
            secondaryChannelButton.onClick.RemoveListener(SelectSecondary);

        if (accentChannelButton != null)
            accentChannelButton.onClick.RemoveListener(SelectAccent);
    }

    private void ClearSwatches()
    {
        for (int i = 0; i < swatches.Count; i++)
        {
            ShipColorPaletteButton swatch = swatches[i];
            GameObject swatchObject = swatch != null ? swatch.gameObject : null;
            if (swatchObject != null)
                Destroy(swatchObject);
        }

        swatches.Clear();
    }

    private static void SetButtonColor(Button button, Image colorImage, Color color)
    {
        Image targetImage = colorImage != null
            ? colorImage
            : button != null
                ? button.image
                : null;

        if (targetImage != null)
            targetImage.color = color;
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
