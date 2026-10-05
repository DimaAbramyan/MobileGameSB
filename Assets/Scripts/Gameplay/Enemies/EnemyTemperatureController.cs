using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class EnemyTemperatureController : IInitializable, ITickable, IDisposable
{
    private const float FullTemperature = 100f;
    private const float EmptyTemperatureThreshold = 0.001f;
    private const int OverlapBufferSize = 32;

    private readonly EnemyManager enemyManager;
    private readonly EnemyHeatSystem heatSystem;
    private readonly LazyInject<DealDamageManager> dealDamageManager;
    private readonly DiContainer container;
    private readonly Dictionary<Enemy, TemperatureState> states = new();
    private readonly List<Enemy> trackedEnemies = new();
    private Collider2D[] overlapBuffer = new Collider2D[OverlapBufferSize];
    private readonly Stack<ExplosionContext> explosionContextPool = new();

    public EnemyTemperatureController(
        EnemyManager enemyManager,
        EnemyHeatSystem heatSystem,
        LazyInject<DealDamageManager> dealDamageManager,
        DiContainer container)
    {
        this.enemyManager = enemyManager;
        this.heatSystem = heatSystem;
        this.dealDamageManager = dealDamageManager;
        this.container = container;
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
        explosionContextPool.Clear();
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
        EnemyHeatProfile profile = config.CreateProfile(owner);
        float remainingHeat = amount - enemy.RemoveMovementSlow(amount);
        EnemyDebuffProgress progress = EnemyDebuffProgress.None;
        if (remainingHeat > 0f)
        {
            progress = heatSystem.ApplyHeat(
                enemy,
                remainingHeat,
                profile);
        }

        state.Temperature = heatSystem.GetHeatPercent(enemy);
        state.LastManipulationTime = Time.time;
        state.NormalizationDelay = config.CoolingDelay;
        state.NormalizationPercentPerSecond = config.CoolingPercentPerSecond;
        state.ThermalExplosionProfile = profile;
        state.HasThermalExplosionProfile = true;
        state = RefreshBurning(enemy, state, config, owner);
        enemy.SetTemperaturePercent(state.Temperature);
        states[enemy] = state;
        return progress;
    }

    public EnemyDebuffProgress ApplyThermal(
        Enemy enemy,
        EnemyHeatDebuffConfig config,
        float amount,
        ParentShip owner)
    {
        if (enemy == null
            || enemy.isDead
            || config == null
            || amount <= 0f)
        {
            return EnemyDebuffProgress.None;
        }

        return enemy.TemperaturePercent < -EmptyTemperatureThreshold
            ? ApplyThermalCold(enemy, config, amount, owner)
            : ApplyHeat(enemy, config, amount, owner);
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

    private EnemyDebuffProgress ApplyThermalCold(
        Enemy enemy,
        EnemyHeatDebuffConfig config,
        float amount,
        ParentShip owner)
    {
        TemperatureState state = GetOrCreateState(enemy);
        float previousTemperature = Mathf.Abs(state.Temperature);
        float cooledHeat = heatSystem != null
            ? heatSystem.CoolHeat(enemy, amount)
            : 0f;
        float remainingCold = Mathf.Max(0f, amount - cooledHeat);
        if (remainingCold > 0f)
            enemy.ApplyMovementSlow(remainingCold, FullTemperature);

        float heat = heatSystem != null
            ? heatSystem.GetHeatPercent(enemy)
            : 0f;
        state.Temperature = heat > EmptyTemperatureThreshold
            ? heat
            : -enemy.MovementSlowPercent;
        state.LastManipulationTime = Time.time;
        state.NormalizationDelay = config.CoolingDelay;
        state.NormalizationPercentPerSecond = config.CoolingPercentPerSecond;
        state.ThermalExplosionProfile = config.CreateProfile(owner);
        state.HasThermalExplosionProfile = true;
        enemy.SetTemperaturePercent(state.Temperature);
        states[enemy] = state;

        return new EnemyDebuffProgress(
            true,
            previousTemperature,
            Mathf.Abs(state.Temperature),
            FullTemperature);
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
        if (enemy == null || !states.TryGetValue(enemy, out TemperatureState state))
            return;

        Vector3 explosionPosition = enemy.transform.position;
        bool hasMaximumColdTemperature = state.Temperature <= -FullTemperature;
        EnemyHeatProfile profile = state.ThermalExplosionProfile;
        bool shouldExplode = hasMaximumColdTemperature
            && state.HasThermalExplosionProfile;
        RemoveStateAt(state.Index);

        if (shouldExplode && Time.timeScale > 0f)
        {
            TriggerThermalExplosion(
                explosionPosition,
                enemy,
                -FullTemperature,
                profile);
        }
    }

    private void TriggerThermalExplosion(
        Vector3 position,
        Enemy sourceEnemy,
        float sourceTemperature,
        EnemyHeatProfile profile)
    {
        SpawnExplosionVisual(position, profile);

        if (profile.ExplosionRadius <= 0f)
            return;

        ExplosionContext context = RentExplosionContext();
        try
        {
            ContactFilter2D filter = CreateContactFilter(profile.AffectedLayers);
            int colliderCount = FindOverlappingColliders(
                position,
                profile.ExplosionRadius,
                filter);

            for (int index = 0; index < colliderCount; index++)
            {
                Collider2D collider = overlapBuffer[index];
                if (collider == null)
                    continue;

                Enemy enemy = collider.GetComponentInParent<Enemy>();
                if (enemy == null || enemy == sourceEnemy || enemy.isDead)
                    continue;

                if (context.UniqueEnemies.Add(enemy))
                    context.Enemies.Add(enemy);
            }

            float transferredTemperature = sourceTemperature
                * profile.TransferredHeatPercent / 100f;
            if (profile.SourceConfig != null
                && !Mathf.Approximately(transferredTemperature, 0f))
            {
                for (int index = 0; index < context.Enemies.Count; index++)
                {
                    ApplyTransferredThermalTemperature(
                        context.Enemies[index],
                        profile,
                        transferredTemperature);
                }
            }

            float explosionDamage = sourceEnemy.MaximumHealth
                * profile.ExplosionDamagePercent / 100f;
            if (explosionDamage <= 0f || dealDamageManager == null)
                return;

            for (int index = 0; index < context.Enemies.Count; index++)
            {
                Enemy enemy = context.Enemies[index];
                if (enemy == null || enemy.isDead)
                    continue;

                dealDamageManager.Value.DealDamage(
                    enemy,
                    profile.Owner,
                    explosionDamage,
                    EnemyDamageType.Explosion);
            }
        }
        finally
        {
            ReturnExplosionContext(context);
        }
    }

    private void ApplyTransferredThermalTemperature(
        Enemy enemy,
        EnemyHeatProfile profile,
        float temperature)
    {
        if (temperature > 0f)
        {
            ApplyHeat(
                enemy,
                profile.SourceConfig,
                temperature,
                profile.Owner);
            return;
        }

        ApplyThermalCold(
            enemy,
            profile.SourceConfig,
            -temperature,
            profile.Owner);
    }

    private void SpawnExplosionVisual(Vector3 position, EnemyHeatProfile profile)
    {
        if (profile.ExplosionPrefab == null || container == null)
            return;

        GameObject instance = container.InstantiatePrefab(
            profile.ExplosionPrefab.gameObject,
            position,
            Quaternion.identity,
            null);
        if (instance == null)
            return;

        instance.transform.localScale *= Mathf.Max(0.01f, profile.ExplosionRadius);

        Explode explosion = instance.GetComponent<Explode>();
        if (explosion != null)
            explosion.SetDamage(0f);

        Collider2D collision = instance.GetComponent<Collider2D>();
        if (collision != null)
            collision.enabled = false;
    }

    private ExplosionContext RentExplosionContext()
    {
        if (explosionContextPool.Count > 0)
            return explosionContextPool.Pop();

        return new ExplosionContext();
    }

    private void ReturnExplosionContext(ExplosionContext context)
    {
        context.Clear();
        explosionContextPool.Push(context);
    }

    private static ContactFilter2D CreateContactFilter(LayerMask layerMask)
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = layerMask,
            useTriggers = true
        };
    }

    private int FindOverlappingColliders(
        Vector3 position,
        float radius,
        ContactFilter2D filter)
    {
        int colliderCount = Physics2D.OverlapCircle(
            position,
            radius,
            filter,
            overlapBuffer);

        while (colliderCount == overlapBuffer.Length
               && overlapBuffer.Length < 1024)
        {
            Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
            colliderCount = Physics2D.OverlapCircle(
                position,
                radius,
                filter,
                overlapBuffer);
        }

        return colliderCount;
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
        public bool HasThermalExplosionProfile;
        public EnemyHeatProfile ThermalExplosionProfile;
    }

    private sealed class ExplosionContext
    {
        public readonly List<Enemy> Enemies = new();
        public readonly HashSet<Enemy> UniqueEnemies = new();

        public void Clear()
        {
            Enemies.Clear();
            UniqueEnemies.Clear();
        }
    }
}
