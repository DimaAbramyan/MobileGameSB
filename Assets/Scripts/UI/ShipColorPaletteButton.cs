using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Outline))]
public sealed class ShipColorPaletteButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image colorImage;
    [SerializeField] private Outline selectionOutline;

    [Header("Color")]
    [SerializeField] private ShipColorPaletteColor color;

    private Action<ShipColorPaletteColor> selected;

    public int ColorNumber => color != null ? color.ColorNumber : -1;

    private void Awake()
    {
        if (button == null || colorImage == null || selectionOutline == null)
        {
            Debug.LogError("Ship color palette button is not fully configured.", this);
            enabled = false;
            return;
        }

        button.onClick.AddListener(Select);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Select);
    }

    public void Initialize(
        ShipColorPaletteColor color,
        Sprite fallbackSprite,
        Color outlineColor,
        float outlineDistance,
        Action<ShipColorPaletteColor> onSelected)
    {
        this.color = color;
        selected = onSelected;

        colorImage.sprite = color != null && color.Preview != null
            ? color.Preview
            : fallbackSprite;
        colorImage.color = color != null ? color.Color : Color.clear;

        selectionOutline.effectColor = outlineColor;
        selectionOutline.effectDistance = new Vector2(outlineDistance, -outlineDistance);
        selectionOutline.useGraphicAlpha = false;
        selectionOutline.enabled = false;
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionOutline != null)
            selectionOutline.enabled = isSelected;
    }

    private void Select()
    {
        if (color != null)
            selected?.Invoke(color);
    }
}
