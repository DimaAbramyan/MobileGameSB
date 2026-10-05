using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerProjectileEnemyCollisionSystem))]
public partial struct PlayerProjectileEntityDamageReceiverSnapshotSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileEntityDamageReceiverTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileEntityDamageReceiverTarget>();
        DynamicBuffer<PlayerProjectileEntityDamageReceiverGridEntry> grid = SystemAPI
            .GetSingletonBuffer<PlayerProjectileEntityDamageReceiverGridEntry>();
        float cellSize = SystemAPI
            .GetSingleton<PlayerProjectileCollisionGridSettings>()
            .CellSize;
        targets.Clear();
        grid.Clear();
        if (cellSize <= 0f)
            return;

        foreach ((RefRO<PlayerProjectileStaticData> projectileData,
                  RefRO<PlayerProjectileResonanceSphereState> sphereState,
                  RefRO<PlayerProjectileScaleState> scaleState,
                  RefRO<LocalTransform> transform,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerProjectileStaticData>,
                     RefRO<PlayerProjectileResonanceSphereState>,
                     RefRO<PlayerProjectileScaleState>,
                     RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (sphereState.ValueRO.IsDetonating != 0)
                continue;

            float2 scaleRatio = scaleState.ValueRO.RootScale / math.max(
                new float2(0.001f),
                scaleState.ValueRO.InitialRootScale);
            float collisionRadius = projectileData.ValueRO.Radius * math.max(
                0.001f,
                math.max(scaleRatio.x, scaleRatio.y));
            float2 center = transform.ValueRO.Position.xy;
            float2 minimum = center - collisionRadius;
            float2 maximum = center + collisionRadius;
            targets.Add(new PlayerProjectileEntityDamageReceiverTarget
            {
                Entity = entity,
                Min = minimum,
                Max = maximum
            });

            int minimumX = (int)math.floor(minimum.x / cellSize);
            int maximumX = (int)math.floor(maximum.x / cellSize);
            int minimumY = (int)math.floor(minimum.y / cellSize);
            int maximumY = (int)math.floor(maximum.y / cellSize);
            for (int cellX = minimumX; cellX <= maximumX; cellX++)
            {
                for (int cellY = minimumY; cellY <= maximumY; cellY++)
                {
                    grid.Add(new PlayerProjectileEntityDamageReceiverGridEntry
                    {
                        CellX = cellX,
                        CellY = cellY,
                        Entity = entity
                    });
                }
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
[UpdateBefore(typeof(PlayerProjectileRangeSystem))]
public partial struct PlayerProjectileBallLightningSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileHomingTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileHomingTarget>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        ComponentLookup<PlayerProjectileStaticData> projectileDataLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileStaticData>(true);
        ComponentLookup<PlayerProjectileOwner> ownerLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileOwner>(true);
        double currentTime = SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<PlayerProjectileBallLightningStaticData> ballData,
                  RefRW<PlayerProjectileBallLightningState> ballState,
                  RefRO<PlayerProjectileResolution> resolution,
                  RefRO<LocalTransform> transform,
                  DynamicBuffer<PlayerProjectileBallLightningDirectContact> contacts,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerProjectileBallLightningStaticData>,
                     RefRW<PlayerProjectileBallLightningState>,
                     RefRO<PlayerProjectileResolution>,
                     RefRO<LocalTransform>,
                     DynamicBuffer<PlayerProjectileBallLightningDirectContact>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            if (ballState.ValueRO.NextPulseTime < 0d)
            {
                ballState.ValueRW.NextPulseTime = currentTime
                    + ballData.ValueRO.AreaTickInterval;
                continue;
            }

            if (currentTime < ballState.ValueRO.NextPulseTime)
                continue;

            ballState.ValueRW.NextPulseTime = currentTime
                + ballData.ValueRO.AreaTickInterval;
            if (ballData.ValueRO.AreaDamage <= 0f
                || ballData.ValueRO.AreaRadius <= 0f
                || ballData.ValueRO.DamageLayers == 0)
            {
                continue;
            }

            PlayerProjectileStaticData projectileData = projectileDataLookup[entity];
            float2 position = transform.ValueRO.Position.xy;
            float radiusSquared = ballData.ValueRO.AreaRadius
                * ballData.ValueRO.AreaRadius;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                PlayerProjectileHomingTarget target = targets[targetIndex];
                if (!IsInLayerMask(target.Layer, ballData.ValueRO.DamageLayers)
                    || math.lengthsq(target.Position - position) > radiusSquared
                    || IsReceivingDirectDamage(contacts, target.TargetId, currentTime))
                {
                    continue;
                }

                events.Add(new PlayerProjectileResolutionEvent
                {
                    TargetId = target.TargetId,
                    TargetKind = PlayerProjectileCollisionTargetKind.Enemy,
                    OwnerId = ownerLookup[entity].Id,
                    DebuffSetId = projectileData.DebuffSetId,
                    Damage = ballData.ValueRO.AreaDamage,
                    DamageType = projectileData.DamageType,
                    BypassesEnemyShield = projectileData.BypassesEnemyShield,
                    ImpactPosition = target.Position
                });
            }
        }
    }

    private static bool IsInLayerMask(int layer, int layerMask)
    {
        return layer >= 0 && layer < 32 && (layerMask & (1 << layer)) != 0;
    }

    private static bool IsReceivingDirectDamage(
        DynamicBuffer<PlayerProjectileBallLightningDirectContact> contacts,
        int targetId,
        double currentTime)
    {
        for (int index = 0; index < contacts.Length; index++)
        {
            PlayerProjectileBallLightningDirectContact contact = contacts[index];
            if (contact.TargetId == targetId
                && contact.LastContactTime == currentTime)
            {
                return true;
            }
        }

        return false;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerProjectileMovementSystem))]
