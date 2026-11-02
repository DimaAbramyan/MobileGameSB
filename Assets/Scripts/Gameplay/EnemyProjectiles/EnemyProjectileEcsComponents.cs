using Unity.Entities;
using Unity.Mathematics;

public enum EnemyProjectileCharacteristic : byte
{
    Normal = 0,
    Fragile = 1,
    Indestructible = 2
}

public static class EnemyProjectileCharacteristics
{
    public static UnityEngine.Color FragileColour => new UnityEngine.Color(1f, 0.35f, 0.65f, 1f);
    public static float4 ToRenderColour(UnityEngine.Color colour)
    {
        if (UnityEngine.QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear)
            colour = colour.linear;
        return new float4(colour.r, colour.g, colour.b, colour.a);
    }

    public static UnityEngine.Color FromRenderColour(float4 value)
    {
        var colour = new UnityEngine.Color(value.x, value.y, value.z, value.w);
        return UnityEngine.QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear
            ? colour.gamma : colour;
    }
    public static bool CanBeDestroyed(EnemyProjectileCharacteristic characteristic, bool destroysNormal)
        => characteristic == EnemyProjectileCharacteristic.Fragile
            || characteristic == EnemyProjectileCharacteristic.Normal && destroysNormal;
}

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
    public EnemyProjectileCharacteristic Characteristic;
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
    public float2 TargetPoint;
    public float TargetInitialDistance;
    public byte HasReachedTarget;
    public float ReferenceSpeed;
    public float AngularSpeedDegreesPerSecond;
    public float LateralSpeed;
    public float2 OrbitCenter;
    public float OrbitRadius;
    public float OrbitAngleRadians;
    public float OrbitRadialSpeed;
    public EnemyProjectileOrbitCenterMode OrbitCenterMode;
    public float2 DnaMedianDirection;
    public float DnaHalfCycleDuration;
    public float DnaElapsedTime;
}

// Arc speed is corrected by angle/sin(angle), so fan rays cross together.
// Shared by Burst movement and the editor preview; no per-frame allocation.
public static class EnemyProjectileDnaMotion
{
    public const float MaximumHalfAngleDegrees = 179.9f;

    public static void Evaluate(float2 median, float initialAngle, float halfCycleDuration,
        float elapsed, float medianSpeed, out float2 offset, out float2 direction, out float angularSpeed)
    {
        median = math.normalizesafe(median, new float2(0f, 1f));
        float2 left = new float2(-median.y, median.x);
        initialAngle = math.clamp(initialAngle, -math.radians(MaximumHalfAngleDegrees), math.radians(MaximumHalfAngleDegrees));
        if (halfCycleDuration <= 0f)
        {
            direction = median * math.cos(initialAngle) + left * math.sin(initialAngle);
            offset = direction * (medianSpeed * elapsed);
            angularSpeed = 0f;
            return;
        }
        float phase = math.fmod(math.max(0f, elapsed), 2f * halfCycleDuration) / halfCycleDuration;
        float side = phase < 1f ? 1f : -1f;
        float progress = phase < 1f ? phase : phase - 1f;
        float angle = side * initialAngle * (1f - 2f * progress);
        direction = median * math.cos(angle) + left * math.sin(angle);
        angularSpeed = side * math.degrees(2f * initialAngle / halfCycleDuration);
        float segment = math.floor(math.max(0f, elapsed) / halfCycleDuration);
        float length = medianSpeed * halfCycleDuration;
        float x = medianSpeed * elapsed;
        float y = 0f;
        if (math.abs(initialAngle) > 0.0001f)
        {
            float divisor = 2f * math.sin(initialAngle);
            x = length * (segment + (math.sin(initialAngle) - side * math.sin(angle)) / divisor);
            y = side * length * (math.cos(angle) - math.cos(initialAngle)) / divisor;
        }
        offset = median * x + left * y;
    }
}

public struct EnemyProjectileRemainingLifetime : IComponentData
{
    public float Value;
}

[InternalBufferCapacity(1)]
public struct EnemyProjectilePlayerContact : IBufferElementData
{
    public int TargetId;
    public byte Touching;
}

public struct EnemyProjectileDamage : IComponentData
{
    public byte HitPlayer;
    public EnemyDamageType Type;
    public byte UseConfiguredDamage;
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
    public EnemyProjectileSpeedCurveTarget CurveTarget;
    public float SpeedMultiplier;
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
    // Delta storage keeps default/legacy ECS state at scale 1.
    public float BaseSizeDelta;
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
    public byte LifetimeExpired;
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
    public byte DestroysNormal;
    public byte IsSegment;
    public float2 SegmentStart;
    public float2 SegmentEnd;
    public float2 SegmentDisplacement;
    public int TargetId;
    public EnemyProjectileCollisionTargetKind Kind;
    public float2 Min;
    public float2 Max;
    public byte IsCircle;
    public byte IsBox;
    public float2 BoxHalfSize;
    public float2 BoxRight;
    public float2 PreviousCenter;
    public float2 Center;
    public float Radius;
}

