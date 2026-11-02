using UnityEngine;

public enum EnemyShootingType : byte
{
    Default = 0,
    Rotation = 1
}

public enum EnemyProjectileInitialSpeedMode : byte
{
    Default = 0,
    Random = 1,
    Consistent = 2
}

public enum EnemyProjectileFanMode : byte
{
    Default = 0,
    Fan = 1
}

public enum EnemyProjectileShotGunMode : byte
{
    Default = 0,
    ShotGun = 1
}

public enum EnemyProjectileSizeBehavior : byte
{
    Default = 0,
    Customized = 1
}

public enum EnemyProjectileMovementPattern : byte
{
    Default = 0,
    AngularTurn = 1,
    Lateral = 2,
    Orbit = 3,
    TargetPoint = 4
}

public enum EnemyProjectileOrbitCenterMode : byte
{
    SourcePosition = 0,
    FixedWorldPoint = 1,
    [InspectorName("DNA-Like")] DnaLike = 2
}

public enum EnemyProjectileSpeedCurveTarget : byte
{
    Both = 0,
    BaseSpeed = 1,
    Radial = 2
}

[System.Serializable]
public sealed partial class EnemyShootingSettings : ISerializationCallbackReceiver
{
    private const int CurrentProjectilePatternVersion = 2;
#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
    public void SetPerformanceChild(EnemyChildAttackSettings settings)
    {
        useChildAttackPattern = settings != null;
        childAttack = settings;
    }
#endif
    private const int LegacyShotGunFireModeValue = 1;

    [System.NonSerialized] private bool hasRuntimeProjectileBaseSpeedOverride;
    [System.NonSerialized] private float runtimeProjectileBaseSpeedOverride;

