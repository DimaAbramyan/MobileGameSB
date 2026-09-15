using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ProjectileLevelBonusConfig
{
    [SerializeField, HideInInspector] private string projectileSlotId;
    [SerializeField] private float damagePercent;
    [SerializeField] private float rangePercent;
    [SerializeField] private float speedPercent;

    public string ProjectileSlotId => projectileSlotId;
    public float DamagePercent => damagePercent;
    public float RangePercent => rangePercent;
    public float SpeedPercent => speedPercent;

    public ProjectileLevelBonusConfig(string slotId)
    {
        projectileSlotId = slotId;
    }

    public ProjectileLevelBonusConfig Clone()
    {
        return new ProjectileLevelBonusConfig(projectileSlotId)
        {
            damagePercent = damagePercent,
            rangePercent = rangePercent,
            speedPercent = speedPercent
        };
    }

    public static ProjectileLevelBonusConfig Add(
        ProjectileLevelBonusConfig first,
        ProjectileLevelBonusConfig second)
    {
        if (first == null)
            return second?.Clone();
        if (second == null)
            return first.Clone();

        return new ProjectileLevelBonusConfig(first.projectileSlotId)
        {
            damagePercent = first.damagePercent + second.damagePercent,
            rangePercent = first.rangePercent + second.rangePercent,
            speedPercent = first.speedPercent + second.speedPercent
        };
    }
}

[Serializable]
public sealed class WeaponLevelBonusConfig
{
    [Header("Bonuses for this level (%)")]
    [SerializeField] private float fireRatePercent;
    [SerializeField] private float anglePercent;
    [SerializeField] private float damagePercent;
    [SerializeField] private float rangePercent;
    [SerializeField] private float speedPercent;

    [Header("Projectile bonuses for this level (%)")]
    [SerializeField] private List<ProjectileLevelBonusConfig> projectileBonuses =
        new();

    [Header("Fire bonuses for this level (%)")]
    [SerializeField] private float volleysPerActivationPercent;
    [SerializeField] private float projectilesPerVolleyPercent;
    [SerializeField] private float delayBetweenVolleysRatePercent;
    [SerializeField] private float spreadAnglePercent;

    [Header("Targeting bonuses for this level (%)")]
    [SerializeField] private float maxTargetsPercent;
    [SerializeField] private float targetSearchRadiusPercent;

    public float FireRatePercent => fireRatePercent;
    public float AnglePercent => anglePercent;
    public float DamagePercent => damagePercent;
    public float RangePercent => rangePercent;
    public float SpeedPercent => speedPercent;
    public IReadOnlyList<ProjectileLevelBonusConfig> ProjectileBonuses =>
        projectileBonuses;
    public float VolleysPerActivationPercent => volleysPerActivationPercent;
    public float ProjectilesPerVolleyPercent => projectilesPerVolleyPercent;
    public float DelayBetweenVolleysRatePercent => delayBetweenVolleysRatePercent;
    public float SpreadAnglePercent => spreadAnglePercent;
    public float MaxTargetsPercent => maxTargetsPercent;
    public float TargetSearchRadiusPercent => targetSearchRadiusPercent;

    public WeaponLevelBonusConfig Clone()
    {
        WeaponLevelBonusConfig clone = new()
        {
            fireRatePercent = fireRatePercent,
            anglePercent = anglePercent,
            damagePercent = damagePercent,
            rangePercent = rangePercent,
            speedPercent = speedPercent,
            volleysPerActivationPercent = volleysPerActivationPercent,
            projectilesPerVolleyPercent = projectilesPerVolleyPercent,
            delayBetweenVolleysRatePercent = delayBetweenVolleysRatePercent,
            spreadAnglePercent = spreadAnglePercent,
            maxTargetsPercent = maxTargetsPercent,
            targetSearchRadiusPercent = targetSearchRadiusPercent
        };

        if (projectileBonuses != null)
        {
            for (int index = 0; index < projectileBonuses.Count; index++)
            {
                ProjectileLevelBonusConfig projectileBonus =
                    projectileBonuses[index];
                if (projectileBonus != null)
                    clone.projectileBonuses.Add(projectileBonus.Clone());
            }
        }

        return clone;
    }

    public bool TryGetProjectileBonus(
        string projectileSlotId,
        out ProjectileLevelBonusConfig projectileBonus)
    {
        projectileBonus = null;
        if (string.IsNullOrWhiteSpace(projectileSlotId)
            || projectileBonuses == null)
        {
            return false;
        }

        for (int index = 0; index < projectileBonuses.Count; index++)
        {
            ProjectileLevelBonusConfig candidate = projectileBonuses[index];
            if (candidate != null
                && candidate.ProjectileSlotId == projectileSlotId)
            {
                projectileBonus = candidate;
                return true;
            }
        }

        return false;
    }

    public bool SynchronizeProjectileBonuses(
        IReadOnlyList<WeaponProjectileSlot> projectileSlots)
    {
        projectileBonuses ??= new List<ProjectileLevelBonusConfig>();
        bool changed = false;
        HashSet<string> knownSlotIds = new();
        if (projectileSlots != null)
        {
            for (int index = 0; index < projectileSlots.Count; index++)
            {
                WeaponProjectileSlot slot = projectileSlots[index];
                if (slot == null || string.IsNullOrWhiteSpace(slot.Id))
                    continue;

                knownSlotIds.Add(slot.Id);
                if (!TryGetProjectileBonus(slot.Id, out _))
                {
                    projectileBonuses.Add(new ProjectileLevelBonusConfig(slot.Id));
                    changed = true;
                }
            }
        }

        for (int index = projectileBonuses.Count - 1; index >= 0; index--)
        {
            ProjectileLevelBonusConfig bonus = projectileBonuses[index];
            if (bonus == null
                || !knownSlotIds.Contains(bonus.ProjectileSlotId))
            {
                projectileBonuses.RemoveAt(index);
                changed = true;
            }
        }

        return changed;
    }

