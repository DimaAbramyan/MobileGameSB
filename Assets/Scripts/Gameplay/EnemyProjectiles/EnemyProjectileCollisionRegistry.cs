using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

/// <summary>
/// Bridge between ECS enemy projectiles and the existing MonoBehaviour player team.
/// It snapshots only registered player and Arkanoid colliders; ECS performs the
/// projectile sweep and returns a compact resolution event for this class to apply.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class EnemyProjectileCollisionRegistry : MonoBehaviour
{
    private readonly List<RegisteredTarget> targets = new();
    private readonly Dictionary<ParentShip, int> shipTargetIds = new();
    private readonly Dictionary<ArkanoidBall, int> interceptorTargetIds = new();
    private readonly Dictionary<int, RegisteredTarget> targetsById = new();
    private readonly Dictionary<EnemyBullet, int> burstProjectileConfigIds = new();
    private readonly Dictionary<int, EnemyBullet> burstProjectileConfigsById = new();
    private readonly List<BlackHolePrefab> projectilePurgeFields = new();
    private readonly List<EnemyProjectileSpawnRequest> burstSpawnRequests = new(32);

    private EntityManager entityManager;
    private World entityWorld;
    private Entity registryEntity;
    private int nextTargetId = 1;
    private int nextBurstProjectileConfigId = 1;
    private EnemyProjectileEcsSpawner burstProjectileSpawner;

    [Inject] private PlayerController playerController;

    public void RegisterShip(ParentShip ship)
    {
        if (ship == null || shipTargetIds.ContainsKey(ship))
            return;

        Collider2D damageHitbox = ship.DamageHitboxCollider;
        if (damageHitbox == null)
        {
            Debug.LogWarning(
                $"{ship.name} has no Collider2D on its root GameObject. "
                + "ECS enemy projectile damage is disabled for this ship.",
                ship);
            return;
        }

        RegisterTarget(
            ship,
            null,
            EnemyProjectileCollisionTargetKind.PlayerShip,
            new[] { damageHitbox },
            shipTargetIds);
    }

    public void UnregisterShip(ParentShip ship)
    {
        if (ship == null || !shipTargetIds.TryGetValue(ship, out int targetId))
            return;

        shipTargetIds.Remove(ship);
        RemoveTarget(targetId);
    }

    public void RegisterInterceptor(ArkanoidBall ball)
    {
        if (ball == null || interceptorTargetIds.ContainsKey(ball))
            return;

        RegisterTarget(
            null,
            ball,
            EnemyProjectileCollisionTargetKind.Interceptor,
            ball.GetComponentsInChildren<Collider2D>(true),
            interceptorTargetIds);
    }

    public void UnregisterInterceptor(ArkanoidBall ball)
    {
        if (ball == null
            || !interceptorTargetIds.TryGetValue(ball, out int targetId))
        {
            return;
        }

        interceptorTargetIds.Remove(ball);
        RemoveTarget(targetId);
    }

    public void RegisterProjectilePurgeField(BlackHolePrefab field)
    {
        if (field != null && !projectilePurgeFields.Contains(field))
            projectilePurgeFields.Add(field);
    }

    public void UnregisterProjectilePurgeField(BlackHolePrefab field)
    {
        if (field != null)
            projectilePurgeFields.Remove(field);
    }

    public void RegisterBurstProjectileSpawner(EnemyProjectileEcsSpawner spawner)
    {
        burstProjectileSpawner = spawner;
    }

    public int GetOrRegisterBurstProjectileConfigId(EnemyBullet projectile)
    {
        if (projectile == null)
            return 0;

        if (burstProjectileConfigIds.TryGetValue(projectile, out int configId))
            return configId;

        configId = nextBurstProjectileConfigId++;
        burstProjectileConfigIds.Add(projectile, configId);
        burstProjectileConfigsById.Add(configId, projectile);
        return configId;
    }

    private void FixedUpdate()
    {
        if (!EnsureRegistryEntity())
            return;

        ResolveEvents();
        ResolveBurstSpawnRequests();
        UpdateTargetSnapshot();
    }

    private void OnDestroy()
    {
        if (entityWorld != null && entityWorld.IsCreated)
        {
            EntityQuery projectiles = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyProjectileStaticData>());
            entityManager.DestroyEntity(projectiles);

            if (entityManager.Exists(registryEntity))
                entityManager.DestroyEntity(registryEntity);
        }

        targets.Clear();
        targetsById.Clear();
        shipTargetIds.Clear();
        interceptorTargetIds.Clear();
        burstProjectileConfigIds.Clear();
        burstProjectileConfigsById.Clear();
        projectilePurgeFields.Clear();
        burstProjectileSpawner = null;
    }

    private void RegisterTarget<TKey>(
        ParentShip ship,
        ArkanoidBall ball,
        EnemyProjectileCollisionTargetKind kind,
        Collider2D[] colliders,
        Dictionary<TKey, int> targetIds)
        where TKey : Object
    {
        int targetId = nextTargetId++;
        var target = new RegisteredTarget(
            targetId,
            kind,
            ship,
            ball,
            colliders);
        targets.Add(target);
        targetsById.Add(targetId, target);
        targetIds.Add((TKey)(Object)(ship != null ? ship : ball), targetId);
    }

    private void RemoveTarget(int targetId)
    {
        if (!targetsById.Remove(targetId))
            return;

        for (int index = targets.Count - 1; index >= 0; index--)
        {
            if (targets[index].Id == targetId)
            {
                targets.RemoveAt(index);
                return;
            }
        }
    }

    private bool EnsureRegistryEntity()
    {
        if (entityWorld != null
            && entityWorld.IsCreated
            && entityManager.Exists(registryEntity))
        {
            return true;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;

        entityWorld = world;
        entityManager = world.EntityManager;
        registryEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(
            registryEntity,
            new EnemyProjectileCollisionRegistryTag());
        entityManager.AddBuffer<EnemyProjectileCollisionTarget>(registryEntity);
        entityManager.AddBuffer<EnemyProjectileSlowField>(registryEntity);
        entityManager.AddBuffer<EnemyProjectilePurgeField>(registryEntity);
        entityManager.AddBuffer<EnemyProjectileResolutionEvent>(registryEntity);
        entityManager.AddBuffer<EnemyProjectileBurstSpawnRequest>(registryEntity);
        return true;
    }

    private void ResolveEvents()
    {
        DynamicBuffer<EnemyProjectileResolutionEvent> events = entityManager
            .GetBuffer<EnemyProjectileResolutionEvent>(registryEntity);
        for (int index = 0; index < events.Length; index++)
        {
            EnemyProjectileResolutionEvent resolutionEvent = events[index];
            if (!targetsById.TryGetValue(
                    resolutionEvent.TargetId,
                    out RegisteredTarget target))
            {
                continue;
            }

            switch (resolutionEvent.Kind)
            {
                case EnemyProjectileResolutionKind.Intercepted:
                    target.Ball?.NotifyEnemyProjectileIntercepted();
                    break;
                case EnemyProjectileResolutionKind.HitPlayer:
                    if (IsCurrentShip(target.Ship))
                        target.Ship.TryTakeDamage(resolutionEvent.Damage);
                    break;
            }
        }

        events.Clear();
    }

    private void ResolveBurstSpawnRequests()
    {
        DynamicBuffer<EnemyProjectileBurstSpawnRequest> requests = entityManager
            .GetBuffer<EnemyProjectileBurstSpawnRequest>(registryEntity);
        burstSpawnRequests.Clear();
        for (int index = 0; index < requests.Length; index++)
        {
            EnemyProjectileBurstSpawnRequest request = requests[index];
            if (burstProjectileSpawner == null
                || !burstProjectileConfigsById.TryGetValue(
                    request.BurstProjectileConfigId,
                    out EnemyBullet projectile))
            {
                continue;
            }

            burstSpawnRequests.Add(new EnemyProjectileSpawnRequest(
                projectile,
                new Vector3(request.Position.x, request.Position.y, 0f),
                new Vector3(request.Direction.x, request.Direction.y, 0f),
                1f,
                0f,
                EnemyProjectileSpeedBehavior.None,
                0f,
                null,
                EnemyProjectileSizeBehavior.Default,
                0f,
                1f,
                null,
                1f));
        }

        if (burstSpawnRequests.Count > 0)
            burstProjectileSpawner.TrySpawnBatch(burstSpawnRequests);

        requests.Clear();
    }

    private void UpdateTargetSnapshot()
    {
        DynamicBuffer<EnemyProjectileCollisionTarget> snapshot = entityManager
            .GetBuffer<EnemyProjectileCollisionTarget>(registryEntity);
        snapshot.Clear();
        DynamicBuffer<EnemyProjectileSlowField> slowFields = entityManager
            .GetBuffer<EnemyProjectileSlowField>(registryEntity);
        slowFields.Clear();
        DynamicBuffer<EnemyProjectilePurgeField> purgeFields = entityManager
            .GetBuffer<EnemyProjectilePurgeField>(registryEntity);
        purgeFields.Clear();

        for (int fieldIndex = projectilePurgeFields.Count - 1;
             fieldIndex >= 0;
             fieldIndex--)
        {
            BlackHolePrefab field = projectilePurgeFields[fieldIndex];
            if (field == null
                || !field.TryGetProjectilePurgeField(
                    out EnemyProjectilePurgeField purgeField))
            {
                projectilePurgeFields.RemoveAt(fieldIndex);
                continue;
            }

            purgeFields.Add(purgeField);
        }

        for (int targetIndex = targets.Count - 1; targetIndex >= 0; targetIndex--)
        {
            RegisteredTarget target = targets[targetIndex];
            if (!target.IsAlive)
            {
                UnregisterDeadTarget(target);
                continue;
            }

            if (target.Kind == EnemyProjectileCollisionTargetKind.PlayerShip
                && !IsCurrentShip(target.Ship))
            {
                continue;
            }

            if (target.Kind == EnemyProjectileCollisionTargetKind.PlayerShip
                && target.Ship.PassiveAbility is BlackHolePassive blackHole
                && blackHole.TryGetSlowField(out EnemyProjectileSlowField slowField))
            {
                slowFields.Add(slowField);
            }

            Collider2D[] colliders = target.Colliders;
            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider == null || !collider.isActiveAndEnabled)
                    continue;

                Bounds bounds = collider.bounds;
                snapshot.Add(new EnemyProjectileCollisionTarget
                {
                    TargetId = target.Id,
                    Kind = target.Kind,
                    Min = new float2(bounds.min.x, bounds.min.y),
                    Max = new float2(bounds.max.x, bounds.max.y)
                });
            }
        }
    }

    private void UnregisterDeadTarget(RegisteredTarget target)
    {
        targets.Remove(target);
        targetsById.Remove(target.Id);
        if (target.Ship != null)
            shipTargetIds.Remove(target.Ship);
        if (target.Ball != null)
            interceptorTargetIds.Remove(target.Ball);
    }

    private bool IsCurrentShip(ParentShip ship)
    {
        return ship != null && playerController != null
            && playerController.CurrentShip == ship;
    }

    private sealed class RegisteredTarget
    {
        public readonly int Id;
        public readonly EnemyProjectileCollisionTargetKind Kind;
        public readonly ParentShip Ship;
        public readonly ArkanoidBall Ball;
        public readonly Collider2D[] Colliders;

        public bool IsAlive => Kind == EnemyProjectileCollisionTargetKind.PlayerShip
            ? Ship != null && Ship.isActiveAndEnabled
            : Ball != null && Ball.isActiveAndEnabled;

        public RegisteredTarget(
            int id,
            EnemyProjectileCollisionTargetKind kind,
            ParentShip ship,
            ArkanoidBall ball,
            Collider2D[] colliders)
        {
            Id = id;
            Kind = kind;
            Ship = ship;
            Ball = ball;
            Colliders = colliders;
        }
    }
}
