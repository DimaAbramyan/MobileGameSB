using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

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

    [Header("Rewards")]
    [SerializeField, Min(0)] private int metalReward;
    [SerializeField, Min(0)] private int coreReward;

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
    public int MetalReward => metalReward;
    public int CoreReward => coreReward;
    public bool IsStartLevel => requiredLevel == null;
    public EventReference Music => music;
    public bool AutoScaleParallaxSpeedByDepth => autoScaleParallaxSpeedByDepth;
    public float FarthestLayerSpeedMultiplier => farthestLayerSpeedMultiplier;
    public IReadOnlyList<ParallaxLayer> ParallaxLayers => parallaxLayers;
    public IReadOnlyList<RandomBackgroundObject> RandomBackgroundObjects =>
        randomBackgroundObjects;
    public IReadOnlyList<GameObject> Waves => waves;

#if UNITY_EDITOR
    private void OnValidate()
    {
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
