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

[CreateAssetMenu(
    fileName = "QBeamData",
    menuName = "Game/Weapon Data/Q-Beam")]
public sealed class QBeamData : WeaponData
{
    [Header("Q-Beam Levels")]
    [SerializeField] private List<QBeamLevelConfig> qBeamLevels = new();

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
    }
}
