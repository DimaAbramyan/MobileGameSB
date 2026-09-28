using Unity.Entities;
using Unity.Mathematics;

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
/// Written by ECS cleanup and consumed by the managed collision registry.
/// </summary>
public struct EnemyProjectileResolutionEvent : IBufferElementData
{
    public int TargetId;
    public EnemyProjectileResolutionKind Kind;
    public float Damage;
}
