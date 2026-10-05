using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Immutable data compiled once from a <see cref="ProjectileData"/>.
/// Per-shot values such as damage, velocity and range live in separate components.
/// </summary>
public struct PlayerProjectileStaticData : IComponentData
{
    public float Radius;
    public float Lifetime;
    public EnemyDamageType DamageType;
    public ProjectileContactMode ContactMode;
    public float ContinuousDamageInterval;
    public int ExplosionConfigId;
    public byte ExplodesAtMaximumRange;
    public int DebuffSetId;
    public byte BypassesEnemyShield;
}

/// <summary>
/// One ECS contact-area attack. A Weapon creates this entity once per reload;
/// the contact system resolves every target in its configured polygon during the same tick.
/// </summary>
public struct PlayerContactAttackData : IComponentData
{
    public float2 Position;
    public float RotationRadians;
    public int OwnerId;
    public int DebuffSetId;
    public float Damage;
    public EnemyDamageType DamageType;
    public byte BypassesEnemyShield;
}

[InternalBufferCapacity(8)]
public struct PlayerContactPolygonVertex : IBufferElementData
{
    public float2 LocalPosition;
}

[InternalBufferCapacity(8)]
public struct PlayerContactHitTarget : IBufferElementData
{
    public int TargetId;
}

/// <summary>
/// One stationary ECS explosion created by an Entity projectile. Damage is
/// applied once to each target during its short active window.
/// </summary>
public struct PlayerProjectileExplosionData : IComponentData
{
    public float Radius;
    public float Damage;
    public int OwnerId;
    public EnemyDamageType DamageType;
    public byte BypassesEnemyShield;
}

public struct PlayerProjectileExplosionRemainingLifetime : IComponentData
{
    public float Value;
}

[InternalBufferCapacity(8)]
public struct PlayerProjectileExplosionHitTarget : IBufferElementData
{
    public int TargetId;
}

public struct PlayerProjectileVelocity : IComponentData
{
    public float2 Value;
}

public struct PlayerProjectilePreviousPosition : IComponentData
{
    public float2 Value;
}

public struct PlayerProjectileRemainingRange : IComponentData
{
    public float Value;
}

public struct PlayerProjectileRemainingLifetime : IComponentData
{
    public float Value;
}

public struct PlayerProjectileDamage : IComponentData
{
    public float Value;
}

public struct PlayerProjectileOwner : IComponentData
{
    public int Id;
}

/// <summary>
/// Runtime scale state. The renderer uses a non-uniform post-transform matrix,
/// while collision uses the largest root-scale axis.
/// </summary>
public struct PlayerProjectileScaleState : IComponentData
{
    public float2 RootScale;
    public float2 InitialRootScale;
    public float2 VisualScalePerRootUnit;
    public float2 GrowthPerSecond;
}

/// <summary>
/// Per-instance homing state. TargetId is an ECS-safe id resolved against the
/// collision registry's native target snapshot.
/// </summary>
public struct PlayerProjectileHoming : IComponentData
{
    public float RotationSpeedDegrees;
    public int TargetId;
}

public enum PlayerProjectileResolutionKind : byte
{
    None,
    HitEnemy,
    Expired
}

public struct PlayerProjectileResolution : IComponentData
{
    public PlayerProjectileResolutionKind Kind;
    public int TargetId;
    public PlayerProjectileCollisionTargetKind TargetKind;
}

public enum PlayerProjectileCollisionTargetKind : byte
{
    Enemy,
    DamageReceiver
}

/// <summary>
/// Singleton entity owned by <see cref="PlayerProjectileCollisionRegistry"/>.
/// </summary>
public struct PlayerProjectileCollisionRegistryTag : IComponentData
{
}

public struct PlayerProjectileCollisionGridSettings : IComponentData
{
    public float CellSize;
}

/// <summary>
/// Collider bounds snapshot. The managed registry resolves TargetId back to an Enemy.
/// </summary>
public struct PlayerProjectileCollisionTarget : IBufferElementData
{
    public int TargetId;
    public PlayerProjectileCollisionTargetKind Kind;
    public float2 Min;
    public float2 Max;
}

