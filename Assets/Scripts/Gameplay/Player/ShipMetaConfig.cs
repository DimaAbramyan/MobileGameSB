using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class ShipMetaBasicStats
{
    [SerializeField, Min(0f)] private float maximumHealthPoints;
    [SerializeField, Min(0f)] private float maximumShieldPoints;
    [SerializeField, Min(0f)] private float healthRegenRatePercent =
        ShipData.DefaultRegenRatePercent;
    [SerializeField, Min(0f)] private float shieldRegenRatePercent =
        ShipData.DefaultRegenRatePercent;
    [SerializeField, Min(0f)] private float speed;
    [SerializeField, Min(0)] private int maximumEnergy = 10;

    public float MaximumHealthPoints => Mathf.Max(0f, maximumHealthPoints);
    public float MaximumShieldPoints => Mathf.Max(0f, maximumShieldPoints);
    public float HealthRegenRatePercent => Mathf.Max(0f, healthRegenRatePercent);
    public float ShieldRegenRatePercent => Mathf.Max(0f, shieldRegenRatePercent);
    public float Speed => Mathf.Max(0f, speed);
    public int MaximumEnergy => Mathf.Max(0, maximumEnergy);

    public ShipMetaBasicStats Clone()
    {
        return new ShipMetaBasicStats
        {
            maximumHealthPoints = maximumHealthPoints,
            maximumShieldPoints = maximumShieldPoints,
            healthRegenRatePercent = healthRegenRatePercent,
            shieldRegenRatePercent = shieldRegenRatePercent,
            speed = speed,
            maximumEnergy = maximumEnergy
        };
    }

    public void Configure(
        float health,
        float shield,
        float healthRegen,
        float shieldRegen,
        float movementSpeed,
        int energy)
    {
        maximumHealthPoints = Mathf.Max(0f, health);
        maximumShieldPoints = Mathf.Max(0f, shield);
        healthRegenRatePercent = Mathf.Max(0f, healthRegen);
        shieldRegenRatePercent = Mathf.Max(0f, shieldRegen);
        speed = Mathf.Max(0f, movementSpeed);
        maximumEnergy = Mathf.Max(0, energy);
    }
}

public readonly struct ShipMetaRuntimeStats
{
    private readonly ShipMetaBasicStats basicStats;
    private readonly IReadOnlyList<ShipMetaContract> contracts;

    public float MaximumHealthPoints => basicStats != null
        ? basicStats.MaximumHealthPoints
        : 0f;
    public float MaximumShieldPoints => basicStats != null
        ? basicStats.MaximumShieldPoints
        : 0f;
    public float HealthRegenRatePercent => basicStats != null
        ? basicStats.HealthRegenRatePercent
        : 0f;
    public float ShieldRegenRatePercent => basicStats != null
        ? basicStats.ShieldRegenRatePercent
        : 0f;
    public float Speed => basicStats != null ? basicStats.Speed : 0f;
    public int MaximumEnergy => basicStats != null
        ? basicStats.MaximumEnergy
        : 0;

    public ShipMetaRuntimeStats(
        ShipMetaBasicStats basicStats,
        IReadOnlyList<ShipMetaContract> contracts)
    {
        this.basicStats = basicStats;
        this.contracts = contracts;
    }

    public bool TryGetContract<TContract>(out TContract contract)
        where TContract : ShipMetaContract
    {
        if (contracts != null)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                contract = contracts[index] as TContract;
                if (contract != null)
                    return true;
            }
        }

        contract = null;
        return false;
    }
}

[Serializable]
public abstract class ShipMetaContract
{
    public abstract string DisplayName { get; }
    public abstract ShipMetaContract Clone();
}

[Serializable]
public struct AbilityMetaRecoverySettings
{
    public UltimateAbilityMode Mode;
    public float Cooldown;
    public int MaxCharges;
    public float ToggleMaximumTime;
    public float ToggleTimeCostPerSecond;
    public float ToggleRechargeStartTime;
    public float ToggleRechargeDuration;
}

[Serializable]
public sealed class AbilityRecoveryShipMetaContract : ShipMetaContract
{
    [SerializeField] private UltimateAbilityMode abilityMode;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField, Min(1)] private int maxCharges = 1;
    [SerializeField, Min(0.01f)] private float toggleMaximumTime = 3f;
    [SerializeField, Min(0.01f)] private float toggleTimeCostPerSecond = 1f;
    [SerializeField, Min(0f)] private float toggleRechargeStartTime;
    [SerializeField, Min(0.01f)] private float toggleRechargeDuration = 3f;

    public override string DisplayName => "Ability Recovery";
    public UltimateAbilityMode AbilityMode => abilityMode;

    public AbilityMetaRecoverySettings Settings => new()
    {
        Mode = abilityMode,
        Cooldown = Mathf.Max(0f, cooldown),
        MaxCharges = Mathf.Max(1, maxCharges),
        ToggleMaximumTime = Mathf.Max(0.01f, toggleMaximumTime),
        ToggleTimeCostPerSecond = Mathf.Max(0.01f, toggleTimeCostPerSecond),
        ToggleRechargeStartTime = Mathf.Max(0f, toggleRechargeStartTime),
        ToggleRechargeDuration = Mathf.Max(0.01f, toggleRechargeDuration)
    };

    public void Configure(AbilityMetaRecoverySettings settings)
    {
        abilityMode = settings.Mode;
        cooldown = Mathf.Max(0f, settings.Cooldown);
        maxCharges = Mathf.Max(1, settings.MaxCharges);
        toggleMaximumTime = Mathf.Max(0.01f, settings.ToggleMaximumTime);
        toggleTimeCostPerSecond = Mathf.Max(
            0.01f,
            settings.ToggleTimeCostPerSecond);
        toggleRechargeStartTime = Mathf.Max(0f, settings.ToggleRechargeStartTime);
        toggleRechargeDuration = Mathf.Max(0.01f, settings.ToggleRechargeDuration);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new AbilityRecoveryShipMetaContract();
        clone.Configure(Settings);
        return clone;
    }
}