    [SerializeField, HideInInspector] private EnemyShootingType shootingType;
    [SerializeField] private EnemyEmissionOriginSettings emissionOrigin = new EnemyEmissionOriginSettings();
    [SerializeField] private EnemyEmissionLayoutSettings emissionLayout = new EnemyEmissionLayoutSettings();
    [SerializeField] private bool layoutRotationEnabled;
    [SerializeField, HideInInspector] private int emissionLayoutVersion;
    [SerializeField] private EnemyProjectileType projectileType;
    [SerializeField] private EnemyLaserSettings laser = new();
    public EnemyLaserSettings Laser => laser ??= new();
    [SerializeField] private EnemyCompositeProjectileSettings composite = new();
    public EnemyCompositeProjectileSettings Composite => composite ??= new();
    public bool IsComposite => projectileType >= EnemyProjectileType.CompositeCircle && projectileType <= EnemyProjectileType.CompositeSegmentedRing;
    public int ProjectilePrefabIndex => projectileType == EnemyProjectileType.Homing ? 1 : projectileType == EnemyProjectileType.BurstAtPoint ? 2 : 0;
    [SerializeField] private bool overrideProjectileDamage;
    [SerializeField, Min(0f), Tooltip("Exact contact damage; disabled preserves the existing projectile damage rules.")]
    private float projectileDamage = 1f;
    [SerializeField] private bool overwriteProjectileCharacteristic;
    [SerializeField] private EnemyProjectileCharacteristic projectileCharacteristic = EnemyProjectileCharacteristic.Fragile;
    [SerializeField, Tooltip("Replaces the selected projectile prefab's visual colour for this attack profile.")]
    private bool overwriteBaseColour;
    [SerializeField] private Color projectileColour = Color.white;
    [SerializeField, Tooltip("Uses this value instead of the Base Speed on the selected projectile prefab.")]
    private bool overrideProjectileBaseSpeed;
    [SerializeField, Randomizable(nameof(projectileBaseSpeedRandom), .01f), Tooltip("Used only when Override Projectile Base Speed is enabled.")]
    private float projectileBaseSpeed = 1f;
    [SerializeField, HideInInspector] private RandomFloatRange projectileBaseSpeedRandom = new();
    [SerializeField, HideInInspector] private RandomFloatRange shotGunInitialSpeedRandom = new();
    [SerializeField] private EnemySpeedRandomizeOn speedRandomizeOn = EnemySpeedRandomizeOn.PerProjectile;
    [SerializeField, Tooltip("Enables seeded sampling for existing Initial Speed Mode = Random. Disabled preserves legacy per-projectile/DNA behaviour.")]
    private bool seededInitialSpeedRandom;
    [SerializeField] private int numericRandomSeed = 12345;
    public int NumericRandomSeed => numericRandomSeed;
    [System.NonSerialized] private EnemySpeedRandomizeOn runtimeSpeedRandomizeOn;
    public EnemySpeedRandomizeOn SpeedRandomizeOn => hasRuntimeProjectileBaseSpeedOverride && runtimeProjectileBaseSpeedRandom != null
        && runtimeProjectileBaseSpeedRandom.enabled && InitialSpeedMode != EnemyProjectileInitialSpeedMode.Random
            ? runtimeSpeedRandomizeOn : speedRandomizeOn;
    public RandomFloatRange ProjectileBaseSpeedRandom => projectileBaseSpeedRandom ??= new();
    public bool UsesNumericRandomization => EmissionOrigin.X.OffsetRandom.enabled || EmissionOrigin.Y.OffsetRandom.enabled
        || TryGetRandomSpeedRange(out _, out _) || useChildAttackPattern || UsesExtendedNumericRandomization;
    [System.NonSerialized] private RandomFloatRange runtimeProjectileBaseSpeedRandom;
    public bool TryGetRandomSpeedRange(out float min, out float max)
    {
        if (InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random)
        { min = RandomInitialSpeedMinimum; max = RandomInitialSpeedMaximum; return seededInitialSpeedRandom; }
        var range = hasRuntimeProjectileBaseSpeedOverride ? runtimeProjectileBaseSpeedRandom
            : overrideProjectileBaseSpeed ? ProjectileBaseSpeedRandom : UsesShotGun && UsesLegacyStackSpeed ? shotGunInitialSpeedRandom : null;
        min = max = 0f;
        range?.GetBounds(out min, out max, .01f);
        return range != null && range.enabled;
    }
    [SerializeField, Tooltip("Uses this value instead of the Base Lifetime on the selected projectile prefab.")]
    private bool overrideProjectileLifetime;
    [SerializeField, Tooltip("Used only when Override Projectile Lifetime is enabled.")]
    [Randomizable(nameof(projectileLifetimeRandom), .01f, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileLifetime = 1f;
    [SerializeField] private EnemyProjectileScreenSettings screenBoundary;
    public EnemyProjectileScreenSettings ScreenBoundary => screenBoundary;
    public void SetScreenBoundary(EnemyProjectileScreenSettings settings) => screenBoundary = settings;
    [SerializeField] private bool useChildAttackPattern;
    [SerializeReference] private EnemyChildAttackSettings childAttack;

    [Header("Initial Projectile Speed")]
    [SerializeField] private EnemyProjectileInitialSpeedMode initialSpeedMode;
    [SerializeField, Min(0.01f), Tooltip("Used only in Random mode. Every projectile receives a speed in this range.")]
    private float randomInitialSpeedMinimum = 1f;
    [SerializeField, Min(0.01f), Tooltip("Used only in Random mode. Every projectile receives a speed in this range.")]
    private float randomInitialSpeedMaximum = 2f;
    [SerializeField, Tooltip("Used only in Consistent mode. Applied once after the entire volley; negative values slow later volleys.")]
    [Randomizable(nameof(consistentInitialSpeedChangePerVolleyRandom), float.MinValue, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private float consistentInitialSpeedChangePerVolley = -0.1f;
    [SerializeField] private EnemyProjectileFireMode shootingMode;
    [SerializeField] private EnemyProjectileFanMode fanMode;
    [SerializeField, HideInInspector] private int projectilePatternVersion;
    [SerializeField, HideInInspector, Min(1)] private int directionCount = 4;
    [SerializeField] [Randomizable(nameof(fanProjectileCountRandom), 1f, EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private int fanProjectileCount = 3;
    [SerializeField] [Randomizable(nameof(fanMinAngleRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float fanMinAngle = -45f;
    [SerializeField] [Randomizable(nameof(fanMaxAngleRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float fanMaxAngle = 45f;
    [SerializeField] [Randomizable(nameof(randomMinAngleRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float randomMinAngle = -15f;
    [SerializeField] [Randomizable(nameof(randomMaxAngleRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float randomMaxAngle = 15f;
    [SerializeField] private EnemyProjectileShotGunMode shotGunMode;
    [SerializeField] [Randomizable(nameof(shotGunProjectileCountRandom), 1f, EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private int shotGunProjectileCount = 3;
    [SerializeField] [Randomizable(nameof(shotGunDirectionXRandom), nameof(shotGunDirectionYRandom), EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private Vector2 shotGunDirection = Vector2.down;
    [SerializeField, Randomizable(nameof(shotGunInitialSpeedRandom), .01f)] private float shotGunInitialSpeed = 5f;
    [SerializeField] private EnemyProjectileSpeedBehavior projectileSpeedBehavior;
    [SerializeField] [Randomizable(nameof(projectileSpeedChangeYRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileSpeedChangeY;
    [SerializeField] private AnimationCurve projectileSpeedByDistance = AnimationCurve.Linear(0f, 1f, 10f, 1f);
    [SerializeField] private EnemyProjectileSpeedCurveTarget projectileSpeedCurveTarget = EnemyProjectileSpeedCurveTarget.Both;
    [Header("Projectile Movement")]
    [SerializeField] private EnemyProjectileMovementPattern projectileMovementPattern;
    [SerializeField, Tooltip("X: remaining distance / initial distance (1 = launch, 0 = target). Y: base-speed multiplier. Values below 0.01 use a 1% approach floor to guarantee arrival. TargetPoint takes priority over prefab Homing/Burst and screen bounce.")]
    private AnimationCurve targetPointSpeedByRemainingDistance = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Use the first selected screen-edge crossing as TargetPosition. TargetPoint movement uses its captured player destination instead.")]
    private bool targetScreenBoundary;
    [SerializeField] private EnemyChildAttackBoundaries targetBoundaries = EnemyChildAttackBoundaries.All;
    [SerializeField, Tooltip("Positive points inward from the gameplay viewport; negative points outward.")]
    private float targetBoundaryOffset;
    [SerializeField, Tooltip("At TargetPosition, hide the projectile and disable all combat interactions. Keep it only while Child Attack generation is pending. Fixed Count finishes automatically; an infinite repeat remains active until system cleanup.")]
    private bool hideParentOnTargetReached;
    public EnemyProjectileTargetPositionSettings TargetPositionSettings => new(targetScreenBoundary,targetBoundaries,targetBoundaryOffset,hideParentOnTargetReached);
    [SerializeField, Tooltip("Degrees per second. Positive turns clockwise; negative turns counterclockwise.")]
    [Randomizable(nameof(projectileAngularSpeedRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileAngularSpeed = 90f;
    [SerializeField, Tooltip("Constant local-right velocity in units per second. Negative values move to the opposite side.")]
    [Randomizable(nameof(projectileLateralSpeedRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileLateralSpeed = 1f;
    [SerializeField] private EnemyProjectileOrbitCenterMode projectileOrbitCenterMode;
    [SerializeField, Tooltip("World-space center used only when Orbit Center is Fixed World Point.")]
    [Randomizable(nameof(projectileOrbitFixedWorldCenterXRandom), nameof(projectileOrbitFixedWorldCenterYRandom), EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private Vector2 projectileOrbitFixedWorldCenter;
    [SerializeField, Tooltip("Orbit radius. Zero keeps the projectile's spawn distance from the selected center.")]
    [Randomizable(nameof(projectileOrbitRadiusRandom), 0f, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileOrbitRadius;
    [SerializeField, Tooltip("Radius change in units per second. Positive expands the orbit; negative contracts it.")]
    [Randomizable(nameof(projectileOrbitRadialSpeedRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileOrbitRadialSpeed;
    [SerializeField, Tooltip("Multiplier of Angular Velocity for the shared DNA weaving rhythm. All projectiles of a fan cross together.")]
    [Randomizable(nameof(projectileDnaWeaveSpeedRandom), .01f, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileDnaWeaveSpeed = 1f;
    [SerializeField] private bool overrideBaseSize;
    [SerializeField, Min(0f), Tooltip("Uniform scale relative to the authored projectile. 1 preserves its dimensions; Size Curve multiplies this base.")]
    private float baseSize = 1f;
    public bool OverrideBaseSize => overrideBaseSize;
    public float BaseSize => float.IsFinite(baseSize) ? Mathf.Max(0f, baseSize) : 1f;
    [SerializeField] private EnemyProjectileCollisionSettings collisionSettings;
    public EnemyProjectileCollisionSettings CollisionSettings => collisionSettings;

    [Header("Projectile Spawn Offset")]
    [SerializeField] private bool enableSpawnOffset;
    [SerializeField, Randomizable(nameof(baseOffsetXRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float baseOffsetX;
    [SerializeField, Randomizable(nameof(baseOffsetYRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float baseOffsetY;
    [SerializeField, Randomizable(nameof(stepOffsetXRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float stepOffsetX;
    [SerializeField, Randomizable(nameof(stepOffsetYRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerShot)]
    private float stepOffsetY;
    public bool EnableSpawnOffset => enableSpawnOffset;

    [Header("Projectile Size Change")]
    [SerializeField] private EnemyProjectileSizeBehavior projectileSizeBehavior;
    [SerializeField] [Randomizable(nameof(projectileSizeChangeYRandom), float.MinValue, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileSizeChangeY;
    [SerializeField, Tooltip("Normalized size-change progress per second after the projectile reaches Size Change Y.")]
    [Randomizable(nameof(projectileSizeChangeSpeedRandom), .01f, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileSizeChangeSpeed = 1f;
    [SerializeField, Tooltip("Maps normalized size-change progress from 0 to 1 to the size interpolation.")]
    private AnimationCurve projectileSizeChangeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Final multiplier of the selected projectile prefab's authored visual and collision size.")]
    [Randomizable(nameof(projectileFinalSizeRandom), 0f, EnemyNumericScopes.All, EnemyNumericRandomizeOn.PerProjectile)]
    private float projectileFinalSize = 1f;
    [SerializeField, HideInInspector] private bool rotate = true;
    [SerializeField] private EnemyRotationSettings rotation = new EnemyRotationSettings();

    // Retained for callers of the old API; authored data now lives in Emission Layout.
    public EnemyShootingType ShootingType => EmissionLayout.Type == EnemyEmissionLayoutType.Radial
        ? EnemyShootingType.Rotation : EnemyShootingType.Default;
    internal bool HadLegacyRotation => shootingType == EnemyShootingType.Rotation && rotate;
    public EnemyEmissionOriginSettings EmissionOrigin => emissionOrigin ??= new EnemyEmissionOriginSettings();
    public EnemyEmissionLayoutSettings EmissionLayout
    {
        get { MigrateEmissionLayout(); return emissionLayout; }
    }
    public EnemyChildAttackSettings ChildAttack => useChildAttackPattern ? childAttack ??= EnemyChildAttackSettings.CreateDefault() : null;
    public void SetChildAttack(EnemyChildAttackSettings settings) { childAttack = settings; useChildAttackPattern = settings != null; }
    public EnemyProjectileType ProjectileType { get { MigrateCompositeType(); return projectileType; } }
    public bool OverridesProjectileDamage => overrideProjectileDamage;
    public float ProjectileDamage => float.IsFinite(projectileDamage) ? Mathf.Max(0f, projectileDamage) : 0f;
    public bool OverwritesProjectileCharacteristic => overwriteProjectileCharacteristic;
    public EnemyProjectileCharacteristic ProjectileCharacteristic => projectileCharacteristic;
    public bool UsesFragileCharacteristic => overwriteProjectileCharacteristic
        && projectileCharacteristic == EnemyProjectileCharacteristic.Fragile;
    public bool OverwritesBaseColour => overwriteBaseColour;
    public Color ProjectileColour => projectileColour;
    public bool OverridesProjectileBaseSpeed => overrideProjectileBaseSpeed;
    public bool OverridesProjectileLifetime => overrideProjectileLifetime;
    public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);
    public EnemyProjectileInitialSpeedMode InitialSpeedMode => initialSpeedMode;
    public EnemyProjectileFireMode ShootingMode => shootingMode;
    public EnemyProjectileFanMode FanMode => fanMode;
    public EnemyProjectileShotGunMode ShotGunMode => shotGunMode;
    public bool UsesFan => fanMode == EnemyProjectileFanMode.Fan;
    public bool UsesShotGun => shotGunMode == EnemyProjectileShotGunMode.ShotGun;
    public int DirectionCount => EmissionLayout.EmissionCount;
    public int FanProjectileCount => Mathf.Max(1, fanProjectileCount);
    public float FanMinAngle => Mathf.Min(fanMinAngle, fanMaxAngle);
    public float FanMaxAngle => Mathf.Max(fanMinAngle, fanMaxAngle);
    public int ProjectilesPerDirection => (int)System.Math.Min(int.MaxValue,
        (long)(UsesShotGun ? Mathf.Max(1, shotGunProjectileCount) : 1) * (UsesFan ? FanProjectileCount : 1));
    public int ProjectilesPerVolley => (int)System.Math.Min(int.MaxValue, (long)DirectionCount * ProjectilesPerDirection);
    public Vector2 ShotGunDirection => shotGunDirection;
    public float InitialSpeed => hasRuntimeProjectileBaseSpeedOverride
        ? runtimeProjectileBaseSpeedOverride
        : overrideProjectileBaseSpeed ? projectileBaseSpeed
        : UsesShotGun && UsesLegacyStackSpeed ? shotGunInitialSpeed : 0f;
    public float RandomInitialSpeedMinimum => Mathf.Min(
        randomInitialSpeedMinimum,
        randomInitialSpeedMaximum);
    public float RandomInitialSpeedMaximum => Mathf.Max(
        randomInitialSpeedMinimum,
        randomInitialSpeedMaximum);
    public float ConsistentInitialSpeedChangePerVolley =>
        consistentInitialSpeedChangePerVolley;
    public float RandomMinAngle => Mathf.Min(randomMinAngle, randomMaxAngle);
    public float RandomMaxAngle => Mathf.Max(randomMinAngle, randomMaxAngle);
    public float RandomPreviewAngle => (RandomMinAngle + RandomMaxAngle) * 0.5f;
    public bool UsesTargetPoint => projectileMovementPattern == EnemyProjectileMovementPattern.TargetPoint;
    public AnimationCurve TargetPointSpeedByRemainingDistance => targetPointSpeedByRemainingDistance;
    public EnemyProjectileSpeedBehavior SpeedBehavior => UsesTargetPoint ? EnemyProjectileSpeedBehavior.Customized : projectileSpeedBehavior;
    public float SpeedChangeY => projectileSpeedChangeY;
    public AnimationCurve SpeedByDistance => UsesTargetPoint ? targetPointSpeedByRemainingDistance : projectileSpeedByDistance;
    public EnemyProjectileSpeedCurveTarget SpeedCurveTarget => projectileSpeedCurveTarget;
    public EnemyProjectileMovementPattern MovementPattern => projectileMovementPattern;
    public float AngularSpeed => projectileAngularSpeed;
    public float LateralSpeed => projectileLateralSpeed;
    public EnemyProjectileOrbitCenterMode OrbitCenterMode => projectileOrbitCenterMode;
    public Vector2 OrbitFixedWorldCenter => projectileOrbitFixedWorldCenter;
    public float OrbitRadius => Mathf.Max(0f, projectileOrbitRadius);
    public float OrbitRadialSpeed => projectileOrbitRadialSpeed;
    public float DnaWeaveSpeed => Mathf.Max(0.01f, projectileDnaWeaveSpeed);
    public bool UsesDnaOrbit => projectileMovementPattern == EnemyProjectileMovementPattern.Orbit
        && projectileOrbitCenterMode == EnemyProjectileOrbitCenterMode.DnaLike;

    public float GetDnaHalfCycleDuration()
    {
        float speed = Mathf.Abs(projectileAngularSpeed) * DnaWeaveSpeed;
        if (speed < 0.0001f) return 0f;
        float halfAngle = UsesFan && FanProjectileCount > 1
            ? Mathf.Min(EnemyProjectileDnaMotion.MaximumHalfAngleDegrees, Mathf.Abs(fanMaxAngle - fanMinAngle) * 0.5f) : 45f;
        return 2f * Mathf.Max(0.01f, halfAngle) / speed;
    }

    public Vector3 GetDnaMedianDirection(Vector3 baseDirection, int directionIndex, float spreadAngle, EnemyNumericSample numeric = default)
    {
        float medianAngle = UsesFan && FanProjectileCount > 1 ? (NumericFanStart(numeric) + NumericFanEnd(numeric)) * 0.5f : 0f;
        return Quaternion.Euler(0f, 0f, directionIndex * (360f / DirectionCount) + spreadAngle + medianAngle)
            * baseDirection.normalized;
    }
    public EnemyProjectileSizeBehavior SizeBehavior => projectileSizeBehavior;
    public float SizeChangeY => projectileSizeChangeY;
    public float SizeChangeSpeed => projectileSizeChangeSpeed;
    public AnimationCurve SizeChangeCurve => projectileSizeChangeCurve;
    public float FinalSize => projectileFinalSize;
    public bool Rotates => emissionLayoutVersion < 1 ? HadLegacyRotation : layoutRotationEnabled;
    public EnemyRotationSettings Rotation => rotation;

    public Quaternion EvaluateDirectionBasis(Quaternion basis, float elapsed, EnemyRotationSettings rotationOverride = null)
    {
        return Rotates ? basis * Quaternion.Euler(0f, 0f, (rotationOverride ?? rotation).EvaluateAngle(elapsed)) : basis;
    }

    public float EvaluateLayoutAngle(float elapsed, EnemyRotationSettings rotationOverride = null)
        => Rotates ? (rotationOverride ?? rotation).EvaluateAngle(elapsed) : 0f;

    public Vector3 GetEmissionPosition(int emitterIndex, Vector3 shootingPoint, Vector3 enemyPosition,
        Quaternion firingBasis, Quaternion shootingPointBasis, Quaternion enemyBasis, Vector3 scale,
        float elapsed, EnemyRotationSettings rotationOverride = null)
    {
        Quaternion turn = Quaternion.Euler(0f, 0f, EvaluateLayoutAngle(elapsed, rotationOverride));
        return EmissionLayout.GetPosition(emitterIndex, shootingPoint, enemyPosition,
            firingBasis * turn, shootingPointBasis * turn, enemyBasis, scale);
    }

    public Vector3 GetEmissionPositionFromOrigin(int emitterIndex, Vector3 shootingPoint, Vector3 enemyPosition,
        Vector3 origin, Quaternion firingBasis, Quaternion shootingPointBasis, Quaternion enemyBasis, Vector3 scale,
        float elapsed, EnemyRotationSettings rotationOverride = null)
    {
        return GetEmissionPosition(emitterIndex, shootingPoint + origin - enemyPosition, origin,
            firingBasis, shootingPointBasis, enemyBasis, scale, elapsed, rotationOverride);
    }

    public Vector3 GetEmissionDirection(int emitterIndex, Quaternion firingBasis, float elapsed,
        Vector3 requestedDirection, bool aimed, EnemyRotationSettings rotationOverride = null, bool stationary = false)
    {
        Quaternion turn = Quaternion.Euler(0f, 0f, EvaluateLayoutAngle(elapsed, rotationOverride));
        Quaternion basis = firingBasis * turn;
        if (stationary)
        {
            requestedDirection.z = 0f;
            Vector3 fixedDirection = requestedDirection.sqrMagnitude > .0001f ? requestedDirection.normalized : Vector3.up;
            if (EmissionLayout.Type == EnemyEmissionLayoutType.Circle)
                return aimed ? fixedDirection : turn * fixedDirection;
            if (EmissionLayout.Type == EnemyEmissionLayoutType.Point && aimed)
                return fixedDirection;
            return turn * Quaternion.Euler(0f, 0f, EmissionLayout.GetAngle(emitterIndex)) * fixedDirection;
        }
        if (EmissionLayout.Type == EnemyEmissionLayoutType.Circle && aimed)
            return requestedDirection.sqrMagnitude > 0.0001f ? requestedDirection.normalized : basis * Vector3.up;
        if (EmissionLayout.Type == EnemyEmissionLayoutType.Sector && aimed)
        {
            Vector3 aim = requestedDirection.sqrMagnitude > .0001f ? requestedDirection.normalized : firingBasis * Vector3.up;
            // Aim the middle of the sector, including sweeps crossing zero, at the target.
            float centerAngle = EmissionLayout.StartAngle + EmissionLayout.SectorSweep * .5f;
            float offset = EmissionLayout.GetAngle(emitterIndex) - centerAngle;
            return turn * Quaternion.Euler(0f, 0f, offset) * aim;
        }
        Vector3 requested = aimed ? requestedDirection : turn * requestedDirection;
        Vector3 direction = EvaluateShotDirection(basis, requested, aimed);
        return Quaternion.Euler(0f, 0f, EmissionLayout.GetAngle(emitterIndex)) * direction;
    }

    public void SetLayoutRotationEnabled(bool enabled)
    {
        MigrateEmissionLayout();
        layoutRotationEnabled = enabled;
    }

    public void ScaleAttackTiming(float multiplier)
    {
        emissionLayout.ScaleAttackTiming(multiplier);
        rotation.ScaleAttackTiming(multiplier);
    }

    public Vector3 EvaluateShotDirection(
        Quaternion basis,
        Vector3 requestedDirection,
        bool useAimedShotGunDirection = false)
    {
        Vector3 direction = EmissionLayout.Type != EnemyEmissionLayoutType.Point ? basis * Vector3.up : requestedDirection;
        if (UsesShotGun && !useAimedShotGunDirection)
            direction = basis * (Vector3)shotGunDirection;
        return direction.sqrMagnitude < 0.0001f ? basis * Vector3.up : direction.normalized;
    }

    // The center of arm zero is the visible aim for radial attacks and fans.
    public Vector3 GetFacingDirection(Vector3 shotDirection)
    {
        float center = UsesFan && FanProjectileCount > 1
            ? (fanMinAngle + fanMaxAngle) * 0.5f : 0f;
        return Quaternion.Euler(0f, 0f, center) * shotDirection;
    }

    public Vector3 GetProjectileDirection(Vector3 baseDirection, int directionIndex, int projectileIndex,
        float randomSpreadAngle = 0f, EnemyNumericSample numeric = default)
    {
        baseDirection = baseDirection.sqrMagnitude < 0.0001f ? Vector3.up : baseDirection.normalized;
        float angle = directionIndex * (360f / DirectionCount) + randomSpreadAngle;
        if (UsesFan && FanProjectileCount > 1)
        {
            float start = NumericFanStart(numeric), end = NumericFanEnd(numeric);
            angle += Mathf.Lerp(Mathf.Max(start, end), Mathf.Min(start, end),
                (projectileIndex % FanProjectileCount) / (float)(FanProjectileCount - 1));
        }
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }

    // Circle owns one Fan/Stack group per emitter. Radial/Sector use direction-major
    // order across the full volley, independent of delayed/reversed launch order.
    public float GetSpawnOffsetGroupCenter(int directionIndex, EnemyNumericSample numeric = default)
    {
        int perDirection = ProjectilesPerDirection;
        if (EmissionLayout.Type != EnemyEmissionLayoutType.Radial && EmissionLayout.Type != EnemyEmissionLayoutType.Sector)
            return -(perDirection - 1) * .5f;
        if (!numeric.Active || !(UsesFan && EnemyNumericSample.PerDirection(fanProjectileCountRandom)
            || UsesShotGun && EnemyNumericSample.PerDirection(shotGunProjectileCountRandom)))
            return (float)((double)directionIndex * perDirection - ((double)DirectionCount * perDirection - 1) * .5);
        double prefix = 0, total = 0;
        for (int arm = 0; arm < DirectionCount; arm++)
        {
            var sample = numeric.ForDirection(arm);
            int fan = UsesFan ? sample.Int(fanProjectileCountRandom, FanProjectileCount, 101u, 1) : 1;
            int stack = UsesShotGun ? sample.Int(shotGunProjectileCountRandom, shotGunProjectileCount, 104u, 1) : 1;
            double count = System.Math.Min(int.MaxValue, (long)fan * stack);
            if (arm < directionIndex) prefix += count;
            total += count;
        }
        return (float)(prefix - (total - 1) * .5);
    }

    public Vector3 GetProjectileSpawnPosition(Vector3 position, Vector3 finalDirection,
        int directionIndex, int projectileIndex, EnemyNumericSample numeric = default, float? groupCenter = null)
    {
        if (!enableSpawnOffset) return position;
        float centered = projectileIndex + (groupCenter ?? GetSpawnOffsetGroupCenter(directionIndex, numeric));
        float x = numeric.Float(baseOffsetXRandom, baseOffsetX, 1801u)
            + centered * numeric.Float(stepOffsetXRandom, stepOffsetX, 1803u);
        float y = numeric.Float(baseOffsetYRandom, baseOffsetY, 1802u)
            + centered * numeric.Float(stepOffsetYRandom, stepOffsetY, 1804u);
        finalDirection.z = 0f;
        Vector3 forward = finalDirection.sqrMagnitude > .0001f ? finalDirection.normalized : Vector3.up;
        Vector3 right = new Vector3(forward.y, -forward.x, 0f);
        return position + right * x + forward * y;
    }

    public void ConfigureLegacy(EnemyProjectileType type, int legacyShootingMode,
        int fanCount, float minAngle, float maxAngle, int shotgunCount, Vector2 shotgunDirection,
        float shotgunSpeed, EnemyProjectileSpeedBehavior speedBehavior, float speedChangeY, AnimationCurve speedCurve)
    {
        projectileType = type;
        ApplyLegacyProjectilePattern(legacyShootingMode);
        fanProjectileCount = fanCount;
        fanMinAngle = minAngle;
        fanMaxAngle = maxAngle;
        shotGunProjectileCount = shotgunCount;
        shotGunDirection = shotgunDirection;
        shotGunInitialSpeed = shotgunSpeed;
        projectileSpeedBehavior = speedBehavior;
        projectileSpeedChangeY = speedChangeY;
        projectileSpeedByDistance = CloneCurve(speedCurve);
        Validate();
    }

    public void ConfigureLegacyFan(int count, float minAngle, float maxAngle)
    {
        shootingMode = EnemyProjectileFireMode.Default;
        fanMode = EnemyProjectileFanMode.Fan;
        shotGunMode = EnemyProjectileShotGunMode.Default;
        projectilePatternVersion = CurrentProjectilePatternVersion;
        fanProjectileCount = count;
        fanMinAngle = minAngle;
        fanMaxAngle = maxAngle;
    }

    public void ConfigureRotation(EnemyRotationSettings settings, bool enabled)
    {
        shootingType = EnemyShootingType.Rotation;
        directionCount = 4;
        rotate = enabled;
        emissionLayout.Configure(EnemyEmissionLayoutType.Radial, 4);
        layoutRotationEnabled = enabled;
        emissionLayoutVersion = 1;
        if (settings != null)
            rotation.CopyFrom(settings);
    }

    public void SetProjectileType(EnemyProjectileType type) { projectileType = type; MigrateCompositeType(); }

    // Pure managed migration: safe during Unity deserialization, including nested child patterns.
    // No AssetDatabase writes or blanket asset resaves are needed.
    public bool MigrateCompositeType()
    {
        if (projectileType != EnemyProjectileType.CompositePolygon && projectileType != EnemyProjectileType.CompositeSegmentedRing) return false;
        Composite.SetShape(projectileType == EnemyProjectileType.CompositePolygon ? EnemyCompositeShape.RegularPolygon : EnemyCompositeShape.SegmentedRing);
        projectileType = EnemyProjectileType.Composite;
        return true;
    }
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() => MigrateCompositeType();

    public void SetProjectileBaseSpeedOverride(bool enabled, float speed)
    {
        overrideProjectileBaseSpeed = enabled;
        projectileBaseSpeed = Mathf.Max(0.01f, speed);
    }
    public void SetProjectileBaseSpeedRange(RandomFloatRange range, EnemySpeedRandomizeOn mode)
    { ProjectileBaseSpeedRandom.CopyFrom(range); speedRandomizeOn = mode; }

    // This is deliberately runtime-only: wave overrides must not alter projectile prefabs
    // or the attack profile stored on an enemy prefab.
    public void SetRuntimeProjectileBaseSpeedOverride(float speed) => SetRuntimeRandomProjectileBaseSpeedOverride(speed, null, EnemySpeedRandomizeOn.PerProjectile);
    public void SetRuntimeRandomProjectileBaseSpeedOverride(float speed, RandomFloatRange random, EnemySpeedRandomizeOn mode)
    {
        hasRuntimeProjectileBaseSpeedOverride = true;
        runtimeProjectileBaseSpeedOverride = Mathf.Max(0.01f, speed);
        runtimeProjectileBaseSpeedRandom = random; runtimeSpeedRandomizeOn = mode;
    }

    public float GetRandomInitialSpeed()
    {
        return Random.Range(
            RandomInitialSpeedMinimum,
            RandomInitialSpeedMaximum);
    }

    public float GenerateRandomSpreadAngle(System.Random generator)
    {
        if (generator == null)
            throw new System.ArgumentNullException(nameof(generator));
        return Mathf.Lerp(RandomMinAngle, RandomMaxAngle, (float)generator.NextDouble());
    }

    public float GetShotGunPreviewRandomAngle(int shotGunIndex)
    {
        if (shootingMode != EnemyProjectileFireMode.Random)
            return 0f;

        int count = Mathf.Max(1, shotGunProjectileCount);
        float position = (Mathf.Clamp(shotGunIndex, 0, count - 1) + 0.5f) / count;
        return Mathf.Lerp(RandomMinAngle, RandomMaxAngle, position);
    }

    public float ResolveConsistentInitialSpeed(float projectileBaseSpeed)
    {
        return InitialSpeed > 0f
            ? InitialSpeed
            : Mathf.Max(0.01f, projectileBaseSpeed);
    }

    public void CopyFrom(EnemyShootingSettings source)
    {
        if (source == null || ReferenceEquals(this, source))
            return;
        source.Validate();
        CopyPresetBindingsFrom(source);
        CopyNumericFrom(source);
        childAttack = EnemyChildAttackSettings.Copy(source.childAttack);
        useChildAttackPattern = source.useChildAttackPattern && (source.childAttack == null || childAttack != null);
        shootingType = source.shootingType;
        EmissionOrigin.CopyFrom(source.EmissionOrigin);
        emissionLayout ??= new EnemyEmissionLayoutSettings();
        emissionLayout.CopyFrom(source.EmissionLayout);
        layoutRotationEnabled = source.layoutRotationEnabled;
        emissionLayoutVersion = source.emissionLayoutVersion;
        projectileType = source.projectileType;
        laser = source.laser?.Copy();
        overrideProjectileDamage = source.overrideProjectileDamage;
        projectileDamage = source.projectileDamage;
        overwriteProjectileCharacteristic = source.overwriteProjectileCharacteristic;
        projectileCharacteristic = source.projectileCharacteristic;
        overwriteBaseColour = source.overwriteBaseColour;
        projectileColour = source.projectileColour;
        shootingMode = source.shootingMode;
        fanMode = source.fanMode;
        shotGunMode = source.shotGunMode;
        projectilePatternVersion = source.projectilePatternVersion;
        directionCount = source.directionCount;
        fanProjectileCount = source.fanProjectileCount;
        fanMinAngle = source.fanMinAngle;
        fanMaxAngle = source.fanMaxAngle;
        randomMinAngle = source.randomMinAngle;
        randomMaxAngle = source.randomMaxAngle;
        shotGunProjectileCount = source.shotGunProjectileCount;
        shotGunDirection = source.shotGunDirection;
        enableSpawnOffset = source.enableSpawnOffset;
        baseOffsetX = source.baseOffsetX; baseOffsetY = source.baseOffsetY;
        stepOffsetX = source.stepOffsetX; stepOffsetY = source.stepOffsetY;
        shotGunInitialSpeed = source.shotGunInitialSpeed;
        overrideProjectileBaseSpeed = source.overrideProjectileBaseSpeed;
        projectileBaseSpeed = source.projectileBaseSpeed;
        ProjectileBaseSpeedRandom.CopyFrom(source.ProjectileBaseSpeedRandom);
        shotGunInitialSpeedRandom ??= new(); shotGunInitialSpeedRandom.CopyFrom(source.shotGunInitialSpeedRandom);
        speedRandomizeOn = source.speedRandomizeOn; seededInitialSpeedRandom = source.seededInitialSpeedRandom;
        numericRandomSeed = source.numericRandomSeed; runtimeProjectileBaseSpeedRandom = null;
        overrideProjectileLifetime = source.overrideProjectileLifetime;
        projectileLifetime = source.projectileLifetime;
        screenBoundary = source.screenBoundary;
        composite = source.Composite.Copy();
        initialSpeedMode = source.initialSpeedMode;
        randomInitialSpeedMinimum = source.randomInitialSpeedMinimum;
        randomInitialSpeedMaximum = source.randomInitialSpeedMaximum;
        consistentInitialSpeedChangePerVolley =
            source.consistentInitialSpeedChangePerVolley;
        projectileSpeedBehavior = source.projectileSpeedBehavior;
        projectileSpeedChangeY = source.projectileSpeedChangeY;
        projectileSpeedByDistance = CloneCurve(source.projectileSpeedByDistance);
        projectileSpeedCurveTarget = source.projectileSpeedCurveTarget;
        targetScreenBoundary=source.targetScreenBoundary;targetBoundaries=source.targetBoundaries;targetBoundaryOffset=source.targetBoundaryOffset;hideParentOnTargetReached=source.hideParentOnTargetReached;
        projectileMovementPattern = source.projectileMovementPattern;
        targetPointSpeedByRemainingDistance = source.targetPointSpeedByRemainingDistance != null
            ? new AnimationCurve(source.targetPointSpeedByRemainingDistance.keys) { preWrapMode = source.targetPointSpeedByRemainingDistance.preWrapMode, postWrapMode = source.targetPointSpeedByRemainingDistance.postWrapMode }
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        projectileAngularSpeed = source.projectileAngularSpeed;
        projectileLateralSpeed = source.projectileLateralSpeed;
        projectileOrbitCenterMode = source.projectileOrbitCenterMode;
        projectileOrbitFixedWorldCenter = source.projectileOrbitFixedWorldCenter;
        projectileOrbitRadius = source.projectileOrbitRadius;
        projectileOrbitRadialSpeed = source.projectileOrbitRadialSpeed;
        projectileDnaWeaveSpeed = source.projectileDnaWeaveSpeed;
        overrideBaseSize = source.overrideBaseSize; baseSize = source.baseSize;
        collisionSettings = source.collisionSettings;
        projectileSizeBehavior = source.projectileSizeBehavior;
        projectileSizeChangeY = source.projectileSizeChangeY;
        projectileSizeChangeSpeed = source.projectileSizeChangeSpeed;
        projectileSizeChangeCurve = CloneSizeCurve(source.projectileSizeChangeCurve);
        projectileFinalSize = source.projectileFinalSize;
        rotate = source.rotate;
        rotation.CopyFrom(source.rotation);
        hasRuntimeProjectileBaseSpeedOverride = false;
        runtimeProjectileBaseSpeedOverride = 0f;
    }

    public void Validate()
    {
        MigrateCompositeType();
        if (useChildAttackPattern) ChildAttack.Validate();
        emissionOrigin ??= new EnemyEmissionOriginSettings();
        MigrateEmissionLayout();
        emissionLayout.Validate();
        MigrateProjectilePattern();
        directionCount = Mathf.Max(1, directionCount);
        projectileBaseSpeed = Mathf.Max(0.01f, projectileBaseSpeed);
        projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
        randomInitialSpeedMinimum = Mathf.Max(0.01f, randomInitialSpeedMinimum);
        randomInitialSpeedMaximum = Mathf.Max(0.01f, randomInitialSpeedMaximum);
        fanProjectileCount = Mathf.Max(1, fanProjectileCount);
        shotGunProjectileCount = Mathf.Max(1, shotGunProjectileCount);
        shotGunInitialSpeed = Mathf.Max(0.01f, shotGunInitialSpeed);
        if (projectileSpeedByDistance == null || projectileSpeedByDistance.length == 0)
            projectileSpeedByDistance = AnimationCurve.Linear(0f, 1f, 10f, 1f);
        projectileOrbitRadius = Mathf.Max(0f, projectileOrbitRadius);
        projectileDnaWeaveSpeed = Mathf.Max(0.01f, projectileDnaWeaveSpeed);
        projectileSizeChangeSpeed = Mathf.Max(0.01f, projectileSizeChangeSpeed);
        projectileFinalSize = Mathf.Max(0f, projectileFinalSize);
        if (projectileSizeChangeCurve == null || projectileSizeChangeCurve.length == 0)
            projectileSizeChangeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        rotation ??= new EnemyRotationSettings();
        rotation.Validate();
    }

    public bool MigrateEmissionLayout()
    {
        emissionLayout ??= new EnemyEmissionLayoutSettings();
        if (emissionLayoutVersion >= 1) return false;
        bool radial = shootingType == EnemyShootingType.Rotation;
        emissionLayout.Configure(radial ? EnemyEmissionLayoutType.Radial : EnemyEmissionLayoutType.Point,
            Mathf.Max(1, directionCount));
        layoutRotationEnabled = radial && rotate;
        emissionLayoutVersion = 1;
        return true;
    }

    private void MigrateProjectilePattern()
    {
        if (projectilePatternVersion < 1)
            ApplyLegacyProjectilePattern((int)shootingMode);

        if (projectilePatternVersion >= CurrentProjectilePatternVersion)
            return;

        if ((int)shootingMode == LegacyShotGunFireModeValue)
        {
            shootingMode = EnemyProjectileFireMode.Default;
            shotGunMode = EnemyProjectileShotGunMode.ShotGun;
        }

        projectilePatternVersion = CurrentProjectilePatternVersion;
    }

    private void ApplyLegacyProjectilePattern(int legacyShootingMode)
    {
        switch (legacyShootingMode)
        {
            case 1:
                shootingMode = EnemyProjectileFireMode.Default;
                fanMode = EnemyProjectileFanMode.Fan;
                shotGunMode = EnemyProjectileShotGunMode.Default;
                break;
            case 2:
                shootingMode = EnemyProjectileFireMode.Default;
                fanMode = EnemyProjectileFanMode.Default;
                shotGunMode = EnemyProjectileShotGunMode.ShotGun;
                break;
            default:
                shootingMode = EnemyProjectileFireMode.Default;
                fanMode = EnemyProjectileFanMode.Default;
                shotGunMode = EnemyProjectileShotGunMode.Default;
                break;
        }

        projectilePatternVersion = 1;
    }

    private static AnimationCurve CloneCurve(AnimationCurve source)
    {
        if (source == null || source.length == 0)
            return AnimationCurve.Linear(0f, 1f, 10f, 1f);
        return new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
    }

    private static AnimationCurve CloneSizeCurve(AnimationCurve source)
    {
        if (source == null || source.length == 0)
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        return new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
    }
}

public struct EnemyProjectileInitialSpeedState
{
    private float consistentDelta;
    public float ConsistentDelta => consistentDelta;
    private bool hasConsistentSpeed;
    private float nextConsistentSpeed;

    public float GetVolleySpeed(
        EnemyShootingSettings settings,
        float projectileBaseSpeed)
    {
        if (settings.InitialSpeedMode != EnemyProjectileInitialSpeedMode.Consistent)
            return settings.InitialSpeed;

        if (!hasConsistentSpeed)
        {
            hasConsistentSpeed = true;
            nextConsistentSpeed = settings.ResolveConsistentInitialSpeed(
                projectileBaseSpeed);
        }

        return nextConsistentSpeed;
    }

    public void AdvanceAfterVolley(EnemyShootingSettings settings)
    {
        if (settings.InitialSpeedMode
            != EnemyProjectileInitialSpeedMode.Consistent)
        {
            return;
        }

        nextConsistentSpeed = Mathf.Max(
            0.01f,
            nextConsistentSpeed
                + settings.ConsistentInitialSpeedChangePerVolley);
        consistentDelta += settings.ConsistentInitialSpeedChangePerVolley;
    }
}
