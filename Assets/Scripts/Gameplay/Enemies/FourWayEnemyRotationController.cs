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

    private FourWayEnemy fourWayEnemy;
    private Quaternion initialLocalRotation;
    private float elapsedRotationTime;
    private bool initialRotationCaptured;
    private bool isRotationRunning;
    private bool isRotationComplete;

    public bool IsRotationComplete => isRotationComplete;

    public void ApplyOverride(DirectedWaveFourWayRotationOverride settings)
    {
        if (settings == null)
            return;

        direction = settings.Direction;
        rotationMode = settings.RotationMode;
        rotationSpeedDegreesPerSecond = settings.RotationSpeedDegreesPerSecond;
        rotationProgressCurve = settings.CreateRotationProgressCurve();
        rotationAngle = settings.RotationAngle;
        rotationFromAngle = settings.RotationFromAngle;
        resetRotationOnEnable = settings.ResetRotationOnEnable;
        RestartRotation();
    }

    private Transform RotationTarget => rotationTarget != null ? rotationTarget : transform;

    private void Awake()
    {
        CacheDependencies();
        CaptureInitialRotation();
    }

    private void OnEnable()
    {
        CacheDependencies();

        if (!initialRotationCaptured)
            CaptureInitialRotation();

        if (resetRotationOnEnable)
            RestartRotation();
        else
            ResumeRotation();
    }

    private void Update()
    {
        if (!isRotationRunning || isRotationComplete)
            return;

        if (!HasRotationMotion())
        {
            CompleteRotation();
            return;
        }

        elapsedRotationTime += Time.deltaTime;

        if (rotationMode == FourWayEnemyRotationMode.ByAngle)
        {
            float duration = GetRotationDuration();
            if (elapsedRotationTime >= duration)
            {
                elapsedRotationTime = duration;
                ApplyRotation(GetRotationProgress(1f) * rotationAngle);
                CompleteRotation();
                return;
            }
        }

        ApplyRotation(GetRotationAngle());
    }

    public void RestartRotation()
    {
        if (!initialRotationCaptured)
            CaptureInitialRotation();

        elapsedRotationTime = 0f;
        isRotationComplete = !HasRotationMotion();
        isRotationRunning = !isRotationComplete;
        RotationTarget.localRotation = initialLocalRotation;
    }

    public void PauseRotation()
    {
        isRotationRunning = false;
    }

    public void ResumeRotation()
    {
        if (!isRotationComplete)
            isRotationRunning = true;
    }

    private void OnValidate()
    {
        rotationSpeedDegreesPerSecond = Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
        if (rotationProgressCurve == null || rotationProgressCurve.length == 0)
            rotationProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void CacheDependencies()
    {
        if (fourWayEnemy == null)
            TryGetComponent(out fourWayEnemy);

        if (fourWayEnemy != null)
            fourWayEnemy.SetProjectileDirectionTransform(RotationTarget);
    }

    private void CaptureInitialRotation()
    {
        initialLocalRotation = RotationTarget.localRotation;
        initialRotationCaptured = true;
    }

    private float GetRotationDuration()
    {
        float angle = rotationMode == FourWayEnemyRotationMode.Continuous
            ? 360f
            : Mathf.Abs(rotationAngle);
        return angle / rotationSpeedDegreesPerSecond;
    }

    private float GetRotationAngle()
    {
        if (rotationMode == FourWayEnemyRotationMode.ByAngle)
        {
            float normalizedTime = Mathf.Clamp01(elapsedRotationTime / GetRotationDuration());
            return GetRotationProgress(normalizedTime) * rotationAngle;
        }

        if (rotationMode == FourWayEnemyRotationMode.PingPongByAngle)
            return GetPingPongRotationAngle();

        float turnDuration = GetRotationDuration();
        int completedTurns = Mathf.FloorToInt(elapsedRotationTime / turnDuration);
        float turnProgress =
            (elapsedRotationTime - completedTurns * turnDuration) / turnDuration;
        return (completedTurns + GetRotationProgress(turnProgress)) * 360f;
    }

    private float GetPingPongRotationAngle()
    {
        float initialPassDuration = Mathf.Abs(rotationFromAngle)
            / rotationSpeedDegreesPerSecond;
        if (initialPassDuration > 0f
            && elapsedRotationTime < initialPassDuration)
        {
            float initialProgress = GetRotationProgress(
                elapsedRotationTime / initialPassDuration);
            return Mathf.LerpUnclamped(
                0f,
                rotationFromAngle,
                initialProgress);
        }

        float passAngle = rotationAngle - rotationFromAngle;
        float passDuration = Mathf.Abs(passAngle)
            / rotationSpeedDegreesPerSecond;
        if (passDuration <= 0f)
            return rotationFromAngle;

        float phaseTime = Mathf.Repeat(
            elapsedRotationTime - initialPassDuration,
            passDuration * 2f);
        bool returningToStart = phaseTime >= passDuration;
        float normalizedPassTime = returningToStart
            ? (phaseTime - passDuration) / passDuration
            : phaseTime / passDuration;
        float progress = GetRotationProgress(normalizedPassTime);
        return returningToStart
            ? Mathf.LerpUnclamped(
                rotationAngle,
                rotationFromAngle,
                progress)
            : Mathf.LerpUnclamped(
                rotationFromAngle,
                rotationAngle,
                progress);
    }

    private bool HasRotationMotion()
    {
        if (rotationMode == FourWayEnemyRotationMode.Continuous)
            return true;

        if (rotationMode == FourWayEnemyRotationMode.ByAngle)
            return !Mathf.Approximately(rotationAngle, 0f);

        return !Mathf.Approximately(rotationFromAngle, 0f)
            || !Mathf.Approximately(rotationAngle, rotationFromAngle);
    }

    private float GetRotationProgress(float normalizedTime)
    {
        if (rotationProgressCurve == null || rotationProgressCurve.length == 0)
            return normalizedTime;

        return Mathf.Clamp01(rotationProgressCurve.Evaluate(normalizedTime));
    }

    private void ApplyRotation(float angle)
    {
        float directionMultiplier = direction == FourWayEnemyRotationDirection.Clockwise ? -1f : 1f;
        RotationTarget.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, angle * directionMultiplier);
    }

    private void CompleteRotation()
    {
        isRotationComplete = true;
        isRotationRunning = false;
    }
}