[Serializable]
public sealed class BlackHoleShipMetaContract : ShipMetaContract, IEnemyProjectileDestructionAbility
{
    [SerializeField, Min(0.01f)] private float activeDuration = 5f;
    [SerializeField, Min(0f)] private float activeCooldown = 5f;
    [SerializeField, Range(0.01f, 1f)] private float frontSpeedMultiplier = 0.2f;
    [SerializeField, Min(1f)] private float rearSpeedMultiplier = 2.5f;
    [SerializeField] private bool rewriteAttackSpeed = true;
    [SerializeField] private bool enablePassiveAbility = true;
    public float ActiveDuration => Mathf.Max(0.01f, activeDuration);
    public float ActiveCooldown => Mathf.Max(0f, activeCooldown);
    public float FrontSpeedMultiplier => Mathf.Clamp(frontSpeedMultiplier, 0.01f, 1f);
    public float RearSpeedMultiplier => Mathf.Max(1f, rearSpeedMultiplier);
    public bool RewriteAttackSpeed => rewriteAttackSpeed;
    public bool EnablePassiveAbility => enablePassiveAbility;

    public void ConfigureActive(float duration, float cooldown, float front, float rear, bool rewriteAttack, bool enablePassive)
    {
        activeDuration = Mathf.Max(0.01f, duration);
        activeCooldown = Mathf.Max(0f, cooldown);
        frontSpeedMultiplier = Mathf.Clamp(front, 0.01f, 1f);
        rearSpeedMultiplier = Mathf.Max(1f, rear);
        rewriteAttackSpeed = rewriteAttack;
        enablePassiveAbility = enablePassive;
    }

    [SerializeField] private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Min(0f)] private float damage = 50f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;
    [SerializeField, Min(0.01f)] private float projectileSlowRadius = 3f;
    [SerializeField, Range(0.01f, 1f)]
    private float minimumProjectileSpeedMultiplier = 0.1f;
    [SerializeField, Range(0f, 170f)] private float frontFullEffectAngle = 30f;
    [SerializeField, Range(0.01f, 90f)] private float frontTransitionAngle = 30f;
    [SerializeField, Range(0f, 170f)] private float backFullEffectAngle = 30f;
    [SerializeField, Range(0.01f, 90f)] private float backTransitionAngle = 30f;
    [SerializeField, Min(1f)] private float maximumProjectileSpeedMultiplier = 1.35f;
    [SerializeField, Min(0.01f)] private float distanceFalloff = 1f;

    public override string DisplayName => "Black Hole";
    public float Duration => Mathf.Max(0.01f, duration);
    public float Damage => Mathf.Max(0f, damage);
    public EnemyDamageType DamageType => damageType;
    public float ProjectileSlowRadius => Mathf.Max(0.01f, projectileSlowRadius);
    public float FrontFullEffectAngle => Mathf.Clamp(frontFullEffectAngle, 0f, 170f);
    public float FrontTransitionAngle => Mathf.Clamp(frontTransitionAngle, 0.01f, 90f - FrontFullEffectAngle * 0.5f);
    public float BackFullEffectAngle => Mathf.Clamp(backFullEffectAngle, 0f, 170f);
    public float BackTransitionAngle => Mathf.Clamp(backTransitionAngle, 0.01f, 90f - BackFullEffectAngle * 0.5f);
    public float MaximumProjectileSpeedMultiplier => Mathf.Max(1f, maximumProjectileSpeedMultiplier);
    public float DistanceFalloff => Mathf.Max(0.01f, distanceFalloff);
    public float MinimumProjectileSpeedMultiplier => Mathf.Clamp(
        minimumProjectileSpeedMultiplier,
        0.01f,
        1f);

    public void Configure(float valueDuration, float valueDamage,
        float valueRadius, float valueMinimumSpeedMultiplier)
    {
        duration = Mathf.Max(0.01f, valueDuration);
        damage = Mathf.Max(0f, valueDamage);
        projectileSlowRadius = Mathf.Max(0.01f, valueRadius);
        minimumProjectileSpeedMultiplier = Mathf.Clamp(
            valueMinimumSpeedMultiplier,
            0.01f,
            1f);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new BlackHoleShipMetaContract();
        clone.ConfigureActive(ActiveDuration, ActiveCooldown, FrontSpeedMultiplier, RearSpeedMultiplier,
            RewriteAttackSpeed, EnablePassiveAbility);
        clone.projectileDestructionPolicy = projectileDestructionPolicy;
        clone.damageType = damageType;
        clone.Configure(Duration, Damage, ProjectileSlowRadius,
            MinimumProjectileSpeedMultiplier);
        clone.ConfigureFieldShape(FrontFullEffectAngle, FrontTransitionAngle,
            BackFullEffectAngle, BackTransitionAngle,
            MaximumProjectileSpeedMultiplier, DistanceFalloff);
        return clone;
    }

    public void ConfigureFieldShape(float frontFullAngle, float frontTransition,
        float backFullAngle, float backTransition, float maximumSpeedMultiplier,
        float falloff)
    {
        frontFullEffectAngle = Mathf.Clamp(frontFullAngle, 0f, 170f);
        frontTransitionAngle = Mathf.Clamp(frontTransition, 0.01f, 90f - frontFullEffectAngle * 0.5f);
        backFullEffectAngle = Mathf.Clamp(backFullAngle, 0f, 170f);
        backTransitionAngle = Mathf.Clamp(backTransition, 0.01f, 90f - backFullEffectAngle * 0.5f);
        maximumProjectileSpeedMultiplier = Mathf.Max(1f, maximumSpeedMultiplier);
        distanceFalloff = Mathf.Max(0.01f, falloff);
    }

    public EnemyProjectileSlowField CreateProjectileField(
        Unity.Mathematics.float2 center, Unity.Mathematics.float2 forward)
    {
        return new EnemyProjectileSlowField
        {
            Center = center,
            Forward = Unity.Mathematics.math.normalizesafe(forward, new Unity.Mathematics.float2(0f, 1f)),
            Radius = ProjectileSlowRadius,
            MinimumSpeedMultiplier = MinimumProjectileSpeedMultiplier,
            MaximumSpeedMultiplier = MaximumProjectileSpeedMultiplier,
            FrontFullEffectCos = Mathf.Cos(FrontFullEffectAngle * 0.5f * Mathf.Deg2Rad),
            FrontOuterCos = Mathf.Cos((FrontFullEffectAngle * 0.5f + FrontTransitionAngle) * Mathf.Deg2Rad),
            BackFullEffectCos = Mathf.Cos(BackFullEffectAngle * 0.5f * Mathf.Deg2Rad),
            BackOuterCos = Mathf.Cos((BackFullEffectAngle * 0.5f + BackTransitionAngle) * Mathf.Deg2Rad),
            DistanceFalloff = DistanceFalloff
        };
    }
}

