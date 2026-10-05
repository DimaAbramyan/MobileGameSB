using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProjectileMovementSystem))]
public partial struct ProjectileCustomSpeedSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCustomSpeed>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<EnemyProjectileBurstAtPoint> burstProjectiles =
            SystemAPI.GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        SystemAPI.TryGetSingletonBuffer<EnemyProjectileSlowField>(out var slowFields, true);
        foreach ((RefRO<LocalTransform> transform,
                  RefRW<EnemyProjectileVelocity> velocity,
                  RefRW<EnemyProjectileCustomSpeed> customSpeed,
                  RefRO<EnemyProjectileResolution> resolution,
                  DynamicBuffer<EnemyProjectileSpeedCurveSample> curveSamples,
                  Entity entity)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                     RefRW<EnemyProjectileVelocity>,
                     RefRW<EnemyProjectileCustomSpeed>,
                     RefRO<EnemyProjectileResolution>,
                     DynamicBuffer<EnemyProjectileSpeedCurveSample>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (burstProjectiles.HasComponent(entity)
                && burstProjectiles[entity].HasDetonated != 0)
            {
                continue;
            }

            if (customSpeed.ValueRO.HasReachedSpeedChangeY == 0)
            {
                if (!HasReachedSpeedChangeY(
                        transform.ValueRO.Position.y,
                        customSpeed.ValueRO.SpeedChangeY,
                        customSpeed.ValueRO.Direction.y))
                {
                    continue;
                }

                customSpeed.ValueRW.HasReachedSpeedChangeY = 1;
                customSpeed.ValueRW.DistanceSinceSpeedChange = 0f;
            }
            else
            {
                customSpeed.ValueRW.DistanceSinceSpeedChange += math.length(
                    velocity.ValueRO.Value) * deltaTime
                    * EnemyProjectileSlowField.EvaluateMultiplier(
                        transform.ValueRO.Position.xy, slowFields);
            }

            float speedMultiplier = EvaluateSpeedMultiplier(
                curveSamples,
                customSpeed.ValueRO.DistanceSinceSpeedChange);
            velocity.ValueRW.Value = customSpeed.ValueRO.Direction
                * (customSpeed.ValueRO.InitialSpeed
                    * math.max(0f, speedMultiplier));
        }
    }

    private static bool HasReachedSpeedChangeY(
        float currentY,
        float speedChangeY,
        float directionY)
    {
        if (directionY > 0.0001f)
            return currentY >= speedChangeY;

        if (directionY < -0.0001f)
            return currentY <= speedChangeY;

        return false;
    }

    private static float EvaluateSpeedMultiplier(
        DynamicBuffer<EnemyProjectileSpeedCurveSample> curveSamples,
        float distance)
    {
        if (curveSamples.Length == 0)
            return 1f;

        EnemyProjectileSpeedCurveSample firstSample = curveSamples[0];
        if (distance <= firstSample.Distance)
            return firstSample.SpeedMultiplier;

        for (int sampleIndex = 1;
             sampleIndex < curveSamples.Length;
             sampleIndex++)
        {
            EnemyProjectileSpeedCurveSample nextSample = curveSamples[sampleIndex];
            if (distance > nextSample.Distance)
                continue;

            EnemyProjectileSpeedCurveSample previousSample =
                curveSamples[sampleIndex - 1];
            float span = math.max(
                0.0001f,
                nextSample.Distance - previousSample.Distance);
            float progress = math.saturate(
                (distance - previousSample.Distance) / span);
            return math.lerp(
                previousSample.SpeedMultiplier,
                nextSample.SpeedMultiplier,
                progress);
        }

        return curveSamples[curveSamples.Length - 1].SpeedMultiplier;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileHomingSystem))]
