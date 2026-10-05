using UnityEngine;

public enum EnemyAttackPatternsMode : byte
{
    Single = 0,
    Multiple = 1
}

// Flat list entry: keeping the collection out of entries avoids recursive Unity serialization.
[System.Serializable]
public sealed class EnemyAttackPatternSettings
{
    [SerializeField, HideInInspector] private string patternId = System.Guid.NewGuid().ToString("N");
    public string PatternId => patternId;

    [SerializeField] private EnemyShootingSettings shooting = new EnemyShootingSettings();
    [SerializeField] private bool useAttackStartDelay;
    [SerializeField, Min(0f)] private float attackStartDelay = 1f;
    [SerializeField] private EnemyBurstAttackMode burstAttack;
    [SerializeField, Min(1)] private int attackShotCount = 1;
    [SerializeField, Min(0f)] private float attackShotInterval = 0.15f;
    [SerializeField, Min(0f)] private float attackCooldown = 1f;
    [SerializeField, Min(1)] private int burstShotCount = 1;
    [SerializeField, Min(0f)] private float burstShotInterval = 0.15f;

    [System.NonSerialized] private EnemyBurstAttackSettings runtimeSettings;

    public EnemyShootingSettings Shooting => shooting;
    public bool UsesAttackStartDelay => useAttackStartDelay;
    public float AttackStartDelay => attackStartDelay;
    public EnemyBurstAttackMode BurstAttack => burstAttack;
    public int AttackShotCount => attackShotCount;
    public float AttackShotInterval => attackShotInterval;
    public float AttackCooldown => attackCooldown;
    public int BurstShotCount => burstShotCount;
    public float BurstShotInterval => burstShotInterval;

    public EnemyBurstAttackSettings RuntimeSettings
    {
        get
        {
            if (runtimeSettings == null)
            {
                runtimeSettings = new EnemyBurstAttackSettings();
                runtimeSettings.CopySinglePatternFrom(this);
            }
            return runtimeSettings;
        }
    }

    public void CopyFrom(EnemyBurstAttackSettings source)
    {
        source.Validate();
        shooting.CopyFrom(source.Shooting);
        useAttackStartDelay = source.UsesAttackStartDelay;
        attackStartDelay = source.ConfiguredAttackStartDelay;
        burstAttack = source.BurstAttack;
        attackShotCount = source.AttackShotCount;
        attackShotInterval = source.AttackShotInterval;
        attackCooldown = source.AttackCooldown;
        burstShotCount = source.BurstShotCount;
        burstShotInterval = source.BurstShotInterval;
        Validate();
    }

    public void CopyFrom(EnemyAttackPatternSettings source, bool preserveIdentity = false)
    {
        if (source == null || ReferenceEquals(this, source))
            return;
        if (preserveIdentity)
            patternId = source.patternId;
        shooting.CopyFrom(source.shooting);
        useAttackStartDelay = source.useAttackStartDelay;
        attackStartDelay = source.attackStartDelay;
        burstAttack = source.burstAttack;
        attackShotCount = source.attackShotCount;
        attackShotInterval = source.attackShotInterval;
        attackCooldown = source.attackCooldown;
        burstShotCount = source.burstShotCount;
        burstShotInterval = source.burstShotInterval;
        Validate();
    }

    public void SetProjectileType(EnemyProjectileType type)
    {
        shooting.SetProjectileType(type);
        runtimeSettings?.Shooting.SetProjectileType(type);
    }

    public void SetRotation(EnemyRotationSettings settings)
    {
        shooting.Rotation.CopyFrom(settings);
        runtimeSettings?.Shooting.Rotation.CopyFrom(settings);
    }

    public void SetRuntimeProjectileBaseSpeedOverride(float speed)
    {
        RuntimeSettings.Shooting.SetRuntimeProjectileBaseSpeedOverride(speed);
    }

    // Keeps the attack cadence owned by the wave while replacing only what a
    // local directed-wave slot is allowed to configure.
    public void CopyShootingFrom(EnemyAttackPatternSettings source)
    {
        if (source == null || ReferenceEquals(this, source))
            return;

        shooting ??= new EnemyShootingSettings();
        shooting.CopyFrom(source.shooting);
        runtimeSettings = null;
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(patternId))
            patternId = System.Guid.NewGuid().ToString("N");
        shooting ??= new EnemyShootingSettings();
        shooting.Validate();
        attackStartDelay = Mathf.Max(0f, attackStartDelay);
        attackShotCount = Mathf.Max(1, attackShotCount);
        attackShotInterval = Mathf.Max(0f, attackShotInterval);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        burstShotCount = Mathf.Max(1, burstShotCount);
        burstShotInterval = Mathf.Max(0f, burstShotInterval);
        runtimeSettings = null;
    }
}
