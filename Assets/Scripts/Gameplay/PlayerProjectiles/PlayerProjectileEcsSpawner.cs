using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

/// <summary>
/// Spawns supported player projectiles as lightweight Entities. ProjectileData is
/// compiled and its visual resources are built once, never while firing.
/// </summary>
public sealed class PlayerProjectileEcsSpawner : IDisposable
{
    private const int ProjectileRenderQueue = 3101;

    private readonly Dictionary<ProjectileData, CompiledConfiguration>
        configurations = new();
    private readonly Dictionary<ProjectileData, ContactConfiguration>
        contactConfigurations = new();
    private readonly Dictionary<ProjectileData, RuntimeVisual> visuals = new();
    private readonly Dictionary<Explode, ExplosionRuntimeVisual> explosionVisuals =
        new();
    private readonly Dictionary<ProjectileData, Entity> projectileEntityPrefabs = new();
    private readonly Dictionary<ProjectileData, Entity> contactEntityPrefabs = new();
    private readonly Dictionary<Explode, Entity> explosionEntityPrefabs = new();
    private readonly HashSet<ProjectileData> invalidConfigurationsLogged = new();
    private readonly HashSet<Explode> invalidExplosionConfigurationsLogged = new();

    [Inject] private PlayerProjectileCollisionRegistry collisionRegistry;

    private EntityManager entityManager;
    private World entityWorld;

    public bool Prewarm(ProjectileData projectileData)
    {
        if (projectileData != null
            && projectileData.DeliveryType == ProjectileDeliveryType.Contact)
        {
            return TryGetContactConfiguration(
                       projectileData,
                       out ContactConfiguration contactConfiguration)
                && EnsureEntityManager()
                && TryGetOrCreateContactPrefab(
                    projectileData,
                    contactConfiguration,
                    out _);
        }

        return TryGetConfiguration(projectileData, out CompiledConfiguration configuration)
            && EnsureEntityManager()
            && TryGetVisual(projectileData, out RuntimeVisual visual)
            && TryGetOrCreateProjectilePrefab(
                projectileData,
                configuration,
                visual,
                out _);
    }

    /// <summary>
    /// Creates the gameplay and visual representation of an explosion without
    /// instantiating its MonoBehaviour prefab at runtime.
    /// </summary>
    public bool TrySpawnExplosion(
        Explode explosionPrefab,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield,
        float radius,
        Vector3 position,
        ParentShip owner)
    {
        if (explosionPrefab == null
            || !EnsureEntityManager()
            || !TryGetExplosionVisual(explosionPrefab, out ExplosionRuntimeVisual visual))
        {
            return false;
        }

        if (!TryGetOrCreateExplosionPrefab(explosionPrefab, visual, out Entity prefab))
            return false;

        Entity entity = entityManager.Instantiate(prefab);
        float effectiveRadius = radius > 0f
            ? radius
            : GetExplosionColliderRadius(explosionPrefab);
        entityManager.SetComponentData(entity, new PlayerProjectileExplosionData
        {
            Radius = effectiveRadius,
            Damage = Mathf.Max(0f, damage),
            OwnerId = collisionRegistry != null
                ? collisionRegistry.GetOrRegisterOwnerId(owner)
                : 0,
            DamageType = damageType,
            BypassesEnemyShield = bypassesEnemyShield ? (byte)1 : (byte)0
        });
        entityManager.SetComponentData(entity,
            LocalTransform.FromPositionRotationScale(
            position,
            quaternion.identity,
            1f));
        return true;
    }

    public bool TrySpawnContact(
        ProjectileData projectileData,
        Vector3 position,
        ProjectileParams parameters,
        ParentShip owner)
    {
        if (!TryGetContactConfiguration(projectileData, out ContactConfiguration configuration)
            || !EnsureEntityManager())
        {
            return false;
        }

        float2 direction = new(parameters.direction.x, parameters.direction.y);
        if (math.lengthsq(direction) <= 0.000001f)
            direction = new float2(0f, 1f);
        else
            direction = math.normalize(direction);

        if (!TryGetOrCreateContactPrefab(
                projectileData,
                configuration,
                out Entity prefab))
        {
            return false;
        }

        Entity entity = entityManager.Instantiate(prefab);
        entityManager.SetComponentData(entity, new PlayerContactAttackData
        {
            Position = new float2(position.x, position.y),
            RotationRadians = math.atan2(direction.y, direction.x) - math.PI * 0.5f,
            OwnerId = collisionRegistry != null
                ? collisionRegistry.GetOrRegisterOwnerId(owner)
                : 0,
            DebuffSetId = configuration.DebuffSetId,
            Damage = Mathf.Max(0f, parameters.damage),
            DamageType = configuration.DamageType,
            BypassesEnemyShield = configuration.BypassesEnemyShield
        });
        return true;
    }

       public bool TrySpawn(
           ProjectileData projectileData,
           Vector3 position,
           ProjectileParams parameters,
           ParentShip owner)
      {
          return TrySpawnInternal(
              projectileData,
              position,
              parameters,
              owner,
              false,
              0f,
               0f,
               0.02f,
               0,
               0,
               false,
               default);
       }

      /// <summary>
      /// Creates a primary Entity projectile whose secondary projectile uses
      /// runtime values from the owning WeaponData.
      /// </summary>
      public bool TrySpawn(
          ProjectileData projectileData,
          Vector3 position,
          ProjectileParams parameters,
          ParentShip owner,
          bool hasSecondaryRuntimeStats,
          ProjectileRuntimeStats secondaryRuntimeStats)
      {
          return TrySpawnInternal(
              projectileData,
              position,
              parameters,
              owner,
              false,
              0f,
              0f,
              0.02f,
              0,
              0,
              hasSecondaryRuntimeStats,
              secondaryRuntimeStats);
      }

      public bool TrySpawnBallLightning(
          ProjectileData projectileData,
          Vector3 position,
          ProjectileParams parameters,
          ParentShip owner,
          float areaDamage,
          float areaRadius,
          float areaTickInterval,
          int damageLayers)
      {
          return TrySpawnInternal(
              projectileData,
              position,
              parameters,
              owner,
              true,
               areaDamage,
               areaRadius,
               areaTickInterval,
               damageLayers,
               0,
               false,
               default);
       }

