using UnityEngine;

public sealed partial class EnemyBurstAttackSettings
{
    [SerializeField, HideInInspector] private RandomFloatRange attackStartDelayRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomFloatRange AttackStartDelayRandom => ResolvePresets().attackStartDelayRandom;
    [SerializeField, HideInInspector] private RandomIntRange attackShotCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomIntRange AttackShotCountRandom => ResolvePresets().attackShotCountRandom;
    [SerializeField, HideInInspector] private RandomFloatRange attackShotIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public RandomFloatRange AttackShotIntervalRandom => ResolvePresets().attackShotIntervalRandom;
    [SerializeField, HideInInspector] private RandomIntRange burstShotCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomIntRange BurstShotCountRandom => ResolvePresets().burstShotCountRandom;
    [SerializeField, HideInInspector] private RandomFloatRange burstShotIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public RandomFloatRange BurstShotIntervalRandom => ResolvePresets().burstShotIntervalRandom;
    public bool UsesRandomTiming => EnemyNumericSample.Enabled(AttackStartDelayRandom) || EnemyNumericSample.Enabled(AttackShotCountRandom) || EnemyNumericSample.Enabled(AttackShotIntervalRandom) || EnemyNumericSample.Enabled(BurstShotCountRandom) || EnemyNumericSample.Enabled(BurstShotIntervalRandom);
    private void CopyNumericTimingFrom(EnemyBurstAttackSettings source)
    {
        attackStartDelayRandom = source.AttackStartDelayRandom?.Copy() ?? new();
        attackShotCountRandom = source.AttackShotCountRandom?.Copy() ?? new();
        attackShotIntervalRandom = source.AttackShotIntervalRandom?.Copy() ?? new();
        burstShotCountRandom = source.BurstShotCountRandom?.Copy() ?? new();
        burstShotIntervalRandom = source.BurstShotIntervalRandom?.Copy() ?? new();
    }
    private void CopyNumericTimingFrom(EnemyAttackPatternSettings source)
    {
        attackStartDelayRandom = source.AttackStartDelayRandom?.Copy() ?? new();
        attackShotCountRandom = source.AttackShotCountRandom?.Copy() ?? new();
        attackShotIntervalRandom = source.AttackShotIntervalRandom?.Copy() ?? new();
        burstShotCountRandom = source.BurstShotCountRandom?.Copy() ?? new();
        burstShotIntervalRandom = source.BurstShotIntervalRandom?.Copy() ?? new();
    }
    public float SampleStartDelay(EnemyAttackNumericState numeric) => UsesAttackStartDelay ? numeric?.SampleTiming(AttackStartDelay, AttackStartDelayRandom, channel: 501u) ?? AttackStartDelay : 0f;
    public float SampleShotInterval(EnemyAttackNumericState numeric) => numeric?.SampleTiming(AttackShotInterval, AttackShotIntervalRandom, channel: 503u) ?? AttackShotInterval;
    public int SampleBurstCount(EnemyAttackNumericState numeric) => numeric?.SampleTiming(BurstShotCount, BurstShotCountRandom, 504u) ?? BurstShotCount;
    public float SampleBurstInterval(EnemyAttackNumericState numeric) => numeric?.SampleTiming(BurstShotInterval, BurstShotIntervalRandom, channel: 505u) ?? BurstShotInterval;
}
