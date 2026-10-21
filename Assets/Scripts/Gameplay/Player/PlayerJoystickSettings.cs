using UnityEngine;

public enum JoystickSpeedMode
{
    Analog,
    Constant
}

[CreateAssetMenu(menuName = "Player/Joystick Settings")]
public sealed class PlayerJoystickSettings : ScriptableObject
{
    [SerializeField, Min(1f), Tooltip("Отклонение ручки для полной скорости, в локальных единицах UI. Масштаб Canvas учитывается автоматически.")]
    private float movementRadius = 75f;
    [SerializeField, Range(0f, 0.95f), Tooltip("Доля Movement Radius без движения. Например, 0.08 — первые 8% радиуса.")]
    private float deadZone = 0.08f;
    [SerializeField, Tooltip("Для Joystick и Joystick Focus. Analog: скорость растёт от края Dead Zone до полной. Constant: вне Dead Zone сразу полная скорость текущего режима. Focus использует свой Dead Zone и множитель скорости.")]
    private JoystickSpeedMode speedMode;

    public float MovementRadius => Mathf.Max(1f, movementRadius);
    public float DeadZone => Mathf.Clamp(deadZone, 0f, 0.95f);
    public JoystickSpeedMode SpeedMode => speedMode;

    public Vector2 EvaluateInput(Vector2 localDisplacement)
    {
        Vector2 normalized = Vector2.ClampMagnitude(localDisplacement / MovementRadius, 1f);
        float magnitude = normalized.magnitude;
        if (magnitude <= DeadZone)
            return Vector2.zero;

        float speed = speedMode == JoystickSpeedMode.Constant
            ? 1f
            : Mathf.InverseLerp(DeadZone, 1f, magnitude);
        return normalized / magnitude * speed;
    }
}
