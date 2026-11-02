using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct EnemyProjectileScreenState : IComponentData
{
    public EnemyProjectileScreenSettings Settings;
    public float2 Previous;
    public byte Entered;
    public int CompletedBounces;
    // Contact times within the current movement step: Left, Right, Bottom, Top.
    // Native snapshot data also preserves the path needed by boundary triggers.
    public float4 BounceContacts;
    public float2 BounceFrom, BounceTo, BounceMin, BounceMax;
    public float BounceZ;
}

// Independent of lifetime holds: a boundary child may wait for a later selected edge,
// including outside the viewport, without suppressing its normal lifetime.
public struct EnemyChildAttackScreenHold : IComponentData, IEnableableComponent { }

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(EnemyChildAttackSystem))]
[UpdateBefore(typeof(ProjectileCleanupSystem))]
public partial class EnemyProjectileScreenBoundarySystem : SystemBase
{
    private Camera previewCamera;
    public void UpdatePreview(Camera camera)
    {
        previewCamera = camera;
        try { Update(); }
        finally { previewCamera = null; }
    }

    protected override void OnUpdate()
    {
        var camera = previewCamera != null ? previewCamera : Camera.main;
        var holds = SystemAPI.GetComponentLookup<EnemyChildAttackScreenHold>(true);
        var composites = SystemAPI.GetComponentLookup<EnemyCompositeBounds>(true);
        var bouncing = SystemAPI.GetComponentLookup<EnemyProjectileBounceEnabled>(true);
        bool cached = false, available = false;
        float cachedZ = 0f;
        Rect bounds = default;
        foreach (var (screen, pose, data, resolution, entity) in SystemAPI.Query<RefRW<EnemyProjectileScreenState>,
            RefRO<LocalTransform>, RefRO<EnemyProjectileStaticData>, RefRW<EnemyProjectileResolution>>().WithNone<EnemyProjectileHiddenAnchor>().WithEntityAccess())
        {
            if (!screen.ValueRO.Settings.DestroyOutsideScreen || resolution.ValueRO.Kind != EnemyProjectileResolutionKind.None) continue;
            float3 position = pose.ValueRO.Position;
            if (!cached || cachedZ != position.z)
            {
                cached = true; cachedZ = position.z;
                available = EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, cachedZ, out bounds);
            }
            if (!available) continue;
            float2 current = position.xy;
            float2 entryMin = new float2(bounds.xMin,bounds.yMin), entryMax = new float2(bounds.xMax,bounds.yMax);
            bool composite = composites.HasComponent(entity);
            if(composite)
            {
                var body=composites[entity];
                entryMin -= body.Max-current; entryMax -= body.Min-current;
            }
            if (screen.ValueRO.Entered == 0 && ProjectileInterceptorSystem.IntersectsExpandedBounds(
                screen.ValueRO.Previous, current, entryMin, entryMax, 0f))
                screen.ValueRW.Entered = 1;
            screen.ValueRW.Previous = current;
            if (screen.ValueRO.Entered == 0 || holds.HasComponent(entity) && holds.IsComponentEnabled(entity)) continue;
            float offset = screen.ValueRO.Settings.OffsetMode == EnemyProjectileBoundaryOffsetMode.Custom
                ? screen.ValueRO.Settings.BoundaryOffset : math.abs(data.ValueRO.Radius * pose.ValueRO.Scale);
            float2 bodyMin=current,bodyMax=current;
            if(composite)
            {
                var body=composites[entity];
                bool custom=screen.ValueRO.Settings.OffsetMode==EnemyProjectileBoundaryOffsetMode.Custom;
                bodyMin=custom?body.CenterMin:body.Min; bodyMax=custom?body.CenterMax:body.Max;
                if(!custom)offset=0;
            }
            var bounce = screen.ValueRO.Settings.BounceOnScreenBounds && screen.ValueRO.Settings.CanBounce(screen.ValueRO.CompletedBounces)
                && bouncing.HasComponent(entity) && bouncing.IsComponentEnabled(entity)
                ? screen.ValueRO.Settings.BounceEdges : EnemyChildAttackBoundaries.None;
            if ((bounce & EnemyChildAttackBoundaries.Left) == 0 && bodyMax.x < bounds.xMin - offset
                || (bounce & EnemyChildAttackBoundaries.Right) == 0 && bodyMin.x > bounds.xMax + offset
                || (bounce & EnemyChildAttackBoundaries.Bottom) == 0 && bodyMax.y < bounds.yMin - offset
                || (bounce & EnemyChildAttackBoundaries.Top) == 0 && bodyMin.y > bounds.yMax + offset)
                resolution.ValueRW = new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired };
        }
    }
}
