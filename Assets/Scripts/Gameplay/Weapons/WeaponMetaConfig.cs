using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WeaponMetaBasicStats
{
    [SerializeField, Min(0f)] private float reloadTime = 1f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float range = 10f;
    [SerializeField, Min(0f)] private float projectileSpeed = 10f;
    [SerializeField] private float angle;
    [SerializeField, Tooltip(
        "Degrees relative to the weapon forward direction. Random angle offset is applied around this direction.")]
    private float initialDirectionAngle;

    public float ReloadTime => reloadTime;
    public float Damage => damage;
    public float Range => range;
    public float ProjectileSpeed => projectileSpeed;
    public float Angle => angle;
    public float InitialDirectionAngle => initialDirectionAngle;

    public WeaponMetaBasicStats Clone()
    {
        return new WeaponMetaBasicStats
        {
            reloadTime = reloadTime,
            damage = damage,
            range = range,
            projectileSpeed = projectileSpeed,
            angle = angle,
            initialDirectionAngle = initialDirectionAngle
        };
    }
}

[Serializable]
public sealed class WeaponProjectileSlot
{
    [SerializeField, HideInInspector] private string id;
    [SerializeField] private ProjectileData projectile;
    [SerializeField] private bool spawnedByAnotherProjectile;

    public string Id => id;
    public ProjectileData Projectile => projectile;
    public bool SpawnedByAnotherProjectile => spawnedByAnotherProjectile;

    public WeaponProjectileSlot()
    {
        EnsureId();
    }

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
    }

    public void RegenerateId()
    {
        id = Guid.NewGuid().ToString("N");
    }
}

[Serializable]
public abstract class WeaponMetaContract
{
    public abstract string DisplayName { get; }
    public abstract WeaponMetaContract Clone();
}

[Serializable]
public sealed class BurstFireWeaponMetaContract : WeaponMetaContract
{
    [SerializeField, Min(1)] private int volleysPerActivation = 1;
    [SerializeField, Min(1)] private int projectilesPerVolley = 1;
    [SerializeField, Min(0f)] private float delayBetweenVolleys;
    [SerializeField, Min(0f)] private float spreadAngle;

    public override string DisplayName => "Fire Bonuses";
    public int VolleysPerActivation => Mathf.Max(1, volleysPerActivation);
    public int ProjectilesPerVolley => Mathf.Max(1, projectilesPerVolley);
    public float DelayBetweenVolleys => Mathf.Max(0f, delayBetweenVolleys);
    public float SpreadAngle => Mathf.Max(0f, spreadAngle);

    public override WeaponMetaContract Clone()
    {
        return new BurstFireWeaponMetaContract
        {
            volleysPerActivation = volleysPerActivation,
            projectilesPerVolley = projectilesPerVolley,
            delayBetweenVolleys = delayBetweenVolleys,
            spreadAngle = spreadAngle
        };
    }
}

[Serializable]
public sealed class SweepFireWeaponMetaContract : WeaponMetaContract
{
    [SerializeField, Min(0.02f)] private float traversalDuration = 1f;
    [SerializeField] private AnimationCurve speedCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public override string DisplayName => "Sweep Fire";
    public float TraversalDuration => Mathf.Max(0.02f, traversalDuration);
    public AnimationCurve SpeedCurve => speedCurve;

    public override WeaponMetaContract Clone()
    {
        AnimationCurve curve = speedCurve != null
            ? new AnimationCurve(speedCurve.keys)
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        curve.preWrapMode = speedCurve != null
            ? speedCurve.preWrapMode
            : WrapMode.Clamp;
        curve.postWrapMode = speedCurve != null
            ? speedCurve.postWrapMode
            : WrapMode.Clamp;

        return new SweepFireWeaponMetaContract
        {
            traversalDuration = traversalDuration,
            speedCurve = curve
        };
    }
}

[Serializable]
public sealed class TargetingWeaponMetaContract : WeaponMetaContract
{
    [SerializeField, Min(1)] private int maxTargets = 1;
    [SerializeField, Min(0f)] private float targetSearchRadius;

    public override string DisplayName => "Targeting";
    public int MaxTargets => Mathf.Max(1, maxTargets);
    public float TargetSearchRadius => Mathf.Max(0f, targetSearchRadius);

    public override WeaponMetaContract Clone()
    {
        return new TargetingWeaponMetaContract
        {
            maxTargets = maxTargets,
            targetSearchRadius = targetSearchRadius
        };
    }
}

[Serializable]
public sealed class ContinuousDamageWeaponMetaContract : WeaponMetaContract
{
    [SerializeField, Min(0.02f)] private float damageTickInterval = 0.25f;

    public override string DisplayName => "Continuous Damage";
    public float DamageTickInterval => Mathf.Max(0.02f, damageTickInterval);

