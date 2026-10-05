using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

/// <summary>
/// Creates the first ECS enemy-projectile slice from existing enemy attack behaviours.
/// Mesh and material are created once per projectile configuration and reused by all shots.
/// </summary>
public sealed class EnemyProjectileEcsSpawner : IDisposable
{
    private const int ProjectileRenderQueue = 3100;
    private const int CustomSpeedCurveSampleCount = 32;

    private readonly Dictionary<EnemyProjectileAuthoring, RuntimeVisual>
        visualsByConfiguration = new();
    private readonly Dictionary<EnemyBullet, EnemyProjectileAuthoring>
        configurationsByPrefab = new();
    private readonly Dictionary<RuntimeProjectileTemplateKey, Entity>
        templatesByKey = new();
    private readonly HashSet<EnemyProjectileAuthoring> invalidConfigurationsLogged = new();
    private readonly List<EnemyProjectileSpawnRequest> singleSpawnRequestBuffer = new(1);
    private readonly List<PreparedProjectileSpawn> preparedSpawns = new(64);

    [Inject] private EnemyProjectileCollisionRegistry collisionRegistry;

    private EntityManager entityManager;
    private World entityWorld;

    public bool TrySpawn(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier)
    {
        return TrySpawn(
            legacyPrefab,
            position,
            direction,
            damageMultiplier,
            0f,
            EnemyProjectileSpeedBehavior.None,
            0f,
            null,
            EnemyProjectileSizeBehavior.Default,
            0f,
            1f,
            null,
            1f);
    }

    public bool TrySpawn(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier,
        float initialSpeed,
        EnemyProjectileSpeedBehavior speedBehavior,
        float speedChangeY,
        AnimationCurve speedByDistance)
    {
        return TrySpawn(
            legacyPrefab,
            position,
            direction,
            damageMultiplier,
            initialSpeed,
            speedBehavior,
            speedChangeY,
            speedByDistance,
            EnemyProjectileSizeBehavior.Default,
            0f,
            1f,
            null,
            1f);
    }

    public bool TrySpawn(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier,
        float initialSpeed,
        EnemyProjectileSpeedBehavior speedBehavior,
        float speedChangeY,
        AnimationCurve speedByDistance,
        EnemyProjectileSizeBehavior sizeBehavior,
        float sizeChangeY,
        float sizeChangeSpeed,
        AnimationCurve sizeChangeCurve,
        float finalSize)
    {
        singleSpawnRequestBuffer.Clear();
        singleSpawnRequestBuffer.Add(new EnemyProjectileSpawnRequest(
            legacyPrefab,
            position,
            direction,
            damageMultiplier,
            initialSpeed,
            speedBehavior,
            speedChangeY,
            speedByDistance,
            sizeBehavior,
            sizeChangeY,
            sizeChangeSpeed,
            sizeChangeCurve,
            finalSize));
        return TrySpawnBatch(singleSpawnRequestBuffer);
    }

    /// <summary>
    /// Instantiates a same-frame volley from prebuilt ECS prefab entities.
    /// The requests preserve all authoring and per-shot overrides while avoiding
    /// a structural component addition for every projectile.
    /// </summary>
    public bool TrySpawnBatch(IReadOnlyList<EnemyProjectileSpawnRequest> requests)
    {
        if (requests == null || requests.Count == 0 || !EnsureEntityManager())
            return false;

        preparedSpawns.Clear();
        for (int index = 0; index < requests.Count; index++)
        {
            if (!TryPrepareSpawn(requests[index], out PreparedProjectileSpawn prepared))
            {
                preparedSpawns.Clear();
                return false;
            }

            preparedSpawns.Add(prepared);
        }

        int rangeStart = 0;
        while (rangeStart < preparedSpawns.Count)
        {
            Entity template = preparedSpawns[rangeStart].Template;
            int rangeEnd = rangeStart + 1;
            while (rangeEnd < preparedSpawns.Count
                && preparedSpawns[rangeEnd].Template == template)
            {
                rangeEnd++;
            }

            SpawnPreparedRange(template, rangeStart, rangeEnd);
            rangeStart = rangeEnd;
        }

        preparedSpawns.Clear();
        return true;
    }

