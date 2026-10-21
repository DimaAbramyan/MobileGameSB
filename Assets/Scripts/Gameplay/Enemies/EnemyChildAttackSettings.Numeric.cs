using UnityEngine;

public sealed partial class EnemyChildAttackSettings
{
    [SerializeField, HideInInspector] private RandomFloatRange spawnOffsetXRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange spawnOffsetYRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };

    [SerializeField, HideInInspector] private RandomFloatRange triggerValueRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomIntRange repeatCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange repeatIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange boundaryOffsetRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    [SerializeField, HideInInspector] private RandomFloatRange timeOffsetRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerProjectile };
    public bool UsesNumericRandomization => EnemyNumericSample.Enabled(spawnOffsetXRandom) || EnemyNumericSample.Enabled(spawnOffsetYRandom) || EnemyNumericSample.Enabled(triggerValueRandom) || EnemyNumericSample.Enabled(repeatCountRandom) || EnemyNumericSample.Enabled(repeatIntervalRandom) || EnemyNumericSample.Enabled(boundaryOffsetRandom) || EnemyNumericSample.Enabled(timeOffsetRandom);
    private void CopyNumericFrom(EnemyChildAttackSettings source)
    {
        spawnOffsetXRandom = source.spawnOffsetXRandom?.Copy() ?? new();
        spawnOffsetYRandom = source.spawnOffsetYRandom?.Copy() ?? new();
        triggerValueRandom = source.triggerValueRandom?.Copy() ?? new();
        repeatCountRandom = source.repeatCountRandom?.Copy() ?? new();
        repeatIntervalRandom = source.repeatIntervalRandom?.Copy() ?? new();
        boundaryOffsetRandom = source.boundaryOffsetRandom?.Copy() ?? new();
        timeOffsetRandom = source.timeOffsetRandom?.Copy() ?? new();
    }
    public EnemyChildAttackSettings SampleNumeric(uint seed)
    {
        if (!UsesNumericRandomization) return this;
        var sample = new EnemyNumericSample(seed, seed);
        var copy = (EnemyChildAttackSettings)MemberwiseClone();
        copy.triggerValue = sample.Float(triggerValueRandom, triggerValue, 401u, 0f);
        copy.repeatCount = sample.Int(repeatCountRandom, repeatCount, 402u, 1, 32);
        copy.repeatInterval = sample.Float(repeatIntervalRandom, repeatInterval, 403u, 0f);
        copy.boundaryOffset = sample.Float(boundaryOffsetRandom, boundaryOffset, 404u, float.MinValue);
        copy.timeOffset = sample.Float(timeOffsetRandom, timeOffset, 405u, 0f);
        copy.repeatCount = Mathf.Clamp(copy.repeatCount, 1, 32);
        copy.spawnOffset.x = sample.Float(spawnOffsetXRandom, spawnOffset.x, 411u);
        copy.spawnOffset.y = sample.Float(spawnOffsetYRandom, spawnOffset.y, 412u);
        return copy;
    }
}
