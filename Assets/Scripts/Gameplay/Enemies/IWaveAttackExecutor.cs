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

public interface IEnemyLockedAimAttackExecutor
{
    Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings);
    bool TryFireInAimedDirection(Vector3 direction, EnemyBurstAttackSettings settings);
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

public interface IEnemyAttackAimReceiver
{
    void SetWaveAimTarget(IEnemyAttackAimTarget target);
}
