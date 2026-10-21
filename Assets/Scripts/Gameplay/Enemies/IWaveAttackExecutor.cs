using UnityEngine;

public interface IWaveAttackExecutor
{
    bool CanPerformWaveAttack { get; }

    void SetWaveAttackControl(bool isControlled);

    bool TryFireAt(
        Vector3 targetPosition,
        EnemyBurstAttackSettings attackSettings);

    bool TryFireInDirection(
        Vector3 direction,
        EnemyBurstAttackSettings attackSettings);
}

public interface IEnemyBurstAttackExecutor : IWaveAttackExecutor
{
    EnemyBurstAttackSettings BurstAttackSettings { get; }
}

public interface IEnemyAttackSequenceExecutor
{
    void BeginAttackSequence(EnemyBurstAttackSettings settings);

    void EndAttackSequence(EnemyBurstAttackSettings settings);
}

// Entry-route attacks can begin an order epoch without resetting speed/aim schedules.
public interface IEnemySectorLaunchOrderSource
{
    void BeginSectorLaunchOrder(EnemyBurstAttackSettings settings);
}

public interface IEnemyLockedAimAttackExecutor
{
    Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings);
    bool TryFireInAimedDirection(Vector3 direction, EnemyBurstAttackSettings settings);
}

public interface IEnemyStationaryAttackExecutor
{
    bool TryFireStationary(Vector3 directionOrPoint, bool worldPoint, EnemyBurstAttackSettings settings);
}

// Circle acquires a separate direction from each emitter, then keeps those
// directions until the wave starts a new attack/burst acquisition.
public interface IEnemyEmissionAimAttackExecutor
{
    bool TryFireWithLockedEmissionAim(Vector3 targetPosition, EnemyBurstAttackSettings settings, bool refresh);
}

// A shot may enqueue several layout directions; the wave waits for their actual launch.
public interface IEnemyPendingEmissionExecutor
{
    bool HasPendingEmissions(EnemyBurstAttackSettings settings);
}

public interface IEnemyAttackActivity
{
    bool HasActiveAttack { get; }
}

public interface IEnemyBurstAttackSettingsOverrideReceiver
{
    void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings);
}

public interface IEnemyProjectileTypeOverrideReceiver
{
    void ApplyProjectileTypeOverride(EnemyProjectileType projectileType);
}

public interface IFormationAttackActivation
{
    void SetFormationAttackReady(bool isReady);
}

public interface IEnemyShootingSettingsReceiver
{
    // Null restores the enemy's own profile. The wave owns the override data.
    void SetWaveShootingSettings(EnemyShootingSettings settings);
}

public interface IEnemyAttackSettingsOverrideState
{
    // An explicit per-instance Attack Pattern takes priority over shared wave settings.
    bool HasAttackSettingsOverride { get; }
}

public interface IEnemyAttackPatternSettingsReceiver
{
    // Includes the collection mode so Multiple rotates streams without rotating the ship.
    void SetWaveAttackSettings(EnemyBurstAttackSettings settings);
}

// The firing orientation is independent of the visible Follow Pattern rotation.
public interface IEnemyAttackDirectionProvider
{
    Vector3 AttackForward { get; }
}

public interface IEnemyAttackAimTarget
{
    Vector3 AttackTargetPosition { get; }
}

public interface IEnemyEmissionOriginContext : IEnemyAttackAimTarget
{
    bool HasEmissionOriginPlayer { get; }
    Camera EmissionOriginCamera { get; }
}

public interface IEnemyEmissionOriginReceiver
{
    void BeginEmissionOriginAttack(EnemyBurstAttackSettings settings);
    bool PrepareEmissionOriginShot(EnemyBurstAttackSettings settings, bool burstStart, Vector3? capturedPlayer = null, Vector2? capturedOffsets = null, EnemyAttackNumericState numericOverride = null);
}

public interface IEnemyAttackAimReceiver
{
    void SetWaveAimTarget(IEnemyAttackAimTarget target);
}