    public static WeaponLevelBonusConfig Add(
        WeaponLevelBonusConfig first,
        WeaponLevelBonusConfig second)
    {
        if (first == null)
            return second?.Clone();
        if (second == null)
            return first.Clone();

        WeaponLevelBonusConfig result = new()
        {
            fireRatePercent = first.fireRatePercent + second.fireRatePercent,
            anglePercent = first.anglePercent + second.anglePercent,
            damagePercent = first.damagePercent + second.damagePercent,
            rangePercent = first.rangePercent + second.rangePercent,
            speedPercent = first.speedPercent + second.speedPercent,
            volleysPerActivationPercent = first.volleysPerActivationPercent + second.volleysPerActivationPercent,
            projectilesPerVolleyPercent = first.projectilesPerVolleyPercent + second.projectilesPerVolleyPercent,
            delayBetweenVolleysRatePercent = first.delayBetweenVolleysRatePercent + second.delayBetweenVolleysRatePercent,
            spreadAnglePercent = first.spreadAnglePercent + second.spreadAnglePercent,
            maxTargetsPercent = first.maxTargetsPercent + second.maxTargetsPercent,
            targetSearchRadiusPercent = first.targetSearchRadiusPercent + second.targetSearchRadiusPercent
        };

        AddProjectileBonuses(result, first.projectileBonuses);
        AddProjectileBonuses(result, second.projectileBonuses);
        return result;
    }

    public static WeaponLevelBonusConfig FromTotals(
        WeaponRuntimeStats baseStats,
        WeaponRuntimeStats currentStats,
        WeaponLevelBonusConfig previousTotals)
    {
        float previousFireRate = previousTotals?.FireRatePercent ?? 0f;
        float previousAngle = previousTotals?.AnglePercent ?? 0f;
        float previousDamage = previousTotals?.DamagePercent ?? 0f;
        float previousRange = previousTotals?.RangePercent ?? 0f;
        float previousSpeed = previousTotals?.SpeedPercent ?? 0f;
        float previousVolleys = previousTotals?.VolleysPerActivationPercent ?? 0f;
        float previousProjectiles = previousTotals?.ProjectilesPerVolleyPercent ?? 0f;
        float previousDelayRate = previousTotals?.DelayBetweenVolleysRatePercent ?? 0f;
        float previousSpread = previousTotals?.SpreadAnglePercent ?? 0f;
        float previousTargets = previousTotals?.MaxTargetsPercent ?? 0f;
        float previousSearchRadius = previousTotals?.TargetSearchRadiusPercent ?? 0f;

        return new WeaponLevelBonusConfig
        {
            fireRatePercent = GetRatePercent(baseStats.ReloadTime, currentStats.ReloadTime) - previousFireRate,
            anglePercent = GetValuePercent(baseStats.Angle, currentStats.Angle) - previousAngle,
            damagePercent = GetValuePercent(baseStats.Damage, currentStats.Damage) - previousDamage,
            rangePercent = GetValuePercent(baseStats.Range, currentStats.Range) - previousRange,
            speedPercent = GetValuePercent(baseStats.Speed, currentStats.Speed) - previousSpeed,
            volleysPerActivationPercent = GetValuePercent(baseStats.VolleysPerActivation, currentStats.VolleysPerActivation) - previousVolleys,
            projectilesPerVolleyPercent = GetValuePercent(baseStats.ProjectilesPerVolley, currentStats.ProjectilesPerVolley) - previousProjectiles,
            delayBetweenVolleysRatePercent = GetRatePercent(baseStats.DelayBetweenVolleys, currentStats.DelayBetweenVolleys) - previousDelayRate,
            spreadAnglePercent = GetValuePercent(baseStats.SpreadAngle, currentStats.SpreadAngle) - previousSpread,
            maxTargetsPercent = GetValuePercent(baseStats.MaxTargets, currentStats.MaxTargets) - previousTargets,
            targetSearchRadiusPercent = GetValuePercent(baseStats.TargetSearchRadius, currentStats.TargetSearchRadius) - previousSearchRadius
        };
    }

    private static float GetValuePercent(float baseValue, float currentValue)
    {
        return Mathf.Approximately(baseValue, 0f)
            ? 0f
            : ((currentValue / baseValue) - 1f) * 100f;
    }

    private static void AddProjectileBonuses(
        WeaponLevelBonusConfig destination,
        List<ProjectileLevelBonusConfig> source)
    {
        if (destination == null || source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            ProjectileLevelBonusConfig bonus = source[index];
            if (bonus == null)
                continue;

            for (int destinationIndex = 0;
                 destinationIndex < destination.projectileBonuses.Count;
                 destinationIndex++)
            {
                ProjectileLevelBonusConfig existing =
                    destination.projectileBonuses[destinationIndex];
                if (existing == null
                    || existing.ProjectileSlotId != bonus.ProjectileSlotId)
                {
                    continue;
                }

                destination.projectileBonuses[destinationIndex] =
                    ProjectileLevelBonusConfig.Add(existing, bonus);
                break;
            }

            if (!destination.TryGetProjectileBonus(
                    bonus.ProjectileSlotId,
                    out _))
            {
                destination.projectileBonuses.Add(bonus.Clone());
            }
        }
    }

    private static float GetRatePercent(float baseInterval, float currentInterval)
    {
        if (baseInterval <= 0f || currentInterval <= 0f)
            return 0f;

        return ((baseInterval / currentInterval) - 1f) * 100f;
    }
}