    public override WeaponMetaContract Clone()
    {
        return new ContinuousDamageWeaponMetaContract
        {
            damageTickInterval = damageTickInterval
        };
    }
}

[Serializable]
public sealed class HomingWeaponMetaContract : WeaponMetaContract
{
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;

    public override string DisplayName => "Homing";
    public float RotationSpeed => Mathf.Max(0f, rotationSpeed);

    public override WeaponMetaContract Clone()
    {
        return new HomingWeaponMetaContract
        {
            rotationSpeed = rotationSpeed
        };
    }
}

[Serializable]
public sealed class WeaponMetaLevel
{
    [SerializeField] private WeaponMetaBasicStats basicStats = new();
    [SerializeReference] private List<WeaponMetaContract> contracts = new();

    public WeaponMetaBasicStats BasicStats => basicStats;
    public IReadOnlyList<WeaponMetaContract> Contracts => contracts;

    public bool TryGetContract<TContract>(out TContract contract)
        where TContract : WeaponMetaContract
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

    public WeaponMetaLevel Clone()
    {
        WeaponMetaLevel clone = new()
        {
            basicStats = basicStats != null
                ? basicStats.Clone()
                : new WeaponMetaBasicStats()
        };

        if (contracts == null)
            return clone;

        for (int index = 0; index < contracts.Count; index++)
        {
            WeaponMetaContract contract = contracts[index];
            if (contract != null)
                clone.contracts.Add(contract.Clone());
        }

        return clone;
    }

#if UNITY_EDITOR
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

    public bool TryAddContract(Type contractType)
    {
        if (contractType == null
            || contractType.IsAbstract
            || !typeof(WeaponMetaContract).IsAssignableFrom(contractType)
            || ContainsContract(contractType))
        {
            return false;
        }

        if (Activator.CreateInstance(contractType) is not WeaponMetaContract contract)
            return false;

        contracts ??= new List<WeaponMetaContract>();
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
#endif
}

public readonly struct WeaponMetaRuntimeStats
{
    public WeaponMetaRuntimeStats(
        WeaponRuntimeStats weaponStats,
        float continuousDamageTickInterval,
        bool usesContinuousDamage,
        bool usesHoming,
        float homingRotationSpeed,
        bool usesSweepFire,
        float sweepTraversalDuration,
        AnimationCurve sweepSpeedCurve)
    {
        WeaponStats = weaponStats;
        ContinuousDamageTickInterval = continuousDamageTickInterval;
        UsesContinuousDamage = usesContinuousDamage;
        UsesHoming = usesHoming;
        HomingRotationSpeed = homingRotationSpeed;
        UsesSweepFire = usesSweepFire;
        SweepTraversalDuration = sweepTraversalDuration;
        SweepSpeedCurve = sweepSpeedCurve;
    }

    public WeaponRuntimeStats WeaponStats { get; }
    public float ContinuousDamageTickInterval { get; }
    public bool UsesContinuousDamage { get; }
    public bool UsesHoming { get; }
    public float HomingRotationSpeed { get; }
    public bool UsesSweepFire { get; }
    public float SweepTraversalDuration { get; }
    public AnimationCurve SweepSpeedCurve { get; }
}

public readonly struct WeaponMetaDpsInfo
{
    public WeaponMetaDpsInfo(
        float dps,
        float shotsPerSecond,
        bool usesContinuousDamage)
    {
        Dps = dps;
        ShotsPerSecond = shotsPerSecond;
        UsesContinuousDamage = usesContinuousDamage;
    }

    public float Dps { get; }
    public float ShotsPerSecond { get; }
    public bool UsesContinuousDamage { get; }
}

[CreateAssetMenu(
    fileName = "WeaponMetaConfig",
    menuName = "Game/Weapon Meta Config")]
public sealed class WeaponMetaConfig : ScriptableObject
{
    [Header("Build")]
    [SerializeField, Min(0)] private int energyCost = 1;

    [Header("Projectiles")]
    [Tooltip("Projectile definitions fired by this weapon. Existing weapons can stay on the legacy path until configured here.")]
    [SerializeField] private List<WeaponProjectileSlot> projectileSlots = new();

    [SerializeField, Min(1)] private int maxLevels = 1;
    [SerializeField] private List<WeaponMetaLevel> levels = new()
    {
        new WeaponMetaLevel()
    };

    public IReadOnlyList<WeaponMetaLevel> Levels => levels;
    public int EnergyCost => Mathf.Max(0, energyCost);
    public IReadOnlyList<WeaponProjectileSlot> ProjectileSlots => projectileSlots;
    public bool HasProjectileSlots => projectileSlots != null
        && projectileSlots.Count > 0;
    public int MaxLevels => Mathf.Max(1, maxLevels);
    public int LevelCount => Mathf.Max(1, levels?.Count ?? 0);

