using UnityEngine;

public enum EnemyStationaryDirectionMode
{
    Angle,
    [InspectorName("World Point")] WorldPoint
}

[System.Serializable]
public sealed class EnemyStationaryFireSettings
{
    [SerializeField] private EnemyStationaryDirectionMode directionMode;
    [SerializeField, Tooltip("World angle: 0 degrees up, 90 left, 180 down, 270 right. Fan and Layout Rotation are applied after this base direction.")]
    private float angle;
    [SerializeField, Tooltip("Fixed world target. Each emitter computes its direction from its own spawn position. This never tracks the player.")]
    private Vector2 worldPoint;
    [SerializeField, HideInInspector] private bool legacyForward;

    public EnemyStationaryDirectionMode DirectionMode => directionMode;
    public Vector2 WorldPoint => worldPoint;
    public bool PreservesLegacyForward => legacyForward;
    public Vector3 Direction => Quaternion.Euler(0f, 0f, angle) * Vector3.up;

    public void ConfigureAngle(float degrees) { directionMode = EnemyStationaryDirectionMode.Angle; angle = degrees; legacyForward = false; }
    public void ConfigureWorldPoint(Vector2 point) { directionMode = EnemyStationaryDirectionMode.WorldPoint; worldPoint = point; legacyForward = false; }
    public void PreserveLegacyForward() => legacyForward = true;
    public void CopyFrom(EnemyStationaryFireSettings source)
    {
        if (source == null || ReferenceEquals(this, source)) return;
        directionMode = source.directionMode; angle = source.angle; worldPoint = source.worldPoint; legacyForward = source.legacyForward;
    }

    public bool TryFire(IWaveAttackExecutor executor, EnemyBurstAttackSettings profile, Vector3 forward)
    {
        if (legacyForward) return executor.TryFireInDirection(forward, profile);
        if (executor is IEnemyStationaryAttackExecutor stationary)
            return stationary.TryFireStationary(directionMode == EnemyStationaryDirectionMode.WorldPoint ? (Vector3)worldPoint : Direction,
                directionMode == EnemyStationaryDirectionMode.WorldPoint, profile);
        return directionMode == EnemyStationaryDirectionMode.WorldPoint
            ? executor.TryFireAt(worldPoint, profile) : executor.TryFireInDirection(Direction, profile);
    }
}
