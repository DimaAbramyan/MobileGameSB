using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ThermalLaserLevelConfig
{
    [SerializeField, Min(0f)] private float heatPerHitPercent = 10f;

    public float HeatPerHitPercent => Mathf.Max(0f, heatPerHitPercent);

    public ThermalLaserLevelConfig Clone()
    {
        return new ThermalLaserLevelConfig
        {
            heatPerHitPercent = heatPerHitPercent
        };
    }
}

[Serializable]
public sealed class ThermalLaserLevelBonusConfig
{
    [SerializeField] private float heatPerHitPercentBonus;

    public float HeatPerHitPercentBonus => heatPerHitPercentBonus;

    public ThermalLaserLevelBonusConfig Clone()
    {
        return new ThermalLaserLevelBonusConfig
        {
            heatPerHitPercentBonus = heatPerHitPercentBonus
        };
    }

    public static ThermalLaserLevelBonusConfig Add(
        ThermalLaserLevelBonusConfig first,
        ThermalLaserLevelBonusConfig second)
    {
        if (first == null)
            return second?.Clone();
        if (second == null)
            return first.Clone();

        return new ThermalLaserLevelBonusConfig
        {
            heatPerHitPercentBonus = first.heatPerHitPercentBonus
                + second.heatPerHitPercentBonus
        };
    }

    public static ThermalLaserLevelBonusConfig FromTotals(
        float baseValue,
        float currentValue,
        float previousTotal)
    {
        float total = Mathf.Approximately(baseValue, 0f)
            ? 0f
            : ((currentValue / baseValue) - 1f) * 100f;
        return new ThermalLaserLevelBonusConfig
        {
            heatPerHitPercentBonus = total - previousTotal
        };
    }
}

[CreateAssetMenu(
    fileName = "ThermalLaserData",
    menuName = "Game/Weapon Data/Thermal Laser")]
public sealed class ThermalLaserData : WeaponData
{
    [Header("Thermal Laser Levels")]
    [SerializeField] private List<ThermalLaserLevelConfig> thermalLevels = new();

    [SerializeField] private ThermalLaserLevelConfig manualBaseThermalStats = new();
    [SerializeField] private List<ThermalLaserLevelBonusConfig>
        thermalLevelBonuses = new();

    [Header("Beam Collision")]
    [SerializeField] private LayerMask beamBlockingLayers = ~0;

    [Header("Enemy Debuff")]
    [SerializeField] private EnemyHeatDebuffConfig heatDebuffConfig;

    [HideInInspector]
    [SerializeField, Min(0f)] private float overheatExplosionRadius = 2f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float overheatExplosionDamage = 30f;
    [HideInInspector]
    [SerializeField, Range(0f, 100f)] private float transferredHeatPercent = 50f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float coolingDelay = 0.5f;
    [HideInInspector]
    [SerializeField, Range(0f, 100f)] private float coolingPercentPerSecond = 25f;
    [HideInInspector]
    [SerializeField] private Explode overheatExplosionPrefab;

    public LayerMask BeamBlockingLayers => beamBlockingLayers;
    public EnemyHeatDebuffConfig HeatDebuffConfig => heatDebuffConfig;
    public float OverheatExplosionRadius => Mathf.Max(0f, overheatExplosionRadius);
    public float OverheatExplosionDamage => Mathf.Max(0f, overheatExplosionDamage);
    public float TransferredHeatPercent => Mathf.Clamp(transferredHeatPercent, 0f, 100f);
    public float CoolingDelay => Mathf.Max(0f, coolingDelay);
    public float CoolingPercentPerSecond =>
        Mathf.Clamp(coolingPercentPerSecond, 0f, 100f);
    public Explode OverheatExplosionPrefab => overheatExplosionPrefab;

    public float GetHeatPerHitPercent(int requestedLevel)
    {
        if (UsesPercentageLevelProgression)
        {
            ThermalLaserLevelConfig baseStats = GetManualBaseThermalStats();
            float bonus = GetCumulativeHeatBonus(requestedLevel);
            return baseStats.HeatPerHitPercent
                * Mathf.Max(0f, 1f + bonus / 100f);
        }

        if (thermalLevels == null || thermalLevels.Count == 0)
            return 10f;

        int index = Mathf.Clamp(requestedLevel, 0, thermalLevels.Count - 1);
        ThermalLaserLevelConfig config = thermalLevels[index];
        return config != null ? config.HeatPerHitPercent : 0f;
    }