[UpdateAfter(typeof(ProjectileBurstAtPointSystem))]
[UpdateAfter(typeof(ProjectileCustomSpeedSystem))]
[UpdateBefore(typeof(ProjectileMovementSystem))]
public partial struct ProjectileCustomScaleSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCustomScale>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<EnemyProjectileBurstAtPoint> burstProjectiles = SystemAPI
            .GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        SystemAPI.TryGetSingletonBuffer<EnemyProjectileSlowField>(out var slowFields, true);
        foreach ((RefRW<LocalTransform> transform,
                  RefRO<EnemyProjectileVelocity> velocity,
                  RefRW<EnemyProjectileCustomScale> customScale,
                  RefRO<EnemyProjectileResolution> resolution,
                  DynamicBuffer<EnemyProjectileScaleCurveSample> curveSamples,
                  Entity entity)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRO<EnemyProjectileVelocity>,
                     RefRW<EnemyProjectileCustomScale>,
                     RefRO<EnemyProjectileResolution>,
                     DynamicBuffer<EnemyProjectileScaleCurveSample>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (burstProjectiles.HasComponent(entity)
                && burstProjectiles[entity].HasDetonated != 0)
            {
                continue;
            }

            if (customScale.ValueRO.HasReachedScaleChangeY == 0)
            {
                if (!HasReachedScaleChangeY(
                        transform.ValueRO.Position.y,
                        customScale.ValueRO.ScaleChangeY,
                        velocity.ValueRO.Value.y))
                {
                    continue;
                }

                customScale.ValueRW.HasReachedScaleChangeY = 1;
                customScale.ValueRW.ProgressSinceScaleChange = 0f;
            }
            else
            {
                customScale.ValueRW.ProgressSinceScaleChange +=
                    customScale.ValueRO.ProgressMode == EnemyProjectileScaleProgressMode.Time
                        ? customScale.ValueRO.ProgressSpeed * deltaTime
                        : math.length(velocity.ValueRO.Value) * deltaTime
                          * EnemyProjectileSlowField.EvaluateMultiplier(
                              transform.ValueRO.Position.xy, slowFields);
            }

            transform.ValueRW.Scale = math.max(
                0.01f,
                EvaluateScaleMultiplier(
                    curveSamples,
                    customScale.ValueRO.ProgressSinceScaleChange));
        }
    }

    private static bool HasReachedScaleChangeY(
        float currentY,
        float scaleChangeY,
        float directionY)
    {
        if (directionY > 0.0001f)
            return currentY >= scaleChangeY;

        if (directionY < -0.0001f)
            return currentY <= scaleChangeY;

        return false;
    }

    private static float EvaluateScaleMultiplier(
        DynamicBuffer<EnemyProjectileScaleCurveSample> curveSamples,
        float input)
    {
        if (curveSamples.Length == 0)
            return 1f;

        EnemyProjectileScaleCurveSample firstSample = curveSamples[0];
        if (input <= firstSample.Input)
            return firstSample.ScaleMultiplier;

        for (int sampleIndex = 1;
             sampleIndex < curveSamples.Length;
             sampleIndex++)
        {
            EnemyProjectileScaleCurveSample nextSample = curveSamples[sampleIndex];
            if (input > nextSample.Input)
                continue;

            EnemyProjectileScaleCurveSample previousSample =
                curveSamples[sampleIndex - 1];
            float span = math.max(
                0.0001f,
                nextSample.Input - previousSample.Input);
            float progress = math.saturate(
                (input - previousSample.Input) / span);
            return math.lerp(
                previousSample.ScaleMultiplier,
                nextSample.ScaleMultiplier,
                progress);
        }

        return curveSamples[curveSamples.Length - 1].ScaleMultiplier;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProjectileHomingSystem))]