/// <summary>
/// Broad-phase cell -> collider snapshot mapping. Entries are sorted by cell so
/// projectile systems can find a cell with binary search.
/// </summary>
public struct PlayerProjectileCollisionGridEntry : IBufferElementData
{
    public int CellX;
    public int CellY;
    public int TargetIndex;
}

/// <summary>
/// Position snapshot for homing. This contains enemies only; damage receivers
/// such as the Resonance Sphere are intentionally never selected as targets.
/// </summary>
  public struct PlayerProjectileHomingTarget : IBufferElementData
  {
      public int TargetId;
      public float2 Position;
      public float RotationRadians;
      public float2 Scale;
      public int Layer;
  }

  /// <summary>
  /// Immutable Resonance Orb settings. Its stored damage and wave progress are
  /// intentionally per-instance so simultaneously active spheres stay independent.
  /// </summary>
  public struct PlayerProjectileResonanceSphereStaticData : IComponentData
  {
      public float MaximumStoredDamage;
      public float ExplosionRadius;
      public float WaveSpeed;
      public float SpriteDiameter;
      public float InitialAlpha;
        public float SlowdownStartY;
        public float DetonationY;
        public float SlowdownDuration;
        public float FullChargeDetonationDelay;
  }

  public struct PlayerProjectileResonanceSphereState : IComponentData
  {
      public float StoredDamage;
      public float CurrentWaveRadius;
        public float SlowdownElapsed;
        public float SlowdownStartX;
        public float FullChargeDetonationElapsed;
      public byte IsDetonating;
        public byte IsSlowingDown;
        public byte IsFullChargeDetonationPending;
  }

  [InternalBufferCapacity(16)]
  public struct PlayerProjectileResonanceSphereDamagedTarget : IBufferElementData
  {
      public int TargetId;
  }

  /// <summary>
  /// Runtime Ball Lightning values come from its current WeaponData level rather
  /// than a fixed ProjectileData contract.
  /// </summary>
  public struct PlayerProjectileBallLightningStaticData : IComponentData
  {
      public float AreaDamage;
      public float AreaRadius;
      public float AreaTickInterval;
      public int DamageLayers;
  }

  public struct PlayerProjectileBallLightningState : IComponentData
  {
      public double NextPulseTime;
  }

  [InternalBufferCapacity(8)]
  public struct PlayerProjectileBallLightningDirectContact : IBufferElementData
  {
      public int TargetId;
      public double LastContactTime;
      public double LastDamageTime;
  }

  /// <summary>
  /// Dynamic ECS-only damage receivers, currently Resonance Orbs. Keeping them
  /// separate avoids managed references in the ordinary Enemy registry.
  /// </summary>
  public struct PlayerProjectileEntityDamageReceiverTarget : IBufferElementData
  {
      public Entity Entity;
      public float2 Min;
      public float2 Max;
  }

  public struct PlayerProjectileEntityDamageReceiverGridEntry : IBufferElementData
  {
      public int CellX;
      public int CellY;
      public Entity Entity;
  }

  [InternalBufferCapacity(4)]
  public struct PlayerProjectileHitEntityDamageReceiver : IBufferElementData
  {
      public Entity Entity;
  }

  [InternalBufferCapacity(4)]
  public struct PlayerProjectileContinuousEntityDamageReceiver : IBufferElementData
  {
      public Entity Entity;
      public double NextDamageTime;
  }

  /// <summary>
  /// Immutable chain settings compiled once from <see cref="ProjectileCircularChainContract"/>.
  /// </summary>
  public struct PlayerProjectileCircularChainStaticData : IComponentData
  {
      public int HitsPerTarget;
      public float DamagePerHit;
      public float HitInterval;
      public int MaximumTargets;
      public float SearchConeAngle;
      public float SearchRange;
      public float RandomEscapeAngle;
  }

  /// <summary>
  /// Per-shot Circular state. AttachmentLocalPosition preserves the point where
  /// the projectile struck the target while the target moves or rotates.
  /// </summary>
  public struct PlayerProjectileCircularChainState : IComponentData
  {
      public int CurrentTargetId;
      public float2 AttachmentLocalPosition;
      public float2 TravelDirection;
      public float TravelSpeed;
      public int HitsAppliedToCurrentTarget;
      public double NextHitTime;
      public byte IsAttachedToTarget;
  }

  /// <summary>
  /// Targets already latched by Circular. They cannot be selected again in the
  /// same chain, matching the legacy projectile behaviour without managed lists.
  /// </summary>
  [InternalBufferCapacity(4)]