public partial struct PlayerProjectileResonanceSphereApproachSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<LocalTransform> transform,
                  RefRW<PlayerProjectilePreviousPosition> previousPosition,
                  RefRW<PlayerProjectileVelocity> velocity,
                  RefRW<PlayerProjectileResolution> resolution,
                  RefRW<PlayerProjectileResonanceSphereState> sphereState,
                  RefRO<PlayerProjectileResonanceSphereStaticData> sphereData)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRW<PlayerProjectilePreviousPosition>,
                     RefRW<PlayerProjectileVelocity>,
                     RefRW<PlayerProjectileResolution>,
                     RefRW<PlayerProjectileResonanceSphereState>,
                     RefRO<PlayerProjectileResonanceSphereStaticData>>())
        {
            if (sphereState.ValueRO.IsDetonating != 0
                || resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
            {
                continue;
            }

            PlayerProjectileResonanceSphereStaticData data = sphereData.ValueRO;
            PlayerProjectileResonanceSphereState currentState = sphereState.ValueRO;
            bool fullChargeDetonationPending = currentState
                .IsFullChargeDetonationPending != 0;
            if (fullChargeDetonationPending)
            {
                currentState.FullChargeDetonationElapsed += deltaTime;
                if (currentState.FullChargeDetonationElapsed
                    >= data.FullChargeDetonationDelay)
                {
                    resolution.ValueRW = new PlayerProjectileResolution
                    {
                        Kind = PlayerProjectileResolutionKind.Expired,
                        TargetId = 0,
                        TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                    };
                    sphereState.ValueRW = currentState;
                    continue;
                }
            }

            float2 currentPosition = transform.ValueRO.Position.xy;
            if (currentState.IsSlowingDown == 0)
            {
                if (currentPosition.y < data.SlowdownStartY)
                    continue;

                currentState.IsSlowingDown = 1;
                currentState.SlowdownElapsed = 0f;
                currentState.SlowdownStartX = currentPosition.x;
                currentPosition = new float2(
                    currentState.SlowdownStartX,
                    data.SlowdownStartY);
                transform.ValueRW.Position = new float3(
                    currentPosition.x,
                    currentPosition.y,
                    transform.ValueRO.Position.z);
            }

            float duration = math.max(0.02f, data.SlowdownDuration);
            currentState.SlowdownElapsed = math.min(
                duration,
                currentState.SlowdownElapsed + deltaTime);
            float progress = math.saturate(currentState.SlowdownElapsed / duration);
            float remainingProgress = 1f - progress;
            float easedProgress = 1f - remainingProgress * remainingProgress;
            float2 targetPosition = new(
                currentState.SlowdownStartX,
                math.lerp(data.SlowdownStartY, data.DetonationY, easedProgress));

            if (progress >= 1f)
            {
                transform.ValueRW.Position = new float3(
                    targetPosition.x,
                    targetPosition.y,
                    transform.ValueRO.Position.z);
                previousPosition.ValueRW.Value = targetPosition;
                velocity.ValueRW.Value = float2.zero;
                if (!fullChargeDetonationPending)
                {
                    resolution.ValueRW = new PlayerProjectileResolution
                    {
                        Kind = PlayerProjectileResolutionKind.Expired,
                        TargetId = 0,
                        TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                    };
                }
            }
            else
            {
                velocity.ValueRW.Value = (targetPosition - currentPosition)
                    / math.max(0.0001f, deltaTime);
            }

            sphereState.ValueRW = currentState;
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileLifetimeSystem))]
[UpdateAfter(typeof(PlayerProjectileResonanceSphereApproachSystem))]
[UpdateBefore(typeof(PlayerProjectileCleanupSystem))]
public partial struct PlayerProjectileResonanceSphereSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileHomingTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileHomingTarget>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        ComponentLookup<PlayerProjectileStaticData> projectileDataLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileStaticData>(true);
        ComponentLookup<PlayerProjectileResonanceSphereStaticData> sphereDataLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileResonanceSphereStaticData>(true);
        ComponentLookup<URPMaterialPropertyBaseColor> baseColorLookup = SystemAPI
            .GetComponentLookup<URPMaterialPropertyBaseColor>();
        ComponentLookup<PlayerProjectileOwner> ownerLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileOwner>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PlayerProjectileResolution> resolution,
                  RefRW<PlayerProjectileResonanceSphereState> sphereState,
                  RefRW<PlayerProjectileVelocity> velocity,
                  RefRW<PlayerProjectileScaleState> scaleState,
                  RefRO<LocalTransform> transform,
                  RefRW<PostTransformMatrix> postTransformMatrix,
                  DynamicBuffer<PlayerProjectileResonanceSphereDamagedTarget>
                      damagedTargets,
                 Entity entity)
                 in SystemAPI.Query<RefRW<PlayerProjectileResolution>,
                     RefRW<PlayerProjectileResonanceSphereState>,
                     RefRW<PlayerProjectileVelocity>,
                     RefRW<PlayerProjectileScaleState>,
                     RefRO<LocalTransform>,
                     RefRW<PostTransformMatrix>,
                     DynamicBuffer<PlayerProjectileResonanceSphereDamagedTarget>>()
                     .WithEntityAccess())
        {
            PlayerProjectileResonanceSphereStaticData sphereData = sphereDataLookup[entity];
            PlayerProjectileResonanceSphereState currentState = sphereState.ValueRO;
            if (currentState.IsDetonating == 0)
            {
                if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.Expired)
                    continue;

                currentState.IsDetonating = 1;
                currentState.CurrentWaveRadius = 0f;
                resolution.ValueRW = new PlayerProjectileResolution();
                velocity.ValueRW = new PlayerProjectileVelocity { Value = float2.zero };
                damagedTargets.Clear();
            }

            currentState.CurrentWaveRadius = math.min(
                sphereData.ExplosionRadius,
                currentState.CurrentWaveRadius + sphereData.WaveSpeed * deltaTime);
            ApplyWaveScale(
                sphereData,
                currentState.CurrentWaveRadius,
                ref scaleState.ValueRW,
                ref postTransformMatrix.ValueRW);
            ApplyWaveTransparency(
                entity,
                sphereData,
                currentState.CurrentWaveRadius,
                baseColorLookup);

            if (currentState.StoredDamage > 0f)
            {
                PlayerProjectileStaticData projectileData = projectileDataLookup[entity];
                float2 spherePosition = transform.ValueRO.Position.xy;
                float radiusSquared = currentState.CurrentWaveRadius
                    * currentState.CurrentWaveRadius;
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    PlayerProjectileHomingTarget target = targets[targetIndex];
                    if (math.lengthsq(target.Position - spherePosition) > radiusSquared
                        || HasDamagedTarget(damagedTargets, target.TargetId))
                    {
                        continue;
                    }

                    damagedTargets.Add(
                        new PlayerProjectileResonanceSphereDamagedTarget
                        {
                            TargetId = target.TargetId
                        });
                    events.Add(new PlayerProjectileResolutionEvent
                    {
                        TargetId = target.TargetId,
                        TargetKind = PlayerProjectileCollisionTargetKind.Enemy,
                        OwnerId = ownerLookup[entity].Id,
                        DebuffSetId = projectileData.DebuffSetId,
                        Damage = currentState.StoredDamage,
                        DamageType = EnemyDamageType.Resonance,
                        BypassesEnemyShield = 0,
                        ImpactPosition = target.Position
                    });
                }
            }

            if (currentState.CurrentWaveRadius >= sphereData.ExplosionRadius)
            {
                resolution.ValueRW = new PlayerProjectileResolution
                {
                    Kind = PlayerProjectileResolutionKind.Expired,
                    TargetId = 0,
                    TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                };
            }

            sphereState.ValueRW = currentState;
        }
    }

    private static void ApplyWaveScale(
        PlayerProjectileResonanceSphereStaticData sphereData,
        float currentWaveRadius,
        ref PlayerProjectileScaleState scaleState,
        ref PostTransformMatrix postTransformMatrix)
    {
        float diameter = math.max(0.02f, currentWaveRadius * 2f);
        float scaleMultiplier = diameter / math.max(0.01f, sphereData.SpriteDiameter);
        float2 rootScale = scaleState.InitialRootScale * scaleMultiplier;
        scaleState.RootScale = rootScale;
        float2 visualScale = scaleState.VisualScalePerRootUnit * rootScale;
        postTransformMatrix.Value = float4x4.Scale(new float3(
            visualScale.x,
            visualScale.y,
            1f));
    }

    private static void ApplyWaveTransparency(
        Entity entity,
        PlayerProjectileResonanceSphereStaticData sphereData,
        float currentWaveRadius,
        ComponentLookup<URPMaterialPropertyBaseColor> baseColorLookup)
    {
        if (!baseColorLookup.HasComponent(entity))
            return;

        float progress = sphereData.ExplosionRadius <= 0.0001f
            ? 1f
            : math.saturate(currentWaveRadius / sphereData.ExplosionRadius);
        URPMaterialPropertyBaseColor color = baseColorLookup[entity];
        color.Value.w = sphereData.InitialAlpha * (1f - progress);
        baseColorLookup[entity] = color;
    }

    private static bool HasDamagedTarget(
        DynamicBuffer<PlayerProjectileResonanceSphereDamagedTarget> targets,
        int targetId)
    {
        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index].TargetId == targetId)
                return true;
        }

        return false;
    }
}