[Serializable]
public sealed class BladeShipMetaContract : ShipMetaContract, IEnemyProjectileDestructionAbility
{
    [SerializeField] private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    [SerializeField, Min(0.05f)] private float recordingDuration = 1f;
    [SerializeField, Min(0.01f)] private float sampleInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float width = 0.45f;
    [SerializeField, Min(0.01f)] private float speed = 12f;
    [SerializeField, Min(0f)] private float damage = 50f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;
    [SerializeField, Min(0.01f)] private float lifetime = 4f;
    [SerializeField, Min(0.01f)] private float passiveSpeedDivisor = 40f;
    [SerializeField, Min(0f)] private float passiveSpeedMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float maximumReloadMultiplier = 2f;
    [SerializeField, Min(0f)] private float minimumReloadMultiplier = 0.25f;

    public override string DisplayName => "Blade";
    public float RecordingDuration => Mathf.Max(0.05f, recordingDuration);
    public float SampleInterval => Mathf.Max(0.01f, sampleInterval);
    public float Width => Mathf.Max(0.01f, width);
    public float Speed => Mathf.Max(0.01f, speed);
    public float Damage => Mathf.Max(0f, damage);
    public EnemyDamageType DamageType => damageType;
    public float Lifetime => Mathf.Max(0.01f, lifetime);
    public float PassiveSpeedDivisor => Mathf.Max(0.01f, passiveSpeedDivisor);
    public float PassiveSpeedMultiplier => Mathf.Max(0f, passiveSpeedMultiplier);
    public float MaximumReloadMultiplier => Mathf.Max(0f, maximumReloadMultiplier);
    public float MinimumReloadMultiplier => Mathf.Max(0f, minimumReloadMultiplier);

    public void Configure(float valueRecordingDuration,
        float valueSampleInterval, float valueWidth, float valueSpeed,
        float valueDamage, float valueLifetime, float valuePassiveSpeedDivisor,
        float valuePassiveSpeedMultiplier, float valueMaximumReloadMultiplier,
        float valueMinimumReloadMultiplier)
    {
        recordingDuration = Mathf.Max(0.05f, valueRecordingDuration);
        sampleInterval = Mathf.Max(0.01f, valueSampleInterval);
        width = Mathf.Max(0.01f, valueWidth);
        speed = Mathf.Max(0.01f, valueSpeed);
        damage = Mathf.Max(0f, valueDamage);
        lifetime = Mathf.Max(0.01f, valueLifetime);
        passiveSpeedDivisor = Mathf.Max(0.01f, valuePassiveSpeedDivisor);
        passiveSpeedMultiplier = Mathf.Max(0f, valuePassiveSpeedMultiplier);
        maximumReloadMultiplier = Mathf.Max(0f, valueMaximumReloadMultiplier);
        minimumReloadMultiplier = Mathf.Max(0f, valueMinimumReloadMultiplier);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new BladeShipMetaContract();
        clone.projectileDestructionPolicy = projectileDestructionPolicy;
        clone.damageType = damageType;
        clone.Configure(RecordingDuration, SampleInterval, Width, Speed,
            Damage, Lifetime, PassiveSpeedDivisor, PassiveSpeedMultiplier,
            MaximumReloadMultiplier, MinimumReloadMultiplier);
        return clone;
    }
}