      /// <summary>
      /// Resolves a compact secondary-spawn request from the collision bridge.
      /// The ignored target is stored on the new Entity, not as a managed
      /// collider reference.
      /// </summary>
      public bool TrySpawnSecondary(
          ProjectileData projectileData,
          Vector3 position,
          ProjectileParams parameters,
          ParentShip owner,
          int ignoredTargetId)
      {
          return TrySpawnInternal(
              projectileData,
              position,
              parameters,
              owner,
              false,
              0f,
              0f,
              0.02f,
              0,
              ignoredTargetId,
              false,
              default);
      }

      private bool TrySpawnInternal(
          ProjectileData projectileData,
          Vector3 position,
          ProjectileParams parameters,
          ParentShip owner,
          bool hasBallLightningSettings,
           float ballLightningAreaDamage,
           float ballLightningAreaRadius,
           float ballLightningAreaTickInterval,
           int ballLightningDamageLayers,
           int ignoredTargetId,
           bool hasSecondaryRuntimeStats,
           ProjectileRuntimeStats secondaryRuntimeStats)
      {
           if (!TryGetConfiguration(projectileData, out CompiledConfiguration configuration)
               || !EnsureEntityManager()
               || !TryGetVisual(projectileData, out RuntimeVisual visual))
          {
               return false;
           }

           collisionRegistry?.RegisterSecondaryProjectileSpawner(this);

          if (configuration.IsBallLightning && !hasBallLightningSettings)
          {
              LogInvalidConfiguration(
                  projectileData,
                  "Ball Lightning requires runtime area settings from BallLightningData.");
              return false;
          }

        float2 direction = new(parameters.direction.x, parameters.direction.y);
        if (math.lengthsq(direction) < 0.0001f)
            direction = new float2(0f, 1f);
        else
            direction = math.normalize(direction);

        if (parameters.maxAngle > 0f)
        {
            float angle = UnityEngine.Random.Range(
                -parameters.maxAngle,
                parameters.maxAngle);
            direction = math.mul(
                quaternion.RotateZ(math.radians(angle)),
                new float3(direction.x, direction.y, 0f)).xy;
        }

        float initialRange = Mathf.Max(0f, parameters.maxLength);
        if (configuration.SecondaryProjectileConfigId != 0
            && configuration.SecondarySpawnTrigger
                == SecondaryProjectileSpawnTrigger.AfterTravelDistance)
        {
            initialRange = Mathf.Max(
                initialRange,
                configuration.SecondaryTravelDistance);
        }

        if (!TryGetOrCreateProjectilePrefab(
                projectileData,
                configuration,
                visual,
                out Entity prefab))
        {
            return false;
        }

        Entity entity = entityManager.Instantiate(prefab);
        entityManager.SetComponentData(entity, new PlayerProjectileVelocity
        {
            Value = direction * Mathf.Max(0f, parameters.speed)
        });
        entityManager.SetComponentData(entity, new PlayerProjectilePreviousPosition
        {
            Value = new float2(position.x, position.y)
        });
        entityManager.SetComponentData(entity, new PlayerProjectileRemainingRange
        {
            Value = initialRange
        });
        entityManager.SetComponentData(entity, new PlayerProjectileDamage
        {
            Value = Mathf.Max(0f, parameters.damage)
        });
        entityManager.SetComponentData(entity, new PlayerProjectileOwner
        {
            Id = collisionRegistry != null
                ? collisionRegistry.GetOrRegisterOwnerId(owner)
                : 0
        });
        DynamicBuffer<PlayerProjectileIgnoredCollisionTarget> ignoredTargets =
            entityManager.GetBuffer<PlayerProjectileIgnoredCollisionTarget>(entity);
           if (ignoredTargetId != 0)
           {
               ignoredTargets.Add(new PlayerProjectileIgnoredCollisionTarget
               {
                   TargetId = ignoredTargetId
               });
           }
           if (configuration.SecondaryProjectileConfigId != 0)
           {
               ProjectileRuntimeStats secondaryStats = hasSecondaryRuntimeStats
                   ? secondaryRuntimeStats
                   : configuration.SecondaryRuntimeStats;
                entityManager.SetComponentData(entity,
                   new PlayerProjectileSecondarySpawnData
                   {
                       SecondaryProjectileConfigId =
                           configuration.SecondaryProjectileConfigId,
                       SpawnTrigger = configuration.SecondarySpawnTrigger,
                       TravelDistance = configuration.SecondaryTravelDistance,
                       TraveledDistance = 0f,
                       Damage = Mathf.Max(0f, secondaryStats.Damage),
                       Range = Mathf.Max(0f, secondaryStats.Range),
                       Speed = Mathf.Max(0f, secondaryStats.Speed),
                       IgnoreTriggeringEnemy =
                           configuration.IgnoreSecondaryTriggeringEnemy
                   });
           }
        if (configuration.IsBallLightning)
        {
            entityManager.SetComponentData(entity,
                  new PlayerProjectileBallLightningStaticData
                  {
                      AreaDamage = Mathf.Max(0f, ballLightningAreaDamage),
                      AreaRadius = Mathf.Max(0f, ballLightningAreaRadius),
                      AreaTickInterval = Mathf.Max(0.02f, ballLightningAreaTickInterval),
                      DamageLayers = ballLightningDamageLayers
                   });
        }
        if (configuration.IsCircularChain)
        {
            entityManager.SetComponentData(entity,
                  new PlayerProjectileCircularChainState
                  {
                      CurrentTargetId = 0,
                      AttachmentLocalPosition = float2.zero,
                      TravelDirection = direction,
                      TravelSpeed = math.length(direction * Mathf.Max(0f, parameters.speed)),
                      HitsAppliedToCurrentTarget = 0,
                      NextHitTime = 0d,
                      IsAttachedToTarget = 0
                   });
        }
        if (projectileData.TryGetContract(out ProjectileArcNodesContract arcNodes))
        {
            float arcDamageMultiplier = projectileData.Damage > 0f
                ? Mathf.Max(0f, parameters.damage) / projectileData.Damage
                : 1f;
            entityManager.SetComponentData(entity,
                      new PlayerProjectileArcNodesStaticData
                      {
                          NetworkId = projectileData.GetInstanceID(),
                          DamagePerArc = arcNodes.DamagePerArc
                              * arcDamageMultiplier,
                        ConnectionRange = arcNodes.ConnectionRange,
                        PulseInterval = arcNodes.PulseInterval,
                        MaximumConnections = arcNodes.MaximumConnections,
                        HitRadius = arcNodes.ArcHitRadius,
                        VisualDuration = arcNodes.VisualDuration,
                        VisualWidth = arcNodes.VisualWidth,
                        VisualSegments = arcNodes.VisualSegments,
                        VisualJitter = arcNodes.VisualJitter,
                        VisualColor = new float4(
                            arcNodes.VisualColor.r,
                            arcNodes.VisualColor.g,
                            arcNodes.VisualColor.b,
                            arcNodes.VisualColor.a)
                    });
        }
        if (configuration.IsHoming)
        {
            entityManager.SetComponentData(entity, new PlayerProjectileHoming
            {
                RotationSpeedDegrees = configuration.HomingRotationSpeedDegrees,
                TargetId = 0
            });
        }
        entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
            position,
            GetRotation(direction),
            1f));
          return true;
    }

    public void Dispose()
    {
        DestroySpawnedEntities();
        DestroyEntityPrefabs();

        foreach (RuntimeVisual visual in visuals.Values)
        {
            if (visual.Material != null)
                DestroyRuntimeObject(visual.Material);

            if (visual.Mesh != null)
                DestroyRuntimeObject(visual.Mesh);
        }

        foreach (ExplosionRuntimeVisual visual in explosionVisuals.Values)
        {
            if (visual.RuntimeVisual.Material != null)
                DestroyRuntimeObject(visual.RuntimeVisual.Material);

            if (visual.RuntimeVisual.Mesh != null)
                DestroyRuntimeObject(visual.RuntimeVisual.Mesh);
        }

        configurations.Clear();
        contactConfigurations.Clear();
        visuals.Clear();
        explosionVisuals.Clear();
        projectileEntityPrefabs.Clear();
        contactEntityPrefabs.Clear();
        explosionEntityPrefabs.Clear();
        invalidConfigurationsLogged.Clear();
        invalidExplosionConfigurationsLogged.Clear();
        entityWorld = null;
    }

    private void DestroySpawnedEntities()
    {
        if (entityWorld == null || !entityWorld.IsCreated)
            return;

        EntityQuery projectiles = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerProjectileStaticData>());
        entityManager.DestroyEntity(projectiles);

        EntityQuery explosions = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerProjectileExplosionData>());
        entityManager.DestroyEntity(explosions);
    }

    private void DestroyEntityPrefabs()
    {
        if (entityWorld == null || !entityWorld.IsCreated)
            return;

        DestroyEntityPrefabs(projectileEntityPrefabs.Values);
        DestroyEntityPrefabs(contactEntityPrefabs.Values);
        DestroyEntityPrefabs(explosionEntityPrefabs.Values);
    }

    private void DestroyEntityPrefabs(Dictionary<ProjectileData, Entity>.ValueCollection prefabs)
    {
        foreach (Entity prefab in prefabs)
        {
            if (entityManager.Exists(prefab))
                entityManager.DestroyEntity(prefab);
        }
    }

    private void DestroyEntityPrefabs(Dictionary<Explode, Entity>.ValueCollection prefabs)
    {
        foreach (Entity prefab in prefabs)
        {
            if (entityManager.Exists(prefab))
                entityManager.DestroyEntity(prefab);
        }
    }

    private bool TryGetOrCreateProjectilePrefab(
        ProjectileData projectileData,
        CompiledConfiguration configuration,
        RuntimeVisual visual,
        out Entity prefab)
    {
        if (projectileEntityPrefabs.TryGetValue(projectileData, out prefab)
            && entityManager.Exists(prefab))
        {
            return true;
        }

        prefab = CreateProjectilePrefab(projectileData, configuration, visual);
        projectileEntityPrefabs[projectileData] = prefab;
        return true;
    }

    private Entity CreateProjectilePrefab(
        ProjectileData projectileData,
        CompiledConfiguration configuration,
        RuntimeVisual visual)
    {
        Entity prefab = entityManager.CreateEntity();
        entityManager.AddComponentData(prefab, new PlayerProjectileStaticData
        {
            Radius = configuration.Radius,
            Lifetime = configuration.Lifetime,
            DamageType = configuration.DamageType,
            ContactMode = configuration.ContactMode,
            ContinuousDamageInterval = configuration.ContinuousDamageInterval,
            ExplosionConfigId = configuration.ExplosionConfigId,
            ExplodesAtMaximumRange = configuration.ExplodesAtMaximumRange,
            DebuffSetId = configuration.DebuffSetId,
            BypassesEnemyShield = configuration.BypassesEnemyShield
        });
        entityManager.AddComponentData(prefab, new PlayerProjectileVelocity());
        entityManager.AddComponentData(prefab, new PlayerProjectilePreviousPosition());
        entityManager.AddComponentData(prefab, new PlayerProjectileRemainingRange());
        entityManager.AddComponentData(prefab, new PlayerProjectileRemainingLifetime
        {
            Value = configuration.Lifetime
        });
        entityManager.AddComponentData(prefab, new PlayerProjectileDamage());
        entityManager.AddComponentData(prefab, new PlayerProjectileOwner());
        entityManager.AddComponentData(prefab, new PlayerProjectileScaleState
        {
            RootScale = configuration.InitialRootScale,
            InitialRootScale = configuration.InitialRootScale,
            VisualScalePerRootUnit = configuration.VisualScalePerRootUnit,
            GrowthPerSecond = configuration.ScaleGrowthPerSecond
        });
        entityManager.AddComponentData(prefab, new PlayerProjectileResolution
        {
            Kind = PlayerProjectileResolutionKind.None,
            TargetId = 0,
            TargetKind = PlayerProjectileCollisionTargetKind.Enemy
        });
        entityManager.AddBuffer<PlayerProjectileHitTarget>(prefab);
        entityManager.AddBuffer<PlayerProjectileContinuousHitTarget>(prefab);
        entityManager.AddBuffer<PlayerProjectileHitEntityDamageReceiver>(prefab);
        entityManager.AddBuffer<PlayerProjectileContinuousEntityDamageReceiver>(prefab);
        entityManager.AddBuffer<PlayerProjectileIgnoredCollisionTarget>(prefab);

        if (configuration.SecondaryProjectileConfigId != 0)
        {
            ProjectileRuntimeStats secondaryStats = configuration.SecondaryRuntimeStats;
            entityManager.AddComponentData(prefab, new PlayerProjectileSecondarySpawnData
            {
                SecondaryProjectileConfigId = configuration.SecondaryProjectileConfigId,
                SpawnTrigger = configuration.SecondarySpawnTrigger,
                TravelDistance = configuration.SecondaryTravelDistance,
                TraveledDistance = 0f,
                Damage = Mathf.Max(0f, secondaryStats.Damage),
                Range = Mathf.Max(0f, secondaryStats.Range),
                Speed = Mathf.Max(0f, secondaryStats.Speed),
                IgnoreTriggeringEnemy = configuration.IgnoreSecondaryTriggeringEnemy
            });
        }

        if (configuration.IsResonanceSphere)
        {
            entityManager.AddComponentData(prefab,
                new PlayerProjectileResonanceSphereStaticData
                {
                    MaximumStoredDamage = configuration.ResonanceSphereMaximumStoredDamage,
                    ExplosionRadius = configuration.ResonanceSphereExplosionRadius,
                    WaveSpeed = configuration.ResonanceSphereWaveSpeed,
                    SpriteDiameter = configuration.SpriteDiameter,
                    InitialAlpha = configuration.InitialAlpha,
                    SlowdownStartY = configuration.ResonanceSphereSlowdownStartY,
                    DetonationY = configuration.ResonanceSphereDetonationY,
                    SlowdownDuration = configuration.ResonanceSphereSlowdownDuration,
                    FullChargeDetonationDelay = configuration
                        .ResonanceSphereFullChargeDetonationDelay
                });
            entityManager.AddComponentData(prefab,
                new PlayerProjectileResonanceSphereState());
            entityManager.AddBuffer<PlayerProjectileResonanceSphereDamagedTarget>(prefab);
        }

        if (configuration.IsBallLightning)
        {
            entityManager.AddComponentData(prefab,
                new PlayerProjectileBallLightningStaticData());
            entityManager.AddComponentData(prefab,
                new PlayerProjectileBallLightningState
                {
                    NextPulseTime = -1d
                });
            entityManager.AddBuffer<PlayerProjectileBallLightningDirectContact>(prefab);
        }

        if (configuration.IsCircularChain)
        {
            entityManager.AddComponentData(prefab,
                new PlayerProjectileCircularChainStaticData
                {
                    HitsPerTarget = configuration.CircularChainHitsPerTarget,
                    DamagePerHit = configuration.CircularChainDamagePerHit,
                    HitInterval = configuration.CircularChainHitInterval,
                    MaximumTargets = configuration.CircularChainMaximumTargets,
                    SearchConeAngle = configuration.CircularChainSearchConeAngle,
                    SearchRange = configuration.CircularChainSearchRange,
                    RandomEscapeAngle = configuration.CircularChainRandomEscapeAngle
                });
            entityManager.AddComponentData(prefab,
                new PlayerProjectileCircularChainState
                {
                    TravelDirection = new float2(0f, 1f)
                });
            entityManager.AddBuffer<PlayerProjectileCircularChainVisitedTarget>(prefab);
        }

        if (projectileData.TryGetContract(out ProjectileArcNodesContract arcNodes))
        {
            entityManager.AddComponentData(prefab,
                new PlayerProjectileArcNodesStaticData
                {
                    NetworkId = projectileData.GetInstanceID(),
                    DamagePerArc = arcNodes.DamagePerArc,
                    ConnectionRange = arcNodes.ConnectionRange,
                    PulseInterval = arcNodes.PulseInterval,
                    MaximumConnections = arcNodes.MaximumConnections,
                    HitRadius = arcNodes.ArcHitRadius,
                    VisualDuration = arcNodes.VisualDuration,
                    VisualWidth = arcNodes.VisualWidth,
                    VisualSegments = arcNodes.VisualSegments,
                    VisualJitter = arcNodes.VisualJitter,
                    VisualColor = new float4(
                        arcNodes.VisualColor.r,
                        arcNodes.VisualColor.g,
                        arcNodes.VisualColor.b,
                        arcNodes.VisualColor.a)
                });
            entityManager.AddComponentData(prefab,
                new PlayerProjectileArcNodesState
                {
                    NextPulseTime = -1d
                });
        }

        if (configuration.IsHoming)
        {
            entityManager.AddComponentData(prefab, new PlayerProjectileHoming
            {
                RotationSpeedDegrees = configuration.HomingRotationSpeedDegrees,
                TargetId = 0
            });
        }

        entityManager.AddComponentData(
            prefab,
            LocalTransform.FromPositionRotationScale(
                Vector3.zero,
                quaternion.identity,
                1f));
        entityManager.AddComponentData(prefab, new PostTransformMatrix
        {
            Value = float4x4.Scale(new float3(
                configuration.VisualScale.x,
                configuration.VisualScale.y,
                1f))
        });
        RenderMeshUtility.AddComponents(
            prefab,
            entityManager,
            visual.Description,
            visual.RenderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(prefab, new URPMaterialPropertyBaseColor
        {
            Value = new float4(
                configuration.InitialColor.r,
                configuration.InitialColor.g,
                configuration.InitialColor.b,
                configuration.InitialColor.a)
        });
        entityManager.AddComponent<Prefab>(prefab);
        return prefab;
    }

    private bool TryGetOrCreateContactPrefab(
        ProjectileData projectileData,
        ContactConfiguration configuration,
        out Entity prefab)
    {
        if (contactEntityPrefabs.TryGetValue(projectileData, out prefab)
            && entityManager.Exists(prefab))
        {
            return true;
        }

        prefab = entityManager.CreateEntity();
        entityManager.AddComponentData(prefab, new PlayerContactAttackData
        {
            DamageType = configuration.DamageType,
            DebuffSetId = configuration.DebuffSetId,
            BypassesEnemyShield = configuration.BypassesEnemyShield
        });
        DynamicBuffer<PlayerContactPolygonVertex> vertices =
            entityManager.AddBuffer<PlayerContactPolygonVertex>(prefab);
        for (int index = 0; index < configuration.LocalVertices.Length; index++)
        {
            vertices.Add(new PlayerContactPolygonVertex
            {
                LocalPosition = configuration.LocalVertices[index]
            });
        }

        entityManager.AddBuffer<PlayerContactHitTarget>(prefab);
        entityManager.AddComponent<Prefab>(prefab);
        contactEntityPrefabs[projectileData] = prefab;
        return true;
    }

    private bool TryGetOrCreateExplosionPrefab(
        Explode explosionPrefab,
        ExplosionRuntimeVisual visual,
        out Entity prefab)
    {
        if (explosionEntityPrefabs.TryGetValue(explosionPrefab, out prefab)
            && entityManager.Exists(prefab))
        {
            return true;
        }

        prefab = entityManager.CreateEntity();
        entityManager.AddComponentData(prefab, new PlayerProjectileExplosionData());
        entityManager.AddComponentData(prefab,
            new PlayerProjectileExplosionRemainingLifetime
            {
                Value = explosionPrefab.ActiveTime
            });
        entityManager.AddBuffer<PlayerProjectileExplosionHitTarget>(prefab);
        entityManager.AddComponentData(
            prefab,
            LocalTransform.FromPositionRotationScale(
                Vector3.zero,
                quaternion.identity,
                1f));
        entityManager.AddComponentData(prefab, new PostTransformMatrix
        {
            Value = float4x4.Scale(new float3(
                visual.VisualScale.x,
                visual.VisualScale.y,
                1f))
        });
        RenderMeshUtility.AddComponents(
            prefab,
            entityManager,
            visual.RuntimeVisual.Description,
            visual.RuntimeVisual.RenderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponentData(prefab, new URPMaterialPropertyBaseColor
        {
            Value = new float4(
                visual.Color.r,
                visual.Color.g,
                visual.Color.b,
                visual.Color.a)
        });
        entityManager.AddComponent<Prefab>(prefab);
        explosionEntityPrefabs[explosionPrefab] = prefab;
        return true;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object runtimeObject)
    {
        if (runtimeObject == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(runtimeObject);
            return;
        }
#endif

        UnityEngine.Object.Destroy(runtimeObject);
    }

    private bool TryGetContactConfiguration(
        ProjectileData projectileData,
        out ContactConfiguration configuration)
    {
        if (projectileData != null
            && contactConfigurations.TryGetValue(projectileData, out configuration))
        {
            return true;
        }

        configuration = default;
        if (projectileData == null)
        {
            LogInvalidConfiguration(projectileData, "Contact ProjectileData is missing.");
            return false;
        }

        if (!projectileData.UsesEntities)
        {
            LogInvalidConfiguration(
                projectileData,
                "Contact delivery requires the Entities simulation backend.");
            return false;
        }

        if (projectileData.DeliveryType != ProjectileDeliveryType.Contact)
        {
            LogInvalidConfiguration(
                projectileData,
                "The Contact spawner requires Contact delivery.");
            return false;
        }

        if (!projectileData.TryGetContract(
                out ProjectileContactAreaContract contactAreaContract)
            || contactAreaContract.ContactArea == null)
        {
            LogInvalidConfiguration(
                projectileData,
                "Contact delivery requires a Contact Area contract with a prefab.");
            return false;
        }

        if (!contactAreaContract.ContactArea.TryGetLocalPolygon(
                out Vector2[] localVertices))
        {
            LogInvalidConfiguration(
                projectileData,
                "The Contact Area prefab requires one PolygonCollider2D path with at least three vertices.");
            return false;
        }

        ProjectileRuntimeConfig runtimeConfig = projectileData.CreateRuntimeConfig();
        configuration = new ContactConfiguration(
            localVertices,
            projectileData.DamageType,
            collisionRegistry != null
                ? collisionRegistry.GetOrRegisterDebuffSetId(runtimeConfig.enemyDebuffs)
                : 0,
            false);
        contactConfigurations.Add(projectileData, configuration);
        return true;
    }

    private bool TryGetConfiguration(
        ProjectileData projectileData,
        out CompiledConfiguration configuration)
    {
        if (projectileData != null
            && configurations.TryGetValue(projectileData, out configuration))
        {
            return true;
        }

        configuration = default;
        if (!IsSupported(projectileData, out string reason))
        {
            LogInvalidConfiguration(projectileData, reason);
            return false;
        }

        Projectile projectilePrefab = projectileData.ProjectilePrefab;
        SpriteRenderer renderer = projectilePrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null || renderer.sprite == null)
        {
            LogInvalidConfiguration(
                projectileData,
                "The projectile prefab requires a SpriteRenderer with a Sprite.");
            return false;
        }

        ProjectileRuntimeConfig runtimeConfig = projectileData.CreateRuntimeConfig();
        Vector2 initialRootScale = GetInitialRootScale(projectilePrefab);
        Vector2 visualScale = GetVisualScale(renderer);
        configuration = new CompiledConfiguration(
            Mathf.Max(0.001f, GetCollisionRadius(projectilePrefab, renderer)),
            Mathf.Max(0.02f, runtimeConfig.projectileLifetime),
            projectileData.DamageType,
              false,
               runtimeConfig.contactMode,
               Mathf.Max(0.02f, runtimeConfig.continuousDamageInterval),
               runtimeConfig.explodeAtMaximumRange,
               runtimeConfig.isResonanceSphere,
              Mathf.Max(0f, runtimeConfig.resonanceSphereMaximumStoredDamage),
              Mathf.Max(0f, runtimeConfig.resonanceSphereExplosionRadius),
              Mathf.Max(0.01f, runtimeConfig.resonanceSphereWaveSpeed),
              runtimeConfig.resonanceSphereSlowdownStartY,
              Mathf.Max(
                  runtimeConfig.resonanceSphereSlowdownStartY + 0.01f,
                  runtimeConfig.resonanceSphereDetonationY),
              Mathf.Max(0.02f, runtimeConfig.resonanceSphereSlowdownDuration),
              Mathf.Max(0f, runtimeConfig.resonanceSphereFullChargeDetonationDelay),
              Mathf.Max(0.01f, renderer.sprite.bounds.size.x),
              renderer.color,
              runtimeConfig.contactMode == ProjectileContactMode.BallLightning,
              runtimeConfig.contactMode == ProjectileContactMode.CircularChain,
              Mathf.Max(1, runtimeConfig.circularChainHitsPerTarget),
              Mathf.Max(0f, runtimeConfig.circularChainDamagePerHit),
              Mathf.Max(0.02f, runtimeConfig.circularChainHitInterval),
              Mathf.Max(1, runtimeConfig.circularChainMaximumTargets),
              Mathf.Clamp(runtimeConfig.circularChainSearchConeAngle, 0f, 360f),
              Mathf.Max(0.01f, runtimeConfig.circularChainSearchRange),
              Mathf.Clamp(runtimeConfig.circularChainRandomEscapeAngle, 0f, 360f),
               runtimeConfig.flightMode == ProjectileFlightMode.Homing,
             Mathf.Max(0f, runtimeConfig.homingRotationSpeed),
            visualScale,
            initialRootScale,
            GetVisualScalePerRootUnit(visualScale, initialRootScale),
            runtimeConfig.growDuringFlight
                ? runtimeConfig.scaleGrowthPerSecond
                : Vector2.zero,
            collisionRegistry != null
                ? collisionRegistry.GetOrRegisterDebuffSetId(runtimeConfig.enemyDebuffs)
                : 0,
             collisionRegistry != null
                 ? collisionRegistry.GetOrRegisterExplosionConfigId(
                     projectileData,
                     runtimeConfig)
                 : 0,
             collisionRegistry != null
                 ? collisionRegistry.GetOrRegisterSecondaryProjectileConfigId(
                     runtimeConfig.secondaryProjectile)
                 : 0,
             runtimeConfig.secondarySpawnTrigger,
             Mathf.Max(0f, runtimeConfig.secondaryTravelDistance),
             runtimeConfig.ignoreSecondaryProjectileTriggeringEnemy,
             runtimeConfig.secondaryProjectile != null
                 ? runtimeConfig.secondaryProjectile.GetRuntimeStats()
                 : default);
        configurations.Add(projectileData, configuration);
        return true;
    }

    private static bool IsSupported(ProjectileData projectileData, out string reason)
    {
        if (projectileData == null)
        {
            reason = "ProjectileData is missing.";
            return false;
        }

        if (!projectileData.UsesEntities)
        {
            reason = "Simulation Backend must be set to Entities.";
            return false;
        }

        if (projectileData.DeliveryType != ProjectileDeliveryType.Projectile)
        {
            reason = "Only physical Projectile delivery is supported.";
            return false;
        }

        if (projectileData.ProjectilePrefab == null)
        {
            reason = "The physical projectile prefab is missing.";
            return false;
        }

        ProjectileRuntimeConfig runtimeConfig = projectileData.CreateRuntimeConfig();
          bool supportsContactMode = runtimeConfig.contactMode
              == ProjectileContactMode.DamageAndDestroy
              || runtimeConfig.contactMode == ProjectileContactMode.PierceOnce
               || runtimeConfig.contactMode == ProjectileContactMode.PierceContinuous
               || runtimeConfig.contactMode == ProjectileContactMode.ExplodeAndSpawn
               || runtimeConfig.contactMode == ProjectileContactMode.ExplodeOnContact
              || runtimeConfig.contactMode == ProjectileContactMode.CircularChain
              || runtimeConfig.contactMode == ProjectileContactMode.BallLightning
                || (runtimeConfig.contactMode == ProjectileContactMode.Ignore
                    && (runtimeConfig.isResonanceSphere
                        || runtimeConfig.isArcNode));
          if ((runtimeConfig.flightMode != ProjectileFlightMode.Straight
                  && runtimeConfig.flightMode != ProjectileFlightMode.Homing)
              || !supportsContactMode
            || runtimeConfig.fadeDuringLifetime
            || runtimeConfig.disableColliderAfterFirstPhysicsStep
            || projectileData.TryGetDamageSource(
                ProjectileDamageTrigger.Contact,
                out _))
        {
              reason = "Only straight or homing Damage And Destroy, Pierce Once, "
                + "Pierce Continuous, Explode And Spawn, Explode On Contact, Circular Chain, "
                  + "Ball Lightning, Resonance Sphere, or Arc Nodes projectiles "
                    + "without fade, "
                + "collider-lifetime or custom contact damage-source contracts are supported.";
            return false;
        }

        reason = null;
        return true;
    }

    private bool EnsureEntityManager()
    {
        if (entityWorld != null && entityWorld.IsCreated)
            return true;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError(
                $"{nameof(PlayerProjectileEcsSpawner)} needs an active default ECS World.");
            return false;
        }

        entityWorld = world;
        entityManager = world.EntityManager;
        return true;
    }

      private bool TryGetVisual(ProjectileData projectileData, out RuntimeVisual visual)
      {
          if (visuals.TryGetValue(projectileData, out visual))
          {
              if (visual.Mesh != null && visual.Material != null)
                  return true;

              visuals.Remove(projectileData);
          }

        SpriteRenderer renderer = projectileData?.ProjectilePrefab
            ?.GetComponentInChildren<SpriteRenderer>(true);
        Sprite sprite = renderer != null ? renderer.sprite : null;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (sprite == null || shader == null)
        {
            LogInvalidConfiguration(
                projectileData,
                sprite == null
                    ? "The projectile prefab has no SpriteRenderer Sprite."
                    : "The Universal Render Pipeline/Unlit shader was not found.");
            return false;
        }

        Mesh mesh = CreateSpriteMesh(sprite, projectileData.name);
        Material material = CreateSpriteMaterial(
            shader,
            sprite,
            renderer.color,
            projectileData.name);
        visual = new RuntimeVisual(
            mesh,
            material,
            new RenderMeshArray(new[] { material }, new[] { mesh }),
            new RenderMeshDescription(ShadowCastingMode.Off, false));
        visuals.Add(projectileData, visual);
        return true;
    }

    private bool TryGetExplosionVisual(
        Explode explosionPrefab,
        out ExplosionRuntimeVisual visual)
    {
        if (explosionVisuals.TryGetValue(explosionPrefab, out visual))
            return true;

        SpriteRenderer renderer = explosionPrefab
            .GetComponentInChildren<SpriteRenderer>(true);
        Sprite sprite = renderer != null ? renderer.sprite : null;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (sprite == null || shader == null)
        {
            if (invalidExplosionConfigurationsLogged.Add(explosionPrefab))
            {
                Debug.LogError(
                    $"Entity explosion '{explosionPrefab.name}' cannot be spawned: "
                    + (sprite == null
                        ? "the prefab has no SpriteRenderer Sprite."
                        : "the Universal Render Pipeline/Unlit shader was not found."),
                    explosionPrefab);
            }

            visual = default;
            return false;
        }

        Mesh mesh = CreateSpriteMesh(sprite, explosionPrefab.name);
        Material material = CreateSpriteMaterial(
            shader,
            sprite,
            renderer.color,
            explosionPrefab.name);
        RuntimeVisual runtimeVisual = new(
            mesh,
            material,
            new RenderMeshArray(new[] { material }, new[] { mesh }),
            new RenderMeshDescription(ShadowCastingMode.Off, false));
        visual = new ExplosionRuntimeVisual(
            runtimeVisual,
            GetVisualScale(renderer),
            renderer.color);
        explosionVisuals.Add(explosionPrefab, visual);
        return true;
    }

    private void LogInvalidConfiguration(ProjectileData projectileData, string reason)
    {
        if (projectileData == null || !invalidConfigurationsLogged.Add(projectileData))
            return;

        Debug.LogError(
            $"Entity projectile '{projectileData.name}' cannot be spawned: {reason}",
            projectileData);
    }

    private static float GetCollisionRadius(
        Projectile projectilePrefab,
        SpriteRenderer renderer)
    {
        Collider2D collider = projectilePrefab.GetComponentInChildren<Collider2D>(true);
        if (collider is BoxCollider2D box)
        {
            Vector3 scale = GetAbsoluteScale(box.transform.lossyScale);
            return 0.5f * Mathf.Max(
                box.size.x * scale.x,
                box.size.y * scale.y);
        }

        if (collider is CircleCollider2D circle)
        {
            Vector3 scale = GetAbsoluteScale(circle.transform.lossyScale);
            return circle.radius * Mathf.Max(scale.x, scale.y);
        }

        if (collider is CapsuleCollider2D capsule)
        {
            Vector3 scale = GetAbsoluteScale(capsule.transform.lossyScale);
            return 0.5f * Mathf.Max(
                capsule.size.x * scale.x,
                capsule.size.y * scale.y);
        }

        Vector3 rendererScale = GetAbsoluteScale(renderer.transform.lossyScale);
        Vector3 extents = renderer.sprite.bounds.extents;
        return Mathf.Max(
            extents.x * rendererScale.x,
            extents.y * rendererScale.y);
    }

    private static float GetExplosionColliderRadius(Explode explosionPrefab)
    {
        Collider2D collider = explosionPrefab.GetComponentInChildren<Collider2D>(true);
        if (collider is CircleCollider2D circle)
        {
            Vector3 scale = GetAbsoluteScale(circle.transform.lossyScale);
            return circle.radius * Mathf.Max(scale.x, scale.y);
        }

        if (collider is BoxCollider2D box)
        {
            Vector3 scale = GetAbsoluteScale(box.transform.lossyScale);
            return 0.5f * Mathf.Max(
                box.size.x * scale.x,
                box.size.y * scale.y);
        }

        if (collider is CapsuleCollider2D capsule)
        {
            Vector3 scale = GetAbsoluteScale(capsule.transform.lossyScale);
            return 0.5f * Mathf.Max(
                capsule.size.x * scale.x,
                capsule.size.y * scale.y);
        }

        return 0f;
    }

    private static Vector2 GetVisualScale(SpriteRenderer renderer)
    {
        Vector3 scale = GetAbsoluteScale(renderer.transform.lossyScale);
        return new Vector2(scale.x, scale.y);
    }

    private static Vector2 GetInitialRootScale(Projectile projectilePrefab)
    {
        Vector3 scale = GetAbsoluteScale(projectilePrefab.transform.localScale);
        return new Vector2(
            Mathf.Max(0.001f, scale.x),
            Mathf.Max(0.001f, scale.y));
    }

    private static Vector2 GetVisualScalePerRootUnit(
        Vector2 visualScale,
        Vector2 rootScale)
    {
        return new Vector2(
            visualScale.x / Mathf.Max(0.001f, rootScale.x),
            visualScale.y / Mathf.Max(0.001f, rootScale.y));
    }

    private static Vector3 GetAbsoluteScale(Vector3 scale)
    {
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
    }

    private static Mesh CreateSpriteMesh(Sprite sprite, string configurationName)
    {
        Vector2[] sourceVertices = sprite.vertices;
        Vector3[] vertices = new Vector3[sourceVertices.Length];
        for (int index = 0; index < sourceVertices.Length; index++)
        {
            Vector2 source = sourceVertices[index];
            vertices[index] = new Vector3(source.x, source.y, 0f);
        }

        ushort[] sourceTriangles = sprite.triangles;
        int[] triangles = new int[sourceTriangles.Length];
        for (int index = 0; index < sourceTriangles.Length; index++)
            triangles[index] = sourceTriangles[index];

        var mesh = new Mesh
        {
            name = $"{configurationName} Player ECS Projectile Mesh"
        };
        mesh.vertices = vertices;
        mesh.uv = sprite.uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateSpriteMaterial(
        Shader shader,
        Sprite sprite,
        Color color,
        string configurationName)
    {
          var material = new Material(shader)
          {
              name = $"{configurationName} Player ECS Projectile Material",
              renderQueue = ProjectileRenderQueue,
              enableInstancing = true
          };
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", sprite.texture);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    private static quaternion GetRotation(float2 direction)
    {
        return quaternion.RotateZ(math.atan2(direction.y, direction.x)
            + math.PI * 0.5f);
    }

    private readonly struct ContactConfiguration
    {
        public readonly Vector2[] LocalVertices;
        public readonly EnemyDamageType DamageType;
        public readonly int DebuffSetId;
        public readonly byte BypassesEnemyShield;

        public ContactConfiguration(
            Vector2[] localVertices,
            EnemyDamageType damageType,
            int debuffSetId,
            bool bypassesEnemyShield)
        {
            LocalVertices = localVertices;
            DamageType = damageType;
            DebuffSetId = debuffSetId;
            BypassesEnemyShield = bypassesEnemyShield ? (byte)1 : (byte)0;
        }
    }

    private readonly struct CompiledConfiguration
    {
        public readonly float Radius;
        public readonly float Lifetime;
          public readonly EnemyDamageType DamageType;
           public readonly ProjectileContactMode ContactMode;
           public readonly float ContinuousDamageInterval;
           public readonly byte ExplodesAtMaximumRange;
          public readonly bool IsResonanceSphere;
          public readonly float ResonanceSphereMaximumStoredDamage;
          public readonly float ResonanceSphereExplosionRadius;
          public readonly float ResonanceSphereWaveSpeed;
          public readonly float ResonanceSphereSlowdownStartY;
          public readonly float ResonanceSphereDetonationY;
          public readonly float ResonanceSphereSlowdownDuration;
          public readonly float ResonanceSphereFullChargeDetonationDelay;
          public readonly float SpriteDiameter;
          public readonly Color InitialColor;
          public readonly float InitialAlpha;
          public readonly bool IsBallLightning;
          public readonly bool IsCircularChain;
          public readonly int CircularChainHitsPerTarget;
          public readonly float CircularChainDamagePerHit;
          public readonly float CircularChainHitInterval;
          public readonly int CircularChainMaximumTargets;
          public readonly float CircularChainSearchConeAngle;
          public readonly float CircularChainSearchRange;
          public readonly float CircularChainRandomEscapeAngle;
          public readonly bool IsHoming;
        public readonly float HomingRotationSpeedDegrees;
        public readonly byte BypassesEnemyShield;
        public readonly Vector2 VisualScale;
        public readonly Vector2 InitialRootScale;
        public readonly Vector2 VisualScalePerRootUnit;
        public readonly Vector2 ScaleGrowthPerSecond;
        public readonly int DebuffSetId;
           public readonly int ExplosionConfigId;
           public readonly int SecondaryProjectileConfigId;
           public readonly SecondaryProjectileSpawnTrigger SecondarySpawnTrigger;
           public readonly float SecondaryTravelDistance;
           public readonly byte IgnoreSecondaryTriggeringEnemy;
           public readonly ProjectileRuntimeStats SecondaryRuntimeStats;

        public CompiledConfiguration(
            float radius,
            float lifetime,
            EnemyDamageType damageType,
               bool bypassesEnemyShield,
               ProjectileContactMode contactMode,
               float continuousDamageInterval,
               bool explodesAtMaximumRange,
              bool isResonanceSphere,
              float resonanceSphereMaximumStoredDamage,
              float resonanceSphereExplosionRadius,
              float resonanceSphereWaveSpeed,
              float resonanceSphereSlowdownStartY,
              float resonanceSphereDetonationY,
              float resonanceSphereSlowdownDuration,
              float resonanceSphereFullChargeDetonationDelay,
              float spriteDiameter,
              Color initialColor,
              bool isBallLightning,
              bool isCircularChain,
              int circularChainHitsPerTarget,
              float circularChainDamagePerHit,
              float circularChainHitInterval,
              int circularChainMaximumTargets,
              float circularChainSearchConeAngle,
              float circularChainSearchRange,
              float circularChainRandomEscapeAngle,
              bool isHoming,
            float homingRotationSpeedDegrees,
            Vector2 visualScale,
            Vector2 initialRootScale,
            Vector2 visualScalePerRootUnit,
               Vector2 scaleGrowthPerSecond,
               int debuffSetId,
             int explosionConfigId,
             int secondaryProjectileConfigId,
             SecondaryProjectileSpawnTrigger secondarySpawnTrigger,
             float secondaryTravelDistance,
             bool ignoreSecondaryTriggeringEnemy,
             ProjectileRuntimeStats secondaryRuntimeStats)
        {
            Radius = radius;
            Lifetime = lifetime;
              DamageType = damageType;
               ContactMode = contactMode;
               ContinuousDamageInterval = continuousDamageInterval;
               ExplodesAtMaximumRange = explodesAtMaximumRange ? (byte)1 : (byte)0;
              IsResonanceSphere = isResonanceSphere;
              ResonanceSphereMaximumStoredDamage = resonanceSphereMaximumStoredDamage;
              ResonanceSphereExplosionRadius = resonanceSphereExplosionRadius;
              ResonanceSphereWaveSpeed = resonanceSphereWaveSpeed;
              ResonanceSphereSlowdownStartY = resonanceSphereSlowdownStartY;
              ResonanceSphereDetonationY = resonanceSphereDetonationY;
              ResonanceSphereSlowdownDuration = resonanceSphereSlowdownDuration;
              ResonanceSphereFullChargeDetonationDelay =
                  resonanceSphereFullChargeDetonationDelay;
              SpriteDiameter = spriteDiameter;
              InitialColor = initialColor;
              InitialAlpha = Mathf.Clamp01(initialColor.a);
              IsBallLightning = isBallLightning;
              IsCircularChain = isCircularChain;
              CircularChainHitsPerTarget = circularChainHitsPerTarget;
              CircularChainDamagePerHit = circularChainDamagePerHit;
              CircularChainHitInterval = circularChainHitInterval;
              CircularChainMaximumTargets = circularChainMaximumTargets;
              CircularChainSearchConeAngle = circularChainSearchConeAngle;
              CircularChainSearchRange = circularChainSearchRange;
              CircularChainRandomEscapeAngle = circularChainRandomEscapeAngle;
              IsHoming = isHoming;
            HomingRotationSpeedDegrees = homingRotationSpeedDegrees;
            BypassesEnemyShield = bypassesEnemyShield ? (byte)1 : (byte)0;
            VisualScale = visualScale;
            InitialRootScale = initialRootScale;
            VisualScalePerRootUnit = visualScalePerRootUnit;
            ScaleGrowthPerSecond = scaleGrowthPerSecond;
             DebuffSetId = debuffSetId;
             ExplosionConfigId = explosionConfigId;
             SecondaryProjectileConfigId = secondaryProjectileConfigId;
             SecondarySpawnTrigger = secondarySpawnTrigger;
             SecondaryTravelDistance = secondaryTravelDistance;
             IgnoreSecondaryTriggeringEnemy =
                 ignoreSecondaryTriggeringEnemy ? (byte)1 : (byte)0;
             SecondaryRuntimeStats = secondaryRuntimeStats;
        }
    }

    private sealed class RuntimeVisual
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly RenderMeshArray RenderMeshArray;
        public readonly RenderMeshDescription Description;

        public RuntimeVisual(
            Mesh mesh,
            Material material,
            RenderMeshArray renderMeshArray,
            RenderMeshDescription description)
        {
            Mesh = mesh;
            Material = material;
            RenderMeshArray = renderMeshArray;
            Description = description;
        }
    }

    private readonly struct ExplosionRuntimeVisual
    {
        public readonly RuntimeVisual RuntimeVisual;
        public readonly Vector2 VisualScale;
        public readonly Color Color;

        public ExplosionRuntimeVisual(
            RuntimeVisual runtimeVisual,
            Vector2 visualScale,
            Color color)
        {
            RuntimeVisual = runtimeVisual;
            VisualScale = visualScale;
            Color = color;
        }
    }
}
