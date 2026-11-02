using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public enum EnemyProjectileTargetPositionMode : byte { None, TargetPoint, ScreenBoundary }

public readonly struct EnemyProjectileTargetPositionSettings
{
    public readonly bool ScreenBoundary, HideParent;
    public readonly EnemyChildAttackBoundaries Boundaries;
    public readonly float Offset;
    public EnemyProjectileTargetPositionSettings(bool screenBoundary, EnemyChildAttackBoundaries boundaries, float offset, bool hideParent)
    { ScreenBoundary=screenBoundary; Boundaries=boundaries; Offset=float.IsFinite(offset)?offset:0f; HideParent=hideParent; }
}

// Enabled only for configured targets. Reached is latched; Position/Forward are the event pose.
public struct EnemyProjectileTargetPosition : IComponentData, IEnableableComponent
{
    public EnemyProjectileTargetPositionMode Mode;
    public EnemyChildAttackBoundaries Boundaries;
    public float Offset, ReachedTime;
    public float3 Position;
    public float2 Previous, Forward;
    public byte Reached, HideParent;
    public bool TryReach(float3 position, float2 forward, float time)
    {
        if (Reached!=0) return false;
        Position=position; Forward=math.normalizesafe(forward,new float2(0,-1)); ReachedTime=time; Reached=1; return true;
    }
}
public struct EnemyProjectileHiddenAnchor : IComponentData, IEnableableComponent { }

public static class EnemyProjectileTargetPositionUtility
{
    public static bool IsHidden(EntityManager m, Entity e) => m.HasComponent<EnemyProjectileHiddenAnchor>(e) && m.IsComponentEnabled<EnemyProjectileHiddenAnchor>(e);

