using UnityEngine;

// Owned by one executor. Legacy rotation components delegate to this runtime.
public sealed class EnemyAttackRotation
{
    private Transform target;
    private EnemyRotationSettings settings;
    private Quaternion initialLocalRotation;
    private float elapsed;
    private bool running;
    public bool IsComplete { get; private set; }

    public void Bind(Transform newTarget, EnemyRotationSettings newSettings)
    {
        if (target != newTarget)
        {
            target = newTarget;
            initialLocalRotation = target.localRotation;
        }
        settings = newSettings;
    }

    public void Restart()
    {
        if (target == null || settings == null)
            return;
        elapsed = 0f;
        IsComplete = !settings.HasMotion;
        running = !IsComplete;
        target.localRotation = initialLocalRotation;
    }

    public void Pause() => running = false;
    public void RestoreInitialPose()
    {
        if (target != null)
            target.localRotation = initialLocalRotation;
        running = false;
    }
    public void Resume() => running = settings != null && !IsComplete;

    public void Tick(float deltaTime)
    {
        if (!running || target == null || settings == null)
            return;
        if (!settings.HasMotion)
        {
            IsComplete = true;
            running = false;
            return;
        }
        elapsed += Mathf.Max(0f, deltaTime);
        if (settings.RotationMode == FourWayEnemyRotationMode.ByAngle && elapsed >= settings.Duration)
        {
            elapsed = settings.Duration;
            IsComplete = true;
            running = false;
        }
        target.localRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, settings.EvaluateAngle(elapsed));
    }
}