[Serializable]
public sealed class HeavyShieldShipMetaContract : ShipMetaContract
{
    [SerializeField, HideInInspector, Min(0f)] private float shieldHealthMultiplier = 1f;
    [SerializeField, Min(0f)] private float extraHealthAmount = 1f;

    public override string DisplayName => "Heavy Extra Health";
    public float ShieldHealthMultiplier => Mathf.Max(0f, shieldHealthMultiplier);
    public float ExtraHealthAmount => Mathf.Max(0f, extraHealthAmount);

    public void Configure(float multiplier)
    {
        shieldHealthMultiplier = Mathf.Max(0f, multiplier);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new HeavyShieldShipMetaContract();
        clone.Configure(ShieldHealthMultiplier);
        clone.extraHealthAmount = ExtraHealthAmount;
        return clone;
    }
}

[Serializable]
public sealed class ArkanoidShipMetaContract : ShipMetaContract, IEnemyProjectileDestructionAbility
{
    [SerializeField] private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    [SerializeField, Min(0.01f)] private float paddleFollowSpeed = 30f;
    [SerializeField, Min(0f)] private float paddleAttractionRadius = 2f;
    [SerializeField, Min(0f)] private float paddleAttractionForce = 10f;
    [SerializeField, Min(0.01f)] private float ballSpeed = 5f;
    [SerializeField, Min(1f)] private float paddleBounceSpeedMultiplier = 1.5f;
    [SerializeField, Min(0.01f)] private float paddleBounceRecoveryDuration = 1f;
    [SerializeField] private AnimationCurve paddleBounceRecoveryCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField, Min(0f)] private float ballDamage = 10f;
    [SerializeField] private EnemyDamageType ballDamageType = EnemyDamageType.Default;
    [SerializeField, Min(0.05f)] private float stasisDuration = 3f;
    [SerializeField, Min(1f)] private float stasisRadiusMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float stasisDamagePerSecond = 30f;
    [SerializeField] private EnemyDamageType stasisDamageType = EnemyDamageType.Default;
    [SerializeField, Min(0.02f)] private float stasisTickInterval = 0.1f;

    public override string DisplayName => "Arkanoid Ball and Stasis";
    public float PaddleFollowSpeed => Mathf.Max(0.01f, paddleFollowSpeed);
    public float PaddleAttractionRadius => Mathf.Max(0f, paddleAttractionRadius);
    public float PaddleAttractionForce => Mathf.Max(0f, paddleAttractionForce);
    public float BallSpeed => Mathf.Max(0.01f, ballSpeed);
    public float PaddleBounceSpeedMultiplier => Mathf.Max(1f, paddleBounceSpeedMultiplier);
    public float PaddleBounceRecoveryDuration => Mathf.Max(0.01f, paddleBounceRecoveryDuration);
    public float EvaluatePaddleBounceSpeedMultiplier(float elapsed)
    {
        if (elapsed >= PaddleBounceRecoveryDuration) return 1f;
        if (elapsed <= 0f) return PaddleBounceSpeedMultiplier;
        float progress = Mathf.Clamp01(elapsed / PaddleBounceRecoveryDuration);
        float remaining = paddleBounceRecoveryCurve != null && paddleBounceRecoveryCurve.length > 0
            ? Mathf.Clamp01(paddleBounceRecoveryCurve.Evaluate(progress)) : 1f - progress;
        return 1f + (PaddleBounceSpeedMultiplier - 1f) * remaining;
    }
    public float BallDamage => Mathf.Max(0f, ballDamage);
    public EnemyDamageType BallDamageType => ballDamageType;
    public EnemyDamageType StasisDamageType => stasisDamageType;
    public float StasisDuration => Mathf.Max(0.05f, stasisDuration);
    public float StasisRadiusMultiplier => Mathf.Max(1f, stasisRadiusMultiplier);
    public float StasisDamagePerSecond => Mathf.Max(0f, stasisDamagePerSecond);
    public float StasisTickInterval => Mathf.Max(0.02f, stasisTickInterval);

    public void Configure(float valuePaddleFollowSpeed, float valueBallSpeed,
        float valueBallDamage, float valueStasisDuration,
        float valueStasisRadiusMultiplier, float valueStasisDamagePerSecond,
        float valueStasisTickInterval)
    {
        paddleFollowSpeed = Mathf.Max(0.01f, valuePaddleFollowSpeed);
        ballSpeed = Mathf.Max(0.01f, valueBallSpeed);
        ballDamage = Mathf.Max(0f, valueBallDamage);
        stasisDuration = Mathf.Max(0.05f, valueStasisDuration);
        stasisRadiusMultiplier = Mathf.Max(1f, valueStasisRadiusMultiplier);
        stasisDamagePerSecond = Mathf.Max(0f, valueStasisDamagePerSecond);
        stasisTickInterval = Mathf.Max(0.02f, valueStasisTickInterval);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new ArkanoidShipMetaContract();
        clone.paddleAttractionRadius = paddleAttractionRadius;
        clone.paddleAttractionForce = paddleAttractionForce;
        clone.projectileDestructionPolicy = projectileDestructionPolicy;
        clone.ballDamageType = ballDamageType;
        clone.stasisDamageType = stasisDamageType;
        clone.paddleBounceSpeedMultiplier = paddleBounceSpeedMultiplier;
        clone.paddleBounceRecoveryDuration = paddleBounceRecoveryDuration;
        clone.paddleBounceRecoveryCurve = paddleBounceRecoveryCurve == null ? null
            : new AnimationCurve(paddleBounceRecoveryCurve.keys)
            {
                preWrapMode = paddleBounceRecoveryCurve.preWrapMode,
                postWrapMode = paddleBounceRecoveryCurve.postWrapMode
            };
        clone.Configure(PaddleFollowSpeed, BallSpeed, BallDamage,
            StasisDuration, StasisRadiusMultiplier, StasisDamagePerSecond,
            StasisTickInterval);
        return clone;
    }
}

