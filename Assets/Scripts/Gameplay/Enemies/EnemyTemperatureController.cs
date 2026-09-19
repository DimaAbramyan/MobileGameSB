using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class EnemyTemperatureController : IInitializable, ITickable, IDisposable
{
    private const float FullTemperature = 100f;
    private const float EmptyTemperatureThreshold = 0.001f;

    private readonly EnemyManager enemyManager;
    private readonly EnemyHeatSystem heatSystem;
    private readonly LazyInject<DealDamageManager> dealDamageManager;
    private readonly Dictionary<Enemy, TemperatureState> states = new();
    private readonly List<Enemy> trackedEnemies = new();

    public EnemyTemperatureController(
        EnemyManager enemyManager,
        EnemyHeatSystem heatSystem,
        LazyInject<DealDamageManager> dealDamageManager)
    {
        this.enemyManager = enemyManager;
        this.heatSystem = heatSystem;
        this.dealDamageManager = dealDamageManager;
    }

    public void Initialize()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;

        if (heatSystem != null)
            heatSystem.OnHeatTransferred += HandleHeatTransferred;
    }

    public void Dispose()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        if (heatSystem != null)
            heatSystem.OnHeatTransferred -= HandleHeatTransferred;

        for (int index = 0; index < trackedEnemies.Count; index++)
        {
            Enemy enemy = trackedEnemies[index];
            if (enemy != null && !enemy.isDead)
                enemy.StopBurning();
        }

        states.Clear();
        trackedEnemies.Clear();
    }

    public EnemyDebuffProgress ApplyHeat(
        Enemy enemy,
        EnemyHeatDebuffConfig config,
        float amount,
        ParentShip owner)
    {
        if (enemy == null
            || enemy.isDead
            || config == null
            || amount <= 0f
            || heatSystem == null)
        {
            return EnemyDebuffProgress.None;
        }

        TemperatureState state = GetOrCreateState(enemy);
        float remainingHeat = amount - enemy.RemoveMovementSlow(amount);
        EnemyDebuffProgress progress = EnemyDebuffProgress.None;
        if (remainingHeat > 0f)
        {
            progress = heatSystem.ApplyHeat(
                enemy,
                remainingHeat,
                config.CreateProfile(owner));
        }

        state.Temperature = heatSystem.GetHeatPercent(enemy);
        state.LastManipulationTime = Time.time;
        state.NormalizationDelay = config.CoolingDelay;
        state.NormalizationPercentPerSecond = config.CoolingPercentPerSecond;
        state = RefreshBurning(enemy, state, config, owner);
        enemy.SetTemperaturePercent(state.Temperature);
        states[enemy] = state;
        return progress;
    }

    public EnemyDebuffProgress ApplyCold(
        Enemy enemy,
        EnemySlowDebuffConfig config,
        float amount)
    {
        if (enemy == null
            || enemy.isDead
            || config == null
            || amount <= 0f)
        {
            return EnemyDebuffProgress.None;
        }

        TemperatureState state = GetOrCreateState(enemy);
        float cooledHeat = heatSystem != null
            ? heatSystem.CoolHeat(enemy, amount)
            : 0f;
        float remainingCold = Mathf.Max(0f, amount - cooledHeat);
        EnemyDebuffProgress progress = EnemyDebuffProgress.None;
        if (remainingCold > 0f)
        {
            float previousSlow = enemy.MovementSlowPercent;
            enemy.ApplyMovementSlow(remainingCold, config.MaximumSlowPercent);
            progress = new EnemyDebuffProgress(
                true,
                previousSlow,
                enemy.MovementSlowPercent,
                config.MaximumSlowPercent);
        }

        state.Temperature = heatSystem != null
            ? heatSystem.GetHeatPercent(enemy)
            : 0f;
        if (state.Temperature <= EmptyTemperatureThreshold)
            state.Temperature = -enemy.MovementSlowPercent;

        state.LastManipulationTime = Time.time;
        state.NormalizationDelay = config.NormalizationDelay;
        state.NormalizationPercentPerSecond =
            config.NormalizationPercentPerSecond;
        enemy.SetTemperaturePercent(state.Temperature);
        states[enemy] = state;
        return progress;
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
                || !states.TryGetValue(enemy, out TemperatureState state))
            {
                RemoveStateAt(index);
                continue;
            }

            if (state.IsBurning)
            {
                if (currentTime >= state.BurningEndTime)
                {
                    state.IsBurning = false;
                    enemy.StopBurning();
                }
                else if (currentTime >= state.NextBurnDamageTime)
                {
                    if (state.BurningDamagePerTick > 0f
                        && dealDamageManager != null)
                    {
                        dealDamageManager.Value.DealDamage(
                            enemy,
                            state.BurningOwner,
                            state.BurningDamagePerTick);
                    }

                    state.NextBurnDamageTime = currentTime
                        + state.BurningDamageInterval;
                    if (enemy.isDead || !states.ContainsKey(enemy))
                        continue;
                }
            }

            if (currentTime - state.LastManipulationTime
                >= state.NormalizationDelay)
            {
                NormalizeTemperature(enemy, ref state, deltaTime);
            }

            if (!state.IsBurning
                && Mathf.Abs(state.Temperature) <= EmptyTemperatureThreshold)
            {
                enemy.SetTemperaturePercent(0f);
                RemoveStateAt(index);
                continue;
            }

            enemy.SetTemperaturePercent(state.Temperature);
            states[enemy] = state;
        }
    }

    private TemperatureState GetOrCreateState(Enemy enemy)
    {
        if (states.TryGetValue(enemy, out TemperatureState state))
            return state;

        state = new TemperatureState
        {
            Index = trackedEnemies.Count,
            Temperature = enemy.TemperaturePercent
        };
        trackedEnemies.Add(enemy);
        return state;
    }

    private TemperatureState RefreshBurning(
        Enemy enemy,
        TemperatureState state,
        EnemyHeatDebuffConfig config,
        ParentShip owner)
    {
        if (state.Temperature < FullTemperature)
            return state;

        state.IsBurning = config.BurningDuration > 0f;
        state.BurningEndTime = Time.time + config.BurningDuration;
        state.NextBurnDamageTime = Time.time;
        state.BurningDamagePerTick = config.BurningDamagePerTick;
        state.BurningDamageInterval = config.BurningDamageInterval;
        state.BurningOwner = owner;
        if (state.IsBurning)
            enemy.BeginBurning();

        return state;
    }

    private void NormalizeTemperature(
        Enemy enemy,
        ref TemperatureState state,
        float deltaTime)
    {
        float normalizationAmount = state.NormalizationPercentPerSecond
            * deltaTime;
        if (normalizationAmount <= 0f)
            return;

        if (state.Temperature > EmptyTemperatureThreshold)
        {
            float cooledHeat = heatSystem != null
                ? heatSystem.CoolHeat(enemy, normalizationAmount)
                : 0f;
            state.Temperature = Mathf.Max(
                0f,
                state.Temperature - cooledHeat);
            return;
        }

        if (state.Temperature < -EmptyTemperatureThreshold)
            state.Temperature = -enemy.MovementSlowPercent;

        if (state.Temperature < -EmptyTemperatureThreshold)
            enemy.RemoveMovementSlow(normalizationAmount);

        state.Temperature = -enemy.MovementSlowPercent;
    }

    private void HandleHeatTransferred(
        Enemy enemy,
        float heatPercent,
        EnemyHeatProfile profile)
    {
        if (profile.SourceConfig != null)
            ApplyHeat(enemy, profile.SourceConfig, heatPercent, profile.Owner);
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy != null && states.TryGetValue(enemy, out TemperatureState state))
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
            if (states.TryGetValue(lastEnemy, out TemperatureState lastState))
            {
                lastState.Index = index;
                states[lastEnemy] = lastState;
            }
        }

        trackedEnemies.RemoveAt(lastIndex);
        states.Remove(removedEnemy);
        if (removedEnemy != null && !removedEnemy.isDead)
            removedEnemy.StopBurning();
    }

    private struct TemperatureState
    {
        public int Index;
        public float Temperature;
        public float LastManipulationTime;
        public float NormalizationDelay;
        public float NormalizationPercentPerSecond;
        public bool IsBurning;
        public float BurningEndTime;
        public float NextBurnDamageTime;
        public float BurningDamagePerTick;
        public float BurningDamageInterval;
        public ParentShip BurningOwner;
    }
}