    private bool TryPrepareSpawn(
        EnemyProjectileSpawnRequest request,
        out PreparedProjectileSpawn prepared)
    {
        prepared = default;
        if (!TryGetConfiguration(request.LegacyPrefab, out EnemyProjectileAuthoring configuration)
            || !TryGetVisual(configuration, out RuntimeVisual visual))
        {
            return false;
        }

        EnemyProjectileSpawnSettings spawnSettings = configuration.CreateSpawnSettings();
        if (!TryGetBurstProjectileConfigId(
                configuration,
                request.LegacyPrefab,
                spawnSettings,
                out int burstProjectileConfigId))
        {
            return false;
        }

        bool usesCustomSpeed = request.SpeedBehavior
            == EnemyProjectileSpeedBehavior.Customized;
        ProjectileScaleSource scaleSource = GetScaleSource(request, configuration);
        if (!TryGetRuntimeTemplate(
                configuration,
                visual,
                spawnSettings,
                usesCustomSpeed,
                scaleSource,
                out Entity template))
        {
            return false;
        }

        float2 direction = new(request.Direction.x, request.Direction.y);
        if (math.lengthsq(direction) < 0.0001f)
            direction = new float2(0f, -1f);
        else
            direction = math.normalize(direction);

        prepared = new PreparedProjectileSpawn
        {
            Template = template,
            Request = request,
            Configuration = configuration,
            SpawnSettings = spawnSettings,
            Direction = direction,
            EffectiveDamage = configuration.BaseDamage
                * Mathf.Max(0.01f, request.DamageMultiplier),
            EffectiveLifetime = request.OverridesLifetime
                ? Mathf.Max(0.01f, request.Lifetime)
                : Mathf.Max(0.01f, configuration.BaseLifetime),
            EffectiveSpeed = request.InitialSpeed > 0f
                ? request.InitialSpeed
                : configuration.BaseSpeed,
            BurstProjectileConfigId = burstProjectileConfigId,
            ScaleSource = scaleSource
        };
        return true;
    }

    private bool TryGetBurstProjectileConfigId(
        EnemyProjectileAuthoring configuration,
        EnemyBullet legacyPrefab,
        EnemyProjectileSpawnSettings spawnSettings,
        out int burstProjectileConfigId)
    {
        burstProjectileConfigId = 0;
        if (spawnSettings.FlightMode != EnemyProjectileFlightMode.BurstAtPoint)
            return true;

        EnemyBullet projectileToBurst = configuration.GetBurstProjectilePrefab(legacyPrefab);
        if (collisionRegistry == null || projectileToBurst == null)
        {
            Debug.LogError(
                $"{nameof(EnemyProjectileEcsSpawner)} cannot create a burst-at-point projectile "
                + "without an enemy projectile registry and a burst projectile.");
            return false;
        }

        if (!TryGetConfiguration(projectileToBurst, out EnemyProjectileAuthoring burstConfiguration)
            || !TryGetVisual(burstConfiguration, out _))
        {
            return false;
        }

        collisionRegistry.RegisterBurstProjectileSpawner(this);
        burstProjectileConfigId = collisionRegistry
            .GetOrRegisterBurstProjectileConfigId(projectileToBurst);
        return burstProjectileConfigId != 0;
    }

    private bool TryGetRuntimeTemplate(
        EnemyProjectileAuthoring configuration,
        RuntimeVisual visual,
        EnemyProjectileSpawnSettings spawnSettings,
        bool usesCustomSpeed,
        ProjectileScaleSource scaleSource,
        out Entity template)
    {
        var key = new RuntimeProjectileTemplateKey(
            configuration,
            usesCustomSpeed,
            scaleSource,
            spawnSettings.FlightMode);
        if (templatesByKey.TryGetValue(key, out template)
            && entityManager.Exists(template))
        {
            return true;
        }

        template = CreateRuntimeTemplate(
            configuration,
            visual,
            spawnSettings,
            usesCustomSpeed,
            scaleSource);
        templatesByKey[key] = template;
        return true;
    }

