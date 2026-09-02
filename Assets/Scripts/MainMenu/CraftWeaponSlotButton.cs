using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class CraftWeaponSlotButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Color selectedColor = new(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color assignedColor = Color.white;

    private Action clickAction;
    private RectTransform rectTransform;
    private Color defaultColor = Color.white;
    private bool isFocused;
    private bool hasWeapon;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (background == null && button != null)
            background = button.image;

        if (background != null)
            defaultColor = background.color;

        if (button != null)
            button.onClick.AddListener(InvokeClickAction);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(InvokeClickAction);
    }

    public void Initialize(Action action)
    {
        clickAction = action;
    }

    public void SetWeapon(WeaponContentDefinition weapon)
    {
        hasWeapon = weapon != null;

        if (weaponIcon != null)
        {
            weaponIcon.sprite = weapon != null ? weapon.Icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
        }

        RefreshVisuals();
    }

    public void SetWeaponIconScale(Vector2 scale)
    {
        if (weaponIcon == null)
            return;

        weaponIcon.rectTransform.localScale = new Vector3(
            Mathf.Max(0.01f, scale.x),
            Mathf.Max(0.01f, scale.y),
            1f);
    }

    public void SetFocused(bool value)
    {
        isFocused = value;
        RefreshVisuals();
    }

    public void SetViewportPosition(
        Vector2 viewportPosition,
        Vector2 offset,
        Vector2 size,
        bool isVisible)
    {
        if (rectTransform != null)
        {
            rectTransform.anchorMin = viewportPosition;
            rectTransform.anchorMax = viewportPosition;
            rectTransform.anchoredPosition = offset;
            rectTransform.sizeDelta = size;
        }

        if (gameObject.activeSelf != isVisible)
            gameObject.SetActive(isVisible);
    }

    private void RefreshVisuals()
    {
        if (background == null)
            return;

        background.color = hasWeapon
            ? Color.clear
            : isFocused ? selectedColor : defaultColor;

        if (weaponIcon != null && weaponIcon.enabled)
            weaponIcon.color = isFocused ? selectedColor : assignedColor;
    }

    private void InvokeClickAction()
    {
        clickAction?.Invoke();
    }
}
