using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PlayerJoystick : MonoBehaviour, IPointerDownHandler,
    IDragHandler, IPointerUpHandler, ICancelHandler
{
    [SerializeField] private PlayerJoystickSettings settings;
    [SerializeField, Tooltip("Параметры четвёртого режима Joystick Focus. Обычный Joystick продолжает использовать Settings.")]
    private PlayerJoystickFocusSettings focusSettings;
    [SerializeField, Tooltip("Ручка джойстика, дочерний RectTransform с центром в центре основания.")]
    private RectTransform handle;

    private RectTransform surface;
    private int pointerId;
    private bool isHeld;
    private Vector2 input;
    private Vector2 normalizedDisplacement;
    private bool isFocus = true;

    public event Action Released;
    public Vector2 InputDirection => input;
    public bool IsHeld => isHeld;
    public PlayerJoystickSettings Settings => settings;
    public PlayerJoystickFocusSettings FocusSettings => focusSettings;
    public Vector2 NormalizedDisplacement => normalizedDisplacement;
    public bool IsFocus => isFocus;
    public Vector2 FocusInputDirection => focusSettings != null && settings != null
        ? focusSettings.EvaluateInput(normalizedDisplacement, ref isFocus, settings.SpeedMode)
        : Vector2.zero;

    private void Awake()
    {
        surface = (RectTransform)transform;
        if (Application.isPlaying && focusSettings != null)
            focusSettings.LoadPlayerPreferences();
        if (settings == null || handle == null)
        {
            Debug.LogError("PlayerJoystick requires Settings and Handle references.", this);
            enabled = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || isHeld || Time.timeScale <= 0f
            || eventData.button != PointerEventData.InputButton.Left
            || settings == null || handle == null)
            return;

        pointerId = eventData.pointerId;
        isHeld = true;
        UpdateInput(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isHeld && eventData.pointerId == pointerId)
            UpdateInput(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld || eventData.pointerId != pointerId)
            return;

        bool canceled = !Application.isFocused || Time.timeScale <= 0f;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId && touch.phase == TouchPhase.Canceled)
                canceled = true;
        }

        CancelInput();
        if (!canceled)
            Released?.Invoke();
    }

    public void OnCancel(BaseEventData eventData) => CancelInput();

    public void CancelInput()
    {
        isHeld = false;
        input = Vector2.zero;
        normalizedDisplacement = Vector2.zero;
        isFocus = true;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    private void UpdateInput(PointerEventData eventData)
    {
        surface ??= (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                surface, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Vector2 displacement = local - surface.rect.center;
        normalizedDisplacement = Vector2.ClampMagnitude(displacement / settings.MovementRadius, 1f);
        input = settings.EvaluateInput(displacement);
        handle.anchoredPosition = Vector2.ClampMagnitude(displacement, settings.MovementRadius);
    }

    private void Update()
    {
        if (isHeld && Time.timeScale <= 0f)
            CancelInput();
    }

    private void OnDisable() => CancelInput();
    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            CancelInput();
    }
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            CancelInput();
    }
}
