using UnityEngine;

public sealed partial class DirectedWaveAttackSettings
{
    [SerializeField, HideInInspector] private RandomFloatRange attackStartDelayRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    [SerializeField, HideInInspector] private RandomIntRange attacksPerEnemyPerCycleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    [SerializeField, HideInInspector] private RandomFloatRange attacksPerSecondRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    [SerializeField, HideInInspector] private RandomFloatRange delayAfterAttackRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerShot };
    private void CopyNumericFrom(DirectedWaveAttackSettings source)
    {
        attackStartDelayRandom = source.attackStartDelayRandom?.Copy() ?? new();
        attacksPerEnemyPerCycleRandom = source.attacksPerEnemyPerCycleRandom?.Copy() ?? new();
        attacksPerSecondRandom = source.attacksPerSecondRandom?.Copy() ?? new();
        delayAfterAttackRandom = source.delayAfterAttackRandom?.Copy() ?? new();
    }
    public EnemyAttackNumericState CreateSchedulingNumeric(int seedOffset = 0) => EnemyNumericSample.Enabled(attackStartDelayRandom)
        || EnemyNumericSample.Enabled(attacksPerEnemyPerCycleRandom) || EnemyNumericSample.Enabled(attacksPerSecondRandom) || EnemyNumericSample.Enabled(delayAfterAttackRandom)
            ? new EnemyAttackNumericState(unchecked(WaveBurstSettings.GetPattern(0).Shooting.NumericRandomSeed + seedOffset), 0, 1) : null;
    public float SampleStartDelay(EnemyAttackNumericState numeric) => useAttackStartDelay ? numeric?.SampleTiming(AttackStartDelay, attackStartDelayRandom, channel: 601u) ?? AttackStartDelay : 0f;
    public float SampleAttacksPerSecond(EnemyAttackNumericState numeric) => numeric?.SampleTiming(AttacksPerSecond, attacksPerSecondRandom, .01f, 603u) ?? AttacksPerSecond;
    public float SampleDelayAfterAttack(EnemyAttackNumericState numeric) => numeric?.SampleTiming(DelayAfterAttack, delayAfterAttackRandom, channel: 604u) ?? DelayAfterAttack;
    public int SampleAttacksPerCycle(EnemyAttackNumericState numeric) => numeric?.SampleTiming(AttacksPerEnemyPerCycle, attacksPerEnemyPerCycleRandom, 602u) ?? AttacksPerEnemyPerCycle;
}
