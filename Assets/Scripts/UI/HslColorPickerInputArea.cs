using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HslColorPickerInputArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum InputArea
    {
        Palette,
        Saturation
    }

    [SerializeField] private HslColorPicker colorPicker;
    [SerializeField] private InputArea inputArea;

    public void Configure(HslColorPicker picker, InputArea area)
    {
        colorPicker = picker;
        inputArea = area;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateColor(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateColor(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        colorPicker?.CompletePaletteEdit();
    }

    private void UpdateColor(PointerEventData eventData)
    {
        if (colorPicker == null)
            return;

        if (inputArea == InputArea.Palette)
            colorPicker.UpdateHueAndLightnessFromPointer(eventData);
        else
            colorPicker.UpdateSaturationFromPointer(eventData);
    }
}
