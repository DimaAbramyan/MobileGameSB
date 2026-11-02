using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public enum EnemyProjectileCollisionDisableCondition
{
    None = 0,
    ScaleBelowThreshold = 1
}

[Serializable]
public struct EnemyProjectileCollisionSettings
{
    // Missing fields in old assets/JSON must keep collisions enabled.
    [SerializeField] private bool disabled;
    [SerializeField] private EnemyProjectileCollisionDisableCondition disableCondition;
    [SerializeField] private float scaleThresholdDelta;

    public bool CollisionEnabled => !disabled;
    public EnemyProjectileCollisionDisableCondition DisableCondition => disableCondition;
    public float ScaleThreshold => float.IsFinite(scaleThresholdDelta) ? Mathf.Max(0f, .5f + scaleThresholdDelta) : .5f;

    public EnemyProjectileCollisionSettings(bool enabled,
        EnemyProjectileCollisionDisableCondition condition = EnemyProjectileCollisionDisableCondition.None,
        float scaleThreshold = .5f)
    {
        disabled = !enabled;
        disableCondition = condition;
        scaleThresholdDelta = (float.IsFinite(scaleThreshold) ? Mathf.Max(0f, scaleThreshold) : .5f) - .5f;
    }
}

// Independent of radius/rendering/resolution. Once disabled, growth cannot restore contact.
public struct EnemyProjectileCollisionState : IComponentData
{
    public EnemyProjectileCollisionDisableCondition DisableCondition;
    public float ScaleThreshold;
    public byte Disabled;

    public static EnemyProjectileCollisionState Create(EnemyProjectileCollisionSettings settings, float scale)
    {
        var state = new EnemyProjectileCollisionState {
            DisableCondition = settings.DisableCondition, ScaleThreshold = settings.ScaleThreshold,
            Disabled = settings.CollisionEnabled ? (byte)0 : (byte)1
        };
        state.UpdateScale(scale);
        return state;
    }

    public void UpdateScale(float scale)
    {
        if (DisableCondition == EnemyProjectileCollisionDisableCondition.ScaleBelowThreshold
            && math.abs(scale) <= ScaleThreshold)
            Disabled = 1;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileCustomScaleSystem))]
[UpdateAfter(typeof(EnemyCompositePoseSystem))]
[UpdateBefore(typeof(ProjectileInterceptorSystem))]
[UpdateBefore(typeof(ProjectilePlayerCollisionSystem))]
public partial struct EnemyProjectileCollisionStateSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (collision, pose) in SystemAPI.Query<RefRW<EnemyProjectileCollisionState>, RefRO<LocalTransform>>()
            .WithNone<EnemyCompositeCenter, EnemyLaserState>())
        {
            if (collision.ValueRO.Disabled == 0)
                collision.ValueRW.UpdateScale(pose.ValueRO.Scale);
        }
    }
}
