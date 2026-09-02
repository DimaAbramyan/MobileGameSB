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
    [SerializeField] private List<float> damageByLevel;
    [SerializeField] private List<float> rangeByLevel;
    [SerializeField] private List<float> speedByLevel;

    [Header("Level Configurations")]
    [SerializeField] private List<WeaponLevelConfig> levelConfigs =
        new List<WeaponLevelConfig>();

    [Header("Levels")]
    [SerializeField, Min(1)] private int startLevel = 1;
    [SerializeField, Min(1)] private int maxLevel = 10;

    [Header("Build")]
    [SerializeField, Min(0)] private int energyCost = 1;

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
    public IReadOnlyList<float> DamageByLevel => damageByLevel;
    public IReadOnlyList<float> RangeByLevel => rangeByLevel;
    public IReadOnlyList<float> SpeedByLevel => speedByLevel;
    public IReadOnlyList<WeaponLevelConfig> LevelConfigs => levelConfigs;
    public int LevelCount => Mathf.Max(
        1,
        Mathf.Max(GetLegacyLevelCount(), levelConfigs?.Count ?? 0));
    public bool HasLegacyLevelStats => GetLegacyLevelCount() > 0;

    public int StartLevel => startLevel;
    public int MaxLevel => maxLevel;
    public int EnergyCost => Mathf.Max(0, energyCost);
    public EnemyDamageType DamageType => damageType;

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

    public WeaponRuntimeStats GetRuntimeStats(int requestedLevel)
    {
        int level = ClampLevel(requestedLevel);

        if (TryGetLevelConfig(level, out WeaponLevelConfig config))
            return config.ToRuntimeStats();

        return new WeaponRuntimeStats(
            GetLegacyValue(reloadTimeByLevel, level, 1f),
            GetLegacyValue(angleByLevel, level, 0f),
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
        if (levelConfigs == null)
            levelConfigs = new List<WeaponLevelConfig>();

        bool hasLegacyLevelStats = HasLegacyLevelStats;
        if (levelConfigs.Count == 0 && hasLegacyLevelStats)
            TryCreateLevelConfigsFromLegacy();

        WeaponLevelConfig previous = levelConfigs.Count > 0
            ? levelConfigs[levelConfigs.Count - 1]
            : null;

        levelConfigs.Add(previous != null
            ? previous.Clone()
            : WeaponLevelConfig.Create(1f, 0f, 1f, 10f, 10f));

        if (hasLegacyLevelStats)
            maxLevel = Mathf.Max(maxLevel, levelConfigs.Count - 1);
        else
            maxLevel = levelConfigs.Count - 1;
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
            GetLegacyValue(speedByLevel, level, 10f));
    }

    private int GetLegacyLevelCount()
    {
        int count = 0;
        count = Mathf.Max(count, reloadTimeByLevel?.Count ?? 0);
        count = Mathf.Max(count, angleByLevel?.Count ?? 0);
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
    }
#endif
}
