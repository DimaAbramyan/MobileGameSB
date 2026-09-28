using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ProjectileMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyProjectileVelocity>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<LocalTransform> transform,
                  RefRW<EnemyProjectilePreviousPosition> previousPosition,
                  RefRO<EnemyProjectileVelocity> velocity,
                  RefRO<EnemyProjectileResolution> resolution)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRW<EnemyProjectilePreviousPosition>,
                     RefRO<EnemyProjectileVelocity>,
                     RefRO<EnemyProjectileResolution>>())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            float2 currentPosition = transform.ValueRO.Position.xy;
            previousPosition.ValueRW.Value = currentPosition;

            float2 currentVelocity = velocity.ValueRO.Value;
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
        }
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

        foreach ((RefRW<EnemyProjectileResolution> resolution,
                  RefRO<EnemyProjectilePreviousPosition> previousPosition,
                  RefRO<EnemyProjectileStaticData> staticData,
                  RefRO<LocalTransform> transform)
                 in SystemAPI.Query<RefRW<EnemyProjectileResolution>,
                     RefRO<EnemyProjectilePreviousPosition>,
                     RefRO<EnemyProjectileStaticData>,
                     RefRO<LocalTransform>>())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            for (int index = 0; index < targets.Length; index++)
            {
                EnemyProjectileCollisionTarget target = targets[index];
                if (target.Kind != EnemyProjectileCollisionTargetKind.Interceptor)
                    continue;

                if (!IntersectsExpandedBounds(
                        previousPosition.ValueRO.Value,
                        transform.ValueRO.Position.xy,
                        target.Min,
                        target.Max,
                        staticData.ValueRO.Radius))
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

        foreach ((RefRW<EnemyProjectileResolution> resolution,
                  RefRO<EnemyProjectilePreviousPosition> previousPosition,
                  RefRO<EnemyProjectileStaticData> staticData,
                  RefRO<LocalTransform> transform)
                 in SystemAPI.Query<RefRW<EnemyProjectileResolution>,
                     RefRO<EnemyProjectilePreviousPosition>,
                     RefRO<EnemyProjectileStaticData>,
                     RefRO<LocalTransform>>())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

            for (int index = 0; index < targets.Length; index++)
            {
                EnemyProjectileCollisionTarget target = targets[index];
                if (target.Kind != EnemyProjectileCollisionTargetKind.PlayerShip)
                    continue;

                if (!ProjectileInterceptorSystem.IntersectsExpandedBounds(
                        previousPosition.ValueRO.Value,
                        transform.ValueRO.Position.xy,
                        target.Min,
                        target.Max,
                        staticData.ValueRO.Radius))
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
        foreach ((RefRW<EnemyProjectileRemainingLifetime> lifetime,
                  RefRW<EnemyProjectileResolution> resolution)
                 in SystemAPI.Query<RefRW<EnemyProjectileRemainingLifetime>,
                     RefRW<EnemyProjectileResolution>>())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None)
                continue;

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
