using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public readonly struct EnemyDisintegrationProfile
{
    public EnemyDisintegrationProfile(
        float chargeDecayDelay,
        float chargeDecayPerSecond)
    {
        ChargeDecayDelay = Mathf.Max(0f, chargeDecayDelay);
        ChargeDecayPerSecond = Mathf.Max(0f, chargeDecayPerSecond);
    }

    public float ChargeDecayDelay { get; }
    public float ChargeDecayPerSecond { get; }
}

public sealed class EnemyDisintegrationSystem : IInitializable, ITickable, IDisposable
{
    private const float EmptyChargeThreshold = 0.0001f;

    private readonly EnemyManager enemyManager;
    private readonly Dictionary<Enemy, ChargeState> states = new();
    private readonly List<Enemy> trackedEnemies = new();

    public EnemyDisintegrationSystem(EnemyManager enemyManager)
    {
        this.enemyManager = enemyManager;
    }

    public void Initialize()
    {
        enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;
    }

    public void Dispose()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        states.Clear();
        trackedEnemies.Clear();
    }

    public void Tick()
    {
        if (Time.timeScale <= 0f || trackedEnemies.Count == 0)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float currentTime = Time.time;
        for (int index = trackedEnemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = trackedEnemies[index];
            if (enemy == null
                || enemy.isDead
                || !states.TryGetValue(enemy, out ChargeState state))
            {
                RemoveStateAt(index);
                continue;
            }

            if (currentTime - state.LastHitTime < state.Profile.ChargeDecayDelay)
                continue;

            state.Charge = Mathf.Max(
                0f,
                state.Charge - state.Profile.ChargeDecayPerSecond * deltaTime);

            if (state.Charge <= EmptyChargeThreshold)
            {
                RemoveStateAt(index);
                continue;
            }

            states[enemy] = state;
        }
    }

    public void ApplyCharge(
        Enemy enemy,
        float charge,
        EnemyDisintegrationProfile profile)
    {
        if (Time.timeScale <= 0f
            || enemy == null
            || enemy.isDead
            || charge <= 0f)
        {
            return;
        }

        if (!states.TryGetValue(enemy, out ChargeState state))
        {
            state = new ChargeState
            {
                Index = trackedEnemies.Count
            };
            trackedEnemies.Add(enemy);
        }

        state.Charge += charge;
        state.LastHitTime = Time.time;
        state.Profile = profile;
        states[enemy] = state;

        if (state.Charge < enemy._currentHealth)
            return;

        RemoveStateAt(state.Index);
        enemy.Dying();
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || !states.TryGetValue(enemy, out ChargeState state))
            return;

        RemoveStateAt(state.Index);
    }

    private void RemoveStateAt(int index)
    {
        if (index < 0 || index >= trackedEnemies.Count)
            return;

        Enemy removedEnemy = trackedEnemies[index];
        int lastIndex = trackedEnemies.Count - 1;
        Enemy lastEnemy = trackedEnemies[lastIndex];

        if (index != lastIndex)
        {
            trackedEnemies[index] = lastEnemy;
            if (states.TryGetValue(lastEnemy, out ChargeState lastState))
            {
                lastState.Index = index;
                states[lastEnemy] = lastState;
            }
        }

        trackedEnemies.RemoveAt(lastIndex);
        states.Remove(removedEnemy);
    }

    private struct ChargeState
    {
        public int Index;
        public float Charge;
        public float LastHitTime;
        public EnemyDisintegrationProfile Profile;
    }
}
