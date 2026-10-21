using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class JoystickFocusPauseSettings : MonoBehaviour
{
    [SerializeField, Tooltip("Тот же Focus Settings, что назначен джойстику. Значения применяются сразу; личные настройки сохраняются при закрытии паузы.")]
    private PlayerJoystickFocusSettings settings;
    [SerializeField] private Slider focusSpeedSlider;
    [SerializeField] private Slider focusToNormalSlider;
    [SerializeField] private Slider normalToFocusSlider;
    [SerializeField] private Slider deadZoneSlider;
    [SerializeField] private TMP_Text focusSpeedValue;
    [SerializeField] private TMP_Text focusToNormalValue;
    [SerializeField] private TMP_Text normalToFocusValue;
    [SerializeField] private TMP_Text deadZoneValue;

    private bool bound;
    private bool changed;

    private void OnEnable()
    {
        if (settings == null || focusSpeedSlider == null || focusToNormalSlider == null
            || normalToFocusSlider == null || deadZoneSlider == null)
        {
            Debug.LogError("Joystick Focus pause settings require Settings and all four sliders.", this);
            return;
        }

        Configure(focusSpeedSlider, 1f);
        Configure(focusToNormalSlider, 1f);
        Configure(normalToFocusSlider, 1f);
        Configure(deadZoneSlider, 0.95f);
        RefreshValues();
        focusSpeedSlider.onValueChanged.AddListener(SetFocusSpeed);
        focusToNormalSlider.onValueChanged.AddListener(SetExitThreshold);
        normalToFocusSlider.onValueChanged.AddListener(SetReturnThreshold);
        deadZoneSlider.onValueChanged.AddListener(SetDeadZone);
        bound = true;
    }

    private void OnDisable()
    {
        if (bound)
        {
            focusSpeedSlider.onValueChanged.RemoveListener(SetFocusSpeed);
            focusToNormalSlider.onValueChanged.RemoveListener(SetExitThreshold);
            normalToFocusSlider.onValueChanged.RemoveListener(SetReturnThreshold);
            deadZoneSlider.onValueChanged.RemoveListener(SetDeadZone);
            bound = false;
        }
        SaveChanges();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveChanges();
    }

    private void SaveChanges()
    {
        if (!changed || settings == null || !Application.isPlaying)
            return;
        settings.SavePlayerPreferences();
        changed = false;
    }

    private static void Configure(Slider slider, float maximum)
    {
        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = maximum;
    }

    private void SetFocusSpeed(float value)
    {
        settings.SetFocusSpeedMultiplier(value);
        SettingsChanged();
    }

    private void SetExitThreshold(float value)
    {
        settings.SetFocusToNormalThreshold(value);
        SettingsChanged();
    }

    private void SetReturnThreshold(float value)
    {
        settings.SetNormalToFocusThreshold(value);
        SettingsChanged();
    }

    private void SetDeadZone(float value)
    {
        settings.SetDeadZone(value);
        SettingsChanged();
    }

    private void SettingsChanged()
    {
        changed = true;
        RefreshValues();
    }

    private void RefreshValues()
    {
        Show(focusSpeedSlider, focusSpeedValue, settings.FocusSpeedMultiplier);
        Show(focusToNormalSlider, focusToNormalValue, settings.FocusToNormalThreshold);
        Show(normalToFocusSlider, normalToFocusValue, settings.NormalToFocusThreshold);
        Show(deadZoneSlider, deadZoneValue, settings.DeadZone);
    }

    private static void Show(Slider slider, TMP_Text label, float value)
    {
        slider.SetValueWithoutNotify(value);
        if (label != null)
            label.SetText("{0:3}", value);
    }
}
