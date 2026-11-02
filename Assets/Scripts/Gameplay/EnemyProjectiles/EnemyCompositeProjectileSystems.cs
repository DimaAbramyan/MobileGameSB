using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct EnemyCompositeCenter : IComponentData
{
    public EnemyCompositeProjectileSpec Settings;
    public float BirthTime, Angle;
}
public struct EnemyCompositePart : IComponentData { public Entity Center; }
// Preview preserves its existing non-damaging ordinary bullets, while composite parts
// can demonstrate individual contacts using the production collision algorithm.
public struct EnemyProjectilePreviewCollisionPolicy : IComponentData { }
[InternalBufferCapacity(16)]
public struct EnemyCompositeMember : IBufferElementData { public Entity Entity; public float2 Offset; }
public struct EnemyCompositeBounds : IComponentData { public float2 Min, Max, CenterMin, CenterMax; }

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileMovementSystem))]
[UpdateBefore(typeof(ProjectileInterceptorSystem))]
[UpdateBefore(typeof(ProjectilePlayerCollisionSystem))]
public partial struct EnemyCompositePoseSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var poses=SystemAPI.GetComponentLookup<LocalTransform>(false);
        var previous=SystemAPI.GetComponentLookup<EnemyProjectilePreviousPosition>(false);
        var resolutions=SystemAPI.GetComponentLookup<EnemyProjectileResolution>(true);
        var data=SystemAPI.GetComponentLookup<EnemyProjectileStaticData>(true);
        float time=(float)SystemAPI.Time.ElapsedTime;
        foreach(var (center,bounds,members,entity) in SystemAPI.Query<RefRW<EnemyCompositeCenter>,RefRW<EnemyCompositeBounds>,DynamicBuffer<EnemyCompositeMember>>().WithNone<EnemyProjectileHiddenAnchor>().WithEntityAccess())
        {
            var pose=poses[entity];
            var settings=center.ValueRO.Settings;
            float2 motion=pose.Position.xy-previous[entity].Value;
            float angle=settings.Rotation(math.max(0,time-center.ValueRO.BirthTime),motion);
            if(settings.RotationMode==EnemyCompositeRotationMode.AlignToMovement && math.lengthsq(motion)<=.00000001f) angle=center.ValueRO.Angle;
            center.ValueRW.Angle=angle;
            float radians=math.radians(angle);
            math.sincos(radians,out float sine,out float cosine);
            var right=new float2(cosine,-sine); var up=new float2(sine,cosine);
            var rotation=quaternion.RotateZ(-radians);
            float2 min=new float2(float.PositiveInfinity), max=new float2(float.NegativeInfinity), centerMin=min, centerMax=max;
            int live=0;
            for(int i=0;i<members.Length;i++)
            {
                var member=members[i];
                if(!poses.HasComponent(member.Entity) || !resolutions.HasComponent(member.Entity) || resolutions[member.Entity].Kind!=EnemyProjectileResolutionKind.None)continue;
                var part=poses[member.Entity];
                previous[member.Entity]=new EnemyProjectilePreviousPosition {Value=part.Position.xy};
                float2 point=pose.Position.xy+(right*member.Offset.x+up*member.Offset.y)*pose.Scale;
                part.Position=new float3(point,pose.Position.z); part.Scale=settings.ComponentScale*pose.Scale;
                part.Rotation=rotation; poses[member.Entity]=part;
                float r=math.abs(data[member.Entity].Radius*part.Scale);
                min=math.min(min,point-r); max=math.max(max,point+r);
                centerMin=math.min(centerMin,point); centerMax=math.max(centerMax,point); live++;
            }
            if(live==0) min=max=centerMin=centerMax=pose.Position.xy;
            bounds.ValueRW=new EnemyCompositeBounds {Min=min,Max=max,CenterMin=centerMin,CenterMax=centerMax};
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(EnemyProjectileScreenBoundarySystem))]
[UpdateAfter(typeof(EnemyChildAttackSystem))]
[UpdateAfter(typeof(ProjectileLifetimeSystem))]
[UpdateBefore(typeof(ProjectileCleanupSystem))]
public partial struct EnemyCompositeCleanupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var resolutions=SystemAPI.GetComponentLookup<EnemyProjectileResolution>(false);
        foreach(var (members,entity) in SystemAPI.Query<DynamicBuffer<EnemyCompositeMember>>().WithEntityAccess())
        {
            bool expired=resolutions[entity].Kind!=EnemyProjectileResolutionKind.None;
            bool live=false;
            for(int i=0;i<members.Length;i++)
            {
                var member=members[i].Entity;
                if(!resolutions.HasComponent(member)||resolutions[member].Kind!=EnemyProjectileResolutionKind.None)continue;
                if(expired) resolutions[member]=new EnemyProjectileResolution {Kind=EnemyProjectileResolutionKind.Expired};
                else live=true;
            }
            if(!live && !expired)resolutions[entity]=new EnemyProjectileResolution {Kind=EnemyProjectileResolutionKind.Expired};
        }
        // Also covers a center explicitly deleted by an external ECS owner.
        foreach(var (part,resolution) in SystemAPI.Query<RefRO<EnemyCompositePart>,RefRW<EnemyProjectileResolution>>())
            if(!resolutions.HasComponent(part.ValueRO.Center)&&resolution.ValueRO.Kind==EnemyProjectileResolutionKind.None)
                resolution.ValueRW=new EnemyProjectileResolution {Kind=EnemyProjectileResolutionKind.Expired};
    }
}
