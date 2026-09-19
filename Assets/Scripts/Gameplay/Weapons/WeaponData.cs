using System.Collections.Generic;

using UnityEngine;
using FMODUnity;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewShipData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Stats per level")]
    [SerializeField] private List<float> reloadTimeByLevel;
    [SerializeField] private List<float> angleByLevel;
    [SerializeField] private List<float> initialDirectionAngleByLevel;
    [SerializeField] private List<float> damageByLevel;
    [SerializeField] private List<float> rangeByLevel;
    [SerializeField] private List<float> speedByLevel;

    [Header("Level Configurations")]
    [SerializeField] private List<WeaponLevelConfig> levelConfigs =
        new List<WeaponLevelConfig>();

    [Header("Percentage Level Progression")]
    [Tooltip("Optional WeaponData asset that supplies only the base values. Its level bonuses are ignored.")]
    [SerializeField] private WeaponData baseStatsConfig;
    [SerializeField] private WeaponLevelConfig manualBaseStats =
        new WeaponLevelConfig();
    [SerializeField] private List<WeaponLevelBonusConfig> levelBonuses =
        new List<WeaponLevelBonusConfig>();

    [Header("Meta Progression")]
    [Tooltip("Source of this weapon's persistent meta levels. Runtime integration is configured separately.")]
    [SerializeField] private WeaponMetaConfig weaponMetaConfig;

    [Header("Levels")]
    [SerializeField, Min(1)] private int startLevel = 1;
    [SerializeField, Min(1)] private int maxLevel = 10;

    [Header("Build")]
    [SerializeField, Min(0)] private int energyCost = 1;

    [Header("Battle Placement")]
    [Tooltip("Local offset from the ship's shared battle weapon mount.")]
    [SerializeField] private Vector3 battleOffset;

    [Header("Damage Type")]
    [SerializeField] private EnemyDamageType damageType =
        EnemyDamageType.Kinetic;

    [Header("Behaviours")]
    [FormerlySerializedAs("movementMode")]
    [SerializeField] private ProjectileFlightMode flightMode = ProjectileFlightMode.Straight;
    [SerializeField] private ProjectileContactMode contactMode = ProjectileContactMode.DamageAndDestroy;
    [SerializeField] private float homingRotationSpeed = 360f;
    [SerializeField] private bool growDuringFlight;
    [SerializeField] private Vector2 scaleGrowthPerSecond = Vector2.one * 0.5f;

    [Header("Lifetime")]
    [SerializeField, Min(0.02f)] private float projectileLifetime = 10f;
    [SerializeField] private bool disableColliderAfterFirstPhysicsStep;
    [SerializeField] private bool fadeDuringLifetime;
    [SerializeField, Min(0.02f)] private float fadeDuration = 0.5f;

    [Header("Contact")]
    [SerializeField] private Explode explosionPrefab;
    [SerializeField] private float explosionDamage = 30f;
    [SerializeField, Min(0.02f)] private float continuousDamageInterval = 0.25f;

    [Header("Audio")]
    [SerializeField] private EventReference audioClipDefault;
    [SerializeField] private EventReference audioClipProjectileShot;

    // ---------- READ ONLY PROPERTIES ----------

    public IReadOnlyList<float> ReloadTimeByLevel => reloadTimeByLevel;
    public IReadOnlyList<float> AngleByLevel => angleByLevel;
    public IReadOnlyList<float> InitialDirectionAngleByLevel =>
        initialDirectionAngleByLevel;
    public IReadOnlyList<float> DamageByLevel => damageByLevel;
    public IReadOnlyList<float> RangeByLevel => rangeByLevel;
    public IReadOnlyList<float> SpeedByLevel => speedByLevel;
    public IReadOnlyList<WeaponLevelConfig> LevelConfigs => levelConfigs;
    public IReadOnlyList<WeaponLevelBonusConfig> LevelBonuses => levelBonuses;
    public int LevelCount => UsesPercentageLevelProgression
        ? Mathf.Max(1, levelBonuses?.Count ?? 0)
        : Mathf.Max(
            1,
            Mathf.Max(
                GetLegacyLevelCount(),
                levelConfigs?.Count ?? 0));
    public bool HasLegacyLevelStats => GetLegacyLevelCount() > 0;
    public bool UsesPercentageLevelProgression =>
        levelBonuses != null && levelBonuses.Count > 0;
    public WeaponData BaseStatsConfig =>
        baseStatsConfig != this ? baseStatsConfig : null;
    public WeaponMetaConfig WeaponMetaConfig => weaponMetaConfig;
    public bool UsesProjectileData => weaponMetaConfig != null
        && weaponMetaConfig.HasProjectileSlots;

    public int StartLevel => startLevel;
    public int MaxLevel => maxLevel;
    public int EnergyCost => UsesProjectileData
        ? weaponMetaConfig.EnergyCost
        : Mathf.Max(0, energyCost);
    public Vector3 BattleOffset => battleOffset;
    public EnemyDamageType DamageType => TryGetPrimaryProjectileData(
        out _,
        out ProjectileData projectileData)
        ? projectileData.DamageType
        : damageType;

    public ProjectileFlightMode FlightMode => flightMode;
    public ProjectileContactMode ContactMode => contactMode;
    public float HomingRotationSpeed => homingRotationSpeed;
    public bool GrowDuringFlight => growDuringFlight;
    public Vector2 ScaleGrowthPerSecond => scaleGrowthPerSecond;
    public float ProjectileLifetime => projectileLifetime;
    public bool DisableColliderAfterFirstPhysicsStep =>
        disableColliderAfterFirstPhysicsStep;
    public bool FadeDuringLifetime => fadeDuringLifetime;
    public float FadeDuration => fadeDuration;
    public Explode ExplosionPrefab => explosionPrefab;
    public float ExplosionDamage => explosionDamage;
    public float ContinuousDamageInterval => continuousDamageInterval;

    public EventReference AudioClipDefault => audioClipDefault;
    public EventReference AudioClipProjectileShot => audioClipProjectileShot;

    public int ClampLevel(int requestedLevel)
    {
        return Mathf.Clamp(requestedLevel, 0, LevelCount - 1);
    }

    public bool TryGetPrimaryProjectileData(
        out WeaponProjectileSlot projectileSlot,
        out ProjectileData projectileData)
    {
        projectileSlot = null;
        projectileData = null;
        if (weaponMetaConfig == null
            || !weaponMetaConfig.TryGetPrimaryProjectileSlot(out projectileSlot))
        {
            return false;
        }

        projectileData = projectileSlot.Projectile;
        return projectileData != null;
    }

    public bool TryGetProjectileRuntimeStats(
        string projectileSlotId,
        int requestedLevel,
        out ProjectileData projectileData,
        out ProjectileRuntimeStats runtimeStats)
    {
        projectileData = null;
        runtimeStats = default;
        if (weaponMetaConfig == null
            || !weaponMetaConfig.TryGetProjectileSlot(
                projectileSlotId,
                out WeaponProjectileSlot projectileSlot)
            || projectileSlot.Projectile == null)
        {
            return false;
        }

        projectileData = projectileSlot.Projectile;
        ProjectileRuntimeStats baseStats = projectileData.GetRuntimeStats();
        ProjectileLevelBonusConfig totals = GetCumulativeProjectileBonus(
            projectileSlot.Id,
            ClampLevel(requestedLevel));
        runtimeStats = new ProjectileRuntimeStats(
            ApplyValue(baseStats.Damage, totals?.DamagePercent ?? 0f),
            ApplyValue(baseStats.Range, totals?.RangePercent ?? 0f),
            ApplyValue(baseStats.Speed, totals?.SpeedPercent ?? 0f));
        return true;
    }

    public bool TryGetProjectileRuntimeStats(
        ProjectileData requestedProjectileData,
        int requestedLevel,
        out ProjectileData projectileData,
        out ProjectileRuntimeStats runtimeStats)
    {
        projectileData = null;
        runtimeStats = default;
        if (requestedProjectileData == null
            || weaponMetaConfig?.ProjectileSlots == null)
        {
            return false;
        }

        for (int index = 0;
             index < weaponMetaConfig.ProjectileSlots.Count;
             index++)
        {
            WeaponProjectileSlot slot =
                weaponMetaConfig.ProjectileSlots[index];
            if (slot?.Projectile == requestedProjectileData)
            {
                return TryGetProjectileRuntimeStats(
                    slot.Id,
                    requestedLevel,
                    out projectileData,
                    out runtimeStats);
            }
        }

        return false;
    }

    public bool TryGetPrimaryProjectileRuntimeStats(
        int requestedLevel,
        out ProjectileData projectileData,
        out ProjectileRuntimeStats runtimeStats)
    {
        projectileData = null;
        runtimeStats = default;
        if (!TryGetPrimaryProjectileData(
                out WeaponProjectileSlot projectileSlot,
                out _))
        {
            return false;
        }

        return TryGetProjectileRuntimeStats(
            projectileSlot.Id,
            requestedLevel,
            out projectileData,
            out runtimeStats);
    }

    public WeaponRuntimeStats GetRuntimeStats(int requestedLevel)
    {
        int level = ClampLevel(requestedLevel);

        if (UsesPercentageLevelProgression)
            return ApplyLevelBonuses(level);

        if (weaponMetaConfig != null)
            return GetBaseRuntimeStats();

        if (TryGetLevelConfig(level, out WeaponLevelConfig config))
            return config.ToRuntimeStats();

        return new WeaponRuntimeStats(
            GetLegacyValue(reloadTimeByLevel, level, 1f),
            GetLegacyValue(angleByLevel, level, 0f),
            GetLegacyValue(initialDirectionAngleByLevel, level, 0f),
            GetLegacyValue(damageByLevel, level, 1f),
            GetLegacyValue(rangeByLevel, level, 10f),
            GetLegacyValue(speedByLevel, level, 10f),
            1,
            1,
            0f,
            0f,
            1,
            0f);
    }

    public bool TryGetLevelConfig(
        int requestedLevel,
        out WeaponLevelConfig config)
    {
        config = null;

        if (levelConfigs == null
            || requestedLevel < 0
            || requestedLevel >= levelConfigs.Count)
        {
            return false;
        }

        config = levelConfigs[requestedLevel];
        return config != null;
    }

    public bool TryCreateLevelConfigsFromLegacy()
    {
        if (levelConfigs == null)
            levelConfigs = new List<WeaponLevelConfig>();

        if (levelConfigs.Count > 0)
            return false;

        int legacyLevelCount = GetLegacyLevelCount();
        if (legacyLevelCount == 0)
            return false;

        for (int level = 0; level < legacyLevelCount; level++)
            levelConfigs.Add(CreateLegacyLevelConfig(level));

        maxLevel = Mathf.Max(maxLevel, levelConfigs.Count - 1);
        return true;
    }

    public void AddLevelConfigCopyingPrevious()
    {
        TryMigrateLegacyLevelsToPercentage();
        if (!UsesPercentageLevelProgression)
        {
            if (levelBonuses == null)
                levelBonuses = new List<WeaponLevelBonusConfig>();
            if (levelBonuses.Count == 0)
            {
                levelBonuses.Add(new WeaponLevelBonusConfig());
                maxLevel = 1;
                startLevel = Mathf.Clamp(startLevel, 1, maxLevel);
                return;
            }
        }

        SetMaxLevel(Mathf.Max(1, levelBonuses?.Count ?? 0) + 1);
    }

    public void SetMaxLevel(int requestedMaxLevel)
    {
        int requestedCount = Mathf.Max(1, requestedMaxLevel);
        if (!UsesPercentageLevelProgression)
        {
            maxLevel = requestedCount;
            startLevel = Mathf.Clamp(startLevel, 1, maxLevel);
            return;
        }

        if (levelBonuses == null)
            levelBonuses = new List<WeaponLevelBonusConfig>();

        while (levelBonuses.Count > requestedCount)
            levelBonuses.RemoveAt(levelBonuses.Count - 1);

        while (levelBonuses.Count < requestedCount)
        {
            WeaponLevelBonusConfig previousBonus = levelBonuses.Count > 0
                ? levelBonuses[levelBonuses.Count - 1]
                : null;
            levelBonuses.Add(previousBonus != null
                ? previousBonus.Clone()
                : new WeaponLevelBonusConfig());
        }

        maxLevel = requestedCount;
        startLevel = Mathf.Clamp(startLevel, 1, maxLevel);
    }

    public bool TryRemoveLevelConfig(int levelIndex)
    {
        if (!UsesPercentageLevelProgression
            || levelBonuses == null
            || levelBonuses.Count <= 1
            || levelIndex < 0
            || levelIndex >= levelBonuses.Count)
        {
            return false;
        }

        levelBonuses.RemoveAt(levelIndex);
        RemoveAdditionalLevelBonus(levelIndex);
        maxLevel = Mathf.Max(1, levelBonuses.Count);
        startLevel = Mathf.Clamp(startLevel, 1, maxLevel);
        return true;
    }

    public bool TryMigrateLegacyLevelsToPercentage()
    {
        if (UsesPercentageLevelProgression)
            return false;

        int legacyLevelCount = Mathf.Max(
            GetLegacyLevelCount(),
            levelConfigs?.Count ?? 0);
        if (legacyLevelCount == 0)
            return false;

        WeaponRuntimeStats baseStats = GetLegacyRuntimeStats(0);
        manualBaseStats = GetLegacyLevelConfig(0).Clone();

        if (levelBonuses == null)
            levelBonuses = new List<WeaponLevelBonusConfig>();
        else
            levelBonuses.Clear();

        WeaponLevelBonusConfig cumulativeTotals = null;
        for (int level = 0; level < legacyLevelCount; level++)
        {
            WeaponRuntimeStats currentStats = GetLegacyRuntimeStats(level);
            WeaponLevelBonusConfig increment = WeaponLevelBonusConfig.FromTotals(
                baseStats,
                currentStats,
                cumulativeTotals);
            levelBonuses.Add(increment);
            cumulativeTotals = AddBonus(cumulativeTotals, increment);
        }

        MigrateAdditionalLevelProgression(legacyLevelCount);
        maxLevel = Mathf.Max(1, levelBonuses.Count);
        return true;
    }

    public WeaponRuntimeStats GetManualBaseRuntimeStats()
    {
        return GetBaseRuntimeStats();
    }

    private WeaponRuntimeStats GetBaseRuntimeStats()
    {
        if (weaponMetaConfig != null)
        {
            WeaponRuntimeStats metaStats =
                weaponMetaConfig.GetRuntimeStats(0).WeaponStats;
            if (TryGetPrimaryProjectileRuntimeStats(
                    0,
                    out ProjectileData projectileData,
                    out ProjectileRuntimeStats projectileStats))
            {
                return new WeaponRuntimeStats(
                    metaStats.ReloadTime,
                    metaStats.Angle,
                    metaStats.InitialDirectionAngle,
                    projectileStats.Damage,
                    projectileData.DeliveryType
                        == ProjectileDeliveryType.Projectile
                        ? projectileStats.Range
                        : metaStats.Range,
                    projectileData.DeliveryType
                        == ProjectileDeliveryType.Projectile
                        ? projectileStats.Speed
                        : metaStats.Speed,
                    metaStats.VolleysPerActivation,
                    metaStats.ProjectilesPerVolley,
                    metaStats.DelayBetweenVolleys,
                    metaStats.SpreadAngle,
                    metaStats.MaxTargets,
                    metaStats.TargetSearchRadius);
            }

            return metaStats;
        }

        if (BaseStatsConfig != null)
            return BaseStatsConfig.GetManualBaseRuntimeStats();

        return manualBaseStats != null
            ? manualBaseStats.ToRuntimeStats()
            : WeaponLevelConfig.Create(1f, 0f, 1f, 10f, 10f)
                .ToRuntimeStats();
    }

    // ---------- AUDIO HELPERS ----------

    public void PlayDefaultSound(SoundManager soundManager, Vector3 position)
    {
        if (soundManager == null || audioClipDefault.IsNull)
            return;

        soundManager.PlaySound(audioClipDefault, position);
    }

    public void PlayShotSound(SoundManager soundManager, Vector3 position)
    {
        if (soundManager == null || audioClipProjectileShot.IsNull)
            return;

        soundManager.PlaySound(audioClipProjectileShot, position);
    }

    private WeaponLevelConfig CreateLegacyLevelConfig(int level)
    {
        return WeaponLevelConfig.Create(
            GetLegacyValue(reloadTimeByLevel, level, 1f),
            GetLegacyValue(angleByLevel, level, 0f),
            GetLegacyValue(damageByLevel, level, 1f),
            GetLegacyValue(rangeByLevel, level, 10f),
            GetLegacyValue(speedByLevel, level, 10f),
            GetLegacyValue(initialDirectionAngleByLevel, level, 0f));
    }

    private WeaponRuntimeStats ApplyLevelBonuses(int level)
    {
        WeaponRuntimeStats baseStats = GetBaseRuntimeStats();
        WeaponLevelBonusConfig totals = GetCumulativeBonuses(level);
        bool usesFireBonuses = weaponMetaConfig != null
            && weaponMetaConfig.HasContract<BurstFireWeaponMetaContract>();
        bool usesTargetingBonuses = weaponMetaConfig != null
            && weaponMetaConfig.HasContract<TargetingWeaponMetaContract>();
        bool usesProjectileData = UsesProjectileData;
        bool usesBeamProjectileData = UsesBeamProjectileData();

        return new WeaponRuntimeStats(
            ApplyRate(baseStats.ReloadTime, totals.FireRatePercent),
            ApplyValue(baseStats.Angle, totals.AnglePercent),
            baseStats.InitialDirectionAngle,
            ApplyValue(
                baseStats.Damage,
                usesProjectileData ? 0f : totals.DamagePercent),
            ApplyValue(
                baseStats.Range,
                usesProjectileData && !usesBeamProjectileData
                    ? 0f
                    : totals.RangePercent),
            ApplyValue(
                baseStats.Speed,
                usesProjectileData && !usesBeamProjectileData
                    ? 0f
                    : totals.SpeedPercent),
            ApplyIntValue(
                baseStats.VolleysPerActivation,
                usesFireBonuses ? totals.VolleysPerActivationPercent : 0f),
            ApplyIntValue(
                baseStats.ProjectilesPerVolley,
                usesFireBonuses ? totals.ProjectilesPerVolleyPercent : 0f),
            ApplyRate(
                baseStats.DelayBetweenVolleys,
                usesFireBonuses
                    ? totals.DelayBetweenVolleysRatePercent
                    : 0f),
            ApplyValue(
                baseStats.SpreadAngle,
                usesFireBonuses ? totals.SpreadAnglePercent : 0f),
            ApplyIntValue(
                baseStats.MaxTargets,
                usesTargetingBonuses ? totals.MaxTargetsPercent : 0f),
            ApplyValue(
                baseStats.TargetSearchRadius,
                usesTargetingBonuses
                    ? totals.TargetSearchRadiusPercent
                    : 0f));
    }

    private WeaponLevelBonusConfig GetCumulativeBonuses(int level)
    {
        WeaponLevelBonusConfig totals = null;
        int lastBonusIndex = Mathf.Min(level, levelBonuses.Count - 1);
        for (int index = 0; index <= lastBonusIndex; index++)
            totals = AddBonus(totals, levelBonuses[index]);

        return totals ?? new WeaponLevelBonusConfig();
    }

    private ProjectileLevelBonusConfig GetCumulativeProjectileBonus(
        string projectileSlotId,
        int level)
    {
        ProjectileLevelBonusConfig total = null;
        if (levelBonuses == null || string.IsNullOrWhiteSpace(projectileSlotId))
            return null;

        int lastBonusIndex = Mathf.Min(level, levelBonuses.Count - 1);
        for (int index = 0; index <= lastBonusIndex; index++)
        {
            WeaponLevelBonusConfig levelBonus = levelBonuses[index];
            if (levelBonus == null
                || !levelBonus.TryGetProjectileBonus(
                    projectileSlotId,
                    out ProjectileLevelBonusConfig projectileBonus))
            {
                continue;
            }

            total = ProjectileLevelBonusConfig.Add(total, projectileBonus);
        }

        return total;
    }

    private bool UsesBeamProjectileData()
    {
        return TryGetPrimaryProjectileData(
                   out _,
                   out ProjectileData projectileData)
            && projectileData.DeliveryType == ProjectileDeliveryType.Beam;
    }

    public bool SynchronizeProjectileLevelBonuses()
    {
        if (!UsesProjectileData || levelBonuses == null)
            return false;

        weaponMetaConfig.SynchronizeProjectileSlots();
        bool changed = false;
        for (int index = 0; index < levelBonuses.Count; index++)
        {
            WeaponLevelBonusConfig levelBonus = levelBonuses[index];
            if (levelBonus != null)
            {
                changed |= levelBonus.SynchronizeProjectileBonuses(
                    weaponMetaConfig.ProjectileSlots);
            }
        }

        return changed;
    }

    private WeaponRuntimeStats GetLegacyRuntimeStats(int level)
    {
        return GetLegacyLevelConfig(level).ToRuntimeStats();
    }

    private WeaponLevelConfig GetLegacyLevelConfig(int level)
    {
        if (TryGetLevelConfig(level, out WeaponLevelConfig config))
            return config;

        return CreateLegacyLevelConfig(level);
    }

    private static float ApplyValue(float baseValue, float percent)
    {
        return Mathf.Max(0f, baseValue * GetMultiplier(percent));
    }

    private static int ApplyIntValue(int baseValue, float percent)
    {
        return Mathf.Max(
            1,
            Mathf.CeilToInt(baseValue * GetMultiplier(percent)));
    }

    private static float ApplyRate(float baseInterval, float percent)
    {
        if (baseInterval <= 0f)
            return 0f;

        return baseInterval / Mathf.Max(0.01f, GetMultiplier(percent));
    }

    private static float GetMultiplier(float percent)
    {
        return Mathf.Max(0f, 1f + percent / 100f);
    }

    private static WeaponLevelBonusConfig AddBonus(
        WeaponLevelBonusConfig first,
        WeaponLevelBonusConfig second)
    {
        return WeaponLevelBonusConfig.Add(first, second);
    }

    protected virtual void MigrateAdditionalLevelProgression(int levelCount)
    {
    }

    protected virtual void RemoveAdditionalLevelBonus(int levelIndex)
    {
    }

    private int GetLegacyLevelCount()
    {
        int count = 0;
        count = Mathf.Max(count, reloadTimeByLevel?.Count ?? 0);
        count = Mathf.Max(count, angleByLevel?.Count ?? 0);
        count = Mathf.Max(count, initialDirectionAngleByLevel?.Count ?? 0);
        count = Mathf.Max(count, damageByLevel?.Count ?? 0);
        count = Mathf.Max(count, rangeByLevel?.Count ?? 0);
        count = Mathf.Max(count, speedByLevel?.Count ?? 0);
        return count;
    }

    private static float GetLegacyValue(
        List<float> values,
        int level,
        float fallback)
    {
        if (values == null || values.Count == 0)
            return fallback;

        return values[Mathf.Clamp(level, 0, values.Count - 1)];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        energyCost = Mathf.Max(0, energyCost);
        projectileLifetime = Mathf.Max(0.02f, projectileLifetime);
        fadeDuration = Mathf.Clamp(
            fadeDuration,
            0.02f,
            projectileLifetime);
        SynchronizeProjectileLevelBonuses();
    }
#endif
}
