using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelConfig",
    menuName = "Game/Levels/Level Config")]
public sealed class LevelConfig : ScriptableObject
{
    [Serializable]
    public sealed class ParallaxLayer
    {
        public string name = "Layer";
        public Sprite sprite;
        public Vector2 position;
        public Vector2 scale = Vector2.one;
        public Vector2 scrollSpeed;
        public int sortingOrder = -100;
        public Color color = Color.white;
    }

    [Serializable]
    public sealed class RandomBackgroundObject
    {
        public string name = "Background Object";
        public Sprite sprite;
        public Color color = Color.white;
        [Min(0)] public int count = 1;
        public Vector2 positionMin = new Vector2(-2.5f, -5f);
        public Vector2 positionMax = new Vector2(2.5f, 5f);
        public float ySpeedMin = -0.2f;
        public float ySpeedMax = -0.5f;
        [Min(0f)] public float scaleMin = 1f;
        [Min(0f)] public float scaleMax = 1f;
        public int sortingOrder = -90;
        [Min(0f)] public float respawnPadding = 1f;
    }

    [Min(0)]
    [SerializeField] private int id;
    [SerializeField] private string displayName;

    [Header("Progression")]
    [SerializeField] private LevelConfig requiredLevel;
    [SerializeField] private bool bonusLevel;

    [Header("Completion Rewards")]
    [FormerlySerializedAs("metalReward")]
    [SerializeField, Min(0)] private int goldReward;
    [SerializeField, Min(0)] private int coreReward;

    [Header("Metal Drops")]
    [SerializeField] private MetalPickup metalPickupPrefab;
    [SerializeField] private List<WaveMetalDropSettings> waveMetalDrops = new();

    [Header("Enemy Difficulty Multipliers")]
    [SerializeField, Min(0.01f)] private float enemyHullHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float enemyShieldHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float enemyDamageMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float enemyFireRateMultiplier = 1f;

    [Header("Music")]
    [SerializeField] private EventReference music;

    [Header("Parallax background")]
    [SerializeField] private bool autoScaleParallaxSpeedByDepth = true;
    [Min(0f)]
    [SerializeField] private float farthestLayerSpeedMultiplier = 0.25f;
    [SerializeField] private ParallaxLayer[] parallaxLayers =
        Array.Empty<ParallaxLayer>();
    [SerializeField] private RandomBackgroundObject[] randomBackgroundObjects =
        Array.Empty<RandomBackgroundObject>();

    [Header("Waves (in spawn order)")]
    [SerializeField] private GameObject[] waves = Array.Empty<GameObject>();

    public int Id => id;
    public string DisplayName => displayName;
    public LevelConfig RequiredLevel => requiredLevel;
    public bool BonusLevel => bonusLevel;
    public int GoldReward => goldReward;
    public int CoreReward => coreReward;
    public MetalPickup MetalPickupPrefab => metalPickupPrefab;
    public IReadOnlyList<WaveMetalDropSettings> WaveMetalDrops => waveMetalDrops;
    public int MetalDropMinimum => GetMetalDropTotal(useMaximum: false);
    public int MetalDropMaximum => GetMetalDropTotal(useMaximum: true);
    public int TotalMetalMinimum => MetalDropMinimum;
    public int TotalMetalMaximum => MetalDropMaximum;
    public float EnemyHullHealthMultiplier => enemyHullHealthMultiplier;
    public float EnemyShieldHealthMultiplier => enemyShieldHealthMultiplier;
    public float EnemyDamageMultiplier => enemyDamageMultiplier;
    public float EnemyFireRateMultiplier => enemyFireRateMultiplier;
    public bool IsStartLevel => requiredLevel == null;
    public EventReference Music => music;
    public bool AutoScaleParallaxSpeedByDepth => autoScaleParallaxSpeedByDepth;
    public float FarthestLayerSpeedMultiplier => farthestLayerSpeedMultiplier;
    public IReadOnlyList<ParallaxLayer> ParallaxLayers => parallaxLayers;
    public IReadOnlyList<RandomBackgroundObject> RandomBackgroundObjects =>
        randomBackgroundObjects;
    public IReadOnlyList<GameObject> Waves => waves;

    public WaveMetalDropSettings GetWaveMetalDropSettings(int waveIndex)
    {
        if (waveMetalDrops == null
            || waveIndex < 0
            || waveIndex >= waveMetalDrops.Count)
        {
            return default;
        }

        return waveMetalDrops[waveIndex];
    }

    public void EnsureWaveMetalDropSettings()
    {
        waveMetalDrops ??= new List<WaveMetalDropSettings>();

        int waveCount = waves?.Length ?? 0;
        while (waveMetalDrops.Count < waveCount)
            waveMetalDrops.Add(default);

        if (waveMetalDrops.Count > waveCount)
            waveMetalDrops.RemoveRange(waveCount, waveMetalDrops.Count - waveCount);

        for (int i = 0; i < waveMetalDrops.Count; i++)
        {
            WaveMetalDropSettings settings = waveMetalDrops[i];
            settings.Validate();
            waveMetalDrops[i] = settings;
        }
    }

    private int GetMetalDropTotal(bool useMaximum)
    {
        if (waveMetalDrops == null)
            return 0;

        int total = 0;
        for (int i = 0; i < waveMetalDrops.Count; i++)
        {
            total += useMaximum
                ? waveMetalDrops[i].MaxMetal
                : waveMetalDrops[i].MinMetal;
        }

        return total;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureWaveMetalDropSettings();
        enemyHullHealthMultiplier = Mathf.Max(0.01f, enemyHullHealthMultiplier);
        enemyShieldHealthMultiplier = Mathf.Max(0.01f, enemyShieldHealthMultiplier);
        enemyDamageMultiplier = Mathf.Max(0.01f, enemyDamageMultiplier);
        enemyFireRateMultiplier = Mathf.Max(0.01f, enemyFireRateMultiplier);

        if (requiredLevel == this)
        {
            Debug.LogError(
                $"{name}: level cannot require itself.",
                this);
            requiredLevel = null;
        }

        for (int i = 0; i < waves.Length; i++)
        {
            GameObject wavePrefab = waves[i];
            if (wavePrefab != null
                && wavePrefab.GetComponent<Wave>() == null)
            {
                Debug.LogError(
                    $"{name}: object {wavePrefab.name} at wave index {i} "
                    + "does not contain a Wave component.",
                    this);
            }
        }
    }
#endif
}
