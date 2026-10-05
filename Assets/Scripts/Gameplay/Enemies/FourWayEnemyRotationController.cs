using UnityEngine;

public enum FourWayEnemyRotationDirection
{
    Clockwise,
    CounterClockwise
}

public enum FourWayEnemyRotationMode
{
    Continuous,
    ByAngle,
    PingPongByAngle
}

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(FourWayEnemy))]
public sealed class FourWayEnemyRotationController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("Leave empty to rotate the whole enemy. Assign a child pivot to rotate only the weapon directions.")]
    private Transform rotationTarget;

    [Header("Rotation")]
    [SerializeField] private FourWayEnemyRotationDirection direction = FourWayEnemyRotationDirection.Clockwise;
    [SerializeField] private FourWayEnemyRotationMode rotationMode = FourWayEnemyRotationMode.Continuous;
    [SerializeField, Min(0.01f), Tooltip("Average rotation speed in degrees per second.")]
    private float rotationSpeedDegreesPerSecond = 90f;
    [SerializeField, Tooltip("X is normalized turn time and Y is normalized turn progress. The curve repeats for every full turn in Continuous mode and for each pass in Ping Pong By Angle mode.")]
    private AnimationCurve rotationProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("The relative target angle for By Angle mode, or the upper bound for Ping Pong By Angle mode.")]
    private float rotationAngle = 360f;
    [SerializeField, Tooltip("The first target and lower bound for Ping Pong By Angle mode. The enemy moves to this angle before moving to To Angle.")]
    private float rotationFromAngle;
    [SerializeField, Tooltip("Restores the starting rotation whenever this enemy is enabled again.")]
    private bool resetRotationOnEnable = true;

    private SimpleEnemyAttackExecutor Executor => GetComponent<FourWayEnemy>().EnsureExecutorConfigured();
    public Transform LegacyRotationTarget => rotationTarget;
    public bool IsRotationComplete => Executor.IsRotationComplete;

    public EnemyRotationSettings CreateLegacySettings()
    {
        var settings = new EnemyRotationSettings();
        settings.Configure(direction, rotationMode, rotationSpeedDegreesPerSecond,
            rotationProgressCurve, rotationAngle, rotationFromAngle, resetRotationOnEnable);
        return settings;
    }

    // Compatibility API. Rotation itself is owned by SimpleEnemyAttackExecutor.
    public void ApplyOverride(DirectedWaveFourWayRotationOverride settings) => Executor.ApplyRotationOverride(settings);
    public void RestartRotation() => Executor.RestartRotation();
    public void PauseRotation() => Executor.PauseRotation();
    public void ResumeRotation() => Executor.ResumeRotation();
}