[Serializable]
public sealed class DictatorTurretSpawnGroup
{
    [SerializeField] private Vector2 localPosition;
    [SerializeField, Min(1)] private int count = 1;
    [SerializeField, Min(0f)] private float horizontalSpacing = 0.45f;

    public Vector2 LocalPosition => localPosition;
    public int Count => Mathf.Max(1, count);
    public float HorizontalSpacing => Mathf.Max(0f, horizontalSpacing);

    public DictatorTurretSpawnGroup()
    {
    }

    public DictatorTurretSpawnGroup(
        Vector2 position,
        int turretCount = 1,
        float spacing = 0.45f)
    {
        localPosition = position;
        count = Mathf.Max(1, turretCount);
        horizontalSpacing = Mathf.Max(0f, spacing);
    }

    public DictatorTurretSpawnGroup Clone()
    {
        return new DictatorTurretSpawnGroup(
            LocalPosition,
            Count,
            HorizontalSpacing);
    }
}

[Serializable]
public sealed class DictatorShipMetaContract : ShipMetaContract
{
    [Header("Turrets")]
    [SerializeField, Min(0.05f)] private float turretLifetime = 8f;
    [SerializeField] private Projectile turretProjectilePrefab;
    [SerializeField] private EnemyDamageType turretDamageType =
        EnemyDamageType.Kinetick;
    [SerializeField, Min(0f)] private float turretProjectileDamage = 10f;
    [SerializeField, Min(0.01f)] private float turretReloadTime = 0.5f;
    [SerializeField, Min(0.01f)] private float turretProjectileSpeed = 8f;
    [SerializeField, Min(0.01f)] private float turretTargetingRange = 10f;
    [SerializeField] private List<DictatorTurretSpawnGroup> turretSpawnGroups =
        new()
        {
            new DictatorTurretSpawnGroup(new Vector2(-0.75f, 0f)),
            new DictatorTurretSpawnGroup(new Vector2(0.75f, 0f))
        };

    [Header("Passive Damage Bonus")]
    [SerializeField, Min(0f)] private float kineticDamageBonusPercent = 15f;
    [SerializeField, HideInInspector, Min(0f)] private float explosionDamageBonusPercent;

    public override string DisplayName => "Dictator Turrets and Damage Passive";
    public float TurretLifetime => Mathf.Max(0.05f, turretLifetime);
    public Projectile TurretProjectilePrefab => turretProjectilePrefab;
    public EnemyDamageType TurretDamageType => turretDamageType;
    public float TurretProjectileDamage => Mathf.Max(0f, turretProjectileDamage);
    public float TurretReloadTime => Mathf.Max(0.01f, turretReloadTime);
    public float TurretProjectileSpeed => Mathf.Max(0.01f, turretProjectileSpeed);
    public float TurretTargetingRange => Mathf.Max(0.01f, turretTargetingRange);
    public IReadOnlyList<DictatorTurretSpawnGroup> TurretSpawnGroups =>
        turretSpawnGroups;
    public float KineticDamageBonusPercent =>
        Mathf.Max(0f, Mathf.Max(kineticDamageBonusPercent, explosionDamageBonusPercent));
    public float ExplosionDamageBonusPercent =>
        Mathf.Max(0f, explosionDamageBonusPercent);

    public void Configure(
        Projectile projectilePrefab,
        EnemyDamageType damageType,
        float lifetime,
        float projectileDamage,
        float reloadTime,
        float projectileSpeed,
        float targetingRange,
        IReadOnlyList<DictatorTurretSpawnGroup> spawnGroups,
        float kineticBonusPercent,
        float explosionBonusPercent)
    {
        turretProjectilePrefab = projectilePrefab;
        turretDamageType = damageType;
        turretLifetime = Mathf.Max(0.05f, lifetime);
        turretProjectileDamage = Mathf.Max(0f, projectileDamage);
        turretReloadTime = Mathf.Max(0.01f, reloadTime);
        turretProjectileSpeed = Mathf.Max(0.01f, projectileSpeed);
        turretTargetingRange = Mathf.Max(0.01f, targetingRange);
        kineticDamageBonusPercent = Mathf.Max(0f, kineticBonusPercent);
        explosionDamageBonusPercent = Mathf.Max(0f, explosionBonusPercent);

        turretSpawnGroups = new List<DictatorTurretSpawnGroup>();
        if (spawnGroups == null)
            return;

        for (int index = 0; index < spawnGroups.Count; index++)
        {
            DictatorTurretSpawnGroup group = spawnGroups[index];
            if (group != null)
                turretSpawnGroups.Add(group.Clone());
        }
    }