public struct PlayerProjectileCircularChainVisitedTarget : IBufferElementData
{
    public int TargetId;
}

/// <summary>
/// Immutable Arc Node settings compiled from <see cref="ProjectileArcNodesContract"/>.
/// NetworkId keeps unrelated node ProjectileData assets from connecting.
/// </summary>
public struct PlayerProjectileArcNodesStaticData : IComponentData
{
    public int NetworkId;
    public float DamagePerArc;
    public float ConnectionRange;
    public float PulseInterval;
    public int MaximumConnections;
    public float HitRadius;
    public float VisualDuration;
    public float VisualWidth;
    public int VisualSegments;
    public float VisualJitter;
    public float4 VisualColor;
}

public struct PlayerProjectileArcNodesState : IComponentData
{
    public double NextPulseTime;
}

/// <summary>
/// ECS-to-managed visual request. The line itself stays managed; Entities only
/// calculates the gameplay segment and damage.
/// </summary>
public struct PlayerProjectileElectricArcVisualRequest : IBufferElementData
{
    public float2 Start;
    public float2 End;
    public float Duration;
    public float Width;
    public int Segments;
    public float Jitter;
    public float4 Color;
}

/// <summary>
/// Compact ECS-to-managed damage request. Managed references and effects stay out of Entities.
/// </summary>
public struct PlayerProjectileResolutionEvent : IBufferElementData
{
    public int TargetId;
    public PlayerProjectileCollisionTargetKind TargetKind;
    public int OwnerId;
    public int DebuffSetId;
    public float Damage;
    public EnemyDamageType DamageType;
    public byte BypassesEnemyShield;
    public float2 ImpactPosition;
}

/// <summary>
/// ECS-to-managed request to create an ECS explosion. The registry resolves its
/// compact config id and never places a prefab reference on an Entity.
/// </summary>
public struct PlayerProjectileExplosionRequest : IBufferElementData
{
    public int ExplosionConfigId;
    public int OwnerId;
    public float2 Position;
}

/// <summary>
/// Unmanaged configuration carried by a projectile which creates a second
/// physical projectile. The ScriptableObject itself stays in the managed
/// registry and is addressed only by SecondaryProjectileConfigId.
/// </summary>
public struct PlayerProjectileSecondarySpawnData : IComponentData
{
    public int SecondaryProjectileConfigId;
    public SecondaryProjectileSpawnTrigger SpawnTrigger;
    public float TravelDistance;
    public float TraveledDistance;
    public float Damage;
    public float Range;
    public float Speed;
    public byte IgnoreTriggeringEnemy;
}

/// <summary>
/// ECS-to-managed request to create a configured secondary projectile. This
/// keeps ScriptableObject and visual references outside an Entity.
/// </summary>
public struct PlayerProjectileSecondarySpawnRequest : IBufferElementData
{
    public int SecondaryProjectileConfigId;
    public int OwnerId;
    public int IgnoredTargetId;
    public float2 Position;
    public float2 Direction;
    public float Damage;
    public float Range;
    public float Speed;
}

/// <summary>
/// Targets that must be skipped by an Entity projectile. Used by secondary
/// projectiles which must not immediately collide with their triggering enemy.
/// </summary>
[InternalBufferCapacity(1)]
public struct PlayerProjectileIgnoredCollisionTarget : IBufferElementData
{
    public int TargetId;
}

/// <summary>
/// Per-projectile memory of targets hit by Pierce Once. Inline storage covers
/// ordinary shots without allocating a managed collection.
/// </summary>
[InternalBufferCapacity(8)]
public struct PlayerProjectileHitTarget : IBufferElementData
{
    public int TargetId;
}

/// <summary>
/// Per-target cooldown used by Pierce Continuous projectiles.
/// </summary>
[InternalBufferCapacity(8)]
public struct PlayerProjectileContinuousHitTarget : IBufferElementData
{
    public int TargetId;
    public double NextDamageTime;
}