/// <summary>
/// Current world-space speed field, snapshotted alongside the player hitbox.
/// Changes displacement without overwriting the projectile's authored velocity.
/// </summary>
[InternalBufferCapacity(4)]
public struct EnemyProjectileSlowField : IBufferElementData
{
    public float2 Center;
    public float Radius;
    public float MinimumSpeedMultiplier;
    public float MaximumSpeedMultiplier;
    public float2 Forward;
    public float FrontFullEffectCos;
    public float FrontOuterCos;
    public float BackFullEffectCos;
    public float BackOuterCos;
    public float DistanceFalloff;
    public float ShipRadius;

    // Shared by Burst systems and legacy MonoBehaviour projectiles. Only the
    // displacement multiplier changes; authored velocity/direction stay intact.
    public float EvaluateMultiplier(float2 position, float projectileRadius = 0f)
    {
        if (Radius <= 0f)
            return 1f;

        float2 offset = position - Center;
        float distanceSq = math.lengthsq(offset);
        float combinedRadius = math.max(0f, ShipRadius) + math.max(0f, projectileRadius);
        float outerRadius = Radius + combinedRadius;
        if (distanceSq >= outerRadius * outerRadius)
            return 1f;

        float distance = math.sqrt(distanceSq);
        float2 direction = distance > 0f ? offset / distance : Forward;
        float edgeDistance = math.max(0f, distance - combinedRadius);
        float forwardDot = math.dot(Forward, direction);
        float front = AngularInfluence(forwardDot, FrontOuterCos, FrontFullEffectCos);
        float back = AngularInfluence(-forwardDot, BackOuterCos, BackFullEffectCos);
        if (front == 0f && back == 0f)
            return 1f;

        float distanceInfluence = math.pow(
            math.smoothstep(0f, 1f, 1f - edgeDistance / Radius),
            math.max(0.01f, DistanceFalloff));
        return 1f + distanceInfluence * (
            front * (MinimumSpeedMultiplier - 1f)
            + back * (MaximumSpeedMultiplier - 1f));
    }

    private static float AngularInfluence(float dot, float outerCos, float fullCos)
    {
        // SmoothStep in dot space avoids acos for every projectile. Both ends
        // have zero slope, including the neutral side region.
        float t = math.saturate((dot - outerCos) / math.max(0.000001f, fullCos - outerCos));
        return t * t * (3f - 2f * t);
    }

    public static float EvaluateMultiplier(
        float2 position,
        DynamicBuffer<EnemyProjectileSlowField> fields,
        float projectileRadius = 0f)
    {
        float slowest = 1f;
        float fastest = 1f;
        if (!fields.IsCreated)
            return 1f;

        for (int index = 0; index < fields.Length; index++)
        {
            float value = fields[index].EvaluateMultiplier(position, projectileRadius);
            slowest = math.min(slowest, value);
            fastest = math.max(fastest, value);
        }

        // Preserve strongest-slowdown stacking. Acceleration also uses the
        // strongest field, so duplicate registration cannot compound it.
        return slowest * fastest;
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
    public EnemyProjectileDestructionPolicy DestructionPolicy;
    public byte SystemCleanup;
}

/// <summary>
/// Written by ECS cleanup and consumed by the managed collision registry.
/// </summary>
public struct EnemyProjectileResolutionEvent : IBufferElementData
{
    public EnemyDamageType DamageType;
    public byte UseConfiguredDamage;
    public int TargetId;
    public EnemyProjectileResolutionKind Kind;
    public float Damage;
}

/// <summary>
/// Written by the burst-at-point ECS system and consumed by the collision
/// registry, which owns the managed <see cref="EnemyBullet"/> references.
/// </summary>
public struct EnemyChildAttackLineage : IComponentData, IEnableableComponent { }
public struct EnemyChildAttackLifetimeHold : IComponentData, IEnableableComponent { }

public struct EnemyProjectileBurstSpawnRequest : IBufferElementData
{
    public Entity SourceProjectile;
    public byte HasChildLineage;
    public EnemyProjectileCharacteristic Characteristic;
    public int BurstProjectileConfigId;
    public float2 Position;
    public float2 Direction;
}
