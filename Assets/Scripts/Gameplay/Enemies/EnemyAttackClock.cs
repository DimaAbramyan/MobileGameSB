using UnityEngine;

// Attack-only clock: movement, animations and authored fire-rate bonuses keep their own time.
public struct EnemyAttackClock
{
    private float previousTime, attackTime;
    private bool initialized;
    public float Tick(float time, float multiplier)
    {
        if (!initialized)
        {
            initialized = true;
            previousTime = attackTime = time;
        }
        attackTime += Mathf.Max(0f, time - previousTime) * Mathf.Max(0f, multiplier);
        previousTime = time;
        return attackTime;
    }
}
