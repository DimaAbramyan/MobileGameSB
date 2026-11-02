using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileCustomScaleSystem))]
[UpdateAfter(typeof(EnemyCompositePoseSystem))]
[UpdateBefore(typeof(ProjectileInterceptorSystem))]
[UpdateBefore(typeof(ProjectilePlayerCollisionSystem))]
[UpdateBefore(typeof(EnemyChildAttackSystem))]
public partial struct EnemyProjectileSizeCleanupSystem : ISystem
{
    public const float SizeEpsilon = .0001f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var bursts = SystemAPI.GetComponentLookup<EnemyProjectileBurstAtPoint>(true);
        // Only visible projectile bodies participate. Composite controllers and
        // hidden service entities can legitimately have no rendered dimensions.
        foreach (var (pose, visual, resolution, entity) in SystemAPI.Query<RefRO<LocalTransform>,
            RefRO<EnemyProjectileVisualStaticData>, RefRW<EnemyProjectileResolution>>()
            .WithNone<EnemyCompositeCenter, DisableRendering, EnemyLaserState>().WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None) continue;
            // A detonated Burst At Point remains as an invisible scheduler until
            // its authored burst waves finish; zero scale is intentional there.
            if (bursts.HasComponent(entity) && bursts[entity].HasDetonated != 0) continue;
            float size = math.cmax(math.abs(visual.ValueRO.Size)) * math.max(0f, pose.ValueRO.Scale);
            if (size <= SizeEpsilon)
                resolution.ValueRW = new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired };
        }
    }
}