    public override ShipMetaContract Clone()
    {
        var clone = new DictatorShipMetaContract();
        clone.Configure(
            TurretProjectilePrefab,
            TurretDamageType,
            TurretLifetime,
            TurretProjectileDamage,
            TurretReloadTime,
            TurretProjectileSpeed,
            TurretTargetingRange,
            TurretSpawnGroups,
            KineticDamageBonusPercent,
            ExplosionDamageBonusPercent);
        return clone;
    }
}

[Serializable]
public sealed class PrismShipMetaContract : ShipMetaContract, IEnemyProjectileDestructionAbility
{
    [Header("Active Energy Wave")]
    [SerializeField] private GameObject wavePrefab;
    [SerializeField, Min(0f)] private float spawnOffset = 1f;
    [SerializeField, Min(0.01f)] private float waveWidth = 4f;
    [SerializeField, Min(0.01f)] private float waveThickness = .3f;
    [SerializeField, Min(0.01f)] private float waveSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float maxDistance = 15f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Plazma;
    [SerializeField] private bool destroyProjectiles;
    [SerializeField] private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly;
    [Header("Active Plasma")]
    [FormerlySerializedAs("beamDamage")]
    [SerializeField, Min(0f)] private float plasmaDamage = 150f;

    [Header("Passive Damage Bonus")]
    [FormerlySerializedAs("beamDamageBonusPercent")]
    [SerializeField, Min(0f)] private float plasmaDamageBonusPercent = 15f;
    [FormerlySerializedAs("energyDamageBonusPercent")]
    [SerializeField, HideInInspector, Min(0f)] private float electricDamageBonusPercent;

    public override string DisplayName => "Prism Energy Wave and Damage Passive";
    public GameObject WavePrefab => wavePrefab;
    public float SpawnOffset => Mathf.Max(0f, spawnOffset);
    public float WaveWidth => Mathf.Max(.01f, waveWidth);
    public float WaveThickness => Mathf.Max(.01f, waveThickness);
    public float WaveSpeed => Mathf.Max(.01f, waveSpeed);
    public float MaxDistance => Mathf.Max(.01f, maxDistance);
    public float Damage => PlasmaDamage;
    public EnemyDamageType DamageType => damageType;
    public bool DestroyProjectiles => destroyProjectiles;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    public float PlasmaDamage => Mathf.Max(0f, plasmaDamage);
    public float PlasmaDamageBonusPercent =>
        Mathf.Max(0f, Mathf.Max(plasmaDamageBonusPercent, electricDamageBonusPercent));
    public float ElectricDamageBonusPercent =>
        Mathf.Max(0f, electricDamageBonusPercent);

    public void ConfigureWave(GameObject prefab, float offset, float width, float thickness,
        float speed, float distance, EnemyDamageType type, bool destroy,
        EnemyProjectileDestructionPolicy policy = EnemyProjectileDestructionPolicy.FragileOnly)
    {
        wavePrefab = prefab; spawnOffset = Mathf.Max(0f, offset);
        waveWidth = Mathf.Max(.01f, width); waveThickness = Mathf.Max(.01f, thickness);
        waveSpeed = Mathf.Max(.01f, speed); maxDistance = Mathf.Max(.01f, distance);
        damageType = type; destroyProjectiles = destroy; projectileDestructionPolicy = policy;
    }

    public void Configure(
        float activePlasmaDamage,
        float plasmaBonusPercent,
        float electricBonusPercent)
    {
        plasmaDamage = Mathf.Max(0f, activePlasmaDamage);
        plasmaDamageBonusPercent = Mathf.Max(0f, plasmaBonusPercent);
        electricDamageBonusPercent = Mathf.Max(0f, electricBonusPercent);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new PrismShipMetaContract();
        clone.Configure(
            PlasmaDamage,
            PlasmaDamageBonusPercent,
            ElectricDamageBonusPercent);
        clone.ConfigureWave(WavePrefab, SpawnOffset, WaveWidth, WaveThickness,
            WaveSpeed, MaxDistance, DamageType, DestroyProjectiles, ProjectileDestructionPolicy);
        return clone;
    }
}

[Serializable]
public sealed class PhantomShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0.05f)] private float phaseDuration = 3f;
    [SerializeField, Min(0f)] private float purgeRadius = 2.5f;

    public override string DisplayName => "Phantom Phase";
    public float PhaseDuration => Mathf.Max(0.05f, phaseDuration);
    public float PurgeRadius => Mathf.Max(0f, purgeRadius);

    public void Configure(float duration, float radius)
    {
        phaseDuration = Mathf.Max(0.05f, duration);
        purgeRadius = Mathf.Max(0f, radius);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new PhantomShipMetaContract();
        clone.Configure(PhaseDuration, PurgeRadius);
        return clone;
    }
}

