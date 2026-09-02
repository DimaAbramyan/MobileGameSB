using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public sealed class EnemyBurstAttackSettings
{
    private const int CurrentSerializedVersion = 1;

    [SerializeField, FormerlySerializedAs("useBurstFire")]
    private bool repeatBurst;

    [SerializeField] private bool useAttackStartDelay;
    [SerializeField, Min(0f)] private float attackStartDelay = 1f;

    [SerializeField] private bool useAreaAttack;
    [SerializeField, Min(1)] private int areaAttackProjectileCount = 3;
    [SerializeField] private float areaAttackMinAngle = -45f;
    [SerializeField] private float areaAttackMaxAngle = 45f;

    [SerializeField, Min(1)] private int attackShotCount = 1;
    [SerializeField, Min(0f)] private float attackShotInterval = 0.15f;
    [SerializeField, Min(0f), FormerlySerializedAs("burstCooldown")]
    private float attackCooldown = 1f;

    [SerializeField, Min(1)] private int burstShotCount = 1;
    [SerializeField, Min(0f)] private float burstShotInterval = 0.15f;

    [SerializeField, HideInInspector, FormerlySerializedAs("shotsPerBurst")]
    private int legacyShotsPerBurst = 1;
    [SerializeField, HideInInspector, FormerlySerializedAs("shotInterval")]
    private float legacyShotInterval = 0.15f;
    [SerializeField, HideInInspector] private int serializedVersion;

    public bool RepeatBurst => repeatBurst;
    public bool UsesAttackStartDelay => useAttackStartDelay;
    public float AttackStartDelay => useAttackStartDelay
        ? Mathf.Max(0f, attackStartDelay)
        : 0f;
    public int ProjectilesPerShot => useAreaAttack
        ? Mathf.Max(1, areaAttackProjectileCount)
        : 1;
    public int AttackShotCount => Mathf.Max(1, attackShotCount);
    public float AttackShotInterval => Mathf.Max(0f, attackShotInterval);
    public float AttackCooldown => Mathf.Max(0f, attackCooldown);
    public int BurstShotCount => Mathf.Max(1, burstShotCount);
    public float BurstShotInterval => Mathf.Max(0f, burstShotInterval);

    public int GetAttackShotCountForFireRate(float fireRateMultiplier)
    {
        if (!RepeatBurst)
            return AttackShotCount;

        return Mathf.Max(
            1,
            Mathf.CeilToInt(
                AttackShotCount * Mathf.Max(0.01f, fireRateMultiplier)));
    }
    public int ShotEventsPerAttack => AttackShotCount
        * (RepeatBurst ? BurstShotCount : 1);
    public int ProjectilesPerAttack => ShotEventsPerAttack * ProjectilesPerShot;
    public float AttackDuration => CalculateAttackDuration(
        RepeatBurst,
        AttackShotCount,
        AttackShotInterval,
        BurstShotCount,
        BurstShotInterval);
    public float AttackCycleDuration => AttackDuration + AttackCooldown;

    public static float CalculateAttackDuration(
        bool repeatBurst,
        int attackShotCount,
        float attackShotInterval,
        int burstShotCount,
        float burstShotInterval)
    {
        int safeAttackShotCount = Mathf.Max(1, attackShotCount);
        float safeAttackShotInterval = Mathf.Max(0f, attackShotInterval);
        float duration = (safeAttackShotCount - 1)
            * safeAttackShotInterval;

        if (!repeatBurst)
            return duration;

        int safeBurstShotCount = Mathf.Max(1, burstShotCount);
        float safeBurstShotInterval = Mathf.Max(0f, burstShotInterval);
        return duration + safeAttackShotCount
            * (safeBurstShotCount - 1)
            * safeBurstShotInterval;
    }

    public void ConfigureLegacySingleAttack(
        int shots,
        float cooldown,
        float interval)
    {
        repeatBurst = false;
        attackShotCount = shots;
        attackShotInterval = interval;
        attackCooldown = cooldown;
        serializedVersion = CurrentSerializedVersion;
        Validate();
    }

    public void ConfigureLegacyRepeatedBurst(
        int shots,
        float cooldown,
        float interval)
    {
        repeatBurst = true;
        attackShotCount = 1;
        attackShotInterval = 0f;
        attackCooldown = cooldown;
        burstShotCount = shots;
        burstShotInterval = interval;
        serializedVersion = CurrentSerializedVersion;
        Validate();
    }

    public void CopyFrom(EnemyBurstAttackSettings source)
    {
        if (source == null)
            return;

        source.Validate();
        repeatBurst = source.repeatBurst;
        useAttackStartDelay = source.useAttackStartDelay;
        attackStartDelay = source.attackStartDelay;
        useAreaAttack = source.useAreaAttack;
        areaAttackProjectileCount = source.areaAttackProjectileCount;
        areaAttackMinAngle = source.areaAttackMinAngle;
        areaAttackMaxAngle = source.areaAttackMaxAngle;
        attackShotCount = source.attackShotCount;
        attackShotInterval = source.attackShotInterval;
        attackCooldown = source.attackCooldown;
        burstShotCount = source.burstShotCount;
        burstShotInterval = source.burstShotInterval;
        serializedVersion = CurrentSerializedVersion;
        Validate();
    }

    public void Validate()
    {
        MigrateLegacySettings();

        attackShotCount = Mathf.Max(1, attackShotCount);
        attackStartDelay = Mathf.Max(0f, attackStartDelay);
        areaAttackProjectileCount = Mathf.Max(1, areaAttackProjectileCount);
        attackShotInterval = Mathf.Max(0f, attackShotInterval);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        burstShotCount = Mathf.Max(1, burstShotCount);
        burstShotInterval = Mathf.Max(0f, burstShotInterval);
    }

    private void MigrateLegacySettings()
    {
        if (serializedVersion >= CurrentSerializedVersion)
            return;

        int legacyCount = Mathf.Max(1, legacyShotsPerBurst);
        float legacyInterval = Mathf.Max(0f, legacyShotInterval);
        if (repeatBurst)
        {
            attackShotCount = 1;
            attackShotInterval = 0f;
            burstShotCount = legacyCount;
            burstShotInterval = legacyInterval;
        }
        else
        {
            attackShotCount = legacyCount;
            attackShotInterval = legacyInterval;
        }

        serializedVersion = CurrentSerializedVersion;
    }

    public Vector3 GetProjectileDirection(
        Vector3 baseDirection,
        int projectileIndex)
    {
        if (baseDirection.sqrMagnitude < 0.0001f)
            baseDirection = Vector3.up;
        else
            baseDirection.Normalize();

        int projectileCount = ProjectilesPerShot;
        if (projectileCount <= 1)
            return baseDirection;

        float minAngle = Mathf.Min(areaAttackMinAngle, areaAttackMaxAngle);
        float maxAngle = Mathf.Max(areaAttackMinAngle, areaAttackMaxAngle);
        float progress = Mathf.Clamp01(
            projectileIndex / (float)(projectileCount - 1));
        float angle = Mathf.Lerp(maxAngle, minAngle, progress);
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }
}
