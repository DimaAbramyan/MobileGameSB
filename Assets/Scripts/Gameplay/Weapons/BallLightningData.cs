using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BallLightningLevelConfig
{
    [SerializeField, Min(0f)] private float directDamage = 0.5f;
    [SerializeField, Min(0f)] private float areaDamage = 4f;
    [SerializeField, Min(0.02f)] private float areaTickInterval = 0.5f;

    public float DirectDamage => Mathf.Max(0f, directDamage);
    public float AreaDamage => Mathf.Max(0f, areaDamage);
    public float AreaTickInterval => Mathf.Max(0.02f, areaTickInterval);

    public BallLightningLevelConfig Clone()
    {
        return new BallLightningLevelConfig
        {
            directDamage = directDamage,
            areaDamage = areaDamage,
            areaTickInterval = areaTickInterval
        };
    }
}

[Serializable]
public sealed class BallLightningLevelBonusConfig
{
    [SerializeField] private float directDamagePercent;
    [SerializeField] private float areaDamagePercent;
    [SerializeField] private float areaTickRatePercent;

    public float DirectDamagePercent => directDamagePercent;
    public float AreaDamagePercent => areaDamagePercent;
    public float AreaTickRatePercent => areaTickRatePercent;

    public BallLightningLevelBonusConfig Clone()
    {
        return new BallLightningLevelBonusConfig
        {
            directDamagePercent = directDamagePercent,
            areaDamagePercent = areaDamagePercent,
            areaTickRatePercent = areaTickRatePercent
        };
    }

    public static BallLightningLevelBonusConfig Add(
        BallLightningLevelBonusConfig first,
        BallLightningLevelBonusConfig second)
    {
        if (first == null)
            return second?.Clone();
        if (second == null)
            return first.Clone();

        return new BallLightningLevelBonusConfig
        {
            directDamagePercent = first.directDamagePercent
                + second.directDamagePercent,
            areaDamagePercent = first.areaDamagePercent
                + second.areaDamagePercent,
            areaTickRatePercent = first.areaTickRatePercent
                + second.areaTickRatePercent
        };
    }

    public static BallLightningLevelBonusConfig FromTotals(
        BallLightningLevelConfig baseStats,
        BallLightningLevelConfig currentStats,
        BallLightningLevelBonusConfig previousTotals)
    {
        float previousDirect = previousTotals?.DirectDamagePercent ?? 0f;
        float previousArea = previousTotals?.AreaDamagePercent ?? 0f;
        float previousRate = previousTotals?.AreaTickRatePercent ?? 0f;
        return new BallLightningLevelBonusConfig
        {
            directDamagePercent = GetValuePercent(
                baseStats.DirectDamage,
                currentStats.DirectDamage) - previousDirect,
            areaDamagePercent = GetValuePercent(
                baseStats.AreaDamage,
                currentStats.AreaDamage) - previousArea,
            areaTickRatePercent = GetRatePercent(
                baseStats.AreaTickInterval,
                currentStats.AreaTickInterval) - previousRate
        };
    }

    private static float GetValuePercent(float baseValue, float currentValue)
    {
        return Mathf.Approximately(baseValue, 0f)
            ? 0f
            : ((currentValue / baseValue) - 1f) * 100f;
    }

    private static float GetRatePercent(float baseInterval, float currentInterval)
    {
        if (baseInterval <= 0f || currentInterval <= 0f)
            return 0f;

        return ((baseInterval / currentInterval) - 1f) * 100f;
    }
}

[CreateAssetMenu(
    fileName = "BallLightningData",
    menuName = "Game/Weapon Data/Ball Lightning")]
public sealed class BallLightningData : WeaponData
{
    private static readonly BallLightningLevelConfig DefaultLevel = new();

    [Header("Ball Lightning Levels")]
    [SerializeField] private List<BallLightningLevelConfig> ballLightningLevels = new();

