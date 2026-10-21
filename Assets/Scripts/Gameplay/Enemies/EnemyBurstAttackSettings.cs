using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum EnemyBurstAttackMode : byte
{
    None = 0,
    Enabled = 1
}

[System.Serializable]
public sealed partial class EnemyBurstAttackSettings
{
    private const int CurrentSerializedVersion = 2;

    [SerializeField] private EnemyAttackPatternsMode attackPatterns;
    [SerializeField] private List<EnemyAttackPatternSettings> patterns = new();
    [SerializeField, Tooltip("Default and Fixed keep the visual pose. Follow Pattern explicitly follows one attack. Rotation turns the visual target with its own independent settings.")]
    private EnemyFacingMode facingMode;
    [SerializeField] private EnemyRotationSettings facingRotation = new EnemyRotationSettings();
    [SerializeField, HideInInspector] private int facingLayoutVersion;
    [SerializeField, HideInInspector] private string facingPatternId;
    [SerializeField, Min(0f), Tooltip("Visual turn speed in degrees per second. Zero follows immediately.")]
    private float facingSpeed = 360f;
    [SerializeField, Tooltip("Visual angle correction in degrees; it never changes projectile directions.")]
    private float facingAngleOffset;

    public EnemyFacingMode FacingMode => facingMode;
    public EnemyRotationSettings FacingRotation => facingRotation;
    public float FacingSpeed => Mathf.Max(0f, facingSpeed);
    public float FacingAngleOffset => facingAngleOffset;
    public int FacingPatternIndex
    {
        get
        {
            if (PatternCount == 0)
                return -1;
            if (!HasMultiplePatterns || string.IsNullOrEmpty(facingPatternId))
                return 0;
            for (int i = 0; i < patterns.Count; i++)
                if (patterns[i].PatternId == facingPatternId)
                    return i;
            return -1;
        }
    }

    public void SetFacing(EnemyFacingMode mode, int patternIndex = 0, float speed = 360f, float angleOffset = 0f)
    {
        if (mode == EnemyFacingMode.FollowPattern && (patternIndex < 0 || patternIndex >= PatternCount))
            throw new System.ArgumentOutOfRangeException(nameof(patternIndex));
        facingMode = mode;
        facingSpeed = Mathf.Max(0f, speed);
        facingAngleOffset = angleOffset;
        facingPatternId = HasMultiplePatterns && patternIndex >= 0 && patternIndex < patterns.Count
            ? patterns[patternIndex].PatternId : null;
    }


    public EnemyAttackPatternsMode AttackPatterns => attackPatterns;
    public bool HasMultiplePatterns => attackPatterns == EnemyAttackPatternsMode.Multiple;
    internal float ConfiguredAttackStartDelay => attackStartDelay;
    public int PatternCount => HasMultiplePatterns ? patterns.Count : 1;

    public EnemyBurstAttackSettings GetPattern(int index)
    {
        return HasMultiplePatterns ? patterns[index].RuntimeSettings : this;
    }

    public float MaxAttackCooldown
    {
        get
        {
            float cooldown = 0f;
            for (int i = 0; i < PatternCount; i++)
                cooldown = Mathf.Max(cooldown, GetPattern(i).AttackCooldown);
            return cooldown;
        }
    }

    public void EnableMultiplePatterns()
    {
        if (patterns.Count == 0)
        {
            var first = new EnemyAttackPatternSettings();
            first.CopyFrom(this);
            patterns.Add(first);
        }
        attackPatterns = EnemyAttackPatternsMode.Multiple;
    }

    public void AddPattern(EnemyAttackPatternSettings pattern)
    {
        var copy = new EnemyAttackPatternSettings();
        copy.CopyFrom(pattern);
        patterns.Add(copy);
    }

    public void SetProjectileTypeForAllPatterns(EnemyProjectileType type)
    {
        shooting.SetProjectileType(type);
        for (int i = 0; i < patterns.Count; i++)
            patterns[i].SetProjectileType(type);
    }

    public void SetRotationForAllPatterns(EnemyRotationSettings settings)
    {
        shooting.Rotation.CopyFrom(settings);
        for (int i = 0; i < patterns.Count; i++)
            patterns[i].SetRotation(settings);
    }