[Serializable]
public sealed class HuskarShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0f)] private float healFromDamagePercent = 0.2f;
    [SerializeField, Min(0.01f)] private float healDuration = 5f;
    [SerializeField, Min(0f)] private float missingHealthFireRateBonus = 0.5f;

    public override string DisplayName => "Huskar Blood Pact";
    public float HealFromDamagePercent => Mathf.Max(0f, healFromDamagePercent);
    public float HealDuration => Mathf.Max(0.01f, healDuration);
    public float MissingHealthFireRateBonus => Mathf.Max(
        0f,
        missingHealthFireRateBonus);

    public void Configure(float healPercent, float duration,
        float fireRateBonus)
    {
        healFromDamagePercent = Mathf.Max(0f, healPercent);
        healDuration = Mathf.Max(0.01f, duration);
        missingHealthFireRateBonus = Mathf.Max(0f, fireRateBonus);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new HuskarShipMetaContract();
        clone.Configure(HealFromDamagePercent, HealDuration,
            MissingHealthFireRateBonus);
        return clone;
    }
}

[Serializable]
public sealed class ShipMetaLevel
{
    [SerializeField] private ShipMetaBasicStats basicStats = new();
    [SerializeReference] private List<ShipMetaContract> contracts = new();

    public ShipMetaBasicStats BasicStats => basicStats;
    public IReadOnlyList<ShipMetaContract> Contracts => contracts;

