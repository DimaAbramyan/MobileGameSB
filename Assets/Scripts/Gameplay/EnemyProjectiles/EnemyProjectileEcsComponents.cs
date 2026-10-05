using Unity.Entities;
using Unity.Mathematics;

public enum EnemyProjectileSpeedBehavior : byte
{
    None = 0,
    Customized = 1
}

public enum EnemyProjectileFlightMode : byte
{
    Straight = 0,
    Homing = 1,
    BurstAtPoint = 2
}

public enum EnemyProjectileScaleBehavior : byte
{
    None = 0,
    Customized = 1
}

public enum EnemyProjectileScaleProgressMode : byte
{
    Distance = 0,
    Time = 1
}

/// <summary>
/// Immutable data copied from an <see cref="EnemyProjectileAuthoring"/> configuration.
/// It is intentionally separate from the values that change for each shot.
/// </summary>
public struct EnemyProjectileStaticData : IComponentData
{
    public float BaseDamage;
    public float Radius;
    public float BaseLifetime;
    public float BaseSpeed;
}

/// <summary>
/// Immutable visual dimensions taken from the projectile sprite.
/// The actual mesh and material are shared by the ECS spawner.
/// </summary>
public struct EnemyProjectileVisualStaticData : IComponentData
{
    public float2 Size;
}

public struct EnemyProjectileVelocity : IComponentData
{
    public float2 Value;
}

/// <summary>
/// Per-projectile movement configured by the enemy attack. The orbit center is
/// always a spawn-time snapshot, so ECS never needs to retain a source object.
/// </summary>
public struct EnemyProjectileMovement : IComponentData
{
    public EnemyProjectileMovementPattern Pattern;
    public float2 ForwardDirection;
    public float ReferenceSpeed;
    public float AngularSpeedDegreesPerSecond;
    public float LateralSpeed;
    public float2 OrbitCenter;
    public float OrbitRadius;
    public float OrbitAngleRadians;
    public float OrbitRadialSpeed;
}

public struct EnemyProjectileRemainingLifetime : IComponentData
{
    public float Value;
}

public struct EnemyProjectileDamage : IComponentData
{
    public float Value;
}

public struct EnemyProjectilePreviousPosition : IComponentData
{
    public float2 Value;
}

/// <summary>
/// Per-instance projectile speed profile. The curve itself is sampled by the
/// managed spawner so the projectile stays entirely ECS-driven at runtime.
/// </summary>
public struct EnemyProjectileCustomSpeed : IComponentData
{
    public float InitialSpeed;
    public float SpeedChangeY;
    public float DistanceSinceSpeedChange;
    public float2 Direction;
    public byte HasReachedSpeedChangeY;
}

[InternalBufferCapacity(8)]
public struct EnemyProjectileSpeedCurveSample : IBufferElementData
{
    public float Distance;
    public float SpeedMultiplier;
}

/// <summary>
/// Per-instance visual and collision scale profile. The authored visual scale
/// remains in PostTransformMatrix; this value is its runtime multiplier.
/// </summary>
public struct EnemyProjectileCustomScale : IComponentData
{
    public float ScaleChangeY;
    public float ProgressSinceScaleChange;
    public float ProgressSpeed;
    public EnemyProjectileScaleProgressMode ProgressMode;
    public byte HasReachedScaleChangeY;
}

[InternalBufferCapacity(8)]
public struct EnemyProjectileScaleCurveSample : IBufferElementData
{
    public float Input;
    public float ScaleMultiplier;
}

/// <summary>
/// Turns an ECS projectile towards the nearest current player-ship collider.
/// A non-positive remaining duration means that homing does not expire.
/// </summary>
public struct EnemyProjectileHoming : IComponentData
{
    public float TurnSpeedDegreesPerSecond;
    public float RemainingDuration;
}

/// <summary>
/// A projectile travelling to a world-space point before emitting radial waves.
/// The managed registry resolves the projectile configuration id in the emitted
/// requests; no managed Unity object is kept on the Entity.
/// </summary>
public struct EnemyProjectileBurstAtPoint : IComponentData
{
    public float2 TargetPosition;
    public float DetonationRadius;
    public int BurstProjectileConfigId;
    public int WaveCount;
    public int SpawnedWaveCount;
    public int ProjectilesPerWave;
    public float AngleStepDegrees;
    public float StartAngleDegrees;
    public float WaveInterval;
    public double NextWaveTime;
    public byte HasDetonated;
}

public enum EnemyProjectileCollisionTargetKind : byte
{
    Interceptor = 1,
    PlayerShip = 2
}

public enum EnemyProjectileResolutionKind : byte
{
    None = 0,
    Intercepted = 1,
    HitPlayer = 2,
    Expired = 3
}

/// <summary>
/// Per-instance collision result. It avoids structural changes while a projectile is
/// travelling and guarantees that Arkanoid interception wins over player damage.
/// </summary>
public struct EnemyProjectileResolution : IComponentData
{
    public EnemyProjectileResolutionKind Kind;
    public int TargetId;
}

/// <summary>
/// Singleton entity owned by <see cref="EnemyProjectileCollisionRegistry"/>.
/// </summary>
public struct EnemyProjectileCollisionRegistryTag : IComponentData
{
}

/// <summary>
/// A current world-space collider snapshot. TargetId is resolved by the managed
/// registry only after ECS has selected the first collision.
/// </summary>
public struct EnemyProjectileCollisionTarget : IBufferElementData
{
    public int TargetId;
    public EnemyProjectileCollisionTargetKind Kind;
    public float2 Min;
    public float2 Max;
}

/// <summary>
/// Current world-space slowing aura, snapshotted alongside the player hitbox.
/// Changes displacement without overwriting the projectile's authored velocity.
/// </summary>
[InternalBufferCapacity(4)]
public struct EnemyProjectileSlowField : IBufferElementData
{
    public float2 Center;
    public float Radius;
    public float MinimumSpeedMultiplier;

    public static float EvaluateMultiplier(
        float2 position,
        DynamicBuffer<EnemyProjectileSlowField> fields)
    {
        float multiplier = 1f;
        if (!fields.IsCreated)
            return multiplier;

        for (int index = 0; index < fields.Length; index++)
        {
            EnemyProjectileSlowField field = fields[index];
            if (field.Radius <= 0f)
                continue;

            float distanceSq = math.distancesq(position, field.Center);
            if (distanceSq >= field.Radius * field.Radius)
                continue;

            float normalizedDistance = math.sqrt(distanceSq) / field.Radius;
            multiplier = math.min(multiplier, math.max(
                math.clamp(field.MinimumSpeedMultiplier, 0.01f, 1f),
                normalizedDistance));
        }

        return multiplier;
    }
}

/// <summary>
/// A world-space area that removes enemy projectiles through the standard ECS
/// cleanup path. It is supplied by a managed ability instance each fixed step.
/// </summary>
[InternalBufferCapacity(2)]
public struct EnemyProjectilePurgeField : IBufferElementData
{
    public float2 Center;
    public float Radius;
}

/// <summary>
/// Written by ECS cleanup and consumed by the managed collision registry.
/// </summary>
public struct EnemyProjectileResolutionEvent : IBufferElementData
{
    public int TargetId;
    public EnemyProjectileResolutionKind Kind;
    public float Damage;
}

/// <summary>
/// Written by the burst-at-point ECS system and consumed by the collision
/// registry, which owns the managed <see cref="EnemyBullet"/> references.
/// </summary>
public struct EnemyProjectileBurstSpawnRequest : IBufferElementData
{
    public int BurstProjectileConfigId;
    public float2 Position;
    public float2 Direction;
}
