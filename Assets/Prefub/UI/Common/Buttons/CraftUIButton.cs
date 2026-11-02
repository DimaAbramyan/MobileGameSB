using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CraftUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public CraftContentDefinition Content { get; private set; }
    private Action<CraftUIButton, bool> previewEntered, previewExited;
    private bool pointerPreview, navigationPreview;

    public void SetPreviewActions(Action<CraftUIButton, bool> entered, Action<CraftUIButton, bool> exited)
    {
        ReleasePreview(); previewEntered = entered; previewExited = exited;
    }
    public void OnPointerEnter(PointerEventData _) { pointerPreview = true; previewEntered?.Invoke(this, true); }
    public void OnPointerExit(PointerEventData _) { pointerPreview = false; previewExited?.Invoke(this, true); }
    public void OnSelect(BaseEventData _) { navigationPreview = true; previewEntered?.Invoke(this, false); }
    public void OnDeselect(BaseEventData _) { navigationPreview = false; previewExited?.Invoke(this, false); }
    private void OnDisable() => ReleasePreview();
    private void ReleasePreview()
    {
        bool pointer = pointerPreview, navigation = navigationPreview;
        pointerPreview = navigationPreview = false;
        if (pointer) previewExited?.Invoke(this, true);
        if (navigation) previewExited?.Invoke(this, false);
    }
    [SerializeField] private UnityEngine.UI.Image shipSprite;
    [SerializeField] private TextMeshProUGUI shipName;
    [SerializeField] private Button button;
    [SerializeField] private ContentRaritySlotController raritySlotController;
    [SerializeField] private Color selectedColor = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.48f, 0.5f, 0.52f, 1f);
    [SerializeField] private Color unavailableIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private Action clickAction;
    private Color defaultColor = Color.white;
    private Color defaultShipSpriteColor = Color.white;
    private bool isSelected;
    private bool isAvailable = true;
    private bool isWeaponContent;

    private void Awake()
    {
        if (raritySlotController == null)
            raritySlotController = GetComponent<ContentRaritySlotController>();

        if (button == null)
            button = GetComponentInChildren<Button>(true);

        if (button != null)
        {
            if (button.image != null)
                defaultColor = button.image.color;

            button.onClick.AddListener(InvokeClickAction);
        }

        if (shipSprite != null)
            defaultShipSpriteColor = shipSprite.color;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(InvokeClickAction);
    }

    public void SetShip(string displayName, Sprite sprite)
    {
        if (shipName != null)
            shipName.text = displayName ?? string.Empty;

        if (shipSprite != null)
            shipSprite.sprite = sprite;
    }

    public void SetContent(CraftContentDefinition content)
    {
        ReleasePreview();
        Content = content;
        if (content == null)
            return;

        SetShip(content.DisplayName, content.Icon);
        raritySlotController?.Apply(content);
        isWeaponContent = content is WeaponContentDefinition;
        RefreshVisual();
    }

    public void SetClickAction(Action action)
    {
        clickAction = action;
    }

    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        RefreshVisual();
    }

    public void SetAvailability(bool isAvailable)
    {
        this.isAvailable = isAvailable;
        RefreshVisual();
    }

    public void SetInteractable(bool isInteractable)
    {
        if (button != null)
            button.interactable = isInteractable;
    }

    private void InvokeClickAction()
    {
        clickAction?.Invoke();
    }

    private void RefreshVisual()
    {
        if (shipSprite != null)
            shipSprite.color = isAvailable
                ? defaultShipSpriteColor
                : unavailableIconColor;

        raritySlotController?.SetLocked(isWeaponContent && !isAvailable);

        if (button == null || button.image == null)
            return;

        button.image.color = isSelected
            ? selectedColor
            : isAvailable
                ? defaultColor
                : unavailableColor;
    }

}
