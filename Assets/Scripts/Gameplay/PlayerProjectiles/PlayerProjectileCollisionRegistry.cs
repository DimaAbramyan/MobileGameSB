using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

/// <summary>
/// Managed bridge for Entity player projectiles. It keeps references to ordinary
/// MonoBehaviour enemies while exposing only compact collider snapshots to ECS.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class PlayerProjectileCollisionRegistry : MonoBehaviour
{
    private const float GridCellSize = 1f;

    private readonly List<RegisteredEnemy> enemies = new();
    private readonly Dictionary<Enemy, int> enemyTargetIds = new();
    private readonly Dictionary<int, RegisteredEnemy> enemiesById = new();
    private readonly List<RegisteredDamageReceiver> damageReceivers = new();
    private readonly Dictionary<IEntityProjectileDamageReceiver, int>
        damageReceiverTargetIds = new();
    private readonly Dictionary<int, RegisteredDamageReceiver>
        damageReceiversById = new();
    private readonly Dictionary<ParentShip, int> ownerIds = new();
    private readonly Dictionary<int, ParentShip> ownersById = new();
    private readonly Dictionary<IReadOnlyList<EnemyDebuffApplication>, int>
        debuffSetIds = new();
    private readonly Dictionary<int, IReadOnlyList<EnemyDebuffApplication>>
        debuffSetsById = new();
    private readonly Dictionary<ProjectileData, int> explosionConfigIds = new();
    private readonly Dictionary<int, RegisteredExplosionConfig>
        explosionConfigsById = new();
    private readonly Dictionary<ProjectileData, int> secondaryProjectileConfigIds = new();
    private readonly Dictionary<int, ProjectileData> secondaryProjectileConfigsById = new();

    [Inject] private EnemyManager enemyManager;
    [Inject] private DealDamageManager dealDamageManager;

    private EntityManager entityManager;
    private World entityWorld;
    private Entity registryEntity;
    private int nextTargetId = 1;
    private int nextOwnerId = 1;
    private int nextDebuffSetId = 1;
    private int nextExplosionConfigId = 1;
    private int nextSecondaryProjectileConfigId = 1;
    private bool subscribed;
      private PlayerProjectileEcsSpawner secondaryProjectileSpawner;
      private PlayerProjectileElectricArcVisualController arcVisualController;

      private void Start()
      {
          arcVisualController = GetComponent<PlayerProjectileElectricArcVisualController>();
          if (arcVisualController == null)
              arcVisualController = gameObject.AddComponent<
                  PlayerProjectileElectricArcVisualController>();
          SubscribeToEnemyManager();
          RegisterExistingEnemies();
    }

    public int GetOrRegisterOwnerId(ParentShip owner)
    {
        if (owner == null)
            return 0;

        if (ownerIds.TryGetValue(owner, out int existingId))
            return existingId;

        int ownerId = nextOwnerId++;
        ownerIds.Add(owner, ownerId);
        ownersById.Add(ownerId, owner);
        return ownerId;
    }

    /// <summary>
    /// Converts the managed designer config into a compact id stored on an Entity.
    /// Only this bridge holds the managed debuff references.
    /// </summary>
    public int GetOrRegisterDebuffSetId(
        IReadOnlyList<EnemyDebuffApplication> applications)
    {
        if (applications == null || applications.Count == 0)
            return 0;

        if (debuffSetIds.TryGetValue(applications, out int existingId))
            return existingId;

        var validApplications = new List<EnemyDebuffApplication>(
            applications.Count);
        for (int index = 0; index < applications.Count; index++)
        {
            EnemyDebuffApplication application = applications[index];
            if (application != null && application.IsValid)
                validApplications.Add(application);
        }

        if (validApplications.Count == 0)
            return 0;

        int debuffSetId = nextDebuffSetId++;
        debuffSetIds.Add(applications, debuffSetId);
        debuffSetsById.Add(debuffSetId, validApplications.ToArray());
        return debuffSetId;
    }

    /// <summary>
    /// Keeps the managed explosion prefab outside ECS while giving a projectile
    /// a stable unmanaged configuration id.
    /// </summary>
    public int GetOrRegisterExplosionConfigId(
        ProjectileData projectileData,
        ProjectileRuntimeConfig runtimeConfig)
    {
        if (projectileData == null || runtimeConfig.explosionPrefab == null)
            return 0;

        if (explosionConfigIds.TryGetValue(projectileData, out int existingId))
            return existingId;

        int configId = nextExplosionConfigId++;
        explosionConfigIds.Add(projectileData, configId);
        explosionConfigsById.Add(configId, new RegisteredExplosionConfig(
            runtimeConfig.explosionPrefab,
            runtimeConfig.explosionDamage,
            runtimeConfig.explosionDamageType,
            runtimeConfig.explosionBypassesEnemyShield,
            runtimeConfig.explosionRadius));
        return configId;
    }

    /// <summary>
    /// Registers the managed ScriptableObject once and returns an unmanaged id
    /// which may be stored on a primary projectile Entity.
    /// </summary>
    public int GetOrRegisterSecondaryProjectileConfigId(ProjectileData projectileData)
    {
        if (projectileData == null)
            return 0;

        if (secondaryProjectileConfigIds.TryGetValue(
                projectileData,
                out int existingId))
        {
            return existingId;
        }

        int configId = nextSecondaryProjectileConfigId++;
        secondaryProjectileConfigIds.Add(projectileData, configId);
        secondaryProjectileConfigsById.Add(configId, projectileData);
        return configId;
    }

    /// <summary>
    /// The ECS spawner owns Entity construction. It registers itself when it
    /// creates a projectile, avoiding a circular Zenject dependency.
    /// </summary>
    public void RegisterSecondaryProjectileSpawner(PlayerProjectileEcsSpawner spawner)
    {
        secondaryProjectileSpawner = spawner;
    }

    public void RegisterDamageReceiver(IEntityProjectileDamageReceiver receiver)
    {
        if (receiver is not MonoBehaviour component
            || damageReceiverTargetIds.ContainsKey(receiver))
        {
            return;
        }

        int targetId = nextTargetId++;
        var registeredReceiver = new RegisteredDamageReceiver(
            targetId,
            receiver,
            component,
            component.GetComponentsInChildren<Collider2D>(true));
        damageReceivers.Add(registeredReceiver);
        damageReceiverTargetIds.Add(receiver, targetId);
        damageReceiversById.Add(targetId, registeredReceiver);
    }

    public void UnregisterDamageReceiver(IEntityProjectileDamageReceiver receiver)
    {
        if (receiver == null
            || !damageReceiverTargetIds.TryGetValue(receiver, out int targetId))
        {
            return;
        }

        damageReceiverTargetIds.Remove(receiver);
        damageReceiversById.Remove(targetId);
        for (int index = damageReceivers.Count - 1; index >= 0; index--)
        {
            if (damageReceivers[index].Id == targetId)
            {
                damageReceivers.RemoveAt(index);
                return;
            }
        }
    }

    private void FixedUpdate()
    {
        SubscribeToEnemyManager();
        if (!EnsureRegistryEntity())
            return;

          ResolveEvents();
          ResolveElectricArcVisualRequests();
          ResolveExplosionRequests();
        ResolveSecondaryProjectileSpawnRequests();
        UpdateTargetSnapshot();
    }

    private void OnDestroy()
    {
        if (subscribed && enemyManager != null)
        {
            enemyManager.OnEnemyAdded -= RegisterEnemy;
            enemyManager.OnEnemyDestroyed -= UnregisterEnemy;
        }

          if (entityWorld != null && entityWorld.IsCreated)
          {
              EntityQuery playerProjectiles = entityManager.CreateEntityQuery(
                  ComponentType.ReadOnly<PlayerProjectileStaticData>());
              entityManager.DestroyEntity(playerProjectiles);

              if (entityManager.Exists(registryEntity))
                  entityManager.DestroyEntity(registryEntity);
          }

        enemies.Clear();
        enemyTargetIds.Clear();
        enemiesById.Clear();
        damageReceivers.Clear();
        damageReceiverTargetIds.Clear();
        damageReceiversById.Clear();
        ownerIds.Clear();
        ownersById.Clear();
        debuffSetIds.Clear();
        debuffSetsById.Clear();
          explosionConfigIds.Clear();
          explosionConfigsById.Clear();
        secondaryProjectileConfigIds.Clear();
        secondaryProjectileConfigsById.Clear();
        secondaryProjectileSpawner = null;
    }

    private void SubscribeToEnemyManager()
    {
        if (subscribed || enemyManager == null)
            return;

        enemyManager.OnEnemyAdded += RegisterEnemy;
        enemyManager.OnEnemyDestroyed += UnregisterEnemy;
        subscribed = true;
    }

    private void RegisterExistingEnemies()
    {
        if (enemyManager?.enemyList == null)
            return;

        for (int index = 0; index < enemyManager.enemyList.Count; index++)
            RegisterEnemy(enemyManager.enemyList[index]);
    }

    private void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null || enemyTargetIds.ContainsKey(enemy))
            return;

        int targetId = nextTargetId++;
        var registeredEnemy = new RegisteredEnemy(
            targetId,
            enemy,
            enemy.GetComponentsInChildren<Collider2D>(true));
        enemies.Add(registeredEnemy);
        enemiesById.Add(targetId, registeredEnemy);
        enemyTargetIds.Add(enemy, targetId);
    }

    private void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null || !enemyTargetIds.TryGetValue(enemy, out int targetId))
            return;

        enemyTargetIds.Remove(enemy);
        enemiesById.Remove(targetId);
        for (int index = enemies.Count - 1; index >= 0; index--)
        {
            if (enemies[index].Id == targetId)
            {
                enemies.RemoveAt(index);
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
            new PlayerProjectileCollisionRegistryTag());
        entityManager.AddComponentData(
            registryEntity,
            new PlayerProjectileCollisionGridSettings
            {
                CellSize = GridCellSize
            });
        entityManager.AddBuffer<PlayerProjectileCollisionTarget>(registryEntity);
          entityManager.AddBuffer<PlayerProjectileCollisionGridEntry>(registryEntity);
          entityManager.AddBuffer<PlayerProjectileHomingTarget>(registryEntity);
          entityManager.AddBuffer<PlayerProjectileEntityDamageReceiverTarget>(
              registryEntity);
          entityManager.AddBuffer<PlayerProjectileEntityDamageReceiverGridEntry>(
              registryEntity);
          entityManager.AddBuffer<PlayerProjectileResolutionEvent>(registryEntity);
          entityManager.AddBuffer<PlayerProjectileElectricArcVisualRequest>(
              registryEntity);
        entityManager.AddBuffer<PlayerProjectileExplosionRequest>(registryEntity);
        entityManager.AddBuffer<PlayerProjectileSecondarySpawnRequest>(registryEntity);
        return true;
    }

      private void ResolveEvents()
    {
        DynamicBuffer<PlayerProjectileResolutionEvent> events = entityManager
            .GetBuffer<PlayerProjectileResolutionEvent>(registryEntity);
        for (int index = 0; index < events.Length; index++)
        {
            PlayerProjectileResolutionEvent resolutionEvent = events[index];
            ownersById.TryGetValue(resolutionEvent.OwnerId, out ParentShip owner);
            if (resolutionEvent.TargetKind
                == PlayerProjectileCollisionTargetKind.DamageReceiver)
            {
                ResolveDamageReceiverEvent(resolutionEvent, owner);
                continue;
            }

            if (!enemiesById.TryGetValue(
                    resolutionEvent.TargetId,
                    out RegisteredEnemy target)
                || !target.IsAlive)
            {
                continue;
            }

            IReadOnlyList<EnemyDebuffApplication> debuffs = null;
            if (resolutionEvent.DebuffSetId != 0)
            {
                debuffSetsById.TryGetValue(
                    resolutionEvent.DebuffSetId,
                    out debuffs);
            }

            dealDamageManager?.DealDamage(
                target.Enemy,
                owner,
                resolutionEvent.Damage,
                resolutionEvent.DamageType,
                resolutionEvent.BypassesEnemyShield != 0,
                new Vector3(
                    resolutionEvent.ImpactPosition.x,
                    resolutionEvent.ImpactPosition.y,
                    target.Enemy.transform.position.z),
                debuffs);
        }

          events.Clear();
      }

      private void ResolveElectricArcVisualRequests()
      {
          if (arcVisualController == null)
              return;

          DynamicBuffer<PlayerProjectileElectricArcVisualRequest> requests =
              entityManager.GetBuffer<PlayerProjectileElectricArcVisualRequest>(
                  registryEntity);
          for (int index = 0; index < requests.Length; index++)
              arcVisualController.Play(requests[index]);

          requests.Clear();
      }

    private void ResolveExplosionRequests()
    {
        DynamicBuffer<PlayerProjectileExplosionRequest> requests = entityManager
            .GetBuffer<PlayerProjectileExplosionRequest>(registryEntity);
        for (int index = 0; index < requests.Length; index++)
        {
            PlayerProjectileExplosionRequest request = requests[index];
            if (!explosionConfigsById.TryGetValue(
                    request.ExplosionConfigId,
                    out RegisteredExplosionConfig config))
            {
                continue;
            }

            ownersById.TryGetValue(request.OwnerId, out ParentShip owner);
            ProjectileExplosionSpawner.Spawn(
                config.Prefab,
                config.Damage,
                config.DamageType,
                config.BypassesEnemyShield,
                config.Radius,
                new Vector3(request.Position.x, request.Position.y, 0f),
                owner,
                dealDamageManager);
        }

        requests.Clear();
    }

    private void ResolveSecondaryProjectileSpawnRequests()
    {
        DynamicBuffer<PlayerProjectileSecondarySpawnRequest> requests = entityManager
            .GetBuffer<PlayerProjectileSecondarySpawnRequest>(registryEntity);
        for (int index = 0; index < requests.Length; index++)
        {
            PlayerProjectileSecondarySpawnRequest request = requests[index];
            if (secondaryProjectileSpawner == null
                || !secondaryProjectileConfigsById.TryGetValue(
                    request.SecondaryProjectileConfigId,
                    out ProjectileData projectileData))
            {
                continue;
            }

            ownersById.TryGetValue(request.OwnerId, out ParentShip owner);
            secondaryProjectileSpawner.TrySpawnSecondary(
                projectileData,
                new Vector3(request.Position.x, request.Position.y, 0f),
                new ProjectileParams
                {
                    speed = request.Speed,
                    damage = request.Damage,
                    maxLength = request.Range,
                    direction = new Vector3(
                        request.Direction.x,
                        request.Direction.y,
                        0f),
                    maxAngle = 0f
                },
                owner,
                request.IgnoredTargetId);
        }

        requests.Clear();
    }

    private void ResolveDamageReceiverEvent(
        PlayerProjectileResolutionEvent resolutionEvent,
        ParentShip owner)
    {
        if (!damageReceiversById.TryGetValue(
                resolutionEvent.TargetId,
                out RegisteredDamageReceiver target)
            || !target.IsAlive
            || !target.Receiver.CanReceiveEntityProjectileDamage(
                owner,
                resolutionEvent.DamageType))
        {
            return;
        }

        target.Receiver.ReceiveEntityProjectileDamage(
            owner,
            resolutionEvent.DamageType,
            resolutionEvent.Damage);
    }

    private void UpdateTargetSnapshot()
    {
        DynamicBuffer<PlayerProjectileCollisionTarget> targets = entityManager
            .GetBuffer<PlayerProjectileCollisionTarget>(registryEntity);
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid = entityManager
            .GetBuffer<PlayerProjectileCollisionGridEntry>(registryEntity);
        DynamicBuffer<PlayerProjectileHomingTarget> homingTargets = entityManager
            .GetBuffer<PlayerProjectileHomingTarget>(registryEntity);
        targets.Clear();
        grid.Clear();
        homingTargets.Clear();

        for (int enemyIndex = enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
        {
            RegisteredEnemy enemy = enemies[enemyIndex];
            if (!enemy.IsAlive)
            {
                UnregisterEnemy(enemy.Enemy);
                continue;
            }

              Vector3 enemyPosition = enemy.Enemy.transform.position;
              Vector3 enemyScale = enemy.Enemy.transform.lossyScale;
              homingTargets.Add(new PlayerProjectileHomingTarget
              {
                  TargetId = enemy.Id,
                  Position = new float2(enemyPosition.x, enemyPosition.y),
                  RotationRadians = math.radians(
                      enemy.Enemy.transform.eulerAngles.z),
                  Scale = new float2(
                      math.abs(enemyScale.x),
                      math.abs(enemyScale.y)),
                  Layer = enemy.Enemy.gameObject.layer
              });

            Collider2D[] colliders = enemy.Colliders;
            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider == null || !collider.isActiveAndEnabled)
                    continue;

                Bounds bounds = collider.bounds;
                AddColliderSnapshot(
                    targets,
                    grid,
                    enemy.Id,
                    PlayerProjectileCollisionTargetKind.Enemy,
                    bounds);
            }
        }

        for (int receiverIndex = damageReceivers.Count - 1;
             receiverIndex >= 0;
             receiverIndex--)
        {
            RegisteredDamageReceiver receiver = damageReceivers[receiverIndex];
            if (!receiver.IsAlive)
            {
                UnregisterDamageReceiver(receiver.Receiver);
                continue;
            }

            Collider2D[] colliders = receiver.Colliders;
            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider == null || !collider.isActiveAndEnabled)
                    continue;

                AddColliderSnapshot(
                    targets,
                    grid,
                    receiver.Id,
                    PlayerProjectileCollisionTargetKind.DamageReceiver,
                    collider.bounds);
            }
        }
    }

    private static void AddColliderSnapshot(
        DynamicBuffer<PlayerProjectileCollisionTarget> targets,
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid,
        int targetId,
        PlayerProjectileCollisionTargetKind targetKind,
        Bounds bounds)
    {
        float2 minimum = new(bounds.min.x, bounds.min.y);
        float2 maximum = new(bounds.max.x, bounds.max.y);
        targets.Add(new PlayerProjectileCollisionTarget
        {
            TargetId = targetId,
            Kind = targetKind,
            Min = minimum,
            Max = maximum
        });

        int minimumX = Mathf.FloorToInt(minimum.x / GridCellSize);
        int maximumX = Mathf.FloorToInt(maximum.x / GridCellSize);
        int minimumY = Mathf.FloorToInt(minimum.y / GridCellSize);
        int maximumY = Mathf.FloorToInt(maximum.y / GridCellSize);
        for (int cellX = minimumX; cellX <= maximumX; cellX++)
        {
            for (int cellY = minimumY; cellY <= maximumY; cellY++)
            {
                grid.Add(new PlayerProjectileCollisionGridEntry
                {
                    CellX = cellX,
                    CellY = cellY,
                    TargetId = targetId
                });
            }
        }
    }

    private sealed class RegisteredEnemy
    {
        public readonly int Id;
        public readonly Enemy Enemy;
        public readonly Collider2D[] Colliders;

        public bool IsAlive => Enemy != null
            && !Enemy.isDead
            && Enemy.isActiveAndEnabled;

        public RegisteredEnemy(int id, Enemy enemy, Collider2D[] colliders)
        {
            Id = id;
            Enemy = enemy;
            Colliders = colliders;
        }
    }

    private sealed class RegisteredDamageReceiver
    {
        public readonly int Id;
        public readonly IEntityProjectileDamageReceiver Receiver;
        public readonly MonoBehaviour Component;
        public readonly Collider2D[] Colliders;

        public bool IsAlive => Component != null && Component.isActiveAndEnabled;

        public RegisteredDamageReceiver(
            int id,
            IEntityProjectileDamageReceiver receiver,
            MonoBehaviour component,
            Collider2D[] colliders)
        {
            Id = id;
            Receiver = receiver;
            Component = component;
            Colliders = colliders;
        }
    }

    private sealed class RegisteredExplosionConfig
    {
        public readonly Explode Prefab;
        public readonly float Damage;
        public readonly EnemyDamageType DamageType;
        public readonly bool BypassesEnemyShield;
        public readonly float Radius;

        public RegisteredExplosionConfig(
            Explode prefab,
            float damage,
            EnemyDamageType damageType,
            bool bypassesEnemyShield,
            float radius)
        {
            Prefab = prefab;
            Damage = damage;
            DamageType = damageType;
            BypassesEnemyShield = bypassesEnemyShield;
            Radius = radius;
        }
    }
}
