using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HslColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private struct HslState
    {
        public float hue;
        public float saturation;
        public float lightness;
    }

    public enum ColorChannel
    {
        Primary,
        Secondary,
        Accent
    }

    [Header("Palette UI")]
    [SerializeField] private RawImage paletteImage;
    [SerializeField] private RawImage saturationImage;
    [SerializeField] private Image previewImage;
    [SerializeField] private RectTransform paletteCursor;
    [SerializeField] private RectTransform saturationCursor;
    [SerializeField] private bool verticalSaturation = true;

    [Header("Channel Buttons")]
    [SerializeField] private Button primaryChannelButton;
    [SerializeField] private Button secondaryChannelButton;
    [SerializeField] private Button accentChannelButton;

    [Header("Ship Preview")]
    [SerializeField] private ShipColorMaterialApplier shipPreview;

    [Header("State")]
    [SerializeField] private ColorChannel selectedChannel = ColorChannel.Primary;
    [SerializeField] private ShipColorPalette palette = new ShipColorPalette();
    [SerializeField, Range(32, 256)] private int paletteResolution = 128;
    [SerializeField, Range(16, 128)] private int saturationResolution = 32;

    private Texture2D paletteTexture;
    private Texture2D saturationTexture;
    private readonly HslState[] channelHslStates = new HslState[3];
    private bool hasInitializedHslStates;

    public event Action<ShipColorPalette> PaletteChanged;
    public event Action PaletteEditCompleted;
    public event Action<ColorChannel, Color> SelectedColorChanged;

    public ShipColorPalette Palette => palette != null ? palette.Clone() : new ShipColorPalette();
    public ColorChannel SelectedChannel => selectedChannel;

    private void Awake()
    {
        if (palette == null)
            palette = new ShipColorPalette();

        if (!hasInitializedHslStates)
            SyncHslStatesFromPalette();
        CreateTextures();
        ConfigureInputArea(paletteImage, HslColorPickerInputArea.InputArea.Palette);
        ConfigureInputArea(saturationImage, HslColorPickerInputArea.InputArea.Saturation);
        RegisterChannelButtons();
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        UnregisterChannelButtons();
        DestroyTexture(paletteTexture);
        DestroyTexture(saturationTexture);
    }

    public void SetPalette(ShipColorPalette sourcePalette)
    {
        SetPalette(sourcePalette, false);
    }

    public void SetPaletteWithMaximumSaturation(ShipColorPalette sourcePalette)
    {
        SetPalette(sourcePalette, true);
    }

    private void SetPalette(ShipColorPalette sourcePalette, bool useMaximumSaturation)
    {
        palette = sourcePalette != null ? sourcePalette.Clone() : new ShipColorPalette();
        SyncHslStatesFromPalette();

        if (useMaximumSaturation)
            SetAllSaturations(1f);

        RefreshVisuals();
    }

    public void SelectPrimary() => SelectChannel(ColorChannel.Primary);
    public void SelectSecondary() => SelectChannel(ColorChannel.Secondary);
    public void SelectAccent() => SelectChannel(ColorChannel.Accent);

    public void SelectChannel(ColorChannel channel)
    {
        selectedChannel = channel;
        RefreshVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateFromPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        PaletteEditCompleted?.Invoke();
    }

    public void UpdateHueAndLightnessFromPointer(PointerEventData eventData)
    {
        UpdateHueAndLightness(eventData);
    }

    public void UpdateSaturationFromPointer(PointerEventData eventData)
    {
        UpdateSaturation(eventData);
    }

    public void CompletePaletteEdit()
    {
        PaletteEditCompleted?.Invoke();
    }

    private void UpdateFromPointer(PointerEventData eventData)
    {
        if (IsPointerInside(eventData, paletteImage))
            UpdateHueAndLightness(eventData);
        else if (IsPointerInside(eventData, saturationImage))
            UpdateSaturation(eventData);
    }

    private void UpdateHueAndLightness(PointerEventData eventData)
    {
        if (!TryGetNormalizedPoint(paletteImage.rectTransform, eventData, out Vector2 point))
            return;

        HslState state = GetSelectedHslState();
        state.hue = point.x;
        state.lightness = point.y;
        SetSelectedColor(HslToRgb(state.hue, state.saturation, state.lightness), state);
    }

    private void UpdateSaturation(PointerEventData eventData)
    {
        if (!TryGetNormalizedPoint(saturationImage.rectTransform, eventData, out Vector2 point))
            return;

        HslState state = GetSelectedHslState();
        float saturation = verticalSaturation ? point.y : point.x;
        state.saturation = saturation;
        SetSelectedColor(HslToRgb(state.hue, state.saturation, state.lightness), state);
    }

    private void SetSelectedColor(Color color, HslState state)
    {
        color.a = 1f;
        SetSelectedHslState(state);

        switch (selectedChannel)
        {
            case ColorChannel.Primary:
                palette.primary = color;
                break;
            case ColorChannel.Secondary:
                palette.secondary = color;
                break;
            case ColorChannel.Accent:
                palette.accent = color;
                break;
        }

        RefreshVisuals();
        SelectedColorChanged?.Invoke(selectedChannel, color);
        PaletteChanged?.Invoke(Palette);
    }

    private Color GetSelectedColor()
    {
        return GetChannelColor(selectedChannel);
    }

    private Color GetChannelColor(ColorChannel channel)
    {
        switch (channel)
        {
            case ColorChannel.Secondary:
                return palette.secondary;
            case ColorChannel.Accent:
                return palette.accent;
            default:
                return palette.primary;
        }
    }

    private void SyncHslStatesFromPalette()
    {
        for (int i = 0; i < channelHslStates.Length; i++)
        {
            Color color = GetChannelColor((ColorChannel)i);
            RgbToHsl(color, out float hue, out float saturation, out float lightness);
            channelHslStates[i] = new HslState
            {
                hue = hue,
                saturation = saturation,
                lightness = lightness
            };
        }

        hasInitializedHslStates = true;
    }

    private HslState GetSelectedHslState()
    {
        return channelHslStates[(int)selectedChannel];
    }

    private void SetSelectedHslState(HslState state)
    {
        channelHslStates[(int)selectedChannel] = state;
    }

    private void SetAllSaturations(float saturation)
    {
        for (int i = 0; i < channelHslStates.Length; i++)
        {
            HslState state = channelHslStates[i];
            state.saturation = saturation;
            channelHslStates[i] = state;
        }
    }

    private void RefreshVisuals()
    {
        if (paletteTexture == null || saturationTexture == null)
            return;

        Color selectedColor = GetSelectedColor();
        HslState state = GetSelectedHslState();
        FillPaletteTexture(state.saturation);
        FillSaturationTexture(state.hue, state.lightness);

        if (previewImage != null)
            previewImage.color = selectedColor;

        if (shipPreview != null)
            shipPreview.Apply(palette);

        UpdateChannelButtonColors();
        SetCursorPosition(paletteCursor, new Vector2(state.hue, state.lightness));
        SetCursorPosition(saturationCursor, verticalSaturation
            ? new Vector2(0.5f, state.saturation)
            : new Vector2(state.saturation, 0.5f));
    }

    private void CreateTextures()
    {
        paletteTexture = new Texture2D(paletteResolution, paletteResolution, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        saturationTexture = new Texture2D(
            verticalSaturation ? saturationResolution : paletteResolution,
            verticalSaturation ? paletteResolution : saturationResolution,
            TextureFormat.RGBA32,
            false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        if (paletteImage != null)
            paletteImage.texture = paletteTexture;

        if (saturationImage != null)
            saturationImage.texture = saturationTexture;
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

    private void ConfigureInputArea(RawImage image, HslColorPickerInputArea.InputArea inputArea)
    {
        if (image == null)
            return;

        HslColorPickerInputArea area = image.GetComponent<HslColorPickerInputArea>();
        if (area == null)
            area = image.gameObject.AddComponent<HslColorPickerInputArea>();

        area.Configure(this, inputArea);
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

    private void UpdateChannelButtonColors()
    {
        SetButtonColor(primaryChannelButton, palette.primary);
        SetButtonColor(secondaryChannelButton, palette.secondary);
        SetButtonColor(accentChannelButton, palette.accent);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.image != null)
            button.image.color = color;
    }

    private void FillPaletteTexture(float saturation)
    {
        int width = paletteTexture.width;
        int height = paletteTexture.height;
        for (int y = 0; y < height; y++)
        {
            float lightness = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
                paletteTexture.SetPixel(x, y, HslToRgb((float)x / (width - 1), saturation, lightness));
        }

        paletteTexture.Apply(false, false);
    }

    private void FillSaturationTexture(float hue, float lightness)
    {
        int width = saturationTexture.width;
        int height = saturationTexture.height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float saturation = verticalSaturation
                    ? (float)y / (height - 1)
                    : (float)x / (width - 1);
                saturationTexture.SetPixel(x, y, HslToRgb(hue, saturation, lightness));
            }
        }

        saturationTexture.Apply(false, false);
    }

    private static bool IsPointerInside(PointerEventData eventData, RawImage image)
    {
        return image != null
            && eventData.pointerEnter != null
            && (eventData.pointerEnter.transform == image.transform
                || eventData.pointerEnter.transform.IsChildOf(image.transform));
    }

    private static bool TryGetNormalizedPoint(
        RectTransform rectTransform,
        PointerEventData eventData,
        out Vector2 point)
    {
        point = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;
        point.x = Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x));
        point.y = Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
        return true;
    }

    private static void SetCursorPosition(RectTransform cursor, Vector2 position)
    {
        if (cursor == null)
            return;

        cursor.anchorMin = position;
        cursor.anchorMax = position;
        cursor.anchoredPosition = Vector2.zero;
    }

    private static void RgbToHsl(Color color, out float hue, out float saturation, out float lightness)
    {
        float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        lightness = (max + min) * 0.5f;

        if (Mathf.Approximately(max, min))
        {
            hue = 0f;
            saturation = 0f;
            return;
        }

        float delta = max - min;
        saturation = delta / (1f - Mathf.Abs(2f * lightness - 1f));
        if (Mathf.Approximately(max, color.r))
            hue = ((color.g - color.b) / delta) % 6f;
        else if (Mathf.Approximately(max, color.g))
            hue = (color.b - color.r) / delta + 2f;
        else
            hue = (color.r - color.g) / delta + 4f;

        hue = Mathf.Repeat(hue / 6f, 1f);
    }

    private static Color HslToRgb(float hue, float saturation, float lightness)
    {
        hue = Mathf.Repeat(hue, 1f);
        saturation = Mathf.Clamp01(saturation);
        lightness = Mathf.Clamp01(lightness);

        float chroma = (1f - Mathf.Abs(2f * lightness - 1f)) * saturation;
        float hueSegment = hue * 6f;
        float second = chroma * (1f - Mathf.Abs(hueSegment % 2f - 1f));
        float match = lightness - chroma * 0.5f;

        float red;
        float green;
        float blue;
        if (hueSegment < 1f)
        {
            red = chroma; green = second; blue = 0f;
        }
        else if (hueSegment < 2f)
        {
            red = second; green = chroma; blue = 0f;
        }
        else if (hueSegment < 3f)
        {
            red = 0f; green = chroma; blue = second;
        }
        else if (hueSegment < 4f)
        {
            red = 0f; green = second; blue = chroma;
        }
        else if (hueSegment < 5f)
        {
            red = second; green = 0f; blue = chroma;
        }
        else
        {
            red = chroma; green = 0f; blue = second;
        }

        return new Color(red + match, green + match, blue + match, 1f);
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
            Destroy(texture);
    }
}