    // Shared with the original screen-boundary child trigger, including reflected steps and offsets.
    public static bool TryBoundaryContact(Vector3 from, Vector3 to, EnemyProjectileScreenState screen, Rect bounds,
        EnemyChildAttackBoundaries edges, float offset, out float fraction, out Vector3 position)
    {
        fraction=0; position=default;
        bool bounced=screen.Settings.BounceOnScreenBounds && EnemyProjectileScreenBounce.TryContact(screen,EnemyChildAttackBoundaries.All,out _,out _);
        float firstFraction=0; Vector3 contact=default;
        if(bounced) EnemyProjectileScreenBounce.TryContact(screen,EnemyChildAttackBoundaries.All,out firstFraction,out contact);
        if(bounced && Mathf.Abs(offset)<.000001f) return EnemyProjectileScreenBounce.TryContact(screen,edges,out fraction,out position);
        Vector3 end=bounced?contact:to;
        if(EnemyChildAttackBoundaryIntersection.TryCross(from,end,bounds,edges,offset,out fraction,out _))
        { position=Vector3.LerpUnclamped(from,end,fraction); if(bounced) fraction*=firstFraction; return true; }
        if(bounced && EnemyChildAttackBoundaryIntersection.TryCross(contact,to,bounds,edges,offset,out fraction,out _))
        { position=Vector3.LerpUnclamped(contact,to,fraction); fraction=Mathf.Lerp(firstFraction,1,fraction); return true; }
        return false;
    }
    private static void HideBody(EntityManager m, Entity e)
    {
        if(!m.Exists(e)) return;
        m.SetComponentEnabled<EnemyProjectileHiddenAnchor>(e,true);
        if(!m.HasComponent<DisableRendering>(e))m.AddComponent<DisableRendering>(e);
        var collision=m.GetComponentData<EnemyProjectileCollisionState>(e);collision.Disabled=1;m.SetComponentData(e,collision);
        if(m.HasComponent<EnemyProjectileVelocity>(e))m.SetComponentData(e,new EnemyProjectileVelocity());
    }
    public static void Hide(EntityManager m, Entity e, float3 position)
    {
        var pose=m.GetComponentData<LocalTransform>(e);float3 translation=position-pose.Position;pose.Position=position;m.SetComponentData(e,pose);
        HideBody(m,e);
        if(!m.HasBuffer<EnemyCompositeMember>(e))return;
        // Hide all damage/render components without deleting the composition controller.
        using var parts=m.GetBuffer<EnemyCompositeMember>(e).ToNativeArray(Allocator.Temp);
        foreach(var member in parts)
        {
            if(!m.Exists(member.Entity))continue;
            var p=m.GetComponentData<LocalTransform>(member.Entity);p.Position+=translation;m.SetComponentData(member.Entity,p);
            HideBody(m,member.Entity);
        }
    }
    public static void Expire(EntityManager m, Entity e)
    {
        m.SetComponentData(e,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Expired});
        if(!m.HasBuffer<EnemyCompositeMember>(e))return;
        var members=m.GetBuffer<EnemyCompositeMember>(e);
        foreach(var part in members)if(m.Exists(part.Entity))m.SetComponentData(part.Entity,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Expired});
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(EnemyProjectileScreenBounceSystem))]
[UpdateBefore(typeof(ProjectileInterceptorSystem))]
[UpdateBefore(typeof(ProjectilePlayerCollisionSystem))]
[UpdateBefore(typeof(ProjectileLifetimeSystem))]
public partial class EnemyProjectileTargetPositionSystem : SystemBase
{
    private EntityQuery query;
    private Camera previewCamera;
    protected override void OnCreate()=>query=GetEntityQuery(typeof(EnemyProjectileTargetPosition),typeof(LocalTransform),typeof(EnemyProjectileResolution));
    public void UpdatePreview(Camera camera){previewCamera=camera;try{Update();}finally{previewCamera=null;}}
    protected override void OnUpdate()
    {
        bool cached=false,available=false;float z=0;Rect bounds=default;
        var camera=previewCamera!=null?previewCamera:Camera.main;
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var e in entities)
        {
            var target=EntityManager.GetComponentData<EnemyProjectileTargetPosition>(e);
            if(target.Reached!=0 || EntityManager.GetComponentData<EnemyProjectileResolution>(e).Kind!=EnemyProjectileResolutionKind.None)continue;
            var pose=EntityManager.GetComponentData<LocalTransform>(e);
            var motion=EntityManager.GetComponentData<EnemyProjectileMovement>(e);
            Vector3 reached=pose.Position;bool arrived=false;float fraction=1;
            if(target.Mode==EnemyProjectileTargetPositionMode.TargetPoint)arrived=motion.HasReachedTarget!=0;
            else if(target.Mode==EnemyProjectileTargetPositionMode.ScreenBoundary)
            {
                if(!cached||z!=pose.Position.z){cached=true;z=pose.Position.z;available=EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,z,out bounds);}
                if(available)arrived=EnemyProjectileTargetPositionUtility.TryBoundaryContact(new Vector3(target.Previous.x,target.Previous.y,z),pose.Position,
                    EntityManager.GetComponentData<EnemyProjectileScreenState>(e),bounds,target.Boundaries,target.Offset,out fraction,out reached);
            }
            target.Previous=pose.Position.xy;
            if(arrived)target.TryReach(reached,motion.ForwardDirection,(float)SystemAPI.Time.ElapsedTime-SystemAPI.Time.DeltaTime*(1-fraction));
            EntityManager.SetComponentData(e,target);
            if(arrived && target.HideParent!=0)EnemyProjectileTargetPositionUtility.Hide(EntityManager,e,target.Position);
        }
    }
}

// Observes the existing runner's pending work; it does not schedule or time attacks.
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(EnemyChildAttackCleanupSystem))]
[UpdateBefore(typeof(ProjectileCleanupSystem))]
public partial class EnemyProjectileHiddenAnchorCleanupSystem : SystemBase
{
    private EntityQuery anchors,owners;
    private readonly HashSet<Entity> pending=new();
    protected override void OnCreate()
    {
        anchors=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<EnemyProjectileHiddenAnchor>(),ComponentType.ReadOnly<EnemyProjectileResolution>()},None=new[]{ComponentType.ReadOnly<EnemyCompositePart>()}});
        owners=GetEntityQuery(typeof(EnemyChildAttackSimulation));
    }
    protected override void OnUpdate()
    {
        if(anchors.IsEmpty)return;
        pending.Clear();
        using(var hosts=owners.ToEntityArray(Allocator.Temp))foreach(var host in hosts)EntityManager.GetComponentObject<EnemyChildAttackSimulation>(host).Runner.CollectPendingParents(pending);
        using var entities=anchors.ToEntityArray(Allocator.Temp);
        foreach(var e in entities)if(!pending.Contains(e))EnemyProjectileTargetPositionUtility.Expire(EntityManager,e);
    }
}