    public void SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(float speed)
    {
        Shooting.SetRuntimeProjectileBaseSpeedOverride(speed);
        for (int i = 0; i < patterns.Count; i++)
            patterns[i].SetRuntimeProjectileBaseSpeedOverride(speed);
    }

    public void ScaleAttackTiming(float multiplier)
    {
        var effective = ResolvePresets();
        if (!ReferenceEquals(effective, this)) { effective.ScaleAttackTiming(multiplier); return; }
        multiplier = Mathf.Max(.01f, multiplier);
        attackStartDelay /= multiplier; attackShotInterval /= multiplier;
        burstShotInterval /= multiplier; attackCooldown /= multiplier;
        AttackCooldownRandom.Scale(1f / multiplier);
        AttackStartDelayRandom?.Scale(1f / multiplier); AttackShotIntervalRandom?.Scale(1f / multiplier);
        BurstShotIntervalRandom?.Scale(1f / multiplier);
        Shooting.ScaleAttackTiming(multiplier);
        facingRotation.ScaleAttackTiming(multiplier);
        for (int i = 0; i < patterns.Count; i++) patterns[i].ScaleAttackTiming(multiplier);
    }

    public void SetProjectileBaseSpeedOverrideForAllPatterns(float speed)
    {
        shooting.SetProjectileBaseSpeedOverride(true, speed);
        for (int i = 0; i < patterns.Count; i++)
        {
            patterns[i].Shooting.SetProjectileBaseSpeedOverride(true, speed);
            patterns[i].Validate();
        }
    }

    public void SetProjectileBaseSpeedRangeForAllPatterns(float speed, RandomFloatRange range, EnemySpeedRandomizeOn mode)
    {
        SetProjectileBaseSpeedOverrideForAllPatterns(speed);
        shooting.SetProjectileBaseSpeedRange(range, mode);
        for (int i = 0; i < patterns.Count; i++)
        { patterns[i].Shooting.SetProjectileBaseSpeedRange(range, mode); patterns[i].Validate(); }
    }

    internal void CopySinglePatternFrom(EnemyAttackPatternSettings source)
    {
        CopyNumericTimingFrom(source);
        shooting.CopyFrom(source.AuthoredShooting);
        shootingSettingsVersion = 1;
        useAttackStartDelay = source.UsesAttackStartDelay;
        attackStartDelay = source.AttackStartDelay;
        burstAttack = source.BurstAttack;
        attackShotCount = source.AttackShotCount;
        attackShotInterval = source.AttackShotInterval;
        attackCooldown = source.AttackCooldown;
        attackCooldownRandom ??= new();
        attackCooldownRandom.CopyFrom(source.AttackCooldownRandom);
        burstShotCount = source.BurstShotCount;
        burstShotInterval = source.BurstShotInterval;
        serializedVersion = CurrentSerializedVersion;
        facingLayoutVersion = 1;
        Validate();
    }

    [SerializeField, HideInInspector, FormerlySerializedAs("useBurstFire")]
    private bool repeatBurst;

    [SerializeField] private EnemyBurstAttackMode burstAttack;