    [SerializeField] private BallLightningLevelConfig manualBaseBallLightningStats =
        new();
    [SerializeField] private List<BallLightningLevelBonusConfig>
        ballLightningLevelBonuses = new();

    [Header("Projectile")]
    [SerializeField, Min(0f)] private float projectileSpeed = 4f;
    [SerializeField, Min(1)] private int ballsPerShot = 1;
    [SerializeField, Min(0f)] private float ballSpreadAngle = 12f;

    [Header("Area Damage")]
    [SerializeField, Min(0f)] private float areaRadius = 2f;
    [SerializeField] private LayerMask areaDamageLayers = ~0;

    public float ProjectileSpeed => Mathf.Max(0f, projectileSpeed);
    public int BallsPerShot => Mathf.Max(1, ballsPerShot);
    public float BallSpreadAngle => Mathf.Max(0f, ballSpreadAngle);
    public float AreaRadius => Mathf.Max(0f, areaRadius);
    public LayerMask AreaDamageLayers => areaDamageLayers;
    public float MaxTravelDistance => Mathf.Max(
        0.02f,
        ProjectileSpeed * ProjectileLifetime);

    public float GetDirectDamage(int requestedLevel)
    {
        if (UsesPercentageLevelProgression)
        {
            BallLightningLevelConfig baseStats = GetManualBaseBallLightningStats();
            return ApplyValue(
                baseStats.DirectDamage,
                GetCumulativeBonus(requestedLevel, BonusKind.DirectDamage));
        }

        return GetLevel(requestedLevel).DirectDamage;
    }

    public float GetAreaDamage(int requestedLevel)
    {
        if (UsesPercentageLevelProgression)
        {
            BallLightningLevelConfig baseStats = GetManualBaseBallLightningStats();
            return ApplyValue(
                baseStats.AreaDamage,
                GetCumulativeBonus(requestedLevel, BonusKind.AreaDamage));
        }

        return GetLevel(requestedLevel).AreaDamage;
    }

    public float GetAreaTickInterval(int requestedLevel)
    {
        if (UsesPercentageLevelProgression)
        {
            BallLightningLevelConfig baseStats = GetManualBaseBallLightningStats();
            return ApplyRate(
                baseStats.AreaTickInterval,
                GetCumulativeBonus(requestedLevel, BonusKind.AreaTickRate));
        }

        return GetLevel(requestedLevel).AreaTickInterval;
    }

    public void SynchronizeBallLightningLevels()
    {
        if (UsesPercentageLevelProgression)
        {
            if (ballLightningLevelBonuses == null)
            {
                ballLightningLevelBonuses =
                    new List<BallLightningLevelBonusConfig>();
            }

            int desiredBonusCount = Mathf.Max(1, LevelCount);
            while (ballLightningLevelBonuses.Count < desiredBonusCount)
            {
                BallLightningLevelBonusConfig previous =
                    ballLightningLevelBonuses.Count > 0
                        ? ballLightningLevelBonuses[
                            ballLightningLevelBonuses.Count - 1]
                        : null;
                ballLightningLevelBonuses.Add(previous != null
                    ? previous.Clone()
                    : new BallLightningLevelBonusConfig());
            }

            while (ballLightningLevelBonuses.Count > desiredBonusCount)
                ballLightningLevelBonuses.RemoveAt(
                    ballLightningLevelBonuses.Count - 1);

            return;
        }

        if (ballLightningLevels == null)
            ballLightningLevels = new List<BallLightningLevelConfig>();

        int desiredCount = Mathf.Max(1, LevelCount);
        while (ballLightningLevels.Count < desiredCount)
        {
            BallLightningLevelConfig previous = ballLightningLevels.Count > 0
                ? ballLightningLevels[ballLightningLevels.Count - 1]
                : null;
            ballLightningLevels.Add(previous != null
                ? previous.Clone()
                : new BallLightningLevelConfig());
        }

        while (ballLightningLevels.Count > desiredCount)
            ballLightningLevels.RemoveAt(ballLightningLevels.Count - 1);
    }