    public EnemyHeatProfile CreateHeatProfile(ParentShip owner)
    {
        if (heatDebuffConfig != null)
            return heatDebuffConfig.CreateProfile(owner);

        return new EnemyHeatProfile(
            owner,
            beamBlockingLayers,
            OverheatExplosionRadius,
            OverheatExplosionDamage,
            TransferredHeatPercent,
            CoolingDelay,
            CoolingPercentPerSecond,
            OverheatExplosionPrefab,
            null);
    }

    public void SynchronizeThermalLevels()
    {
        if (UsesPercentageLevelProgression)
        {
            if (thermalLevelBonuses == null)
                thermalLevelBonuses = new List<ThermalLaserLevelBonusConfig>();

            int desiredBonusCount = Mathf.Max(1, LevelCount);
            while (thermalLevelBonuses.Count < desiredBonusCount)
            {
                ThermalLaserLevelBonusConfig previous =
                    thermalLevelBonuses.Count > 0
                        ? thermalLevelBonuses[thermalLevelBonuses.Count - 1]
                        : null;
                thermalLevelBonuses.Add(previous != null
                    ? previous.Clone()
                    : new ThermalLaserLevelBonusConfig());
            }

            while (thermalLevelBonuses.Count > desiredBonusCount)
                thermalLevelBonuses.RemoveAt(thermalLevelBonuses.Count - 1);

            return;
        }

        if (thermalLevels == null)
            thermalLevels = new List<ThermalLaserLevelConfig>();

        int desiredCount = Mathf.Max(1, LevelCount);
        while (thermalLevels.Count < desiredCount)
        {
            ThermalLaserLevelConfig previous = thermalLevels.Count > 0
                ? thermalLevels[thermalLevels.Count - 1]
                : null;
            thermalLevels.Add(previous != null
                ? previous.Clone()
                : new ThermalLaserLevelConfig());
        }

        while (thermalLevels.Count > desiredCount)
            thermalLevels.RemoveAt(thermalLevels.Count - 1);
    }

    protected override void RemoveAdditionalLevelBonus(int levelIndex)
    {
        if (thermalLevelBonuses != null
            && levelIndex >= 0
            && levelIndex < thermalLevelBonuses.Count)
        {
            thermalLevelBonuses.RemoveAt(levelIndex);
        }
    }

    public ThermalLaserLevelConfig GetManualBaseThermalStats()
    {
        ThermalLaserData source = BaseStatsConfig as ThermalLaserData;
        if (source != null)
            return source.GetOwnManualBaseThermalStats();

        return GetOwnManualBaseThermalStats();
    }

    protected override void MigrateAdditionalLevelProgression(int levelCount)
    {
        ThermalLaserLevelConfig baseStats = GetLegacyThermalLevel(0);
        manualBaseThermalStats = baseStats.Clone();

        if (thermalLevelBonuses == null)
            thermalLevelBonuses = new List<ThermalLaserLevelBonusConfig>();
        else
            thermalLevelBonuses.Clear();

        float cumulativeBonus = 0f;
        for (int level = 0; level < levelCount; level++)
        {
            ThermalLaserLevelBonusConfig increment =
                ThermalLaserLevelBonusConfig.FromTotals(
                    baseStats.HeatPerHitPercent,
                    GetLegacyThermalLevel(level).HeatPerHitPercent,
                    cumulativeBonus);
            thermalLevelBonuses.Add(increment);
            cumulativeBonus += increment.HeatPerHitPercentBonus;
        }
    }

    private ThermalLaserLevelConfig GetOwnManualBaseThermalStats()
    {
        return manualBaseThermalStats ?? new ThermalLaserLevelConfig();
    }

    private ThermalLaserLevelConfig GetLegacyThermalLevel(int level)
    {
        if (thermalLevels == null || thermalLevels.Count == 0)
            return new ThermalLaserLevelConfig();

        int index = Mathf.Clamp(level, 0, thermalLevels.Count - 1);
        return thermalLevels[index] ?? new ThermalLaserLevelConfig();
    }

    private float GetCumulativeHeatBonus(int requestedLevel)
    {
        if (thermalLevelBonuses == null || thermalLevelBonuses.Count == 0)
            return 0f;

        float total = 0f;
        int lastIndex = Mathf.Min(requestedLevel, thermalLevelBonuses.Count - 1);
        for (int index = 0; index <= lastIndex; index++)
            total += thermalLevelBonuses[index]?.HeatPerHitPercentBonus ?? 0f;

        return total;
    }
}
