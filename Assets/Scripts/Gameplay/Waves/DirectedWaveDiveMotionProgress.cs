using UnityEngine;

// Governs time and displacement only. MoveEnemy still owns the curve, path and
// standoff calculation. A capped curve sample is completed before advancing
// curve time, so steep sections and reversals cannot be skipped while catching up.
public struct DirectedWaveDiveMotionProgress
{
    private readonly float minimumDuration;
    private float elapsed, pendingElapsed, normalizedTime;
    private bool pending;
    public float Duration { get; }
    public Vector3 Position { get; private set; }
    public bool IsComplete => !pending && elapsed >= Duration;

    public DirectedWaveDiveMotionProgress(Vector3 from, float distance, float speed, float minimumDuration)
    {
        this.minimumDuration = Mathf.Max(0f, minimumDuration);
        Duration = distance > 0f ? Mathf.Max(distance / Mathf.Max(.01f, speed), this.minimumDuration) : 0f;
        Position = from;
        elapsed = pendingElapsed = normalizedTime = 0f;
        pending = false;
    }

    public float PrepareStep(float deltaTime, float movementMultiplier)
    {
        if (pending) return normalizedTime;
        float delta = Mathf.Max(0f, deltaTime) * Mathf.Max(0f, movementMultiplier);
        if (minimumDuration > 0f)
            delta = Mathf.Min(delta, Mathf.Max(0f, deltaTime) * (Duration / minimumDuration));
        pendingElapsed = Mathf.Min(Duration, elapsed + delta);
        normalizedTime = Duration > 0f ? Mathf.Clamp01(pendingElapsed / Duration) : 1f;
        pending = true;
        return normalizedTime;
    }

    public Vector3 ApplyStep(Vector3 desiredPosition, float deltaTime, float maximumSpeed)
    {
        MovePosition(desiredPosition, deltaTime, maximumSpeed);
        // Vector3 == has a tolerance which would allow a final small teleport.
        if (Position.Equals(desiredPosition))
        {
            elapsed = pendingElapsed;
            pending = false;
        }
        return Position;
    }

    public Vector3 MovePosition(Vector3 target, float deltaTime, float maximumSpeed)
    {
        Position = maximumSpeed > 0f
            ? Vector3.MoveTowards(Position, target, maximumSpeed * Mathf.Max(0f, deltaTime))
            : target;
        return Position;
    }
}
