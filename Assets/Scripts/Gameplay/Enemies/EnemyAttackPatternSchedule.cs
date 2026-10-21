using UnityEngine;

// One attack cycle. A wave can advance several of these in the same frame.
public struct EnemyAttackPatternSchedule
{
    private int remainingAttackShots;
    private int remainingBurstShots;
    private int burstShotCount;
    private float nextShotTime;
    private EnemyAttackNumericState numeric;

    public bool IsComplete => remainingAttackShots <= 0;
    public bool IsBurstStart => remainingBurstShots == burstShotCount;
    public bool IsLastShot => remainingAttackShots == 1 && remainingBurstShots == 1;
    public float NextAttackTime { get; private set; }

    public void Begin(EnemyBurstAttackSettings settings, float time, float fireRate, bool initialDelay, EnemyAttackNumericState numeric = null)
    {
        this.numeric = numeric;
        remainingAttackShots = settings.GetAttackShotCountForFireRate(fireRate, numeric);
        remainingBurstShots = settings.RepeatBurst ? settings.SampleBurstCount(numeric) : 1;
        burstShotCount = remainingBurstShots;
        nextShotTime = time + (initialDelay || settings.AttackStartDelayRandom.enabled ? settings.SampleStartDelay(numeric) / Mathf.Max(0.01f, fireRate) : 0f);
        NextAttackTime = nextShotTime;
    }

    public bool IsDue(float time) => !IsComplete && time >= nextShotTime;

    public void Advance(EnemyBurstAttackSettings settings, float time, float fireRate, float? cooldown = null)
    {
        if (--remainingBurstShots > 0)
        {
            nextShotTime = time + settings.SampleBurstInterval(numeric);
            return;
        }
        if (--remainingAttackShots > 0)
        {
            remainingBurstShots = burstShotCount;
            nextShotTime = time + settings.SampleShotInterval(numeric) / (settings.RepeatBurst ? 1f : Mathf.Max(0.01f, fireRate));
            return;
        }
        NextAttackTime = time + (cooldown ?? settings.AttackCooldown) / Mathf.Max(0.01f, fireRate);
    }
}