    public WeaponMetaLevel GetLevel(int requestedLevel)
    {
        EnsureLevelZero();
        int level = Mathf.Clamp(requestedLevel, 0, levels.Count - 1);
        return levels[level] ?? new WeaponMetaLevel();
    }

    public bool HasContract<TContract>(int requestedLevel = 0)
        where TContract : WeaponMetaContract
    {
        return GetLevel(requestedLevel).TryGetContract<TContract>(out _);
    }

    public bool TryGetProjectileSlot(
        string slotId,
        out WeaponProjectileSlot projectileSlot)
    {
        projectileSlot = null;
        if (string.IsNullOrWhiteSpace(slotId) || projectileSlots == null)
            return false;

        for (int index = 0; index < projectileSlots.Count; index++)
        {
            WeaponProjectileSlot slot = projectileSlots[index];
            if (slot != null && slot.Id == slotId)
            {
                projectileSlot = slot;
                return true;
            }
        }

        return false;
    }

    public bool TryGetPrimaryProjectileSlot(
        out WeaponProjectileSlot projectileSlot)
    {
        projectileSlot = null;
        if (projectileSlots == null || projectileSlots.Count == 0)
            return false;

        for (int index = 0; index < projectileSlots.Count; index++)
        {
            WeaponProjectileSlot slot = projectileSlots[index];
            if (slot != null
                && !slot.SpawnedByAnotherProjectile
                && slot.Projectile != null)
            {
                projectileSlot = slot;
                return true;
            }
        }

        return false;
    }

    public void AddProjectileSlot()
    {
        projectileSlots ??= new List<WeaponProjectileSlot>();
        projectileSlots.Add(new WeaponProjectileSlot());
    }

    public bool TryRemoveProjectileSlot(int slotIndex)
    {
        if (projectileSlots == null
            || slotIndex < 0
            || slotIndex >= projectileSlots.Count)
        {
            return false;
        }

        projectileSlots.RemoveAt(slotIndex);
        return true;
    }

    public WeaponMetaRuntimeStats GetRuntimeStats(int requestedLevel)
    {
        WeaponMetaLevel level = GetLevel(requestedLevel);
        WeaponMetaBasicStats basic = level.BasicStats ?? new WeaponMetaBasicStats();

        int volleysPerActivation = 1;
        int projectilesPerVolley = 1;
        float delayBetweenVolleys = 0f;
        float spreadAngle = 0f;
        if (level.TryGetContract(out BurstFireWeaponMetaContract burstFire))
        {
            volleysPerActivation = burstFire.VolleysPerActivation;
            projectilesPerVolley = burstFire.ProjectilesPerVolley;
            delayBetweenVolleys = burstFire.DelayBetweenVolleys;
            spreadAngle = burstFire.SpreadAngle;
        }

        int maxTargets = 1;
        float targetSearchRadius = 0f;
        if (level.TryGetContract(out TargetingWeaponMetaContract targeting))
        {
            maxTargets = targeting.MaxTargets;
            targetSearchRadius = targeting.TargetSearchRadius;
        }

        bool usesContinuousDamage = level.TryGetContract(
            out ContinuousDamageWeaponMetaContract continuousDamage);
        float tickInterval = usesContinuousDamage
            ? continuousDamage.DamageTickInterval
            : 0f;

        bool usesHoming = level.TryGetContract(
            out HomingWeaponMetaContract homing);
        float homingRotationSpeed = usesHoming
            ? homing.RotationSpeed
            : 0f;

        bool usesSweepFire = level.TryGetContract(
            out SweepFireWeaponMetaContract sweepFire);
        float sweepTraversalDuration = usesSweepFire
            ? sweepFire.TraversalDuration
            : 0f;
        AnimationCurve sweepSpeedCurve = usesSweepFire
            ? sweepFire.SpeedCurve
            : null;

        WeaponRuntimeStats weaponStats = new(
            basic.ReloadTime,
            basic.Angle,
            basic.InitialDirectionAngle,
            basic.Damage,
            basic.Range,
            basic.ProjectileSpeed,
            volleysPerActivation,
            projectilesPerVolley,
            delayBetweenVolleys,
            spreadAngle,
            maxTargets,
            targetSearchRadius);

        return new WeaponMetaRuntimeStats(
            weaponStats,
            tickInterval,
            usesContinuousDamage,
            usesHoming,
            homingRotationSpeed,
            usesSweepFire,
            sweepTraversalDuration,
            sweepSpeedCurve);
    }

