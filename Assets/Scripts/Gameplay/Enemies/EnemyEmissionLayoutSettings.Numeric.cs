using UnityEngine;

public sealed partial class EnemyEmissionLayoutSettings
{
    public bool UsesPerDirectionRandomization => EnemyNumericSample.PerDirection(radiusRandom)
        || EnemyNumericSample.PerDirection(centerOffsetXRandom) || EnemyNumericSample.PerDirection(centerOffsetYRandom)
        || EnemyNumericSample.PerDirection(positionOffsetXRandom) || EnemyNumericSample.PerDirection(positionOffsetYRandom);
    [SerializeField, HideInInspector] private RandomFloatRange positionOffsetXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange positionOffsetYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };

    [SerializeField, HideInInspector] private RandomFloatRange centerOffsetXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange centerOffsetYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };

    [SerializeField, HideInInspector] private RandomIntRange directionsCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomIntRange emitterCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange radiusRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange startAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange endAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange startDelayRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange launchIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public bool UsesNumericRandomization => EnemyNumericSample.Enabled(positionOffsetXRandom) || EnemyNumericSample.Enabled(positionOffsetYRandom) || EnemyNumericSample.Enabled(centerOffsetXRandom) || EnemyNumericSample.Enabled(centerOffsetYRandom) || EnemyNumericSample.Enabled(directionsCountRandom) || EnemyNumericSample.Enabled(emitterCountRandom) || EnemyNumericSample.Enabled(radiusRandom) || EnemyNumericSample.Enabled(startAngleRandom) || EnemyNumericSample.Enabled(endAngleRandom) || EnemyNumericSample.Enabled(startDelayRandom) || EnemyNumericSample.Enabled(launchIntervalRandom);
    private void CopyNumericFrom(EnemyEmissionLayoutSettings source)
    {
        positionOffsetXRandom = source.positionOffsetXRandom?.Copy() ?? new();
        positionOffsetYRandom = source.positionOffsetYRandom?.Copy() ?? new();
        centerOffsetXRandom = source.centerOffsetXRandom?.Copy() ?? new();
        centerOffsetYRandom = source.centerOffsetYRandom?.Copy() ?? new();
        directionsCountRandom = source.directionsCountRandom?.Copy() ?? new();
        emitterCountRandom = source.emitterCountRandom?.Copy() ?? new();
        radiusRandom = source.radiusRandom?.Copy() ?? new();
        startAngleRandom = source.startAngleRandom?.Copy() ?? new();
        endAngleRandom = source.endAngleRandom?.Copy() ?? new();
        startDelayRandom = source.startDelayRandom?.Copy() ?? new();
        launchIntervalRandom = source.launchIntervalRandom?.Copy() ?? new();
    }
    public EnemyEmissionLayoutSettings SampleNumeric(EnemyNumericSample sample, bool perDirection = false)
    {
        if (!sample.Active || !UsesNumericRandomization) return this;
        var copy = (EnemyEmissionLayoutSettings)MemberwiseClone();
        if (EnemyShootingSettings.AtScope(directionsCountRandom?.randomizeOn ?? default, perDirection)) copy.directionsCount = sample.Int(directionsCountRandom, directionsCount, 201u, 1);
        if (EnemyShootingSettings.AtScope(emitterCountRandom?.randomizeOn ?? default, perDirection)) copy.emitterCount = sample.Int(emitterCountRandom, emitterCount, 202u, 1);
        if (EnemyShootingSettings.AtScope(radiusRandom?.randomizeOn ?? default, perDirection)) copy.radius = sample.Float(radiusRandom, radius, 203u, 0f);
        if (EnemyShootingSettings.AtScope(startAngleRandom?.randomizeOn ?? default, perDirection)) copy.startAngle = sample.Float(startAngleRandom, startAngle, 204u, float.MinValue);
        if (EnemyShootingSettings.AtScope(endAngleRandom?.randomizeOn ?? default, perDirection)) copy.endAngle = sample.Float(endAngleRandom, endAngle, 205u, float.MinValue);
        if (EnemyShootingSettings.AtScope(startDelayRandom?.randomizeOn ?? default, perDirection)) copy.startDelay = sample.Float(startDelayRandom, startDelay, 206u, 0f);
        if (EnemyShootingSettings.AtScope(launchIntervalRandom?.randomizeOn ?? default, perDirection)) copy.launchInterval = sample.Float(launchIntervalRandom, launchInterval, 207u, 0f);
        (copy.orderSource, copy.orderPatternIndex) = OrderKey;
        if (EnemyShootingSettings.AtScope(centerOffsetXRandom?.randomizeOn ?? default, perDirection)) copy.centerOffset.x = sample.Float(centerOffsetXRandom, centerOffset.x, 211u);
        if (EnemyShootingSettings.AtScope(centerOffsetYRandom?.randomizeOn ?? default, perDirection)) copy.centerOffset.y = sample.Float(centerOffsetYRandom, centerOffset.y, 212u);
        if (EnemyShootingSettings.AtScope(positionOffsetXRandom?.randomizeOn ?? default, perDirection)) copy.positionOffset.x = sample.Float(positionOffsetXRandom, positionOffset.x, 213u);
        if (EnemyShootingSettings.AtScope(positionOffsetYRandom?.randomizeOn ?? default, perDirection)) copy.positionOffset.y = sample.Float(positionOffsetYRandom, positionOffset.y, 214u);
        return copy;
    }
}