    protected override void RemoveAdditionalLevelBonus(int levelIndex)
    {
        if (ballLightningLevelBonuses != null
            && levelIndex >= 0
            && levelIndex < ballLightningLevelBonuses.Count)
        {
            ballLightningLevelBonuses.RemoveAt(levelIndex);
        }
    }

    public BallLightningLevelConfig GetManualBaseBallLightningStats()
    {
        BallLightningData source = BaseStatsConfig as BallLightningData;
        if (source != null)
            return source.GetOwnManualBaseBallLightningStats();

        return GetOwnManualBaseBallLightningStats();
    }

    protected override void MigrateAdditionalLevelProgression(int levelCount)
    {
        BallLightningLevelConfig baseStats = GetLegacyBallLightningLevel(0);
        manualBaseBallLightningStats = baseStats.Clone();

        if (ballLightningLevelBonuses == null)
        {
            ballLightningLevelBonuses =
                new List<BallLightningLevelBonusConfig>();
        }
        else
        {
            ballLightningLevelBonuses.Clear();
        }

        BallLightningLevelBonusConfig cumulativeTotals = null;
        for (int level = 0; level < levelCount; level++)
        {
            BallLightningLevelBonusConfig increment =
                BallLightningLevelBonusConfig.FromTotals(
                    baseStats,
                    GetLegacyBallLightningLevel(level),
                    cumulativeTotals);
            ballLightningLevelBonuses.Add(increment);
            cumulativeTotals = AddBonus(cumulativeTotals, increment);
        }
    }

    private BallLightningLevelConfig GetLevel(int requestedLevel)
    {
        if (ballLightningLevels == null || ballLightningLevels.Count == 0)
            return DefaultLevel;

        int index = Mathf.Clamp(
            requestedLevel,
            0,
            ballLightningLevels.Count - 1);
        return ballLightningLevels[index] ?? DefaultLevel;
    }

    private BallLightningLevelConfig GetOwnManualBaseBallLightningStats()
    {
        return manualBaseBallLightningStats ?? DefaultLevel;
    }

    private BallLightningLevelConfig GetLegacyBallLightningLevel(int level)
    {
        if (ballLightningLevels == null || ballLightningLevels.Count == 0)
            return DefaultLevel;

        int index = Mathf.Clamp(level, 0, ballLightningLevels.Count - 1);
        return ballLightningLevels[index] ?? DefaultLevel;
    }

    private float GetCumulativeBonus(int requestedLevel, BonusKind kind)
    {
        if (ballLightningLevelBonuses == null
            || ballLightningLevelBonuses.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        int lastIndex = Mathf.Min(
            requestedLevel,
            ballLightningLevelBonuses.Count - 1);
        for (int index = 0; index <= lastIndex; index++)
        {
            BallLightningLevelBonusConfig bonus =
                ballLightningLevelBonuses[index];
            if (bonus == null)
                continue;

            total += kind switch
            {
                BonusKind.DirectDamage => bonus.DirectDamagePercent,
                BonusKind.AreaDamage => bonus.AreaDamagePercent,
                _ => bonus.AreaTickRatePercent
            };
        }

        return total;
    }

    private static BallLightningLevelBonusConfig AddBonus(
        BallLightningLevelBonusConfig first,
        BallLightningLevelBonusConfig second)
    {
        return BallLightningLevelBonusConfig.Add(first, second);
    }

    private static float ApplyValue(float baseValue, float percent)
    {
        return Mathf.Max(0f, baseValue * Mathf.Max(0f, 1f + percent / 100f));
    }

    private static float ApplyRate(float baseInterval, float percent)
    {
        if (baseInterval <= 0f)
            return 0f;

        return baseInterval / Mathf.Max(0.01f, 1f + percent / 100f);
    }

    private enum BonusKind
    {
        DirectDamage,
        AreaDamage,
        AreaTickRate
    }
}
