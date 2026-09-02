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

public interface IEnemyBurstAttackSettingsOverrideReceiver
{
    void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings);
}

public interface IFormationAttackActivation
{
    void SetFormationAttackReady(bool isReady);
}