    private Entity CreateRuntimeTemplate(
        EnemyProjectileAuthoring configuration,
        RuntimeVisual visual,
        EnemyProjectileSpawnSettings spawnSettings,
        bool usesCustomSpeed,
        ProjectileScaleSource scaleSource)
    {
        Entity template = entityManager.CreateEntity();
        entityManager.AddComponentData(template, new EnemyProjectileStaticData
        {
            BaseDamage = configuration.BaseDamage,
            Radius = configuration.EffectiveRadius,
            BaseLifetime = configuration.BaseLifetime,
            BaseSpeed = configuration.BaseSpeed
        });
        entityManager.AddComponentData(template, new EnemyProjectileVisualStaticData
        {
            Size = visual.Size
        });
        entityManager.AddComponentData(template, new EnemyProjectileVelocity());
        entityManager.AddComponentData(template, new EnemyProjectileMovement());
        entityManager.AddComponentData(template, new EnemyProjectileRemainingLifetime());
        entityManager.AddComponentData(template, new EnemyProjectileDamage());
        entityManager.AddComponentData(template, new EnemyProjectilePreviousPosition());
        entityManager.AddComponentData(template, new EnemyProjectileResolution());

        if (usesCustomSpeed)
        {
            entityManager.AddComponentData(template, new EnemyProjectileCustomSpeed());
            entityManager.AddBuffer<EnemyProjectileSpeedCurveSample>(template);
        }

        if (scaleSource != ProjectileScaleSource.None)
        {
            entityManager.AddComponentData(template, new EnemyProjectileCustomScale());
            DynamicBuffer<EnemyProjectileScaleCurveSample> scaleSamples =
                entityManager.AddBuffer<EnemyProjectileScaleCurveSample>(template);
            if (scaleSource == ProjectileScaleSource.Configuration)
                AddScaleCurveSamples(scaleSamples, configuration.ScaleMultiplierByDistance);
        }

        switch (spawnSettings.FlightMode)
        {
            case EnemyProjectileFlightMode.Homing:
                entityManager.AddComponentData(template, new EnemyProjectileHoming());
                break;
            case EnemyProjectileFlightMode.BurstAtPoint:
                entityManager.AddComponentData(template, new EnemyProjectileBurstAtPoint());
                break;
        }

        entityManager.AddComponentData(
            template,
            LocalTransform.FromPositionRotationScale(Vector3.zero, quaternion.identity, 1f));
        entityManager.AddComponentData(template, new PostTransformMatrix
        {
            Value = float4x4.Scale(new float3(
                configuration.VisualScale.x,
                configuration.VisualScale.y,
                1f))
        });
        RenderMeshUtility.AddComponents(
            template,
            entityManager,
            visual.Description,
            visual.RenderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        entityManager.AddComponent<Prefab>(template);
        return template;
    }

    private void SpawnPreparedRange(Entity template, int rangeStart, int rangeEnd)
    {
        int count = rangeEnd - rangeStart;
        var entities = new NativeArray<Entity>(count, Allocator.Temp);
        try
        {
            entityManager.Instantiate(template, entities);
            for (int index = 0; index < count; index++)
            {
                ConfigureSpawnedEntity(
                    entities[index],
                    preparedSpawns[rangeStart + index]);
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    private void ConfigureSpawnedEntity(Entity entity, PreparedProjectileSpawn prepared)
    {
        EnemyProjectileMovement movement = CreateMovement(
            prepared,
            out Vector3 spawnPosition);
        entityManager.SetComponentData(entity, new EnemyProjectileVelocity
        {
            Value = prepared.Direction * prepared.EffectiveSpeed
        });
        entityManager.SetComponentData(entity, movement);
        entityManager.SetComponentData(entity, new EnemyProjectileRemainingLifetime
        {
            Value = prepared.EffectiveLifetime
        });
        entityManager.SetComponentData(entity, new EnemyProjectileDamage
        {
            Value = prepared.EffectiveDamage
        });
        entityManager.SetComponentData(entity, new EnemyProjectilePreviousPosition
        {
            Value = new float2(spawnPosition.x, spawnPosition.y)
        });
        entityManager.SetComponentData(entity, new EnemyProjectileResolution());

        if (prepared.Request.SpeedBehavior == EnemyProjectileSpeedBehavior.Customized)
        {
            entityManager.SetComponentData(entity, new EnemyProjectileCustomSpeed
            {
                InitialSpeed = prepared.EffectiveSpeed,
                SpeedChangeY = prepared.Request.SpeedChangeY,
                DistanceSinceSpeedChange = 0f,
                Direction = prepared.Direction,
                HasReachedSpeedChangeY = 0
            });
            DynamicBuffer<EnemyProjectileSpeedCurveSample> speedSamples =
                entityManager.GetBuffer<EnemyProjectileSpeedCurveSample>(entity);
            speedSamples.Clear();
            AddSpeedCurveSamples(speedSamples, prepared.Request.SpeedByDistance);
        }

        if (prepared.ScaleSource == ProjectileScaleSource.Attack)
        {
            entityManager.SetComponentData(entity, new EnemyProjectileCustomScale
            {
                ScaleChangeY = prepared.Request.SizeChangeY,
                ProgressSinceScaleChange = 0f,
                ProgressSpeed = Mathf.Max(0.01f, prepared.Request.SizeChangeSpeed),
                ProgressMode = EnemyProjectileScaleProgressMode.Time,
                HasReachedScaleChangeY = 0
            });
            DynamicBuffer<EnemyProjectileScaleCurveSample> scaleSamples =
                entityManager.GetBuffer<EnemyProjectileScaleCurveSample>(entity);
            scaleSamples.Clear();
            AddAttackScaleCurveSamples(
                scaleSamples,
                prepared.Request.SizeChangeCurve,
                prepared.Request.FinalSize);
        }

        switch (prepared.SpawnSettings.FlightMode)
        {
            case EnemyProjectileFlightMode.Homing:
                entityManager.SetComponentData(entity, new EnemyProjectileHoming
                {
                    TurnSpeedDegreesPerSecond = Mathf.Max(
                        0f,
                        prepared.SpawnSettings.HomingTurnSpeedDegreesPerSecond),
                    RemainingDuration = Mathf.Max(
                        0f,
                        prepared.SpawnSettings.HomingDuration)
                });
                break;
            case EnemyProjectileFlightMode.BurstAtPoint:
                entityManager.SetComponentData(entity, new EnemyProjectileBurstAtPoint
                {
                    TargetPosition = prepared.SpawnSettings.BurstTargetPoint,
                    DetonationRadius = Mathf.Max(
                        0.01f,
                        prepared.SpawnSettings.BurstDetonationRadius),
                    BurstProjectileConfigId = prepared.BurstProjectileConfigId,
                    WaveCount = Mathf.Max(1, prepared.SpawnSettings.BurstWaveCount),
                    SpawnedWaveCount = 0,
                    ProjectilesPerWave = Mathf.Max(
                        1,
                        prepared.SpawnSettings.BurstProjectilesPerWave),
                    AngleStepDegrees = prepared.SpawnSettings.BurstAngleStepDegrees,
                    StartAngleDegrees = prepared.SpawnSettings.BurstStartAngleDegrees,
                    WaveInterval = Mathf.Max(0f, prepared.SpawnSettings.BurstWaveInterval),
                    NextWaveTime = 0d,
                    HasDetonated = 0
                });
                break;
        }

        entityManager.SetComponentData(
            entity,
            LocalTransform.FromPositionRotationScale(
                spawnPosition,
                GetRotation(prepared.Direction),
                1f));
    }

    private static EnemyProjectileMovement CreateMovement(
        PreparedProjectileSpawn prepared,
        out Vector3 spawnPosition)
    {
        EnemyProjectileSpawnRequest request = prepared.Request;
        float2 requestedPosition = new(request.Position.x, request.Position.y);
        float2 orbitCenter = request.OrbitCenterMode
            == EnemyProjectileOrbitCenterMode.FixedWorldPoint
                ? new float2(
                    request.OrbitFixedWorldCenter.x,
                    request.OrbitFixedWorldCenter.y)
                : new float2(request.SourcePosition.x, request.SourcePosition.y);
        float2 orbitOffset = requestedPosition - orbitCenter;
        float currentRadius = math.length(orbitOffset);
        float orbitRadius = request.OrbitRadius > 0f
            ? request.OrbitRadius
            : currentRadius;
        float2 radialDirection = math.normalizesafe(
            orbitOffset,
            prepared.Direction);
        float2 resolvedPosition = requestedPosition;
        if (request.MovementPattern == EnemyProjectileMovementPattern.Orbit
            && request.OrbitRadius > 0f)
        {
            resolvedPosition = orbitCenter + radialDirection * orbitRadius;
        }

        spawnPosition = new Vector3(
            resolvedPosition.x,
            resolvedPosition.y,
            request.Position.z);
        float referenceSpeed = prepared.Configuration.BaseSpeed > 0.0001f
            ? prepared.Configuration.BaseSpeed
            : prepared.EffectiveSpeed;
        return new EnemyProjectileMovement
        {
            Pattern = request.MovementPattern,
            ForwardDirection = prepared.Direction,
            ReferenceSpeed = Mathf.Max(0.0001f, referenceSpeed),
            AngularSpeedDegreesPerSecond = request.AngularSpeed,
            LateralSpeed = request.LateralSpeed,
            OrbitCenter = orbitCenter,
            OrbitRadius = Mathf.Max(0f, orbitRadius),
            OrbitAngleRadians = math.atan2(radialDirection.y, radialDirection.x),
            OrbitRadialSpeed = request.OrbitRadialSpeed
        };
    }

    private static ProjectileScaleSource GetScaleSource(
        EnemyProjectileSpawnRequest request,
        EnemyProjectileAuthoring configuration)
    {
        if (request.SizeBehavior == EnemyProjectileSizeBehavior.Customized)
            return ProjectileScaleSource.Attack;

        return configuration.ScaleBehavior == EnemyProjectileScaleBehavior.Customized
            ? ProjectileScaleSource.Configuration
            : ProjectileScaleSource.None;
    }

    private enum ProjectileScaleSource : byte
    {
        None,
        Attack,
        Configuration
    }

    private readonly struct RuntimeProjectileTemplateKey :
        IEquatable<RuntimeProjectileTemplateKey>
    {
        private readonly EnemyProjectileAuthoring configuration;
        private readonly bool usesCustomSpeed;
        private readonly ProjectileScaleSource scaleSource;
        private readonly EnemyProjectileFlightMode flightMode;

        public RuntimeProjectileTemplateKey(
            EnemyProjectileAuthoring configuration,
            bool usesCustomSpeed,
            ProjectileScaleSource scaleSource,
            EnemyProjectileFlightMode flightMode)
        {
            this.configuration = configuration;
            this.usesCustomSpeed = usesCustomSpeed;
            this.scaleSource = scaleSource;
            this.flightMode = flightMode;
        }

        public bool Equals(RuntimeProjectileTemplateKey other)
        {
            return ReferenceEquals(configuration, other.configuration)
                && usesCustomSpeed == other.usesCustomSpeed
                && scaleSource == other.scaleSource
                && flightMode == other.flightMode;
        }

        public override bool Equals(object obj) =>
            obj is RuntimeProjectileTemplateKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = configuration != null ? configuration.GetInstanceID() : 0;
                hash = hash * 397 ^ (usesCustomSpeed ? 1 : 0);
                hash = hash * 397 ^ (int)scaleSource;
                return hash * 397 ^ (int)flightMode;
            }
        }
    }

    private struct PreparedProjectileSpawn
    {
        public Entity Template;
        public EnemyProjectileSpawnRequest Request;
        public EnemyProjectileAuthoring Configuration;
        public EnemyProjectileSpawnSettings SpawnSettings;
        public float2 Direction;
        public float EffectiveDamage;
        public float EffectiveLifetime;
        public float EffectiveSpeed;
        public int BurstProjectileConfigId;
        public ProjectileScaleSource ScaleSource;
    }

    private static void AddSpeedCurveSamples(
        DynamicBuffer<EnemyProjectileSpeedCurveSample> samples,
        AnimationCurve speedByDistance)
    {
        if (speedByDistance == null || speedByDistance.length == 0)
        {
            samples.Add(new EnemyProjectileSpeedCurveSample
            {
                Distance = 0f,
                SpeedMultiplier = 1f
            });
            return;
        }

        Keyframe[] keys = speedByDistance.keys;
        float maximumDistance = 0f;
        for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
            maximumDistance = Mathf.Max(maximumDistance, keys[keyIndex].time);

        maximumDistance = Mathf.Max(0.01f, maximumDistance);
        for (int sampleIndex = 0;
             sampleIndex < CustomSpeedCurveSampleCount;
             sampleIndex++)
        {
            float progress = sampleIndex
                / (float)(CustomSpeedCurveSampleCount - 1);
            float distance = maximumDistance * progress;
            samples.Add(new EnemyProjectileSpeedCurveSample
            {
                Distance = distance,
                SpeedMultiplier = speedByDistance.Evaluate(distance)
            });
        }
    }

    private static void AddScaleCurveSamples(
        DynamicBuffer<EnemyProjectileScaleCurveSample> samples,
        AnimationCurve scaleMultiplierByDistance)
    {
        if (scaleMultiplierByDistance == null
            || scaleMultiplierByDistance.length == 0)
        {
            samples.Add(new EnemyProjectileScaleCurveSample
            {
                Input = 0f,
                ScaleMultiplier = 1f
            });
            return;
        }

        Keyframe[] keys = scaleMultiplierByDistance.keys;
        float maximumDistance = 0f;
        for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
            maximumDistance = Mathf.Max(maximumDistance, keys[keyIndex].time);

        maximumDistance = Mathf.Max(0.01f, maximumDistance);
        for (int sampleIndex = 0;
             sampleIndex < CustomSpeedCurveSampleCount;
             sampleIndex++)
        {
            float progress = sampleIndex
                / (float)(CustomSpeedCurveSampleCount - 1);
            float distance = maximumDistance * progress;
            samples.Add(new EnemyProjectileScaleCurveSample
            {
                Input = distance,
                ScaleMultiplier = scaleMultiplierByDistance.Evaluate(distance)
            });
        }
    }

    private static void AddAttackScaleCurveSamples(
        DynamicBuffer<EnemyProjectileScaleCurveSample> samples,
        AnimationCurve sizeChangeCurve,
        float finalSize)
    {
        float clampedFinalSize = Mathf.Max(0.01f, finalSize);
        for (int sampleIndex = 0;
             sampleIndex < CustomSpeedCurveSampleCount;
             sampleIndex++)
        {
            float progress = sampleIndex
                / (float)(CustomSpeedCurveSampleCount - 1);
            float curveValue = sizeChangeCurve != null && sizeChangeCurve.length > 0
                ? sizeChangeCurve.Evaluate(progress)
                : progress;
            float scaleMultiplier = sampleIndex == CustomSpeedCurveSampleCount - 1
                ? clampedFinalSize
                : Mathf.Lerp(1f, clampedFinalSize, curveValue);
            samples.Add(new EnemyProjectileScaleCurveSample
            {
                Input = progress,
                ScaleMultiplier = scaleMultiplier
            });
        }
    }

    public void Dispose()
    {
        DestroySpawnedEntities();

        foreach (RuntimeVisual visual in visualsByConfiguration.Values)
        {
            if (visual.Material != null)
                UnityEngine.Object.Destroy(visual.Material);

            if (visual.Mesh != null)
                UnityEngine.Object.Destroy(visual.Mesh);
        }

        visualsByConfiguration.Clear();
        configurationsByPrefab.Clear();
        templatesByKey.Clear();
        invalidConfigurationsLogged.Clear();
        entityWorld = null;
    }

    private void DestroySpawnedEntities()
    {
        if (entityWorld == null || !entityWorld.IsCreated)
            return;

        EntityQuery projectiles = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EnemyProjectileStaticData>());
        entityManager.DestroyEntity(projectiles);

        foreach (Entity template in templatesByKey.Values)
        {
            if (entityManager.Exists(template))
                entityManager.DestroyEntity(template);
        }
    }

    private bool TryGetConfiguration(
        EnemyBullet legacyPrefab,
        out EnemyProjectileAuthoring configuration)
    {
        if (legacyPrefab != null
            && configurationsByPrefab.TryGetValue(legacyPrefab, out configuration)
            && configuration != null
            && configuration.IsConfigured)
        {
            return true;
        }

        configuration = legacyPrefab != null
            ? legacyPrefab.GetComponent<EnemyProjectileAuthoring>()
            : null;
        if (configuration != null && configuration.IsConfigured)
        {
            configurationsByPrefab[legacyPrefab] = configuration;
            return true;
        }

        if (configuration != null && invalidConfigurationsLogged.Add(configuration))
        {
            Debug.LogError(
                $"{nameof(EnemyProjectileAuthoring)} on '{configuration.name}' is incomplete. "
                + "Enemy projectile was not spawned.",
                configuration);
        }
        else if (configuration == null)
        {
            Debug.LogError(
                $"Enemy projectile prefab '{legacyPrefab?.name ?? "missing"}' needs "
                + $"{nameof(EnemyProjectileAuthoring)}. Enemy projectile was not spawned.");
        }

        return false;
    }

    private bool EnsureEntityManager()
    {
        if (entityWorld != null && entityWorld.IsCreated)
            return true;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError(
                $"{nameof(EnemyProjectileEcsSpawner)} needs an active default ECS World.");
            return false;
        }

        entityWorld = world;
        entityManager = world.EntityManager;
        return true;
    }

