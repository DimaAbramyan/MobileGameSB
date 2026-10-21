using UnityEngine;

public sealed partial class EnemyAttackPatternSettings
{
    [SerializeField, HideInInspector] private RandomFloatRange attackStartDelayRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomFloatRange AttackStartDelayRandom => attackStartDelayRandom;
    [SerializeField, HideInInspector] private RandomIntRange attackShotCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomIntRange AttackShotCountRandom => attackShotCountRandom;
    [SerializeField, HideInInspector] private RandomFloatRange attackShotIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public RandomFloatRange AttackShotIntervalRandom => attackShotIntervalRandom;
    [SerializeField, HideInInspector] private RandomIntRange burstShotCountRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public RandomIntRange BurstShotCountRandom => burstShotCountRandom;
    [SerializeField, HideInInspector] private RandomFloatRange burstShotIntervalRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    public RandomFloatRange BurstShotIntervalRandom => burstShotIntervalRandom;
    public bool UsesRandomTiming => EnemyNumericSample.Enabled(attackStartDelayRandom) || EnemyNumericSample.Enabled(attackShotCountRandom) || EnemyNumericSample.Enabled(attackShotIntervalRandom) || EnemyNumericSample.Enabled(burstShotCountRandom) || EnemyNumericSample.Enabled(burstShotIntervalRandom);
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
}
