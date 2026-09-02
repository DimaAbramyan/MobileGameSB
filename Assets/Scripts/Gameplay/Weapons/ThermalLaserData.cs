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

[CreateAssetMenu(
    fileName = "ThermalLaserData",
    menuName = "Game/Weapon Data/Thermal Laser")]
public sealed class ThermalLaserData : WeaponData
{
    [Header("Thermal Laser Levels")]
    [SerializeField] private List<ThermalLaserLevelConfig> thermalLevels = new();

    [Header("Beam Collision")]
    [SerializeField] private LayerMask beamBlockingLayers = ~0;

    [Header("Overheat Explosion")]
    [SerializeField, Min(0f)] private float overheatExplosionRadius = 2f;
    [SerializeField, Min(0f)] private float overheatExplosionDamage = 30f;
    [SerializeField, Range(0f, 100f)] private float transferredHeatPercent = 50f;
    [SerializeField, Min(0f)] private float coolingDelay = 0.5f;
    [SerializeField, Range(0f, 100f)] private float coolingPercentPerSecond = 25f;
    [SerializeField] private Explode overheatExplosionPrefab;

    public LayerMask BeamBlockingLayers => beamBlockingLayers;
    public float OverheatExplosionRadius => Mathf.Max(0f, overheatExplosionRadius);
    public float OverheatExplosionDamage => Mathf.Max(0f, overheatExplosionDamage);
    public float TransferredHeatPercent => Mathf.Clamp(transferredHeatPercent, 0f, 100f);
    public float CoolingDelay => Mathf.Max(0f, coolingDelay);
    public float CoolingPercentPerSecond =>
        Mathf.Clamp(coolingPercentPerSecond, 0f, 100f);
    public Explode OverheatExplosionPrefab => overheatExplosionPrefab;

    public float GetHeatPerHitPercent(int requestedLevel)
    {
        if (thermalLevels == null || thermalLevels.Count == 0)
            return 10f;

        int index = Mathf.Clamp(requestedLevel, 0, thermalLevels.Count - 1);
        ThermalLaserLevelConfig config = thermalLevels[index];
        return config != null ? config.HeatPerHitPercent : 0f;
    }

    public EnemyHeatProfile CreateHeatProfile(ParentShip owner)
    {
        return new EnemyHeatProfile(
            owner,
            beamBlockingLayers,
            OverheatExplosionRadius,
            OverheatExplosionDamage,
            TransferredHeatPercent,
            CoolingDelay,
            CoolingPercentPerSecond,
            OverheatExplosionPrefab);
    }

    public void SynchronizeThermalLevels()
    {
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
    }
}