    public WeaponMetaDpsInfo GetDpsInfo(int requestedLevel)
    {
        WeaponMetaRuntimeStats runtimeStats = GetRuntimeStats(requestedLevel);
        WeaponRuntimeStats weaponStats = runtimeStats.WeaponStats;
        float damage = weaponStats.Damage;
        bool usesContinuousDamage = runtimeStats.UsesContinuousDamage;
        float continuousDamageTickInterval =
            runtimeStats.ContinuousDamageTickInterval;

        if (TryGetPrimaryProjectileSlot(out WeaponProjectileSlot projectileSlot)
            && projectileSlot.Projectile != null)
        {
            ProjectileData projectileData = projectileSlot.Projectile;
            damage = projectileData.Damage;
            if (projectileData.TryGetContract(
                    out ProjectileContinuousDamageContract continuousDamage))
            {
                usesContinuousDamage = true;
                continuousDamageTickInterval =
                    continuousDamage.DamageTickInterval;
            }
            else if (projectileData.TryGetContract(
                         out ProjectileCircularChainContract circularChain))
            {
                damage = circularChain.DamagePerHit;
                usesContinuousDamage = true;
                continuousDamageTickInterval = circularChain.HitInterval;
            }
            else if (projectileData.TryGetContract(
                         out ProjectileBeamContract beam))
            {
                usesContinuousDamage = true;
                continuousDamageTickInterval = beam.DamageTickInterval;
            }
        }

        float shotsPerSecond = weaponStats.ReloadTime <= 0f
            ? 0f
            : 1f / weaponStats.ReloadTime;

        if (usesContinuousDamage)
        {
            float dps = continuousDamageTickInterval <= 0f
                ? 0f
                : damage / continuousDamageTickInterval;
            return new WeaponMetaDpsInfo(dps, shotsPerSecond, true);
        }

        float damagePerActivation = damage
            * weaponStats.VolleysPerActivation
            * weaponStats.ProjectilesPerVolley;
        float burstDps = weaponStats.ReloadTime <= 0f
            ? 0f
            : damagePerActivation / weaponStats.ReloadTime;
        return new WeaponMetaDpsInfo(burstDps, shotsPerSecond, false);
    }

    public void AddLevelCopyingPrevious()
    {
        SetMaxLevels(LevelCount + 1);
    }

    public void SetMaxLevels(int requestedMaxLevels)
    {
        EnsureLevelZero();

        int requestedCount = Mathf.Max(1, requestedMaxLevels);
        while (levels.Count > requestedCount)
            levels.RemoveAt(levels.Count - 1);

        while (levels.Count < requestedCount)
        {
            WeaponMetaLevel previous = levels[levels.Count - 1];
            levels.Add(previous != null
                ? previous.Clone()
                : new WeaponMetaLevel());
        }

        maxLevels = requestedCount;
    }

    public bool TryRemoveLevel(int levelIndex)
    {
        EnsureLevelZero();
        if (levelIndex <= 0 || levelIndex >= levels.Count)
            return false;

        levels.RemoveAt(levelIndex);
        maxLevels = Mathf.Max(1, levels.Count);
        return true;
    }

    public void SynchronizeProjectileSlots()
    {
        if (projectileSlots == null)
            projectileSlots = new List<WeaponProjectileSlot>();

        HashSet<string> usedIds = new();
        for (int index = projectileSlots.Count - 1; index >= 0; index--)
        {
            WeaponProjectileSlot slot = projectileSlots[index];
            if (slot == null)
            {
                projectileSlots.RemoveAt(index);
                continue;
            }

            slot.EnsureId();
            while (!usedIds.Add(slot.Id))
            {
                // A copied serialized slot must not share its in-battle bonus key.
                slot.RegenerateId();
            }
        }
    }

#if UNITY_EDITOR
    public bool TryAddContractToAllLevels(Type contractType)
    {
        EnsureLevelZero();
        if (levels[0].ContainsContract(contractType))
            return false;

        bool added = false;
        for (int index = 0; index < levels.Count; index++)
        {
            if (levels[index] == null)
                levels[index] = new WeaponMetaLevel();

            added |= levels[index].TryAddContract(contractType);
        }

        return added;
    }

    public bool TryRemoveContractFromAllLevels(Type contractType)
    {
        EnsureLevelZero();

        bool removed = false;
        for (int index = 0; index < levels.Count; index++)
        {
            if (levels[index] != null)
                removed |= levels[index].TryRemoveContract(contractType);
        }

        return removed;
    }
#endif

    private void EnsureLevelZero()
    {
        if (levels == null)
            levels = new List<WeaponMetaLevel>();
        if (levels.Count == 0)
            levels.Add(new WeaponMetaLevel());
        if (levels[0] == null)
            levels[0] = new WeaponMetaLevel();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        energyCost = Mathf.Max(0, energyCost);
        SynchronizeProjectileSlots();
        SetMaxLevels(maxLevels);
    }
#endif
}