    [SerializeField] private bool useAttackStartDelay;
    [SerializeField] [Randomizable(nameof(attackStartDelayRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private float attackStartDelay = 1f;

    [SerializeField] private EnemyShootingSettings shooting = new EnemyShootingSettings();
    [SerializeField, HideInInspector] private int shootingSettingsVersion;

    [SerializeField, HideInInspector] private bool useAreaAttack;
    [SerializeField, HideInInspector, Min(1)] private int areaAttackProjectileCount = 3;
    [SerializeField, HideInInspector] private float areaAttackMinAngle = -45f;
    [SerializeField, HideInInspector] private float areaAttackMaxAngle = 45f;

    [SerializeField] [Randomizable(nameof(attackShotCountRandom), 1f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private int attackShotCount = 1;
    [SerializeField] [Randomizable(nameof(attackShotIntervalRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerShot)]
    private float attackShotInterval = 0.15f;
    [SerializeField, Randomizable(nameof(attackCooldownRandom), 0f), FormerlySerializedAs("burstCooldown")]
    private float attackCooldown = 1f;
    [SerializeField, HideInInspector] private RandomFloatRange attackCooldownRandom = new();
    public RandomFloatRange AttackCooldownRandom => ResolvePresets().attackCooldownRandom ??= new();

    [SerializeField] [Randomizable(nameof(burstShotCountRandom), 1f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerAttack)]
    private int burstShotCount = 1;
    [SerializeField] [Randomizable(nameof(burstShotIntervalRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerShot)]
    private float burstShotInterval = 0.15f;

    [SerializeField, HideInInspector, FormerlySerializedAs("shotsPerBurst")]
    private int legacyShotsPerBurst = 1;
    [SerializeField, HideInInspector, FormerlySerializedAs("shotInterval")]
    private float legacyShotInterval = 0.15f;
    [SerializeField, HideInInspector] private int serializedVersion;

    public bool RepeatBurst => ResolvePresets().burstAttack == EnemyBurstAttackMode.Enabled;
    public EnemyBurstAttackMode BurstAttack => ResolvePresets().burstAttack;
    public bool UsesAttackStartDelay => ResolvePresets().useAttackStartDelay;
    public float AttackStartDelay => ResolvePresets().useAttackStartDelay
        ? Mathf.Max(0f, ResolvePresets().attackStartDelay)
        : 0f;
    public EnemyShootingSettings Shooting => ResolvePresets().shooting;
    public int ProjectilesPerShot => Shooting.ProjectilesPerVolley;
    public bool UsesAreaAttack => useAreaAttack;
    public int AreaAttackProjectileCount => Mathf.Max(1, areaAttackProjectileCount);
    public float AreaAttackMinAngle => areaAttackMinAngle;
    public float AreaAttackMaxAngle => areaAttackMaxAngle;
    public int AttackShotCount => Mathf.Max(1, ResolvePresets().attackShotCount);
    public float AttackShotInterval => Mathf.Max(0f, ResolvePresets().attackShotInterval);
    public float AttackCooldown => Mathf.Max(0f, ResolvePresets().attackCooldown);
    public int BurstShotCount => Mathf.Max(1, ResolvePresets().burstShotCount);
    public float BurstShotInterval => Mathf.Max(0f, ResolvePresets().burstShotInterval);

    public int GetAttackShotCountForFireRate(float fireRateMultiplier, EnemyAttackNumericState numeric = null)
    {
        int count = numeric?.SampleTiming(AttackShotCount, AttackShotCountRandom, 502u) ?? AttackShotCount;
        if (!RepeatBurst)
            return count;

        return Mathf.Max(
            1,
            Mathf.CeilToInt(
                count * Mathf.Max(0.01f, fireRateMultiplier)));
    }
    public int ShotEventsPerAttack => AttackShotCount
        * (RepeatBurst ? BurstShotCount : 1);
    public int ProjectilesPerAttack => ShotEventsPerAttack * ProjectilesPerShot;
    public float AttackDuration => CalculateAttackDuration(
        RepeatBurst,
        AttackShotCount,
        AttackShotInterval,
        BurstShotCount,
        BurstShotInterval) + Shooting.EmissionLayout.LaunchDuration;
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
        burstAttack = EnemyBurstAttackMode.None;
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
        burstAttack = EnemyBurstAttackMode.Enabled;
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
        if (source == null || ReferenceEquals(this, source))
            return;

        source.Validate();
        shooting ??= new EnemyShootingSettings();
        CopyNumericTimingFrom(source);
        shooting.CopyFrom(source.shooting);
        shootingSettingsVersion = 1;
        burstAttack = source.burstAttack;
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
        attackCooldownRandom ??= new();
        attackCooldownRandom.CopyFrom(source.attackCooldownRandom);
        burstShotCount = source.burstShotCount;
        burstShotInterval = source.burstShotInterval;
        attackPatterns = source.attackPatterns;
        facingMode = source.facingMode;
        facingRotation.CopyFrom(source.facingRotation);
        facingLayoutVersion = source.facingLayoutVersion;
        facingPatternId = source.facingPatternId;
        facingSpeed = source.facingSpeed;
        facingAngleOffset = source.facingAngleOffset;
        patterns ??= new List<EnemyAttackPatternSettings>();
        patterns.Clear();
        for (int i = 0; i < source.patterns.Count; i++)
        {
            var copy = new EnemyAttackPatternSettings();
            copy.CopyFrom(source.patterns[i], true);
            patterns.Add(copy);
        }
        serializedVersion = CurrentSerializedVersion;
        Validate();
    }

    // A directed-wave slot may replace its firing profile, but its cadence is
    // still controlled by the selected Directed Wave Attack Behaviour.
    public void ApplyShootingOverrideFrom(EnemyBurstAttackSettings source)
    {
        if (source == null || ReferenceEquals(this, source))
            return;

        source = source.ResolveAllPresets();
        source.Validate();
        shooting ??= new EnemyShootingSettings();
        attackPatterns = source.attackPatterns;
        facingMode = source.facingMode;
        facingRotation.CopyFrom(source.facingRotation);
        facingLayoutVersion = source.facingLayoutVersion;
        facingSpeed = source.facingSpeed;
        facingAngleOffset = source.facingAngleOffset;

        if (!source.HasMultiplePatterns)
        {
            shooting.CopyFrom(source.shooting);
            facingPatternId = null;
            Validate();
            return;
        }

        patterns ??= new List<EnemyAttackPatternSettings>();
        EnemyAttackPatternSettings timingTemplate = patterns.Count > 0
            ? patterns[0]
            : null;
        while (patterns.Count < source.patterns.Count)
        {
            var added = new EnemyAttackPatternSettings();
            if (timingTemplate != null)
                added.CopyFrom(timingTemplate);
            else
                added.CopyFrom(this);
            patterns.Add(added);
        }

        while (patterns.Count > source.patterns.Count)
            patterns.RemoveAt(patterns.Count - 1);

        for (int i = 0; i < patterns.Count; i++)
            patterns[i].CopyShootingFrom(source.patterns[i]);

        int facingIndex = source.FacingPatternIndex;
        facingPatternId = facingMode == EnemyFacingMode.FollowPattern
            && facingIndex >= 0
            && facingIndex < patterns.Count
                ? patterns[facingIndex].PatternId
                : null;
        Validate();
    }

    public void Validate()
    {
        presetResolved = null;
        resolvedPatterns = null;
        patterns ??= new List<EnemyAttackPatternSettings>();
        for (int i = 0; i < patterns.Count; i++)
        {
            patterns[i] ??= new EnemyAttackPatternSettings();
            patterns[i].Validate();
        }
        facingSpeed = Mathf.Max(0f, facingSpeed);
        if (facingMode == EnemyFacingMode.FollowPattern && HasMultiplePatterns
            && patterns.Count > 0 && string.IsNullOrEmpty(facingPatternId))
            facingPatternId = patterns[0].PatternId;
        MigrateLegacySettings();
        shooting ??= new EnemyShootingSettings();
        if (shootingSettingsVersion < 1)
        {
            if (useAreaAttack)
                shooting.ConfigureLegacyFan(areaAttackProjectileCount, areaAttackMinAngle, areaAttackMaxAngle);
            shootingSettingsVersion = 1;
        }
        shooting.Validate();
        facingRotation ??= new EnemyRotationSettings();
        if (facingLayoutVersion < 1)
        {
            if (!HasMultiplePatterns && facingMode == EnemyFacingMode.Default && shooting.HadLegacyRotation)
            {
                // Old Single Rotation turned a Transform. Preserve that visual
                // motion as an explicit, independent facing configuration.
                facingMode = EnemyFacingMode.Rotation;
                facingRotation.CopyFrom(shooting.Rotation);
                shooting.EmissionLayout.SetRotateShootingPointOffset(true);
            }
            facingLayoutVersion = 1;
        }
        facingRotation.Validate();

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
        if (serializedVersion < 1)
        {
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

            serializedVersion = 1;
        }

        if (serializedVersion < 2)
        {
            burstAttack = repeatBurst
                ? EnemyBurstAttackMode.Enabled
                : EnemyBurstAttackMode.None;
            serializedVersion = 2;
        }

        repeatBurst = burstAttack == EnemyBurstAttackMode.Enabled;
    }

    public Vector3 GetProjectileDirection(
        Vector3 baseDirection,
        int projectileIndex)
    {
        int perDirection = shooting.ProjectilesPerDirection;
        return shooting.GetProjectileDirection(baseDirection,
            projectileIndex / perDirection, projectileIndex % perDirection);
    }
}
