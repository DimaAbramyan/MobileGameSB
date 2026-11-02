using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

public sealed partial class EnemyProjectileEcsSpawner
{
    private bool compositeLimitWarned;
    private bool CompositeBatchFits(IReadOnlyList<EnemyProjectileSpawnRequest> requests)
    {
        int cost=0; bool composite=false;
        for(int i=0;i<requests.Count;i++){cost+=requests[i].EntityCost;composite|=requests[i].Composite.Enabled;}
        if(!composite)return true;
        using var query=entityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
        if((long)query.CalculateEntityCount()+cost<=32768)return true;
        if(!compositeLimitWarned){UnityEngine.Debug.LogWarning("Composite projectile safety limit: maximum 32768 live projectile entities.");compositeLimitWarned=true;}
        return false;
    }
    private readonly Dictionary<Entity,Entity> compositeCenters=new();
    private readonly Dictionary<Entity,Entity> compositeMembers=new();
    private Entity GetCompositeCenterTemplate(PreparedProjectileSpawn prepared)
    {
        if(prepared.SpawnSettings.FlightMode==EnemyProjectileFlightMode.BurstAtPoint)
            throw new InvalidOperationException("Composite components require a Default or Homing prefab; Burst At Point is not supported.");
        var child=prepared.Request.ChildAttack;
        if(child!=null && child.TriggerMode==EnemyChildAttackTriggerMode.OnEvent && child.EventType==EnemyChildAttackEvent.HitPlayer)
            throw new InvalidOperationException("A Composite center has no hitbox: Child Attack Hit Player is not supported. Use time, distance, expiry or boundary events.");
        if(compositeCenters.TryGetValue(prepared.Template,out var cached)&&entityManager.Exists(cached))return cached;
        var center=entityManager.Instantiate(prepared.Template);
        entityManager.AddComponent<Prefab>(center); entityManager.AddComponent<DisableRendering>(center);
        entityManager.AddComponentData(center,new EnemyCompositeCenter());
        entityManager.AddComponentData(center,new EnemyCompositeBounds());
        entityManager.AddBuffer<EnemyCompositeMember>(center);
        var member=entityManager.Instantiate(prepared.Template); entityManager.AddComponent<Prefab>(member);
        entityManager.RemoveComponent<EnemyProjectileVelocity>(member);
        entityManager.RemoveComponent<EnemyProjectileCustomSpeed>(member); entityManager.RemoveComponent<EnemyProjectileSpeedCurveSample>(member);
        entityManager.RemoveComponent<EnemyProjectileHoming>(member); entityManager.RemoveComponent<EnemyProjectileBurstAtPoint>(member);
        entityManager.RemoveComponent<EnemyProjectileRemainingLifetime>(member); entityManager.RemoveComponent<EnemyProjectileScaleCurveSample>(member);
        if(!entityManager.HasComponent<EnemyProjectileCustomScale>(member)) entityManager.AddComponentData(member,new EnemyProjectileCustomScale());
        entityManager.AddComponentData(member,new EnemyCompositePart());
        compositeCenters.Add(prepared.Template,center); compositeMembers.Add(center,member);
        return center;
    }
    private void ConfigureComposite(Entity center,PreparedProjectileSpawn prepared)
    {
        var spec=prepared.Request.Composite;
        float damage = prepared.Request.OverridesDamage ? prepared.Request.Damage : spec.Damage * prepared.Request.DamageMultiplier;
        var pose=entityManager.GetComponentData<LocalTransform>(center);
        entityManager.SetComponentData(center,new EnemyCompositeCenter {Settings=spec,BirthTime=(float)entityWorld.Time.ElapsedTime,Angle=spec.InitialRotation});
        var centerData=entityManager.GetComponentData<EnemyProjectileStaticData>(center);
        centerData.Radius=0; centerData.BaseDamage=0; entityManager.SetComponentData(center,centerData);
        entityManager.SetComponentData(center,new EnemyProjectileDamage());
        entityManager.SetComponentData(center, EnemyProjectileCollisionState.Create(new EnemyProjectileCollisionSettings(false), pose.Scale));
        int count=spec.ComponentCount;
        float radius=spec.OverrideHitRadius?spec.HitRadius:prepared.Configuration.EffectiveRadius;
        float shapeRadius=spec.ResolveRadius(radius*spec.ComponentScale);
        using var parts=new NativeArray<Entity>(count,Allocator.Temp);
        entityManager.Instantiate(compositeMembers[prepared.Template],parts);
        var colour=entityManager.GetComponentData<URPMaterialPropertyBaseColor>(center);
        float2 min=new float2(float.PositiveInfinity),max=new float2(float.NegativeInfinity),centerMin=min,centerMax=max;
        var members=entityManager.GetBuffer<EnemyCompositeMember>(center);
        for(int i=0;i<count;i++)
        {
            var part=parts[i]; float2 offset=spec.Offset(i,shapeRadius);
            float2 point=pose.Position.xy+EnemyCompositeProjectileSpec.Rotate(offset,spec.InitialRotation)*pose.Scale;
            float scale=pose.Scale*spec.ComponentScale;
            entityManager.SetComponentData(part,new EnemyCompositePart {Center=center});
            entityManager.SetComponentData(part,LocalTransform.FromPositionRotationScale(new float3(point,pose.Position.z),quaternion.RotateZ(-math.radians(spec.InitialRotation)),scale));
            entityManager.SetComponentData(part,new EnemyProjectilePreviousPosition {Value=point});
            entityManager.SetComponentData(part,new EnemyProjectileMovement());
            entityManager.SetComponentData(part,new EnemyProjectileResolution());
            entityManager.SetComponentData(part, EnemyProjectileCollisionState.Create(prepared.Request.CollisionSettings, scale));
            entityManager.SetComponentData(part,new EnemyProjectileStaticData {Radius=radius,BaseDamage=damage,Characteristic=prepared.EffectiveCharacteristic});
            entityManager.SetComponentData(part,new EnemyProjectileDamage {Value=damage,Type=spec.DamageType,UseConfiguredDamage=2});
            entityManager.SetComponentData(part,colour);
            entityManager.SetComponentData(part,new EnemyProjectileScreenState {Settings=new EnemyProjectileScreenSettings(false),Previous=point});
            members.Add(new EnemyCompositeMember {Entity=part,Offset=offset});
            min=math.min(min,point-radius*scale); max=math.max(max,point+radius*scale);
            centerMin=math.min(centerMin,point); centerMax=math.max(centerMax,point);
        }
        entityManager.SetComponentData(center,new EnemyCompositeBounds {Min=min,Max=max,CenterMin=centerMin,CenterMax=centerMax});
    }
}