    public bool TryGetContract<TContract>(out TContract contract)
        where TContract : ShipMetaContract
    {
        if (contracts != null)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                contract = contracts[index] as TContract;
                if (contract != null)
                    return true;
            }
        }

        contract = null;
        return false;
    }

    public ShipMetaLevel Clone()
    {
        var clone = new ShipMetaLevel
        {
            basicStats = basicStats != null
                ? basicStats.Clone()
                : new ShipMetaBasicStats()
        };

        if (contracts == null)
            return clone;

        for (int index = 0; index < contracts.Count; index++)
        {
            ShipMetaContract contract = contracts[index];
            if (contract != null)
                clone.contracts.Add(contract.Clone());
        }

        return clone;
    }

    public bool ContainsContract(Type contractType)
    {
        if (contractType == null || contracts == null)
            return false;

        for (int index = 0; index < contracts.Count; index++)
        {
            if (contracts[index] != null
                && contracts[index].GetType() == contractType)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAddContract(ShipMetaContract contract)
    {
        if (contract == null || ContainsContract(contract.GetType()))
            return false;

        contracts ??= new List<ShipMetaContract>();
        contracts.Add(contract);
        return true;
    }

    public bool TryRemoveContract(Type contractType)
    {
        if (contractType == null || contracts == null)
            return false;

        bool removed = false;
        for (int index = contracts.Count - 1; index >= 0; index--)
        {
            if (contracts[index] == null
                || contracts[index].GetType() != contractType)
            {
                continue;
            }

            contracts.RemoveAt(index);
            removed = true;
        }

        return removed;
    }
}

[CreateAssetMenu(fileName = "ShipMetaConfig", menuName = "Game/Ship Meta Config")]
public sealed class ShipMetaConfig : ScriptableObject
{
    [SerializeField] private ShipData sourceShipData;
    [SerializeField] private ParentShip sourceHullPrefab;
    [SerializeField, Min(1)] private int maxLevels = 1;
    [SerializeField] private List<ShipMetaLevel> levels = new()
    {
        new ShipMetaLevel()
    };

    public ShipData SourceShipData => sourceShipData;
    public ParentShip SourceHullPrefab => sourceHullPrefab;
    public IReadOnlyList<ShipMetaLevel> Levels => levels;
    public int MaxLevels => Mathf.Max(1, maxLevels);
    public int LevelCount => Mathf.Max(1, levels?.Count ?? 0);

    public ShipMetaLevel GetLevel(int requestedLevel)
    {
        EnsureLevelZero();
        int level = Mathf.Clamp(requestedLevel, 0, levels.Count - 1);
        return levels[level];
    }

    public ShipMetaRuntimeStats GetRuntimeStats(int requestedLevel)
    {
        ShipMetaLevel level = GetLevel(requestedLevel);
        return new ShipMetaRuntimeStats(level.BasicStats, level.Contracts);
    }

    public void InitializeFromSource(ShipData data, ParentShip hullPrefab)
    {
        sourceShipData = data;
        sourceHullPrefab = hullPrefab;
        maxLevels = 1;
        levels = new List<ShipMetaLevel> { new ShipMetaLevel() };
        GetLevel(0).BasicStats.Configure(
            data != null ? data.maximumHealthPoints : 0f,
            data != null ? data.maximumShieldPoints : 0f,
            data != null ? data.HealthRegenRatePercent : 0f,
            data != null ? data.ShieldRegenRatePercent : 0f,
            data != null ? data.speed : 0f,
            data != null ? data.maximumEnergy : 0);
    }

    public void SetMaxLevels(int requestedMaxLevels)
    {
        EnsureLevelZero();
        int count = Mathf.Max(1, requestedMaxLevels);
        while (levels.Count > count)
            levels.RemoveAt(levels.Count - 1);

        while (levels.Count < count)
            levels.Add(levels[levels.Count - 1].Clone());

        maxLevels = count;
    }

    public void AddLevelCopyingPrevious()
    {
        SetMaxLevels(levels.Count + 1);
    }

    public bool TryRemoveLevel(int levelIndex)
    {
        EnsureLevelZero();
        if (levelIndex <= 0 || levelIndex >= levels.Count)
            return false;

        levels.RemoveAt(levelIndex);
        maxLevels = levels.Count;
        return true;
    }

    public List<Type> GetSupportedContractTypes()
    {
        var types = new List<Type>();
        if (sourceHullPrefab == null)
            return types;

        if (sourceHullPrefab.ActiveAbility != null)
            types.Add(typeof(AbilityRecoveryShipMetaContract));

        if (sourceHullPrefab.ActiveAbility is BlackHoleActive
            || sourceHullPrefab.PassiveAbility is BlackHolePassive)
        {
            types.Add(typeof(BlackHoleShipMetaContract));
        }

        if (sourceHullPrefab.ActiveAbility is CreateBladeAbility)
            types.Add(typeof(BladeShipMetaContract));
        if (sourceHullPrefab.ActiveAbility is ShieldCreateAbility)
            types.Add(typeof(HeavyShieldShipMetaContract));
        if (sourceHullPrefab.ActiveAbility is ArkanoidStasisActiveAbility
            || sourceHullPrefab.PassiveAbility is ArkanoidPassiveAbility)
        {
            types.Add(typeof(ArkanoidShipMetaContract));
        }

        if (sourceHullPrefab.PassiveAbility is OrbitPassiveAbility)
            types.Add(typeof(OrbitShipMetaContract));

        if (sourceHullPrefab.ActiveAbility is DictatorTurretActiveAbility
            || sourceHullPrefab.PassiveAbility is DictatorDamagePassiveAbility)
        {
            types.Add(typeof(DictatorShipMetaContract));
        }

        if (sourceHullPrefab.ActiveAbility is PrismBeamActiveAbility
            || sourceHullPrefab.PassiveAbility is PrismDamagePassiveAbility)
        {
            types.Add(typeof(PrismShipMetaContract));
        }

        if (sourceHullPrefab.ActiveAbility is PhantomPhaseActiveAbility
            || sourceHullPrefab.PassiveAbility is PhantomProjectilePurgePassive)
        {
            types.Add(typeof(PhantomShipMetaContract));
        }

        if (sourceHullPrefab.ActiveAbility is MissingHealthActive
            || sourceHullPrefab.PassiveAbility is MissingHealthPassive)
        {
            types.Add(typeof(HuskarShipMetaContract));
        }

        return types;
    }

    public bool TryAddContractToAllLevels(Type contractType)
    {
        if (contractType == null || !GetSupportedContractTypes().Contains(contractType))
            return false;

        ShipMetaContract template = CreateContract(contractType);
        if (template == null || GetLevel(0).ContainsContract(contractType))
            return false;

        for (int index = 0; index < levels.Count; index++)
        {
            if (levels[index] == null)
                levels[index] = new ShipMetaLevel();

            levels[index].TryAddContract(template.Clone());
        }

        return true;
    }

    public bool TryRemoveContractFromAllLevels(Type contractType)
    {
        bool removed = false;
        for (int index = 0; index < levels.Count; index++)
        {
            if (levels[index] != null)
                removed |= levels[index].TryRemoveContract(contractType);
        }

        return removed;
    }

    private ShipMetaContract CreateContract(Type contractType)
    {
        if (contractType == typeof(AbilityRecoveryShipMetaContract))
        {
            var recovery = new AbilityRecoveryShipMetaContract();
            if (sourceHullPrefab?.ActiveAbility != null)
            {
                recovery.Configure(
                    sourceHullPrefab.ActiveAbility.ExportMetaRecoverySettings());
            }

            return recovery;
        }
        if (contractType == typeof(BlackHoleShipMetaContract))
            return new BlackHoleShipMetaContract();
        if (contractType == typeof(BladeShipMetaContract))
            return new BladeShipMetaContract();
        if (contractType == typeof(HeavyShieldShipMetaContract))
            return new HeavyShieldShipMetaContract();
        if (contractType == typeof(ArkanoidShipMetaContract))
            return new ArkanoidShipMetaContract();
        if (contractType == typeof(OrbitShipMetaContract))
            return new OrbitShipMetaContract();
        if (contractType == typeof(DictatorShipMetaContract))
        {
            if (sourceHullPrefab?.ActiveAbility
                is DictatorTurretActiveAbility dictatorAbility)
            {
                return dictatorAbility.CreateDefaultMetaContract();
            }

            return new DictatorShipMetaContract();
        }
        if (contractType == typeof(PrismShipMetaContract))
        {
            if (sourceHullPrefab?.ActiveAbility
                is PrismBeamActiveAbility prismAbility)
            {
                return prismAbility.CreateDefaultMetaContract();
            }

            return new PrismShipMetaContract();
        }
        if (contractType == typeof(PhantomShipMetaContract))
            return new PhantomShipMetaContract();
        if (contractType == typeof(HuskarShipMetaContract))
            return new HuskarShipMetaContract();

        return null;
    }

    private void EnsureLevelZero()
    {
        if (levels == null)
            levels = new List<ShipMetaLevel>();
        if (levels.Count == 0)
            levels.Add(new ShipMetaLevel());
        if (levels[0] == null)
            levels[0] = new ShipMetaLevel();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetMaxLevels(maxLevels);
    }
#endif
}