[UpdateBefore(typeof(ProjectileBurstAtPointSystem))]
public partial struct ProjectilePurgeFieldSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SystemAPI.TryGetSingletonBuffer<EnemyProjectilePurgeField>(
            out var purgeFields,
            true);
        if (!purgeFields.IsCreated || purgeFields.Length == 0)
            return;

        foreach ((RefRO<LocalTransform> transform,
                  RefRW<EnemyProjectileResolution> resolution)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                     RefRW<EnemyProjectileResolution>>())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            for (int fieldIndex = 0;
                 fieldIndex < purgeFields.Length;
                 fieldIndex++)
            {
                EnemyProjectilePurgeField field = purgeFields[fieldIndex];
                if (field.Radius <= 0f
                    || math.distancesq(
                        transform.ValueRO.Position.xy,
                        field.Center) > field.Radius * field.Radius)
                {
                    continue;
                }

                resolution.ValueRW = new EnemyProjectileResolution
                {
                    Kind = EnemyProjectileResolutionKind.Expired,
                    TargetId = 0
                };
                break;
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProjectileCustomSpeedSystem))]
public partial struct ProjectileHomingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
        state.RequireForUpdate<EnemyProjectileHoming>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<EnemyProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<EnemyProjectileCollisionTarget>(true);
        if (targets.Length == 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<EnemyProjectileCustomSpeed> customSpeeds = SystemAPI
            .GetComponentLookup<EnemyProjectileCustomSpeed>(false);
        foreach ((RefRO<LocalTransform> transform,
                  RefRW<EnemyProjectileVelocity> velocity,
                  RefRW<EnemyProjectileHoming> homing,
                  RefRO<EnemyProjectileResolution> resolution,
                  Entity entity)
                 in SystemAPI.Query<RefRO<LocalTransform>,
                     RefRW<EnemyProjectileVelocity>,
                     RefRW<EnemyProjectileHoming>,
                     RefRO<EnemyProjectileResolution>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (homing.ValueRO.RemainingDuration > 0f)
            {
                homing.ValueRW.RemainingDuration -= deltaTime;
                if (homing.ValueRO.RemainingDuration <= 0f)
                    continue;
            }

            float2 position = transform.ValueRO.Position.xy;
            if (!TryGetNearestPlayerPosition(targets, position, out float2 targetPosition))
                continue;

            float2 targetDirection = math.normalizesafe(
                targetPosition - position,
                new float2(0f, -1f));
            float2 currentVelocity = velocity.ValueRO.Value;
            float speed = math.length(currentVelocity);
            if (speed <= 0.0001f)
                continue;

            float currentAngle = math.atan2(currentVelocity.y, currentVelocity.x);
            float targetAngle = math.atan2(targetDirection.y, targetDirection.x);
            float angleDelta = math.atan2(
                math.sin(targetAngle - currentAngle),
                math.cos(targetAngle - currentAngle));
            float maxTurn = math.radians(
                math.max(0f, homing.ValueRO.TurnSpeedDegreesPerSecond))
                * deltaTime;
            float nextAngle = currentAngle + math.clamp(
                angleDelta,
                -maxTurn,
                maxTurn);
            float2 nextDirection = new float2(
                math.cos(nextAngle),
                math.sin(nextAngle));
            velocity.ValueRW.Value = nextDirection * speed;

            if (customSpeeds.HasComponent(entity))
            {
                EnemyProjectileCustomSpeed customSpeed = customSpeeds[entity];
                customSpeed.Direction = nextDirection;
                customSpeeds[entity] = customSpeed;
            }
        }
    }

    private static bool TryGetNearestPlayerPosition(
        DynamicBuffer<EnemyProjectileCollisionTarget> targets,
        float2 position,
        out float2 playerPosition)
    {
        playerPosition = default;
        float nearestDistanceSq = float.MaxValue;
        for (int index = 0; index < targets.Length; index++)
        {
            EnemyProjectileCollisionTarget target = targets[index];
            if (target.Kind != EnemyProjectileCollisionTargetKind.PlayerShip)
                continue;

            float2 center = (target.Min + target.Max) * 0.5f;
            float distanceSq = math.distancesq(position, center);
            if (distanceSq >= nearestDistanceSq)
                continue;

            nearestDistanceSq = distanceSq;
            playerPosition = center;
        }

        return nearestDistanceSq < float.MaxValue;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProjectileCustomSpeedSystem))]
public partial struct ProjectileBurstAtPointSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
        state.RequireForUpdate<EnemyProjectileBurstAtPoint>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<EnemyProjectileBurstSpawnRequest> requests = SystemAPI
            .GetSingletonBuffer<EnemyProjectileBurstSpawnRequest>();
        float deltaTime = SystemAPI.Time.DeltaTime;
        double currentTime = SystemAPI.Time.ElapsedTime;
        ComponentLookup<EnemyProjectileCustomSpeed> customSpeeds = SystemAPI
            .GetComponentLookup<EnemyProjectileCustomSpeed>(false);

        SystemAPI.TryGetSingletonBuffer<EnemyProjectileSlowField>(out var slowFields, true);
        foreach ((RefRW<LocalTransform> transform,
                  RefRW<EnemyProjectilePreviousPosition> previousPosition,
                  RefRW<EnemyProjectileVelocity> velocity,
                  RefRW<EnemyProjectileBurstAtPoint> burst,
                  RefRW<EnemyProjectileResolution> resolution,
                  Entity entity)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRW<EnemyProjectilePreviousPosition>,
                     RefRW<EnemyProjectileVelocity>,
                     RefRW<EnemyProjectileBurstAtPoint>,
                     RefRW<EnemyProjectileResolution>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            EnemyProjectileBurstAtPoint burstData = burst.ValueRO;
            if (burstData.HasDetonated == 0)
            {
                float2 position = transform.ValueRO.Position.xy;
                float2 toTarget = burstData.TargetPosition - position;
                float distance = math.length(toTarget);
                float reachDistance = math.max(
                    burstData.DetonationRadius,
                    math.length(velocity.ValueRO.Value) * deltaTime
                        * EnemyProjectileSlowField.EvaluateMultiplier(position, slowFields));
                if (distance > reachDistance)
                {
                    float2 direction = toTarget / math.max(distance, 0.0001f);
                    float speed = math.length(velocity.ValueRO.Value);
                    velocity.ValueRW.Value = direction * speed;
                    if (customSpeeds.HasComponent(entity))
                    {
                        EnemyProjectileCustomSpeed customSpeed = customSpeeds[entity];
                        customSpeed.Direction = direction;
                        customSpeeds[entity] = customSpeed;
                    }

                    continue;
                }

                transform.ValueRW.Position = new float3(
                    burstData.TargetPosition.x,
                    burstData.TargetPosition.y,
                    transform.ValueRO.Position.z);
                transform.ValueRW.Scale = 0f;
                previousPosition.ValueRW.Value = burstData.TargetPosition;
                velocity.ValueRW.Value = float2.zero;
                burstData.HasDetonated = 1;
                burstData.NextWaveTime = currentTime;
                burst.ValueRW = burstData;
            }

            if (currentTime + 0.0001d < burstData.NextWaveTime)
                continue;

            for (int projectileIndex = 0;
                 projectileIndex < burstData.ProjectilesPerWave;
                 projectileIndex++)
            {
                float angleRadians = math.radians(
                    burstData.StartAngleDegrees
                    + projectileIndex * burstData.AngleStepDegrees);
                requests.Add(new EnemyProjectileBurstSpawnRequest
                {
                    BurstProjectileConfigId = burstData.BurstProjectileConfigId,
                    Position = burstData.TargetPosition,
                    Direction = new float2(
                        math.cos(angleRadians),
                        math.sin(angleRadians))
                });
            }

            burstData.SpawnedWaveCount++;
            if (burstData.SpawnedWaveCount >= burstData.WaveCount)
            {
                resolution.ValueRW = new EnemyProjectileResolution
                {
                    Kind = EnemyProjectileResolutionKind.Expired,
                    TargetId = 0
                };
                continue;
            }

            burstData.NextWaveTime = currentTime + burstData.WaveInterval;
            burst.ValueRW = burstData;
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ProjectileMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileVelocity>();
        state.RequireForUpdate<EnemyProjectileMovement>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        SystemAPI.TryGetSingletonBuffer<EnemyProjectileSlowField>(out var slowFields, true);
        ComponentLookup<EnemyProjectileCustomSpeed> customSpeeds = SystemAPI
            .GetComponentLookup<EnemyProjectileCustomSpeed>(false);
        foreach ((RefRW<LocalTransform> transform,
                  RefRW<EnemyProjectilePreviousPosition> previousPosition,
                  RefRW<EnemyProjectileVelocity> velocity,
                  RefRW<EnemyProjectileMovement> movement,
                  RefRO<EnemyProjectileResolution> resolution,
                  Entity entity)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRW<EnemyProjectilePreviousPosition>,
                     RefRW<EnemyProjectileVelocity>,
                     RefRW<EnemyProjectileMovement>,
                     RefRO<EnemyProjectileResolution>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            float2 currentPosition = transform.ValueRO.Position.xy;
            previousPosition.ValueRW.Value = currentPosition;

            float slowMultiplier = EnemyProjectileSlowField.EvaluateMultiplier(
                currentPosition,
                slowFields);
            float2 authoredVelocity = velocity.ValueRO.Value;
            if (movement.ValueRO.Pattern == EnemyProjectileMovementPattern.Default)
            {
                float2 currentVelocity = authoredVelocity * slowMultiplier;
                transform.ValueRW.Position += new float3(
                    currentVelocity.x * deltaTime,
                    currentVelocity.y * deltaTime,
                    0f);
                if (math.lengthsq(currentVelocity) > 0.0001f)
                {
                    transform.ValueRW.Rotation = quaternion.RotateZ(
                        math.atan2(currentVelocity.y, currentVelocity.x)
                        + math.PI * 0.5f);
                }

                continue;
            }

            float authoredSpeed = math.length(authoredVelocity);
            float2 forward = math.normalizesafe(
                authoredVelocity,
                movement.ValueRO.ForwardDirection);
            float speedRatio = movement.ValueRO.ReferenceSpeed > 0.0001f
                ? authoredSpeed / movement.ValueRO.ReferenceSpeed
                : 1f;
            float2 nextPosition = currentPosition;
            float2 facingVelocity = authoredVelocity;

            switch (movement.ValueRO.Pattern)
            {
                case EnemyProjectileMovementPattern.AngularTurn:
                {
                    float turnRadians = -math.radians(
                        movement.ValueRO.AngularSpeedDegreesPerSecond)
                        * speedRatio
                        * slowMultiplier
                        * deltaTime;
                    forward = Rotate(forward, turnRadians);
                    authoredVelocity = forward * authoredSpeed;
                    velocity.ValueRW.Value = authoredVelocity;
                    movement.ValueRW.ForwardDirection = forward;
                    UpdateCustomSpeedDirection(entity, forward, ref customSpeeds);
                    facingVelocity = authoredVelocity * slowMultiplier;
                    nextPosition += facingVelocity * deltaTime;
                    break;
                }
                case EnemyProjectileMovementPattern.Lateral:
                {
                    movement.ValueRW.ForwardDirection = forward;
                    float2 right = new float2(forward.y, -forward.x);
                    float2 lateralVelocity = (authoredVelocity
                        + right * movement.ValueRO.LateralSpeed)
                        * slowMultiplier;
                    nextPosition += lateralVelocity * deltaTime;
                    facingVelocity = authoredVelocity * slowMultiplier;
                    break;
                }
                case EnemyProjectileMovementPattern.Orbit:
                {
                    float orbitRadius = math.max(
                        0f,
                        movement.ValueRO.OrbitRadius
                        + movement.ValueRO.OrbitRadialSpeed
                        * slowMultiplier
                        * deltaTime);
                    float orbitAngle = movement.ValueRO.OrbitAngleRadians
                        - math.radians(
                        movement.ValueRO.AngularSpeedDegreesPerSecond)
                        * speedRatio
                        * slowMultiplier
                        * deltaTime;
                    movement.ValueRW.OrbitRadius = orbitRadius;
                    movement.ValueRW.OrbitAngleRadians = orbitAngle;
                    nextPosition = movement.ValueRO.OrbitCenter
                        + new float2(math.cos(orbitAngle), math.sin(orbitAngle))
                        * orbitRadius;
                    float2 displacement = nextPosition - currentPosition;
                    if (math.lengthsq(displacement) > 0.0000001f)
                    {
                        forward = math.normalize(displacement);
                        movement.ValueRW.ForwardDirection = forward;
                        velocity.ValueRW.Value = forward * authoredSpeed;
                        UpdateCustomSpeedDirection(entity, forward, ref customSpeeds);
                        facingVelocity = displacement / math.max(deltaTime, 0.0001f);
                    }
                    else
                    {
                        facingVelocity = float2.zero;
                    }
                    break;
                }
                default:
                    continue;
            }

            transform.ValueRW.Position = new float3(
                nextPosition.x,
                nextPosition.y,
                transform.ValueRO.Position.z);
            if (math.lengthsq(facingVelocity) > 0.0001f)
            {
                transform.ValueRW.Rotation = quaternion.RotateZ(
                    math.atan2(facingVelocity.y, facingVelocity.x)
                    + math.PI * 0.5f);
            }
        }
    }

    private static float2 Rotate(float2 direction, float radians)
    {
        float sine = math.sin(radians);
        float cosine = math.cos(radians);
        return new float2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }

    private static void UpdateCustomSpeedDirection(
        Entity entity,
        float2 direction,
        ref ComponentLookup<EnemyProjectileCustomSpeed> customSpeeds)
    {
        if (!customSpeeds.HasComponent(entity))
            return;

        EnemyProjectileCustomSpeed customSpeed = customSpeeds[entity];
        customSpeed.Direction = direction;
        customSpeeds[entity] = customSpeed;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileMovementSystem))]
