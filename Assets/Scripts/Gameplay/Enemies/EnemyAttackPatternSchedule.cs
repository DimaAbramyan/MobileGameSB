using UnityEngine;

// One attack cycle. A wave can advance several of these in the same frame.
public struct EnemyAttackPatternSchedule
{
    private int remainingAttackShots;
    private int remainingBurstShots;
    private int burstShotCount;
    private float nextShotTime;

    public bool IsComplete => remainingAttackShots <= 0;
    public bool IsBurstStart => remainingBurstShots == burstShotCount;
    public float NextAttackTime { get; private set; }

    public void Begin(EnemyBurstAttackSettings settings, float time, float fireRate, bool initialDelay)
    {
        remainingAttackShots = settings.GetAttackShotCountForFireRate(fireRate);
        remainingBurstShots = settings.RepeatBurst ? settings.BurstShotCount : 1;
        burstShotCount = remainingBurstShots;
        nextShotTime = time + (initialDelay ? settings.AttackStartDelay / Mathf.Max(0.01f, fireRate) : 0f);
        NextAttackTime = nextShotTime;
    }

    public bool IsDue(float time) => !IsComplete && time >= nextShotTime;

    public void Advance(EnemyBurstAttackSettings settings, float time, float fireRate)
    {
        if (--remainingBurstShots > 0)
        {
            nextShotTime = time + settings.BurstShotInterval;
            return;
        }
        if (--remainingAttackShots > 0)
        {
            remainingBurstShots = settings.RepeatBurst ? settings.BurstShotCount : 1;
            nextShotTime = time + settings.AttackShotInterval / (settings.RepeatBurst ? 1f : Mathf.Max(0.01f, fireRate));
            return;
        }
        NextAttackTime = time + settings.AttackCooldown / Mathf.Max(0.01f, fireRate);
    }
}
