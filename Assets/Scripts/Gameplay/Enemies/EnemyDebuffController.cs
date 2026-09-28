using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class EnemyDebuffController : IInitializable, IDisposable
{
    private readonly EnemyManager enemyManager;
    private readonly EnemyTemperatureController temperatureController;
    private readonly EnemyDisintegrationSystem disintegrationSystem;
    private readonly EnemyPeriodicDamageSystem periodicDamageSystem;
    private readonly HashSet<ThresholdKey> triggeredThresholds = new();

    public EnemyDebuffController(
        EnemyManager enemyManager,
        EnemyTemperatureController temperatureController,
        EnemyDisintegrationSystem disintegrationSystem,
        EnemyPeriodicDamageSystem periodicDamageSystem)
    {
        this.enemyManager = enemyManager;
        this.temperatureController = temperatureController;
        this.disintegrationSystem = disintegrationSystem;
        this.periodicDamageSystem = periodicDamageSystem;
    }

    public event Action<EnemyDebuffThresholdEvent> OnThresholdReached;

    public void Initialize()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;

        if (periodicDamageSystem != null)
            periodicDamageSystem.OnTickDamagedHull += HandlePeriodicDamageTickDamagedHull;
    }

    public void Dispose()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        if (periodicDamageSystem != null)
            periodicDamageSystem.OnTickDamagedHull -= HandlePeriodicDamageTickDamagedHull;

        triggeredThresholds.Clear();
    }

    public bool Apply(
        Enemy enemy,
        EnemyDebuffApplication application,
        ParentShip owner)
    {
        if (application == null)
            return false;

        return Apply(
            enemy,
            application.Debuff,
            application.AmountPerHit,
            owner);
    }

    public bool Apply(
        Enemy enemy,
        EnemyDebuffConfig debuff,
        float amount,
        ParentShip owner)
    {
        if (enemy == null
            || enemy.isDead
            || debuff == null
            || amount <= 0f)
        {
            return false;
        }

        EnemyDebuffProgress progress = debuff switch
        {
            EnemySlowDebuffConfig slow => temperatureController != null
                ? temperatureController.ApplyCold(enemy, slow, amount)
                : EnemyDebuffProgress.None,
            EnemyHeatDebuffConfig heat => temperatureController != null
                ? temperatureController.ApplyHeat(enemy, heat, amount, owner)
                : EnemyDebuffProgress.None,
              EnemyDisintegrationDebuffConfig disintegration =>
                  disintegrationSystem != null
                      ? disintegrationSystem.ApplyCharge(
                          enemy,
                          amount,
                          disintegration.CreateProfile())
                      : EnemyDebuffProgress.None,
              EnemyHullDestructionDebuffConfig hullDestruction =>
                  ApplyHullDestruction(enemy, amount, hullDestruction),
              EnemyPeriodicDamageDebuffConfig periodicDamage => periodicDamageSystem != null
                  ? periodicDamageSystem.Apply(enemy, periodicDamage, owner)
                  : EnemyDebuffProgress.None,
              _ => EnemyDebuffProgress.None
          };

        if (!progress.IsValid)
            return false;

        TryRaiseThreshold(enemy, debuff, progress);
        return true;
    }

    private static EnemyDebuffProgress ApplyHullDestruction(
        Enemy enemy,
        float amount,
        EnemyHullDestructionDebuffConfig debuff)
    {
        float previousValue = enemy.HullDamageVulnerabilityPercent;
        float currentValue = enemy.ApplyHullDamageVulnerability(
            amount,
            debuff.MaximumBonusPercent);
        return new EnemyDebuffProgress(
            true,
            previousValue,
            currentValue,
            debuff.MaximumBonusPercent);
    }

    private void HandlePeriodicDamageTickDamagedHull(
        Enemy enemy,
        ParentShip owner,
        IReadOnlyList<EnemyDebuffApplication> tickDebuffs)
    {
        if (tickDebuffs == null)
            return;

        for (int index = 0; index < tickDebuffs.Count; index++)
            Apply(enemy, tickDebuffs[index], owner);
    }

    private void TryRaiseThreshold(
        Enemy enemy,
        EnemyDebuffConfig debuff,
        EnemyDebuffProgress progress)
    {
        if (!debuff.TryGetThresholdValue(
                progress.MaximumValue,
                out float threshold)
            || progress.PreviousValue >= threshold
            || progress.CurrentValue < threshold)
        {
            return;
        }

        ThresholdKey key = new(enemy, debuff);
        if (debuff.TriggerOncePerEnemy && !triggeredThresholds.Add(key))
            return;

        EnemyDebuffThresholdEvent thresholdEvent = new(
            enemy,
            debuff,
            progress.CurrentValue,
            threshold);
        OnThresholdReached?.Invoke(thresholdEvent);
        enemy.NotifyDebuffThresholdReached(thresholdEvent);

        switch (debuff.ThresholdAction)
        {
            case EnemyDebuffThresholdAction.BeginBurning:
                enemy.BeginBurning();
                break;
            case EnemyDebuffThresholdAction.DestroyEnemy:
                enemy.Dying();
                break;
        }
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || triggeredThresholds.Count == 0)
            return;

        triggeredThresholds.RemoveWhere(key => key.IsFor(enemy));
    }

    private readonly struct ThresholdKey : IEquatable<ThresholdKey>
    {
        private readonly Enemy enemy;
        private readonly EnemyDebuffConfig debuff;

        public ThresholdKey(Enemy enemy, EnemyDebuffConfig debuff)
        {
            this.enemy = enemy;
            this.debuff = debuff;
        }

        public bool Equals(ThresholdKey other)
        {
            return ReferenceEquals(enemy, other.enemy)
                && ReferenceEquals(debuff, other.debuff);
        }

        public override bool Equals(object obj)
        {
            return obj is ThresholdKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int enemyHash = enemy != null ? enemy.GetInstanceID() : 0;
                int debuffHash = debuff != null ? debuff.GetInstanceID() : 0;
                return (enemyHash * 397) ^ debuffHash;
            }
        }

        public bool IsFor(Enemy candidate)
        {
            return ReferenceEquals(enemy, candidate);
        }
    }
}
