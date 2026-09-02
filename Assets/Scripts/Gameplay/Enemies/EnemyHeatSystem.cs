using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public readonly struct EnemyHeatProfile
{
    public EnemyHeatProfile(
        ParentShip owner,
        LayerMask affectedLayers,
        float explosionRadius,
        float explosionDamage,
        float transferredHeatPercent,
        float coolingDelay,
        float coolingPercentPerSecond,
        Explode explosionPrefab)
    {
        Owner = owner;
        AffectedLayers = affectedLayers;
        ExplosionRadius = Mathf.Max(0f, explosionRadius);
        ExplosionDamage = Mathf.Max(0f, explosionDamage);
        TransferredHeatPercent = Mathf.Max(0f, transferredHeatPercent);
        CoolingDelay = Mathf.Max(0f, coolingDelay);
        CoolingPerSecond = Mathf.Max(0f, coolingPercentPerSecond) / 100f;
        ExplosionPrefab = explosionPrefab;
    }

    public ParentShip Owner { get; }
    public LayerMask AffectedLayers { get; }
    public float ExplosionRadius { get; }
    public float ExplosionDamage { get; }
    public float TransferredHeatPercent { get; }
    public float CoolingDelay { get; }
    public float CoolingPerSecond { get; }
    public Explode ExplosionPrefab { get; }
}

public sealed class EnemyHeatSystem : IInitializable, ITickable, IDisposable
{
    private const int OverlapBufferSize = 32;
    private const float FullHeat = 1f;
    private const float EmptyHeatThreshold = 0.0001f;

    private readonly EnemyManager enemyManager;
    private readonly DealDamageManager dealDamageManager;
    private readonly DiContainer container;
    private readonly Dictionary<Enemy, HeatState> states = new();
    private readonly List<Enemy> trackedEnemies = new();
    private Collider2D[] overlapBuffer = new Collider2D[OverlapBufferSize];
    private readonly Stack<ExplosionContext> explosionContextPool = new();

    public EnemyHeatSystem(
        EnemyManager enemyManager,
        DealDamageManager dealDamageManager,
        DiContainer container)
    {
        this.enemyManager = enemyManager;
        this.dealDamageManager = dealDamageManager;
        this.container = container;
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
        explosionContextPool.Clear();
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
                || !states.TryGetValue(enemy, out HeatState state))
            {
                RemoveStateAt(index);
                continue;
            }

            if (currentTime - state.LastHitTime < state.Profile.CoolingDelay)
                continue;

            state.Heat = Mathf.Max(
                0f,
                state.Heat - state.Profile.CoolingPerSecond * deltaTime);

            if (state.Heat <= EmptyHeatThreshold)
            {
                RemoveStateAt(index);
                continue;
            }

            states[enemy] = state;
        }
    }

    public void ApplyHeat(
        Enemy enemy,
        float heatPercent,
        EnemyHeatProfile profile)
    {
        if (Time.timeScale <= 0f
            || enemy == null
            || enemy.isDead
            || heatPercent <= 0f)
        {
            return;
        }

        if (!states.TryGetValue(enemy, out HeatState state))
        {
            state = new HeatState
            {
                Index = trackedEnemies.Count
            };
            trackedEnemies.Add(enemy);
        }

        state.Heat = Mathf.Min(FullHeat, state.Heat + heatPercent / 100f);
        state.LastHitTime = Time.time;
        state.Profile = profile;
        states[enemy] = state;
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || !states.TryGetValue(enemy, out HeatState state))
            return;

        Vector3 explosionPosition = enemy.transform.position;
        bool isOverheated = state.Heat >= FullHeat;
        RemoveStateAt(state.Index);

        if (isOverheated && Time.timeScale > 0f)
            TriggerOverheatExplosion(explosionPosition, enemy, state.Profile);
    }

    private void TriggerOverheatExplosion(
        Vector3 position,
        Enemy sourceEnemy,
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

            for (int index = 0; index < context.Enemies.Count; index++)
            {
                ApplyHeat(
                    context.Enemies[index],
                    profile.TransferredHeatPercent,
                    profile);
            }

            if (profile.ExplosionDamage <= 0f)
                return;

            for (int index = 0; index < context.Enemies.Count; index++)
            {
                Enemy enemy = context.Enemies[index];
                if (enemy == null || enemy.isDead)
                    continue;

                dealDamageManager.DealDamage(
                    enemy,
                    profile.Owner,
                    profile.ExplosionDamage);
            }
        }
        finally
        {
            ReturnExplosionContext(context);
        }
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
            if (states.TryGetValue(lastEnemy, out HeatState lastState))
            {
                lastState.Index = index;
                states[lastEnemy] = lastState;
            }
        }

        trackedEnemies.RemoveAt(lastIndex);
        states.Remove(removedEnemy);
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

    private struct HeatState
    {
        public int Index;
        public float Heat;
        public float LastHitTime;
        public EnemyHeatProfile Profile;
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