public partial struct ProjectileInterceptorSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<EnemyProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<EnemyProjectileCollisionTarget>(true);
        if (targets.Length == 0)
            return;

        ComponentLookup<EnemyProjectileBurstAtPoint> burstProjectiles = SystemAPI
            .GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        ComponentLookup<EnemyProjectileCustomScale> customScales = SystemAPI
            .GetComponentLookup<EnemyProjectileCustomScale>(true);

        foreach ((RefRW<EnemyProjectileResolution> resolution,
                  RefRO<EnemyProjectilePreviousPosition> previousPosition,
                  RefRO<EnemyProjectileStaticData> staticData,
                  RefRO<LocalTransform> transform,
                  Entity entity)
                 in SystemAPI.Query<RefRW<EnemyProjectileResolution>,
                     RefRO<EnemyProjectilePreviousPosition>,
                     RefRO<EnemyProjectileStaticData>,
                     RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (burstProjectiles.HasComponent(entity)
                && burstProjectiles[entity].HasDetonated != 0)
            {
                continue;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                EnemyProjectileCollisionTarget target = targets[index];
                if (target.Kind != EnemyProjectileCollisionTargetKind.Interceptor)
                    continue;

                float projectileRadius = staticData.ValueRO.Radius;
                if (customScales.HasComponent(entity))
                    projectileRadius *= transform.ValueRO.Scale;

                if (!IntersectsExpandedBounds(
                        previousPosition.ValueRO.Value,
                        transform.ValueRO.Position.xy,
                        target.Min,
                        target.Max,
                        projectileRadius))
                    continue;

                resolution.ValueRW = new EnemyProjectileResolution
                {
                    Kind = EnemyProjectileResolutionKind.Intercepted,
                    TargetId = target.TargetId
                };
                break;
            }
        }
    }

    internal static bool IntersectsExpandedBounds(
        float2 start,
        float2 end,
        float2 min,
        float2 max,
        float radius)
    {
        min -= radius;
        max += radius;

        float2 direction = end - start;
        float entry = 0f;
        float exit = 1f;
        if (!IntersectsAxis(start.x, direction.x, min.x, max.x, ref entry, ref exit)
            || !IntersectsAxis(
                start.y,
                direction.y,
                min.y,
                max.y,
                ref entry,
                ref exit))
        {
            return false;
        }

        return entry <= exit && exit >= 0f && entry <= 1f;
    }

    private static bool IntersectsAxis(
        float start,
        float direction,
        float minimum,
        float maximum,
        ref float entry,
        ref float exit)
    {
        if (math.abs(direction) < 0.00001f)
            return start >= minimum && start <= maximum;

        float inverseDirection = 1f / direction;
        float first = (minimum - start) * inverseDirection;
        float second = (maximum - start) * inverseDirection;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        entry = math.max(entry, first);
        exit = math.min(exit, second);
        return entry <= exit;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileInterceptorSystem))]
public partial struct ProjectilePlayerCollisionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<EnemyProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<EnemyProjectileCollisionTarget>(true);
        if (targets.Length == 0)
            return;

        ComponentLookup<EnemyProjectileBurstAtPoint> burstProjectiles = SystemAPI
            .GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        ComponentLookup<EnemyProjectileCustomScale> customScales = SystemAPI
            .GetComponentLookup<EnemyProjectileCustomScale>(true);

        foreach ((RefRW<EnemyProjectileResolution> resolution,
                  RefRO<EnemyProjectilePreviousPosition> previousPosition,
                  RefRO<EnemyProjectileStaticData> staticData,
                  RefRO<LocalTransform> transform,
                  Entity entity)
                 in SystemAPI.Query<RefRW<EnemyProjectileResolution>,
                     RefRO<EnemyProjectilePreviousPosition>,
                     RefRO<EnemyProjectileStaticData>,
                     RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (burstProjectiles.HasComponent(entity)
                && burstProjectiles[entity].HasDetonated != 0)
            {
                continue;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                EnemyProjectileCollisionTarget target = targets[index];
                if (target.Kind != EnemyProjectileCollisionTargetKind.PlayerShip)
                    continue;

                float projectileRadius = staticData.ValueRO.Radius;
                if (customScales.HasComponent(entity))
                    projectileRadius *= transform.ValueRO.Scale;

                if (!ProjectileInterceptorSystem.IntersectsExpandedBounds(
                        previousPosition.ValueRO.Value,
                        transform.ValueRO.Position.xy,
                        target.Min,
                        target.Max,
                        projectileRadius))
                    continue;

                resolution.ValueRW = new EnemyProjectileResolution
                {
                    Kind = EnemyProjectileResolutionKind.HitPlayer,
                    TargetId = target.TargetId
                };
                break;
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectilePlayerCollisionSystem))]
public partial struct ProjectileLifetimeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileRemainingLifetime>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<EnemyProjectileBurstAtPoint> burstProjectiles = SystemAPI
            .GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        foreach ((RefRW<EnemyProjectileRemainingLifetime> lifetime,
                  RefRW<EnemyProjectileResolution> resolution,
                  Entity entity)
                 in SystemAPI.Query<RefRW<EnemyProjectileRemainingLifetime>,
                     RefRW<EnemyProjectileResolution>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            if (burstProjectiles.HasComponent(entity)
                && burstProjectiles[entity].HasDetonated != 0)
            {
                continue;
            }

            lifetime.ValueRW.Value -= deltaTime;
            if (lifetime.ValueRW.Value <= 0f)
            {
                resolution.ValueRW = new EnemyProjectileResolution
                {
                    Kind = EnemyProjectileResolutionKind.Expired,
                    TargetId = 0
                };
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileLifetimeSystem))]
public partial struct ProjectileCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<EnemyProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<EnemyProjectileResolutionEvent>();
        EntityCommandBuffer commandBuffer = SystemAPI
            .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        foreach ((RefRO<EnemyProjectileResolution> resolution,
                  RefRO<EnemyProjectileDamage> damage,
                  Entity entity)
                 in SystemAPI.Query<RefRO<EnemyProjectileResolution>,
                     RefRO<EnemyProjectileDamage>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind == EnemyProjectileResolutionKind.None)
                continue;

            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.Expired)
            {
                events.Add(new EnemyProjectileResolutionEvent
                {
                    TargetId = resolution.ValueRO.TargetId,
                    Kind = resolution.ValueRO.Kind,
                    Damage = damage.ValueRO.Value
                });
            }

            commandBuffer.DestroyEntity(entity);
        }
    }
}
