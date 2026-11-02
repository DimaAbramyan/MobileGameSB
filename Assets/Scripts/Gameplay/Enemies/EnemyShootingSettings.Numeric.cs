using UnityEngine;

public sealed partial class EnemyShootingSettings
{
    [System.NonSerialized] private bool geometrySampled, directionSampled, needsDirectionSamples;
    [System.NonSerialized] private int sampledDirection;
    [SerializeField, HideInInspector] private RandomFloatRange projectileOrbitFixedWorldCenterXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileOrbitFixedWorldCenterYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };

    [SerializeField, HideInInspector] private RandomFloatRange shotGunDirectionXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange shotGunDirectionYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };

    [SerializeField, HideInInspector] private RandomIntRange fanProjectileCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange fanMinAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange fanMaxAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomIntRange shotGunProjectileCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange randomMinAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange randomMaxAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange consistentInitialSpeedChangePerVolleyRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange projectileLifetimeRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileSpeedChangeYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileAngularSpeedRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileLateralSpeedRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileOrbitRadiusRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileOrbitRadialSpeedRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileDnaWeaveSpeedRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileSizeChangeYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileSizeChangeSpeedRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange projectileFinalSizeRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange baseOffsetXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange baseOffsetYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange stepOffsetXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange stepOffsetYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public bool UsesSpawnOffsetRandomization => enableSpawnOffset && (EnemyNumericSample.Enabled(baseOffsetXRandom)
        || EnemyNumericSample.Enabled(baseOffsetYRandom) || EnemyNumericSample.Enabled(stepOffsetXRandom) || EnemyNumericSample.Enabled(stepOffsetYRandom));
    public bool UsesGeometryRandomization => EnemyNumericSample.Enabled(shotGunDirectionXRandom) || EnemyNumericSample.Enabled(shotGunDirectionYRandom) || EnemyNumericSample.Enabled(fanProjectileCountRandom) || EnemyNumericSample.Enabled(fanMinAngleRandom) || EnemyNumericSample.Enabled(fanMaxAngleRandom) || EnemyNumericSample.Enabled(shotGunProjectileCountRandom) || EnemyNumericSample.Enabled(randomMinAngleRandom) || EnemyNumericSample.Enabled(randomMaxAngleRandom) || EnemyNumericSample.Enabled(consistentInitialSpeedChangePerVolleyRandom) || EmissionLayout.UsesNumericRandomization || Rotation.UsesNumericRandomization;
    public bool UsesProjectileRandomization => EnemyNumericSample.Enabled(projectileOrbitFixedWorldCenterXRandom) || EnemyNumericSample.Enabled(projectileOrbitFixedWorldCenterYRandom) || EnemyNumericSample.Enabled(projectileLifetimeRandom) || EnemyNumericSample.Enabled(projectileSpeedChangeYRandom) || EnemyNumericSample.Enabled(projectileAngularSpeedRandom) || EnemyNumericSample.Enabled(projectileLateralSpeedRandom) || EnemyNumericSample.Enabled(projectileOrbitRadiusRandom) || EnemyNumericSample.Enabled(projectileOrbitRadialSpeedRandom) || EnemyNumericSample.Enabled(projectileDnaWeaveSpeedRandom) || EnemyNumericSample.Enabled(projectileSizeChangeYRandom) || EnemyNumericSample.Enabled(projectileSizeChangeSpeedRandom) || EnemyNumericSample.Enabled(projectileFinalSizeRandom);
    public bool UsesExtendedNumericRandomization => UsesSpawnOffsetRandomization || UsesGeometryRandomization || UsesProjectileRandomization || IsComposite && Composite.UsesNumericRandomization;
    internal static bool AtScope(EnemyNumericRandomizeOn frequency, bool perDirection) => perDirection
        ? frequency == EnemyNumericRandomizeOn.PerDirection : frequency == EnemyNumericRandomizeOn.PerAttack || frequency == EnemyNumericRandomizeOn.PerShot;
    private void CopyNumericFrom(EnemyShootingSettings source)
    {
        geometrySampled = directionSampled = needsDirectionSamples = false;
        baseOffsetXRandom = source.baseOffsetXRandom?.Copy() ?? new();
        baseOffsetYRandom = source.baseOffsetYRandom?.Copy() ?? new();
        stepOffsetXRandom = source.stepOffsetXRandom?.Copy() ?? new();
        stepOffsetYRandom = source.stepOffsetYRandom?.Copy() ?? new();
        projectileOrbitFixedWorldCenterXRandom = source.projectileOrbitFixedWorldCenterXRandom?.Copy() ?? new();
        projectileOrbitFixedWorldCenterYRandom = source.projectileOrbitFixedWorldCenterYRandom?.Copy() ?? new();
        shotGunDirectionXRandom = source.shotGunDirectionXRandom?.Copy() ?? new();
        shotGunDirectionYRandom = source.shotGunDirectionYRandom?.Copy() ?? new();
        fanProjectileCountRandom = source.fanProjectileCountRandom?.Copy() ?? new();
        fanMinAngleRandom = source.fanMinAngleRandom?.Copy() ?? new();
        fanMaxAngleRandom = source.fanMaxAngleRandom?.Copy() ?? new();
        shotGunProjectileCountRandom = source.shotGunProjectileCountRandom?.Copy() ?? new();
        randomMinAngleRandom = source.randomMinAngleRandom?.Copy() ?? new();
        randomMaxAngleRandom = source.randomMaxAngleRandom?.Copy() ?? new();
        consistentInitialSpeedChangePerVolleyRandom = source.consistentInitialSpeedChangePerVolleyRandom?.Copy() ?? new();
        projectileLifetimeRandom = source.projectileLifetimeRandom?.Copy() ?? new();
        projectileSpeedChangeYRandom = source.projectileSpeedChangeYRandom?.Copy() ?? new();
        projectileAngularSpeedRandom = source.projectileAngularSpeedRandom?.Copy() ?? new();
        projectileLateralSpeedRandom = source.projectileLateralSpeedRandom?.Copy() ?? new();
        projectileOrbitRadiusRandom = source.projectileOrbitRadiusRandom?.Copy() ?? new();
        projectileOrbitRadialSpeedRandom = source.projectileOrbitRadialSpeedRandom?.Copy() ?? new();
        projectileDnaWeaveSpeedRandom = source.projectileDnaWeaveSpeedRandom?.Copy() ?? new();
        projectileSizeChangeYRandom = source.projectileSizeChangeYRandom?.Copy() ?? new();
        projectileSizeChangeSpeedRandom = source.projectileSizeChangeSpeedRandom?.Copy() ?? new();
        projectileFinalSizeRandom = source.projectileFinalSizeRandom?.Copy() ?? new();
    }
    public EnemyShootingSettings SampleGeometry(EnemyNumericSample sample, bool perDirection = false)
    {
        if (!sample.Active || !UsesGeometryRandomization) return this;
        if (perDirection && geometrySampled && (!needsDirectionSamples || directionSampled && sampledDirection == sample.DirectionIndex)) return this;
        var copy = (EnemyShootingSettings)MemberwiseClone();
        if (perDirection) { copy.directionSampled = true; copy.sampledDirection = sample.DirectionIndex; }
        else
        {
            copy.geometrySampled = true; copy.directionSampled = false;
            copy.needsDirectionSamples = EnemyNumericSample.PerDirection(fanProjectileCountRandom)
                || EnemyNumericSample.PerDirection(fanMinAngleRandom) || EnemyNumericSample.PerDirection(fanMaxAngleRandom)
                || EnemyNumericSample.PerDirection(shotGunProjectileCountRandom)
                || EnemyNumericSample.PerDirection(shotGunDirectionXRandom) || EnemyNumericSample.PerDirection(shotGunDirectionYRandom)
                || EnemyNumericSample.PerDirection(randomMinAngleRandom) || EnemyNumericSample.PerDirection(randomMaxAngleRandom)
                || EmissionLayout.UsesPerDirectionRandomization;
        }
        if (AtScope(fanProjectileCountRandom?.randomizeOn ?? default, perDirection)) copy.fanProjectileCount = sample.Int(fanProjectileCountRandom, fanProjectileCount, 101u, 1);
        if (AtScope(fanMinAngleRandom?.randomizeOn ?? default, perDirection)) copy.fanMinAngle = sample.Float(fanMinAngleRandom, fanMinAngle, 102u, float.MinValue);
        if (AtScope(fanMaxAngleRandom?.randomizeOn ?? default, perDirection)) copy.fanMaxAngle = sample.Float(fanMaxAngleRandom, fanMaxAngle, 103u, float.MinValue);
        if (AtScope(shotGunProjectileCountRandom?.randomizeOn ?? default, perDirection)) copy.shotGunProjectileCount = sample.Int(shotGunProjectileCountRandom, shotGunProjectileCount, 104u, 1);
        if (AtScope(randomMinAngleRandom?.randomizeOn ?? default, perDirection)) copy.randomMinAngle = sample.Float(randomMinAngleRandom, randomMinAngle, 105u, float.MinValue);
        if (AtScope(randomMaxAngleRandom?.randomizeOn ?? default, perDirection)) copy.randomMaxAngle = sample.Float(randomMaxAngleRandom, randomMaxAngle, 106u, float.MinValue);
        if (AtScope(consistentInitialSpeedChangePerVolleyRandom?.randomizeOn ?? default, perDirection)) copy.consistentInitialSpeedChangePerVolley = sample.Float(consistentInitialSpeedChangePerVolleyRandom, consistentInitialSpeedChangePerVolley, 107u, float.MinValue);
        copy.emissionLayout = EmissionLayout.SampleNumeric(sample, perDirection);
        copy.rotation = Rotation.SampleNumeric(sample, perDirection);
        if (EnemyShootingSettings.AtScope(shotGunDirectionXRandom?.randomizeOn ?? default, perDirection)) copy.shotGunDirection.x = sample.Float(shotGunDirectionXRandom, shotGunDirection.x, 111u);
        if (EnemyShootingSettings.AtScope(shotGunDirectionYRandom?.randomizeOn ?? default, perDirection)) copy.shotGunDirection.y = sample.Float(shotGunDirectionYRandom, shotGunDirection.y, 112u);
        return copy;
    }
    public EnemyProjectileNumericValues SampleProjectile(EnemyNumericSample sample) => new(
        sample.Float(projectileLifetimeRandom, ProjectileLifetime, 301u, .01f),
        sample.Float(projectileSpeedChangeYRandom, SpeedChangeY, 302u, float.MinValue),
        sample.Float(projectileAngularSpeedRandom, AngularSpeed, 303u, float.MinValue),
        sample.Float(projectileLateralSpeedRandom, LateralSpeed, 304u, float.MinValue),
        sample.Float(projectileOrbitRadiusRandom, OrbitRadius, 305u, 0f),
        sample.Float(projectileOrbitRadialSpeedRandom, OrbitRadialSpeed, 306u, float.MinValue),
        sample.Float(projectileDnaWeaveSpeedRandom, DnaWeaveSpeed, 307u, .01f),
        sample.Float(projectileSizeChangeYRandom, SizeChangeY, 308u, float.MinValue),
        sample.Float(projectileSizeChangeSpeedRandom, SizeChangeSpeed, 309u, .01f),
        sample.Float(projectileFinalSizeRandom, FinalSize, 310u, 0f),
        new Vector2(sample.Float(projectileOrbitFixedWorldCenterXRandom, projectileOrbitFixedWorldCenter.x, 311u), sample.Float(projectileOrbitFixedWorldCenterYRandom, projectileOrbitFixedWorldCenter.y, 312u))
    );
    public float NumericFanStart(EnemyNumericSample sample) => fanMinAngleRandom?.randomizeOn == EnemyNumericRandomizeOn.PerProjectile ? sample.Float(fanMinAngleRandom, fanMinAngle, 102u) : fanMinAngle;
    public float NumericFanEnd(EnemyNumericSample sample) => fanMaxAngleRandom?.randomizeOn == EnemyNumericRandomizeOn.PerProjectile ? sample.Float(fanMaxAngleRandom, fanMaxAngle, 103u) : fanMaxAngle;
    public float NumericSpread(EnemyNumericSample sample) => Mathf.Lerp(sample.Float(randomMinAngleRandom, RandomMinAngle, 105u), sample.Float(randomMaxAngleRandom, RandomMaxAngle, 106u), sample.Unit(108u));
    public float GetNumericDnaHalfCycleDuration(EnemyProjectileNumericValues values, EnemyNumericSample numeric)
    {
        float speed = Mathf.Abs(values.AngularSpeed) * values.DnaWeaveSpeed;
        if (speed < .0001f) return 0f;
        float halfAngle = UsesFan && FanProjectileCount > 1
            ? Mathf.Min(EnemyProjectileDnaMotion.MaximumHalfAngleDegrees, Mathf.Abs(NumericFanEnd(numeric) - NumericFanStart(numeric)) * .5f) : 45f;
        return 2f * Mathf.Max(.01f, halfAngle) / speed;
    }
}

public readonly struct EnemyProjectileNumericValues
{
    public readonly Vector2 OrbitFixedWorldCenter;
    public readonly float Lifetime, SpeedChangeY, AngularSpeed, LateralSpeed, OrbitRadius, OrbitRadialSpeed, DnaWeaveSpeed, SizeChangeY, SizeChangeSpeed, FinalSize;
    public EnemyProjectileNumericValues(float lifetime, float speedChangeY, float angularSpeed, float lateralSpeed, float orbitRadius, float orbitRadialSpeed, float dnaWeaveSpeed, float sizeChangeY, float sizeChangeSpeed, float finalSize, Vector2 orbitFixedWorldCenter)
    {
        Lifetime = lifetime;
        SpeedChangeY = speedChangeY;
        AngularSpeed = angularSpeed;
        LateralSpeed = lateralSpeed;
        OrbitRadius = orbitRadius;
        OrbitRadialSpeed = orbitRadialSpeed;
        DnaWeaveSpeed = dnaWeaveSpeed;
        SizeChangeY = sizeChangeY;
        SizeChangeSpeed = sizeChangeSpeed;
        FinalSize = finalSize;
        OrbitFixedWorldCenter = orbitFixedWorldCenter;
    }
}
