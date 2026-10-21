using UnityEngine;

[CreateAssetMenu(menuName = "Player/Joystick Focus Settings")]
public sealed class PlayerJoystickFocusSettings : ScriptableObject
{
    private const float MinimumThresholdGap = 0.001f;
    private const string PreferencePrefix = "Fighting.JoystickFocus.";

    [SerializeField, Range(0f, 1f), Tooltip("Доля максимальной скорости в Focus. В Normal используется полная максимальная скорость.")]
    private float focusSpeedMultiplier = 0.4f;
    [SerializeField, Range(0f, 1f), Tooltip("Отклонение 0–1, при котором Focus переключается в Normal. Должно быть выше порога возврата.")]
    private float focusToNormalThreshold = 0.6f;
    [SerializeField, Range(0f, 1f), Tooltip("Отклонение 0–1, при котором Normal возвращается в Focus. Между порогами сохраняется текущий режим.")]
    private float normalToFocusThreshold = 0.4f;
    [SerializeField, Range(0f, 0.95f), Tooltip("Доля радиуса без движения. Применяется к исходному отклонению, до аналогового преобразования.")]
    private float deadZone = 0.1f;

    public float FocusSpeedMultiplier => Mathf.Clamp01(focusSpeedMultiplier);
    public float NormalToFocusThreshold => Mathf.Clamp(normalToFocusThreshold, 0f, 1f - MinimumThresholdGap);
    public float FocusToNormalThreshold => Mathf.Clamp(focusToNormalThreshold, NormalToFocusThreshold + MinimumThresholdGap, 1f);
    public float DeadZone => Mathf.Clamp(deadZone, 0f, 0.95f);

    public void SetFocusSpeedMultiplier(float value) => focusSpeedMultiplier = Mathf.Clamp01(value);
    public void SetFocusToNormalThreshold(float value)
        => focusToNormalThreshold = Mathf.Clamp(value, NormalToFocusThreshold + MinimumThresholdGap, 1f);
    public void SetNormalToFocusThreshold(float value)
        => normalToFocusThreshold = Mathf.Clamp(value, 0f, FocusToNormalThreshold - MinimumThresholdGap);
    public void SetDeadZone(float value) => deadZone = Mathf.Clamp(value, 0f, 0.95f);

    public void LoadPlayerPreferences()
    {
        focusSpeedMultiplier = PlayerPrefs.GetFloat(PreferencePrefix + "Speed", FocusSpeedMultiplier);
        focusToNormalThreshold = PlayerPrefs.GetFloat(PreferencePrefix + "Exit", FocusToNormalThreshold);
        normalToFocusThreshold = PlayerPrefs.GetFloat(PreferencePrefix + "Return", NormalToFocusThreshold);
        deadZone = PlayerPrefs.GetFloat(PreferencePrefix + "DeadZone", DeadZone);
        OnValidate();
    }

    public void SavePlayerPreferences()
    {
        PlayerPrefs.SetFloat(PreferencePrefix + "Speed", FocusSpeedMultiplier);
        PlayerPrefs.SetFloat(PreferencePrefix + "Exit", FocusToNormalThreshold);
        PlayerPrefs.SetFloat(PreferencePrefix + "Return", NormalToFocusThreshold);
        PlayerPrefs.SetFloat(PreferencePrefix + "DeadZone", DeadZone);
        PlayerPrefs.Save();
    }

    public Vector2 EvaluateInput(Vector2 normalizedDisplacement, ref bool isFocus)
        => EvaluateInput(normalizedDisplacement, ref isFocus, JoystickSpeedMode.Constant);

    public Vector2 EvaluateInput(Vector2 normalizedDisplacement, ref bool isFocus, JoystickSpeedMode speedMode)
    {
        Vector2 displacement = Vector2.ClampMagnitude(normalizedDisplacement, 1f);
        float magnitude = displacement.magnitude;
        if (isFocus)
        {
            if (magnitude >= FocusToNormalThreshold)
                isFocus = false;
        }
        else if (magnitude <= NormalToFocusThreshold)
            isFocus = true;

        if (magnitude <= DeadZone)
            return Vector2.zero;
        float speed = speedMode == JoystickSpeedMode.Constant
            ? 1f
            : Mathf.InverseLerp(DeadZone, 1f, magnitude);
        return displacement / magnitude * speed * (isFocus ? FocusSpeedMultiplier : 1f);
    }

    private void OnValidate()
    {
        focusSpeedMultiplier = FocusSpeedMultiplier;
        normalToFocusThreshold = NormalToFocusThreshold;
        focusToNormalThreshold = FocusToNormalThreshold;
        deadZone = DeadZone;
    }
}
