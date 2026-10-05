using UnityEngine;

[System.Serializable]
public class EnemyRotationSettings
{
    [SerializeField] private FourWayEnemyRotationDirection direction = FourWayEnemyRotationDirection.Clockwise;
    [SerializeField] private FourWayEnemyRotationMode rotationMode = FourWayEnemyRotationMode.Continuous;
    [SerializeField, Min(0.01f)] private float rotationSpeedDegreesPerSecond = 90f;
    [SerializeField] private AnimationCurve rotationProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float rotationAngle = 360f;
    [SerializeField] private float rotationFromAngle;
    [SerializeField] private bool resetRotationOnEnable = true;

    public FourWayEnemyRotationDirection Direction => direction;
    public FourWayEnemyRotationMode RotationMode => rotationMode;
    public float RotationSpeedDegreesPerSecond => Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
    public float RotationAngle => rotationAngle;
    public float RotationFromAngle => rotationFromAngle;
    public bool ResetRotationOnEnable => resetRotationOnEnable;
    public float Duration => Mathf.Abs(rotationAngle) / RotationSpeedDegreesPerSecond;
    public bool HasMotion => rotationMode switch
    {
        FourWayEnemyRotationMode.Continuous => true,
        FourWayEnemyRotationMode.ByAngle => !Mathf.Approximately(rotationAngle, 0f),
        _ => !Mathf.Approximately(rotationFromAngle, 0f) || !Mathf.Approximately(rotationAngle, rotationFromAngle)
    };

    // Shared by the runtime and the Scene view preview.
    public float EvaluateAngle(float elapsed)
    {
        elapsed = Mathf.Max(0f, elapsed);
        float angle;
        if (rotationMode == FourWayEnemyRotationMode.ByAngle)
            angle = Progress(Duration > 0f ? Mathf.Clamp01(elapsed / Duration) : 1f) * rotationAngle;
        else if (rotationMode == FourWayEnemyRotationMode.PingPongByAngle)
            angle = EvaluatePingPong(elapsed);
        else
        {
            float turnDuration = 360f / RotationSpeedDegreesPerSecond;
            float turns = Mathf.Floor(elapsed / turnDuration);
            angle = (turns + Progress((elapsed - turns * turnDuration) / turnDuration)) * 360f;
        }
        return angle * (direction == FourWayEnemyRotationDirection.Clockwise ? -1f : 1f);
    }

    private float EvaluatePingPong(float elapsed)
    {
        float initialDuration = Mathf.Abs(rotationFromAngle) / RotationSpeedDegreesPerSecond;
        if (initialDuration > 0f && elapsed < initialDuration)
            return Mathf.LerpUnclamped(0f, rotationFromAngle, Progress(elapsed / initialDuration));
        float passDuration = Mathf.Abs(rotationAngle - rotationFromAngle) / RotationSpeedDegreesPerSecond;
        if (passDuration <= 0f)
            return rotationFromAngle;
        float phase = Mathf.Repeat(elapsed - initialDuration, passDuration * 2f);
        bool returning = phase >= passDuration;
        float progress = Progress((returning ? phase - passDuration : phase) / passDuration);
        return returning ? Mathf.LerpUnclamped(rotationAngle, rotationFromAngle, progress)
            : Mathf.LerpUnclamped(rotationFromAngle, rotationAngle, progress);
    }

    private float Progress(float time) => rotationProgressCurve != null && rotationProgressCurve.length > 0
        ? Mathf.Clamp01(rotationProgressCurve.Evaluate(time)) : time;

    public void Configure(FourWayEnemyRotationDirection newDirection, FourWayEnemyRotationMode mode,
        float speed, AnimationCurve curve, float angle, float fromAngle, bool resetOnEnable)
    {
        direction = newDirection;
        rotationMode = mode;
        rotationSpeedDegreesPerSecond = speed;
        rotationProgressCurve = curve;
        rotationAngle = angle;
        rotationFromAngle = fromAngle;
        resetRotationOnEnable = resetOnEnable;
        Validate();
    }

    public AnimationCurve CreateRotationProgressCurve()
    {
        Validate();
        return new AnimationCurve(rotationProgressCurve.keys)
        {
            preWrapMode = rotationProgressCurve.preWrapMode,
            postWrapMode = rotationProgressCurve.postWrapMode
        };
    }

    public void CopyFrom(EnemyRotationSettings source)
    {
        if (source == null || ReferenceEquals(this, source))
            return;
        source.Validate();
        Configure(source.direction, source.rotationMode, source.rotationSpeedDegreesPerSecond,
            source.CreateRotationProgressCurve(), source.rotationAngle, source.rotationFromAngle, source.resetRotationOnEnable);
    }

    public void Validate()
    {
        rotationSpeedDegreesPerSecond = Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
        if (rotationProgressCurve == null || rotationProgressCurve.length == 0)
            rotationProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}
