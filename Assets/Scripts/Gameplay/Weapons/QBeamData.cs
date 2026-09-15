using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class QBeamLevelConfig
{
    [SerializeField, Min(0f)] private float chargePerHit = 6f;

    public float ChargePerHit => Mathf.Max(0f, chargePerHit);

    public QBeamLevelConfig Clone()
    {
        return new QBeamLevelConfig
        {
            chargePerHit = chargePerHit
        };
    }
}

[Serializable]
public sealed class QBeamLevelBonusConfig
{
    [SerializeField] private float chargePerHitPercentBonus;

    public float ChargePerHitPercentBonus => chargePerHitPercentBonus;

    public QBeamLevelBonusConfig Clone()
    {
        return new QBeamLevelBonusConfig
        {
            chargePerHitPercentBonus = chargePerHitPercentBonus
        };
    }

    public static QBeamLevelBonusConfig FromTotals(
        float baseValue,
        float currentValue,
        float previousTotal)
    {
        float total = Mathf.Approximately(baseValue, 0f)
            ? 0f
            : ((currentValue / baseValue) - 1f) * 100f;
        return new QBeamLevelBonusConfig
        {
            chargePerHitPercentBonus = total - previousTotal
        };
    }
}

[CreateAssetMenu(
    fileName = "QBeamData",
    menuName = "Game/Weapon Data/Q-Beam")]
public sealed class QBeamData : WeaponData
{
    [Header("Q-Beam Levels")]
    [SerializeField] private List<QBeamLevelConfig> qBeamLevels = new();

    [SerializeField] private QBeamLevelConfig manualBaseQBeamStats = new();
    [SerializeField] private List<QBeamLevelBonusConfig> qBeamLevelBonuses =
        new();

    [Header("Beam Collision")]
    [SerializeField] private LayerMask beamBlockingLayers = ~0;

    [Header("Charge Decay")]
    [SerializeField, Min(0f)] private float chargeDecayDelay = 0.5f;
    [SerializeField, Min(0f)] private float chargeDecayPerSecond = 12f;

    public LayerMask BeamBlockingLayers => beamBlockingLayers;
    public float ChargeDecayDelay => Mathf.Max(0f, chargeDecayDelay);
    public float ChargeDecayPerSecond => Mathf.Max(0f, chargeDecayPerSecond);

    public float GetChargePerHit(int requestedLevel)
    {
        if (UsesPercentageLevelProgression)
        {
            QBeamLevelConfig baseStats = GetManualBaseQBeamStats();
            float bonus = GetCumulativeChargeBonus(requestedLevel);
            return baseStats.ChargePerHit
                * Mathf.Max(0f, 1f + bonus / 100f);
        }

        if (qBeamLevels == null || qBeamLevels.Count == 0)
            return 6f;

        int index = Mathf.Clamp(requestedLevel, 0, qBeamLevels.Count - 1);
        QBeamLevelConfig config = qBeamLevels[index];
        return config != null ? config.ChargePerHit : 0f;
    }

    public EnemyDisintegrationProfile CreateDisintegrationProfile()
    {
        return new EnemyDisintegrationProfile(
            ChargeDecayDelay,
            ChargeDecayPerSecond);
    }

    public void SynchronizeQBeamLevels()
    {
        if (UsesPercentageLevelProgression)
        {
            if (qBeamLevelBonuses == null)
                qBeamLevelBonuses = new List<QBeamLevelBonusConfig>();

            int desiredBonusCount = Mathf.Max(1, LevelCount);
            while (qBeamLevelBonuses.Count < desiredBonusCount)
            {
                QBeamLevelBonusConfig previous = qBeamLevelBonuses.Count > 0
                    ? qBeamLevelBonuses[qBeamLevelBonuses.Count - 1]
                    : null;
                qBeamLevelBonuses.Add(previous != null
                    ? previous.Clone()
                    : new QBeamLevelBonusConfig());
            }

            while (qBeamLevelBonuses.Count > desiredBonusCount)
                qBeamLevelBonuses.RemoveAt(qBeamLevelBonuses.Count - 1);

            return;
        }

        if (qBeamLevels == null)
            qBeamLevels = new List<QBeamLevelConfig>();

        int desiredCount = Mathf.Max(1, LevelCount);
        while (qBeamLevels.Count < desiredCount)
        {
            QBeamLevelConfig previous = qBeamLevels.Count > 0
                ? qBeamLevels[qBeamLevels.Count - 1]
                : null;
            qBeamLevels.Add(previous != null
                ? previous.Clone()
                : new QBeamLevelConfig());
        }

        while (qBeamLevels.Count > desiredCount)
            qBeamLevels.RemoveAt(qBeamLevels.Count - 1);
    }

    protected override void RemoveAdditionalLevelBonus(int levelIndex)
    {
        if (qBeamLevelBonuses != null
            && levelIndex >= 0
            && levelIndex < qBeamLevelBonuses.Count)
        {
            qBeamLevelBonuses.RemoveAt(levelIndex);
        }
    }

    public QBeamLevelConfig GetManualBaseQBeamStats()
    {
        QBeamData source = BaseStatsConfig as QBeamData;
        if (source != null)
            return source.GetOwnManualBaseQBeamStats();

        return GetOwnManualBaseQBeamStats();
    }

    protected override void MigrateAdditionalLevelProgression(int levelCount)
    {
        QBeamLevelConfig baseStats = GetLegacyQBeamLevel(0);
        manualBaseQBeamStats = baseStats.Clone();

        if (qBeamLevelBonuses == null)
            qBeamLevelBonuses = new List<QBeamLevelBonusConfig>();
        else
            qBeamLevelBonuses.Clear();

        float cumulativeBonus = 0f;
        for (int level = 0; level < levelCount; level++)
        {
            QBeamLevelBonusConfig increment = QBeamLevelBonusConfig.FromTotals(
                baseStats.ChargePerHit,
                GetLegacyQBeamLevel(level).ChargePerHit,
                cumulativeBonus);
            qBeamLevelBonuses.Add(increment);
            cumulativeBonus += increment.ChargePerHitPercentBonus;
        }
    }

    private QBeamLevelConfig GetOwnManualBaseQBeamStats()
    {
        return manualBaseQBeamStats ?? new QBeamLevelConfig();
    }

    private QBeamLevelConfig GetLegacyQBeamLevel(int level)
    {
        if (qBeamLevels == null || qBeamLevels.Count == 0)
            return new QBeamLevelConfig();

        int index = Mathf.Clamp(level, 0, qBeamLevels.Count - 1);
        return qBeamLevels[index] ?? new QBeamLevelConfig();
    }

    private float GetCumulativeChargeBonus(int requestedLevel)
    {
        if (qBeamLevelBonuses == null || qBeamLevelBonuses.Count == 0)
            return 0f;

        float total = 0f;
        int lastIndex = Mathf.Min(requestedLevel, qBeamLevelBonuses.Count - 1);
        for (int index = 0; index <= lastIndex; index++)
            total += qBeamLevelBonuses[index]?.ChargePerHitPercentBonus ?? 0f;

        return total;
    }
}