    private bool TryGetVisual(
        EnemyProjectileAuthoring configuration,
        out RuntimeVisual visual)
    {
        if (visualsByConfiguration.TryGetValue(configuration, out visual))
            return true;

        SpriteRenderer renderer = configuration.VisualRenderer;
        Sprite sprite = renderer != null ? renderer.sprite : null;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (sprite == null || shader == null)
        {
            LogIncompleteVisual(configuration, sprite == null
                ? "SpriteRenderer does not have a Sprite."
                : "URP Unlit shader was not found.");
            return false;
        }

        Mesh mesh = CreateSpriteMesh(sprite, configuration.name);
        Material material = CreateSpriteMaterial(shader, sprite, renderer.color,
            configuration.name);
        RenderMeshArray renderMeshArray = new(
            new[] { material },
            new[] { mesh });
        visual = new RuntimeVisual(
            mesh,
            material,
            renderMeshArray,
            new RenderMeshDescription(ShadowCastingMode.Off, false),
            sprite.bounds.size);
        visualsByConfiguration.Add(configuration, visual);
        return true;
    }

    private void LogIncompleteVisual(
        EnemyProjectileAuthoring configuration,
        string reason)
    {
        if (!invalidConfigurationsLogged.Add(configuration))
            return;

        Debug.LogError(
            $"{nameof(EnemyProjectileAuthoring)} on '{configuration.name}' cannot "
            + $"prepare ECS rendering: {reason}",
            configuration);
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
            name = $"{configurationName} ECS Projectile Mesh"
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
            name = $"{configurationName} ECS Projectile Material",
            renderQueue = ProjectileRenderQueue
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

    private sealed class RuntimeVisual
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly RenderMeshArray RenderMeshArray;
        public readonly RenderMeshDescription Description;
        public readonly float2 Size;

        public RuntimeVisual(
            Mesh mesh,
            Material material,
            RenderMeshArray renderMeshArray,
            RenderMeshDescription description,
            Vector2 size)
        {
            Mesh = mesh;
            Material = material;
            RenderMeshArray = renderMeshArray;
            Description = description;
            Size = new float2(size.x, size.y);
        }
    }
}

