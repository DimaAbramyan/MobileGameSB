using System;
using System.Collections.Generic;
using UnityEngine;

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
public sealed class BlackHoleShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField, Min(0f)] private float damage = 50f;
    [SerializeField, Min(0.01f)] private float projectileSlowRadius = 3f;
    [SerializeField, Range(0.01f, 1f)]
    private float minimumProjectileSpeedMultiplier = 0.1f;

    public override string DisplayName => "Black Hole";
    public float Duration => Mathf.Max(0.01f, duration);
    public float Damage => Mathf.Max(0f, damage);
    public float ProjectileSlowRadius => Mathf.Max(0.01f, projectileSlowRadius);
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
        clone.Configure(Duration, Damage, ProjectileSlowRadius,
            MinimumProjectileSpeedMultiplier);
        return clone;
    }
}

[Serializable]
public sealed class BladeShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0.05f)] private float recordingDuration = 1f;
    [SerializeField, Min(0.01f)] private float sampleInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float width = 0.45f;
    [SerializeField, Min(0.01f)] private float speed = 12f;
    [SerializeField, Min(0f)] private float damage = 50f;
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
        clone.Configure(RecordingDuration, SampleInterval, Width, Speed,
            Damage, Lifetime, PassiveSpeedDivisor, PassiveSpeedMultiplier,
            MaximumReloadMultiplier, MinimumReloadMultiplier);
        return clone;
    }
}

[Serializable]
public sealed class HeavyShieldShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0f)] private float shieldHealthMultiplier = 1f;

    public override string DisplayName => "Heavy Shield";
    public float ShieldHealthMultiplier => Mathf.Max(0f, shieldHealthMultiplier);

    public void Configure(float multiplier)
    {
        shieldHealthMultiplier = Mathf.Max(0f, multiplier);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new HeavyShieldShipMetaContract();
        clone.Configure(ShieldHealthMultiplier);
        return clone;
    }
}

[Serializable]
public sealed class ArkanoidShipMetaContract : ShipMetaContract
{
    [SerializeField, Min(0.01f)] private float paddleFollowSpeed = 30f;
    [SerializeField, Min(0.01f)] private float ballSpeed = 5f;
    [SerializeField, Min(0f)] private float ballDamage = 10f;
    [SerializeField, Min(0.05f)] private float stasisDuration = 3f;
    [SerializeField, Min(1f)] private float stasisRadiusMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float stasisDamagePerSecond = 30f;
    [SerializeField, Min(0.02f)] private float stasisTickInterval = 0.1f;

    public override string DisplayName => "Arkanoid Ball and Stasis";
    public float PaddleFollowSpeed => Mathf.Max(0.01f, paddleFollowSpeed);
    public float BallSpeed => Mathf.Max(0.01f, ballSpeed);
    public float BallDamage => Mathf.Max(0f, ballDamage);
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
        EnemyDamageType.Kinetic;
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
    [SerializeField, Min(0f)] private float explosionDamageBonusPercent = 10f;

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
        Mathf.Max(0f, kineticDamageBonusPercent);
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
public sealed class PrismShipMetaContract : ShipMetaContract
{
    [Header("Active Beam")]
    [SerializeField, Min(0f)] private float beamDamage = 150f;

    [Header("Passive Damage Bonus")]
    [SerializeField, Min(0f)] private float beamDamageBonusPercent = 15f;
    [SerializeField, Min(0f)] private float energyDamageBonusPercent = 10f;

    public override string DisplayName => "Prism Beam and Damage Passive";
    public float BeamDamage => Mathf.Max(0f, beamDamage);
    public float BeamDamageBonusPercent =>
        Mathf.Max(0f, beamDamageBonusPercent);
    public float EnergyDamageBonusPercent =>
        Mathf.Max(0f, energyDamageBonusPercent);

    public void Configure(
        float activeBeamDamage,
        float beamBonusPercent,
        float energyBonusPercent)
    {
        beamDamage = Mathf.Max(0f, activeBeamDamage);
        beamDamageBonusPercent = Mathf.Max(0f, beamBonusPercent);
        energyDamageBonusPercent = Mathf.Max(0f, energyBonusPercent);
    }

    public override ShipMetaContract Clone()
    {
        var clone = new PrismShipMetaContract();
        clone.Configure(
            BeamDamage,
            BeamDamageBonusPercent,
            EnergyDamageBonusPercent);
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
