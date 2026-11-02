using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Zenject;

/// <summary>
/// Bridge between ECS enemy projectiles and the existing MonoBehaviour player team.
/// It snapshots only registered player and interceptor colliders; ECS performs the
/// projectile sweep and returns a compact resolution event for this class to apply.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class EnemyProjectileCollisionRegistry : MonoBehaviour
{
    private readonly List<RegisteredTarget> targets = new();
    private readonly Dictionary<ParentShip, int> shipTargetIds = new();
    private readonly Dictionary<EnemyProjectileInterceptor, int> interceptorTargetIds = new();
    private readonly Dictionary<int, RegisteredTarget> targetsById = new();
    private readonly Dictionary<EnemyBullet, int> burstProjectileConfigIds = new();
    private readonly Dictionary<int, EnemyBullet> burstProjectileConfigsById = new();
    private readonly List<BlackHolePrefab> projectilePurgeFields = new();
    private readonly Dictionary<int, EnemyProjectilePurgeField> waveClearPurgeFields = new();
    public void SetWaveClearPurgeField(int id, EnemyProjectilePurgeField field) => waveClearPurgeFields[id] = field;
    public void RemoveWaveClearPurgeField(int id) => waveClearPurgeFields.Remove(id);
    private readonly List<EnemyProjectileSpawnRequest> burstSpawnRequests = new(32);
    private readonly List<BlackHoleActive> regionSpeedFields = new(2);

    public void RegisterRegionSpeedField(BlackHoleActive ability)
    {
        if (ability == null || regionSpeedFields.Contains(ability)) return;
        regionSpeedFields.Add(ability);
        if (EnsureRegistryEntity()) UpdateTargetSnapshot();
    }

    public void UnregisterRegionSpeedField(BlackHoleActive ability)
    {
        regionSpeedFields.Remove(ability);
        if (entityWorld != null && entityWorld.IsCreated && entityManager.Exists(registryEntity))
            UpdateTargetSnapshot();
    }

    public float EvaluateActiveSpeedMultiplier(Vector2 position, bool attackSpeed = false)
    {
        float slowest = 1f, fastest = 1f;
        for (int i = 0; i < regionSpeedFields.Count; i++)
        {
            var ability = regionSpeedFields[i];
            if (ability == null || attackSpeed && !ability.RewriteAttackSpeed
                || !ability.TryGetRegionSpeedField(out var field)) continue;
            float value = field.EvaluateMultiplier(new float2(position.x, position.y));
            slowest = Mathf.Min(slowest, value);
            fastest = Mathf.Max(fastest, value);
        }
        return slowest * fastest;
    }

    private EntityManager entityManager;
    private World entityWorld;
    private Entity registryEntity;
    private int nextTargetId = 1;
    private int nextBurstProjectileConfigId = 1;
    private EnemyProjectileEcsSpawner burstProjectileSpawner;

    [Inject] private PlayerController playerController;

#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
    private World performanceWorld;
    public void BindPerformanceWorld(World world, PlayerController player)
    {
        if (world == null || !world.IsCreated || entityWorld != null)
            throw new System.InvalidOperationException("Bind once, before the registry is used.");
        performanceWorld = world;
        playerController = player;
        EnsureRegistryEntity();
    }
#endif

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
        RegisterInterceptor((EnemyProjectileInterceptor)ball);
    }

    public void UnregisterInterceptor(ArkanoidBall ball)
    {
        UnregisterInterceptor((EnemyProjectileInterceptor)ball);
    }

    public void RegisterInterceptor(EnemyProjectileInterceptor ball)
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

    public void UnregisterInterceptor(EnemyProjectileInterceptor ball)
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
        waveClearPurgeFields.Clear();
        burstProjectileSpawner = null;
        regionSpeedFields.Clear();
    }

    private void RegisterTarget<TKey>(
        ParentShip ship,
        EnemyProjectileInterceptor ball,
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

    public void ClearEnemyProjectiles(Vector2 center, float radius, bool systemCleanup = false)
    {
        if (radius <= 0f || !EnsureRegistryEntity()) return;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EnemyProjectileStaticData>(),
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<EnemyProjectileResolution>());
        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        float2 origin = new float2(center.x, center.y);
        float radiusSquared = radius * radius;
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            bool anchor=EnemyProjectileTargetPositionUtility.IsHidden(entityManager,entity);
            if(anchor && !systemCleanup)continue;
            if (entityManager.HasComponent<EnemyCompositeCenter>(entity) && !(anchor&&systemCleanup)) continue;
            if (!systemCleanup && entityManager.GetComponentData<EnemyProjectileStaticData>(entity).Characteristic
                == EnemyProjectileCharacteristic.Indestructible) continue;
            if (entityManager.GetComponentData<EnemyProjectileResolution>(entity).Kind
                != EnemyProjectileResolutionKind.None) continue;
            float2 position = entityManager.GetComponentData<LocalTransform>(entity).Position.xy;
            if (entityManager.HasComponent<EnemyLaserState>(entity))
            {
                var beam = entityManager.GetComponentData<EnemyLaserState>(entity);
                if (!EnemyLaserGeometryUtility.Hits(beam, new EnemyProjectileCollisionTarget
                    { IsCircle = 1, Center = origin, PreviousCenter = origin, Radius = radius })) continue;
            }
            else if (math.distancesq(position, origin) > radiusSquared) continue;
            // Resolve through normal ECS cleanup without structural changes while
            // this registry may be consuming its damage-event buffer.
            entityManager.SetComponentData(entity, new EnemyProjectileResolution
            {
                Kind = EnemyProjectileResolutionKind.Expired
            });
        }
    }

    private bool EnsureRegistryEntity()
    {
        if (entityWorld != null
            && entityWorld.IsCreated
            && entityManager.Exists(registryEntity))
        {
            if (!entityManager.HasBuffer<EnemyProjectileRegionSpeedField>(registryEntity))
                entityManager.AddBuffer<EnemyProjectileRegionSpeedField>(registryEntity);
            return true;
        }

        World world = World.DefaultGameObjectInjectionWorld;
#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
        world = performanceWorld ?? world;
#endif
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
        entityManager.AddBuffer<EnemyProjectileRegionSpeedField>(registryEntity);
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
                    target.Interceptor?.NotifyEnemyProjectileIntercepted();
                    break;
                case EnemyProjectileResolutionKind.HitPlayer:
                    if (IsCurrentShip(target.Ship))
                    {
                        if (resolutionEvent.UseConfiguredDamage == 2) target.Ship.TryTakeConfiguredDamage(resolutionEvent.Damage,resolutionEvent.DamageType);
                        else if (resolutionEvent.UseConfiguredDamage != 0) target.Ship.TryTakeConfiguredDamage(resolutionEvent.Damage);
                        else target.Ship.TryTakeDamage(resolutionEvent.Damage);
                    }
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

            burstProjectileSpawner.AppendBurstSpawnRequest(burstSpawnRequests, request, projectile);
        }

        // Structural spawns invalidate dynamic buffer handles.
        requests.Clear();
        if (burstSpawnRequests.Count > 0)
            burstProjectileSpawner.TrySpawnBatch(burstSpawnRequests);
    }

    private void UpdateTargetSnapshot()
    {
        var regions = entityManager.GetBuffer<EnemyProjectileRegionSpeedField>(registryEntity);
        regions.Clear();
        for (int i = regionSpeedFields.Count - 1; i >= 0; i--)
        {
            var ability = regionSpeedFields[i];
            if (ability == null || !ability.TryGetRegionSpeedField(out var region))
            { regionSpeedFields.RemoveAt(i); continue; }
            regions.Add(region);
        }
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

        foreach (EnemyProjectilePurgeField field in waveClearPurgeFields.Values)
            purgeFields.Add(field);

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

            if (target.Interceptor is MaterializedBladeTrail blade)
            {
                blade.AppendProjectileCollisionTargets(snapshot, target.Id);
                continue;
            }

            if (target.Interceptor != null
                && target.Interceptor.TryGetCircularSweep(
                    out Vector2 previousCenter, out Vector2 center, out float radius))
            {
                snapshot.Add(new EnemyProjectileCollisionTarget
                {
                    TargetId = target.Id,
                    Kind = target.Kind,
                    DestroysNormal = target.Interceptor.DestroysNormalProjectiles ? (byte)1 : (byte)0,
                    IsCircle = 1,
                    PreviousCenter = new float2(previousCenter.x, previousCenter.y),
                    Center = new float2(center.x, center.y),
                    Radius = radius,
                    Min = new float2(center.x - radius, center.y - radius),
                    Max = new float2(center.x + radius, center.y + radius)
                });
                continue;
            }

            Collider2D[] colliders = target.Colliders;
            if (target.Interceptor != null && target.Interceptor.TryGetBoxSweep(
                out Vector2 previousBoxCenter, out Vector2 boxCenter, out Vector2 boxSize, out Vector2 boxRight))
            {
                snapshot.Add(new EnemyProjectileCollisionTarget
                {
                    TargetId = target.Id, Kind = target.Kind,
                    DestroysNormal = target.Interceptor.DestroysNormalProjectiles ? (byte)1 : (byte)0,
                    IsBox = 1,
                    PreviousCenter = new float2(previousBoxCenter.x, previousBoxCenter.y),
                    Center = new float2(boxCenter.x, boxCenter.y),
                    BoxHalfSize = new float2(boxSize.x, boxSize.y) * .5f,
                    BoxRight = new float2(boxRight.x, boxRight.y)
                });
                continue;
            }
            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider == null || !collider.isActiveAndEnabled)
                    continue;

                Bounds bounds = collider.bounds;
                bool isHullCircle = target.Kind == EnemyProjectileCollisionTargetKind.PlayerShip
                    && collider is CircleCollider2D;
                float2 hullCenter = new float2(bounds.center.x, bounds.center.y);
                snapshot.Add(new EnemyProjectileCollisionTarget
                {
                    TargetId = target.Id,
                    Kind = target.Kind,
                    DestroysNormal = target.Interceptor != null && target.Interceptor.DestroysNormalProjectiles ? (byte)1 : (byte)0,
                    IsCircle = isHullCircle ? (byte)1 : (byte)0,
                    PreviousCenter = hullCenter,
                    Center = hullCenter,
                    Radius = isHullCircle ? Mathf.Max(bounds.extents.x, bounds.extents.y) : 0f,
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
        if (target.Interceptor != null)
            interceptorTargetIds.Remove(target.Interceptor);
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
        public readonly EnemyProjectileInterceptor Interceptor;
        public readonly Collider2D[] Colliders;

        public bool IsAlive => Kind == EnemyProjectileCollisionTargetKind.PlayerShip
            ? Ship != null && Ship.isActiveAndEnabled
            : Interceptor != null && Interceptor.isActiveAndEnabled;

        public RegisteredTarget(
            int id,
            EnemyProjectileCollisionTargetKind kind,
            ParentShip ship,
            EnemyProjectileInterceptor ball,
            Collider2D[] colliders)
        {
            Id = id;
            Kind = kind;
            Ship = ship;
            Interceptor = ball;
            Colliders = colliders;
        }
    }
}
