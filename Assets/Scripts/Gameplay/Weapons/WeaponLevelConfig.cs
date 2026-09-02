using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class WeaponLevelConfig
{
    [Header("Base Stats")]
    [SerializeField, Min(0f)] private float reloadTime = 1f;
    [SerializeField] private float angle;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float range = 10f;
    [SerializeField, Min(0f)] private float speed = 10f;

    [Header("Fire")]
    [SerializeField, Min(1)] private int volleysPerActivation = 1;
    [SerializeField, Min(1)] private int projectilesPerVolley = 1;
    [SerializeField, Min(0f)] private float delayBetweenVolleys;
    [SerializeField, Min(0f)] private float spreadAngle;

    [Header("Targeting")]
    [FormerlySerializedAs("maxLockedTargets")]
    [SerializeField, Min(1)] private int maxTargets = 1;
    [FormerlySerializedAs("targetAcquireRange")]
    [SerializeField, Min(0f)] private float targetSearchRadius = 10f;

    public float ReloadTime => reloadTime;
    public float Angle => angle;
    public float Damage => damage;
    public float Range => range;
    public float Speed => speed;
    public int VolleysPerActivation => Mathf.Max(1, volleysPerActivation);
    public int ProjectilesPerVolley => Mathf.Max(1, projectilesPerVolley);
    public float DelayBetweenVolleys => Mathf.Max(0f, delayBetweenVolleys);
    public float SpreadAngle => Mathf.Max(0f, spreadAngle);
    public int MaxTargets => Mathf.Max(1, maxTargets);
    public float TargetSearchRadius => Mathf.Max(0f, targetSearchRadius);
    public int MaxLockedTargets => MaxTargets;
    public float TargetAcquireRange => TargetSearchRadius;

    public static WeaponLevelConfig Create(
        float reloadTime,
        float angle,
        float damage,
        float range,
        float speed)
    {
        return new WeaponLevelConfig
        {
            reloadTime = reloadTime,
            angle = angle,
            damage = damage,
            range = range,
            speed = speed
        };
    }

    public WeaponLevelConfig Clone()
    {
        return new WeaponLevelConfig
        {
            reloadTime = reloadTime,
            angle = angle,
            damage = damage,
            range = range,
            speed = speed,
            volleysPerActivation = volleysPerActivation,
            projectilesPerVolley = projectilesPerVolley,
            delayBetweenVolleys = delayBetweenVolleys,
            spreadAngle = spreadAngle,
            maxTargets = maxTargets,
            targetSearchRadius = targetSearchRadius
        };
    }

    public WeaponRuntimeStats ToRuntimeStats()
    {
        return new WeaponRuntimeStats(
            reloadTime,
            angle,
            damage,
            range,
            speed,
            VolleysPerActivation,
            ProjectilesPerVolley,
            DelayBetweenVolleys,
            SpreadAngle,
            MaxTargets,
            TargetSearchRadius);
    }
}

public readonly struct WeaponRuntimeStats
{
    public WeaponRuntimeStats(
        float reloadTime,
        float angle,
        float damage,
        float range,
        float speed,
        int volleysPerActivation,
        int projectilesPerVolley,
        float delayBetweenVolleys,
        float spreadAngle,
        int maxTargets,
        float targetSearchRadius)
    {
        ReloadTime = reloadTime;
        Angle = angle;
        Damage = damage;
        Range = range;
        Speed = speed;
        VolleysPerActivation = volleysPerActivation;
        ProjectilesPerVolley = projectilesPerVolley;
        DelayBetweenVolleys = delayBetweenVolleys;
        SpreadAngle = spreadAngle;
        MaxTargets = maxTargets;
        TargetSearchRadius = targetSearchRadius;
    }

    public float ReloadTime { get; }
    public float Angle { get; }
    public float Damage { get; }
    public float Range { get; }
    public float Speed { get; }
    public int VolleysPerActivation { get; }
    public int ProjectilesPerVolley { get; }
    public float DelayBetweenVolleys { get; }
    public float SpreadAngle { get; }
    public int MaxTargets { get; }
    public float TargetSearchRadius { get; }
    public int MaxLockedTargets => MaxTargets;
    public float TargetAcquireRange => TargetSearchRadius;
}
