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

[CreateAssetMenu(
    fileName = "BallLightningData",
    menuName = "Game/Weapon Data/Ball Lightning")]
public sealed class BallLightningData : WeaponData
{
    private static readonly BallLightningLevelConfig DefaultLevel = new();

    [Header("Ball Lightning Levels")]
    [SerializeField] private List<BallLightningLevelConfig> ballLightningLevels = new();

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
        return GetLevel(requestedLevel).DirectDamage;
    }

    public float GetAreaDamage(int requestedLevel)
    {
        return GetLevel(requestedLevel).AreaDamage;
    }

    public float GetAreaTickInterval(int requestedLevel)
    {
        return GetLevel(requestedLevel).AreaTickInterval;
    }

    public void SynchronizeBallLightningLevels()
    {
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
}
