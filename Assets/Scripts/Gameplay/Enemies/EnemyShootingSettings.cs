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
    Orbit = 3
}

public enum EnemyProjectileOrbitCenterMode : byte
{
    SourcePosition = 0,
    FixedWorldPoint = 1
}

[System.Serializable]
public sealed class EnemyShootingSettings
{
    private const int CurrentProjectilePatternVersion = 2;
    private const int LegacyShotGunFireModeValue = 1;

    [System.NonSerialized] private bool hasRuntimeProjectileBaseSpeedOverride;
    [System.NonSerialized] private float runtimeProjectileBaseSpeedOverride;

    [SerializeField] private EnemyShootingType shootingType;
    [SerializeField] private EnemyProjectileType projectileType;
    [SerializeField, Tooltip("Uses this value instead of the Base Speed on the selected projectile prefab.")]
    private bool overrideProjectileBaseSpeed;
    [SerializeField, Min(0.01f), Tooltip("Used only when Override Projectile Base Speed is enabled.")]
    private float projectileBaseSpeed = 1f;
    [SerializeField, Tooltip("Uses this value instead of the Base Lifetime on the selected projectile prefab.")]
    private bool overrideProjectileLifetime;
    [SerializeField, Min(0.01f), Tooltip("Used only when Override Projectile Lifetime is enabled.")]
    private float projectileLifetime = 1f;
    [Header("Initial Projectile Speed")]
    [SerializeField] private EnemyProjectileInitialSpeedMode initialSpeedMode;
    [SerializeField, Min(0.01f), Tooltip("Used only in Random mode. Every projectile receives a speed in this range.")]
    private float randomInitialSpeedMinimum = 1f;
    [SerializeField, Min(0.01f), Tooltip("Used only in Random mode. Every projectile receives a speed in this range.")]
    private float randomInitialSpeedMaximum = 2f;
    [SerializeField, Tooltip("Used only in Consistent mode. Applied once after the entire volley; negative values slow later volleys.")]
    private float consistentInitialSpeedChangePerVolley = -0.1f;
    [SerializeField] private EnemyProjectileFireMode shootingMode;
    [SerializeField] private EnemyProjectileFanMode fanMode;
    [SerializeField, HideInInspector] private int projectilePatternVersion;
    [SerializeField, Min(1)] private int directionCount = 4;
    [SerializeField, Min(1)] private int fanProjectileCount = 3;
    [SerializeField] private float fanMinAngle = -45f;
    [SerializeField] private float fanMaxAngle = 45f;
    [SerializeField] private float randomMinAngle = -15f;
    [SerializeField] private float randomMaxAngle = 15f;
    [SerializeField] private EnemyProjectileShotGunMode shotGunMode;
    [SerializeField, Min(1)] private int shotGunProjectileCount = 3;
    [SerializeField] private Vector2 shotGunDirection = Vector2.down;
    [SerializeField, Min(0.01f)] private float shotGunInitialSpeed = 5f;
    [SerializeField] private EnemyProjectileSpeedBehavior projectileSpeedBehavior;
    [SerializeField] private float projectileSpeedChangeY;
    [SerializeField] private AnimationCurve projectileSpeedByDistance = AnimationCurve.Linear(0f, 1f, 10f, 1f);
    [Header("Projectile Movement")]
    [SerializeField] private EnemyProjectileMovementPattern projectileMovementPattern;
    [SerializeField, Tooltip("Degrees per second. Positive turns clockwise; negative turns counterclockwise.")]
    private float projectileAngularSpeed = 90f;
    [SerializeField, Tooltip("Constant local-right velocity in units per second. Negative values move to the opposite side.")]
    private float projectileLateralSpeed = 1f;
    [SerializeField] private EnemyProjectileOrbitCenterMode projectileOrbitCenterMode;
    [SerializeField, Tooltip("World-space center used only when Orbit Center is Fixed World Point.")]
    private Vector2 projectileOrbitFixedWorldCenter;
    [SerializeField, Min(0f), Tooltip("Orbit radius. Zero keeps the projectile's spawn distance from the selected center.")]
    private float projectileOrbitRadius;
    [SerializeField, Tooltip("Radius change in units per second. Positive expands the orbit; negative contracts it.")]
    private float projectileOrbitRadialSpeed;
    [Header("Projectile Size Change")]
    [SerializeField] private EnemyProjectileSizeBehavior projectileSizeBehavior;
    [SerializeField] private float projectileSizeChangeY;
    [SerializeField, Min(0.01f), Tooltip("Normalized size-change progress per second after the projectile reaches Size Change Y.")]
    private float projectileSizeChangeSpeed = 1f;
    [SerializeField, Tooltip("Maps normalized size-change progress from 0 to 1 to the size interpolation.")]
    private AnimationCurve projectileSizeChangeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(0.01f), Tooltip("Final multiplier of the selected projectile prefab's authored visual and collision size.")]
    private float projectileFinalSize = 1f;
    [SerializeField] private bool rotate = true;
    [SerializeField] private EnemyRotationSettings rotation = new EnemyRotationSettings();

    public EnemyShootingType ShootingType => shootingType;
    public EnemyProjectileType ProjectileType => projectileType;
    public bool OverridesProjectileBaseSpeed => overrideProjectileBaseSpeed;
    public bool OverridesProjectileLifetime => overrideProjectileLifetime;
    public float ProjectileLifetime => Mathf.Max(0.01f, projectileLifetime);
    public EnemyProjectileInitialSpeedMode InitialSpeedMode => initialSpeedMode;
    public EnemyProjectileFireMode ShootingMode => shootingMode;
    public EnemyProjectileFanMode FanMode => fanMode;
    public EnemyProjectileShotGunMode ShotGunMode => shotGunMode;
    public bool UsesFan => fanMode == EnemyProjectileFanMode.Fan;
    public bool UsesShotGun => shotGunMode == EnemyProjectileShotGunMode.ShotGun;
    public int DirectionCount => shootingType == EnemyShootingType.Rotation ? Mathf.Max(1, directionCount) : 1;
    public int FanProjectileCount => Mathf.Max(1, fanProjectileCount);
    public int ProjectilesPerDirection =>
        (UsesShotGun ? Mathf.Max(1, shotGunProjectileCount) : 1)
        * (UsesFan ? FanProjectileCount : 1);
    public int ProjectilesPerVolley => DirectionCount * ProjectilesPerDirection;
    public Vector2 ShotGunDirection => shotGunDirection;
    public float InitialSpeed => hasRuntimeProjectileBaseSpeedOverride
        ? runtimeProjectileBaseSpeedOverride
        : overrideProjectileBaseSpeed ? projectileBaseSpeed
        : UsesShotGun ? shotGunInitialSpeed : 0f;
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
    public EnemyProjectileSpeedBehavior SpeedBehavior => projectileSpeedBehavior;
    public float SpeedChangeY => projectileSpeedChangeY;
    public AnimationCurve SpeedByDistance => projectileSpeedByDistance;
    public EnemyProjectileMovementPattern MovementPattern => projectileMovementPattern;
    public float AngularSpeed => projectileAngularSpeed;
    public float LateralSpeed => projectileLateralSpeed;
    public EnemyProjectileOrbitCenterMode OrbitCenterMode => projectileOrbitCenterMode;
    public Vector2 OrbitFixedWorldCenter => projectileOrbitFixedWorldCenter;
    public float OrbitRadius => Mathf.Max(0f, projectileOrbitRadius);
    public float OrbitRadialSpeed => projectileOrbitRadialSpeed;
    public EnemyProjectileSizeBehavior SizeBehavior => projectileSizeBehavior;
    public float SizeChangeY => projectileSizeChangeY;
    public float SizeChangeSpeed => projectileSizeChangeSpeed;
    public AnimationCurve SizeChangeCurve => projectileSizeChangeCurve;
    public float FinalSize => projectileFinalSize;
    public bool Rotates => shootingType == EnemyShootingType.Rotation && rotate;
    public EnemyRotationSettings Rotation => rotation;

    public Quaternion EvaluateDirectionBasis(Quaternion basis, float elapsed, EnemyRotationSettings rotationOverride = null)
    {
        return Rotates ? basis * Quaternion.Euler(0f, 0f, (rotationOverride ?? rotation).EvaluateAngle(elapsed)) : basis;
    }

    public Vector3 EvaluateShotDirection(
        Quaternion basis,
        Vector3 requestedDirection,
        bool useAimedShotGunDirection = false)
    {
        Vector3 direction = shootingType == EnemyShootingType.Rotation ? basis * Vector3.up : requestedDirection;
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
        float randomSpreadAngle = 0f)
    {
        baseDirection = baseDirection.sqrMagnitude < 0.0001f ? Vector3.up : baseDirection.normalized;
        float angle = directionIndex * (360f / DirectionCount) + randomSpreadAngle;
        if (UsesFan && FanProjectileCount > 1)
        {
            angle += Mathf.Lerp(Mathf.Max(fanMinAngle, fanMaxAngle), Mathf.Min(fanMinAngle, fanMaxAngle),
                (projectileIndex % FanProjectileCount) / (float)(FanProjectileCount - 1));
        }
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
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
        if (settings != null)
            rotation.CopyFrom(settings);
    }

    public void SetProjectileType(EnemyProjectileType type) => projectileType = type;

    public void SetProjectileBaseSpeedOverride(bool enabled, float speed)
    {
        overrideProjectileBaseSpeed = enabled;
        projectileBaseSpeed = Mathf.Max(0.01f, speed);
    }

    // This is deliberately runtime-only: wave overrides must not alter projectile prefabs
    // or the attack profile stored on an enemy prefab.
    public void SetRuntimeProjectileBaseSpeedOverride(float speed)
    {
        hasRuntimeProjectileBaseSpeedOverride = true;
        runtimeProjectileBaseSpeedOverride = Mathf.Max(0.01f, speed);
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
        shootingType = source.shootingType;
        projectileType = source.projectileType;
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
        shotGunInitialSpeed = source.shotGunInitialSpeed;
        overrideProjectileBaseSpeed = source.overrideProjectileBaseSpeed;
        projectileBaseSpeed = source.projectileBaseSpeed;
        overrideProjectileLifetime = source.overrideProjectileLifetime;
        projectileLifetime = source.projectileLifetime;
        initialSpeedMode = source.initialSpeedMode;
        randomInitialSpeedMinimum = source.randomInitialSpeedMinimum;
        randomInitialSpeedMaximum = source.randomInitialSpeedMaximum;
        consistentInitialSpeedChangePerVolley =
            source.consistentInitialSpeedChangePerVolley;
        projectileSpeedBehavior = source.projectileSpeedBehavior;
        projectileSpeedChangeY = source.projectileSpeedChangeY;
        projectileSpeedByDistance = CloneCurve(source.projectileSpeedByDistance);
        projectileMovementPattern = source.projectileMovementPattern;
        projectileAngularSpeed = source.projectileAngularSpeed;
        projectileLateralSpeed = source.projectileLateralSpeed;
        projectileOrbitCenterMode = source.projectileOrbitCenterMode;
        projectileOrbitFixedWorldCenter = source.projectileOrbitFixedWorldCenter;
        projectileOrbitRadius = source.projectileOrbitRadius;
        projectileOrbitRadialSpeed = source.projectileOrbitRadialSpeed;
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
        projectileSizeChangeSpeed = Mathf.Max(0.01f, projectileSizeChangeSpeed);
        projectileFinalSize = Mathf.Max(0.01f, projectileFinalSize);
        if (projectileSizeChangeCurve == null || projectileSizeChangeCurve.length == 0)
            projectileSizeChangeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        rotation ??= new EnemyRotationSettings();
        rotation.Validate();
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
    }
}
