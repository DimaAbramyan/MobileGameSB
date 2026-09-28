using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class EnemyPeriodicDamageSystem : IInitializable, ITickable, IDisposable
{
    private readonly EnemyManager enemyManager;
    private readonly LazyInject<DealDamageManager> dealDamageManager;
    private readonly Dictionary<Enemy, PeriodicDamageState> states = new();
    private readonly List<Enemy> trackedEnemies = new();

    public EnemyPeriodicDamageSystem(
        EnemyManager enemyManager,
        LazyInject<DealDamageManager> dealDamageManager)
    {
        this.enemyManager = enemyManager;
        this.dealDamageManager = dealDamageManager;
    }

    public event Action<Enemy, ParentShip, IReadOnlyList<EnemyDebuffApplication>>
        OnTickDamagedHull;

    public void Initialize()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;
    }

    public void Dispose()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        states.Clear();
        trackedEnemies.Clear();
    }

    public EnemyDebuffProgress Apply(
        Enemy enemy,
        EnemyPeriodicDamageDebuffConfig config,
        ParentShip owner)
    {
        if (Time.timeScale <= 0f
            || enemy == null
            || enemy.isDead
            || config == null
            || config.DamagePerTick <= 0f)
        {
            return EnemyDebuffProgress.None;
        }

        float currentTime = Time.time;
        float previousRemainingDuration = 0f;
        if (!states.TryGetValue(enemy, out PeriodicDamageState state))
        {
            state = new PeriodicDamageState
            {
                Index = trackedEnemies.Count,
                NextTickTime = currentTime + config.TickInterval
            };
            trackedEnemies.Add(enemy);
        }
        else
        {
            previousRemainingDuration = Mathf.Max(
                0f,
                state.ExpiresAt - currentTime);
        }

        state.Owner = owner;
        state.Config = config;
        state.ExpiresAt = currentTime + config.Duration;
        states[enemy] = state;

        return new EnemyDebuffProgress(
            true,
            previousRemainingDuration,
            config.Duration,
            config.Duration);
    }

    public void Tick()
    {
        if (Time.timeScale <= 0f || trackedEnemies.Count == 0)
            return;

        float currentTime = Time.time;
        for (int index = trackedEnemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = trackedEnemies[index];
            if (enemy == null
                || enemy.isDead
                || !states.TryGetValue(enemy, out PeriodicDamageState state)
                || state.Config == null
                || currentTime >= state.ExpiresAt)
            {
                RemoveStateAt(index);
                continue;
            }

            if (currentTime < state.NextTickTime)
                continue;

            state.NextTickTime = currentTime + state.Config.TickInterval;
            states[enemy] = state;

            if (dealDamageManager == null)
                continue;

            EnemyDamageResult result = dealDamageManager.Value.DealDamage(
                enemy,
                state.Owner,
                state.Config.DamagePerTick,
                state.Config.DamageType,
                state.Config.BypassesEnemyShield);
            if (result.DidDamageHull)
            {
                OnTickDamagedHull?.Invoke(
                    enemy,
                    state.Owner,
                    state.Config.TickDebuffs);
            }
        }
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || !states.TryGetValue(enemy, out PeriodicDamageState state))
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
            if (states.TryGetValue(lastEnemy, out PeriodicDamageState lastState))
            {
                lastState.Index = index;
                states[lastEnemy] = lastState;
            }
        }

        trackedEnemies.RemoveAt(lastIndex);
        states.Remove(removedEnemy);
    }

    private struct PeriodicDamageState
    {
        public int Index;
        public float ExpiresAt;
        public float NextTickTime;
        public ParentShip Owner;
        public EnemyPeriodicDamageDebuffConfig Config;
    }
}