/// <summary>
/// Runtime-only flight settings copied from <see cref="EnemyProjectileAuthoring"/>.
/// Managed projectile prefab references remain in the collision registry.
/// </summary>
public struct EnemyProjectileSpawnSettings
{
    public EnemyProjectileFlightMode FlightMode;
    public float HomingTurnSpeedDegreesPerSecond;
    public float HomingDuration;
    public float2 BurstTargetPoint;
    public float BurstDetonationRadius;
    public int BurstWaveCount;
    public float BurstWaveInterval;
    public int BurstProjectilesPerWave;
    public float BurstAngleStepDegrees;
    public float BurstStartAngleDegrees;
}

/// <summary>
/// A single enemy projectile request. The batch API consumes all requests in
/// one volley while retaining per-projectile direction, speed and size data.
/// </summary>
public readonly struct EnemyProjectileSpawnRequest
{
    public readonly EnemyBullet LegacyPrefab;
    public readonly Vector3 Position;
    public readonly Vector3 Direction;
    public readonly float DamageMultiplier;
    public readonly float InitialSpeed;
    public readonly bool OverridesLifetime;
    public readonly float Lifetime;
    public readonly EnemyProjectileSpeedBehavior SpeedBehavior;
    public readonly float SpeedChangeY;
    public readonly AnimationCurve SpeedByDistance;
    public readonly EnemyProjectileSizeBehavior SizeBehavior;
    public readonly float SizeChangeY;
    public readonly float SizeChangeSpeed;
    public readonly AnimationCurve SizeChangeCurve;
    public readonly float FinalSize;
    public readonly Vector3 SourcePosition;
    public readonly EnemyProjectileMovementPattern MovementPattern;
    public readonly float AngularSpeed;
    public readonly float LateralSpeed;
    public readonly EnemyProjectileOrbitCenterMode OrbitCenterMode;
    public readonly Vector2 OrbitFixedWorldCenter;
    public readonly float OrbitRadius;
    public readonly float OrbitRadialSpeed;

    public EnemyProjectileSpawnRequest(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier,
        float initialSpeed,
        EnemyProjectileSpeedBehavior speedBehavior,
        float speedChangeY,
        AnimationCurve speedByDistance,
        EnemyProjectileSizeBehavior sizeBehavior,
        float sizeChangeY,
        float sizeChangeSpeed,
        AnimationCurve sizeChangeCurve,
        float finalSize)
        : this(
            legacyPrefab,
            position,
            direction,
            damageMultiplier,
            initialSpeed,
            speedBehavior,
            speedChangeY,
            speedByDistance,
            sizeBehavior,
            sizeChangeY,
            sizeChangeSpeed,
            sizeChangeCurve,
            finalSize,
            position,
            EnemyProjectileMovementPattern.Default,
            0f,
            0f,
            EnemyProjectileOrbitCenterMode.SourcePosition,
            Vector2.zero,
            0f,
            0f,
            false,
            0f)
    {
    }

    public EnemyProjectileSpawnRequest(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier,
        float initialSpeed,
        EnemyProjectileSpeedBehavior speedBehavior,
        float speedChangeY,
        AnimationCurve speedByDistance,
        EnemyProjectileSizeBehavior sizeBehavior,
        float sizeChangeY,
        float sizeChangeSpeed,
        AnimationCurve sizeChangeCurve,
        float finalSize,
        Vector3 sourcePosition,
        EnemyProjectileMovementPattern movementPattern,
        float angularSpeed,
        float lateralSpeed,
        EnemyProjectileOrbitCenterMode orbitCenterMode,
        Vector2 orbitFixedWorldCenter,
        float orbitRadius,
        float orbitRadialSpeed,
        bool overridesLifetime,
        float lifetime)
    {
        LegacyPrefab = legacyPrefab;
        Position = position;
        Direction = direction;
        DamageMultiplier = damageMultiplier;
        InitialSpeed = initialSpeed;
        OverridesLifetime = overridesLifetime;
        Lifetime = Mathf.Max(0.01f, lifetime);
        SpeedBehavior = speedBehavior;
        SpeedChangeY = speedChangeY;
        SpeedByDistance = speedByDistance;
        SizeBehavior = sizeBehavior;
        SizeChangeY = sizeChangeY;
        SizeChangeSpeed = sizeChangeSpeed;
        SizeChangeCurve = sizeChangeCurve;
        FinalSize = finalSize;
        SourcePosition = sourcePosition;
        MovementPattern = movementPattern;
        AngularSpeed = angularSpeed;
        LateralSpeed = lateralSpeed;
        OrbitCenterMode = orbitCenterMode;
        OrbitFixedWorldCenter = orbitFixedWorldCenter;
        OrbitRadius = Mathf.Max(0f, orbitRadius);
        OrbitRadialSpeed = orbitRadialSpeed;
    }
}
