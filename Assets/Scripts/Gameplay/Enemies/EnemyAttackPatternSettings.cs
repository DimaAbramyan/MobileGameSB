using UnityEngine;

public enum EnemyAttackPatternsMode : byte
{
    Single = 0,
    Multiple = 1
}

// Flat list entry: keeping the collection out of entries avoids recursive Unity serialization.
[System.Serializable]
public sealed partial class EnemyAttackPatternSettings
{
    [SerializeField, HideInInspector] private string patternId = System.Guid.NewGuid().ToString("N");
    public string PatternId => patternId;

    [SerializeField] private EnemyShootingSettings shooting = new EnemyShootingSettings();
    [SerializeField] private bool useAttackStartDelay;
    [SerializeField] [Randomizable(nameof(attackStartDelayRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private float attackStartDelay = 1f;
    [SerializeField] private EnemyBurstAttackMode burstAttack;
    [SerializeField] [Randomizable(nameof(attackShotCountRandom), 1f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private int attackShotCount = 1;
    [SerializeField] [Randomizable(nameof(attackShotIntervalRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerShot)]
    private float attackShotInterval = 0.15f;
    [SerializeField, Randomizable(nameof(attackCooldownRandom), 0f)] private float attackCooldown = 1f;
    [SerializeField, HideInInspector] private RandomFloatRange attackCooldownRandom = new();
    public RandomFloatRange AttackCooldownRandom => attackCooldownRandom ??= new();
    [SerializeField] [Randomizable(nameof(burstShotCountRandom), 1f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private int burstShotCount = 1;
    [SerializeField] [Randomizable(nameof(burstShotIntervalRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerShot)]
    private float burstShotInterval = 0.15f;

    [System.NonSerialized] private EnemyBurstAttackSettings runtimeSettings;

    public EnemyShootingSettings AuthoredShooting => shooting;
    public EnemyShootingSettings Shooting => shooting.HasLinkedPresets ? RuntimeSettings.Shooting : shooting;
    [System.NonSerialized] private int presetRevision = -1;
    public void ScaleAttackTiming(float multiplier)
    {
        var active = shooting.HasLinkedPresets ? RuntimeSettings : runtimeSettings;
        multiplier = Mathf.Max(.01f, multiplier);
        attackStartDelay /= multiplier; attackShotInterval /= multiplier;
        burstShotInterval /= multiplier; attackCooldown /= multiplier;
        AttackCooldownRandom.Scale(1f / multiplier);
        AttackStartDelayRandom?.Scale(1f / multiplier); AttackShotIntervalRandom?.Scale(1f / multiplier);
        BurstShotIntervalRandom?.Scale(1f / multiplier);
        shooting.ScaleAttackTiming(multiplier);
        active?.ScaleAttackTiming(multiplier);
    }
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
            if (runtimeSettings == null || presetRevision != EnemyPresetRevision.Value)
            {
                var copy = new EnemyBurstAttackSettings();
                copy.CopySinglePatternFrom(this);
                runtimeSettings = copy;
                presetRevision = EnemyPresetRevision.Value;
            }
            return runtimeSettings;
        }
    }

    public void CopyFrom(EnemyBurstAttackSettings source)
    {
        source.Validate();
        CopyNumericTimingFrom(source);
        shooting.CopyFrom(source.AuthoredShooting);
        useAttackStartDelay = source.UsesAttackStartDelay;
        attackStartDelay = source.ConfiguredAttackStartDelay;
        burstAttack = source.BurstAttack;
        attackShotCount = source.AttackShotCount;
        attackShotInterval = source.AttackShotInterval;
        attackCooldown = source.AttackCooldown;
        AttackCooldownRandom.CopyFrom(source.AttackCooldownRandom);
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
        CopyNumericTimingFrom(source);
        shooting.CopyFrom(source.shooting);
        useAttackStartDelay = source.useAttackStartDelay;
        attackStartDelay = source.attackStartDelay;
        burstAttack = source.burstAttack;
        attackShotCount = source.attackShotCount;
        attackShotInterval = source.attackShotInterval;
        attackCooldown = source.attackCooldown;
        AttackCooldownRandom.CopyFrom(source.AttackCooldownRandom);
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
