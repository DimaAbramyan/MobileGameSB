using System;
using UnityEngine;

[Serializable]
public sealed class OrbitShipMetaContract : ShipMetaContract, IEnemyProjectileDestructionAbility
{
    [SerializeField] private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    [SerializeField, Min(1)] private int objectCount = 3;
    [SerializeField, Min(0.01f)] private float objectRadius = 0.18f;
    [SerializeField, Min(0.01f)] private float maximumChargedObjectRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float baseOrbitRadius = 0.7f;
    [SerializeField, Min(0.01f)] private float maximumOrbitRadius = 1.8f;
    [SerializeField, Min(0f)] private float baseAngularSpeed = 90f;
    [SerializeField, Min(0f)] private float maximumAngularSpeed = 270f;
    [SerializeField, Tooltip("True: clockwise in the XY plane. False: counterclockwise.")]
    private bool clockwise;
    [SerializeField, Min(0.01f), Tooltip("Seconds to reach full satellite speed, orbit radius and object size after pressing the ability. Independent of ability reserve and recharge.")]
    private float chargeUpDuration = 1f;
    // Preserve the previous serialized setting and public API for existing configs.
    [SerializeField, HideInInspector, Min(0.01f)] private float expansionSpeed = 1f;
    [SerializeField, Min(0.01f)] private float collapseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float contactDamage = 2f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;
    [SerializeField, Min(0.02f)] private float damageInterval = 0.5f;
    [SerializeField, Min(0f), Tooltip("Seconds of active ability recovery gained per intercepted enemy projectile. Also advances the Toggle reserve recharge and its delay, including while the ability is held.")]
    private float cooldownReductionPerProjectile = 0.2f;

    public override string DisplayName => "Orbit Satellites";
    public int ObjectCount => Mathf.Max(1, objectCount);
    public float ObjectRadius => Mathf.Min(Mathf.Max(0.01f, objectRadius), MaximumObjectRadius);
    public float MaximumChargedObjectRadius => Mathf.Min(
        Mathf.Max(ObjectRadius, maximumChargedObjectRadius), GetObjectRadiusLimit(MaximumOrbitRadius));
    public float BaseOrbitRadius => Mathf.Max(0.01f, baseOrbitRadius);
    public float MaximumOrbitRadius => Mathf.Max(BaseOrbitRadius, maximumOrbitRadius);
    public float BaseAngularSpeed => Mathf.Max(0f, baseAngularSpeed);
    public float MaximumAngularSpeed => Mathf.Max(BaseAngularSpeed, maximumAngularSpeed);
    public bool Clockwise => clockwise;
    public float ChargeUpDuration => Mathf.Max(0.01f, chargeUpDuration);
    public float ExpansionSpeed => Mathf.Max(0.01f, expansionSpeed);
    public float CollapseSpeed => Mathf.Max(0.01f, collapseSpeed);
    public float ContactDamage => Mathf.Max(0f, contactDamage);
    public EnemyDamageType DamageType => damageType;
    public float DamageInterval => Mathf.Max(0.02f, damageInterval);
    public float CooldownReductionPerProjectile => Mathf.Max(0f, cooldownReductionPerProjectile);

    // Keep a substantial gap even for dense authoring configurations. A single
    // satellite must also never cover the ship and the entire orbital circle.
    public float MaximumObjectRadius => GetObjectRadiusLimit(BaseOrbitRadius);
    public bool IsObjectRadiusLimited => objectRadius > MaximumObjectRadius
        || maximumChargedObjectRadius > GetObjectRadiusLimit(MaximumOrbitRadius);

    public float EvaluateObjectRadius(float intensity)
    {
        float progress = Mathf.Clamp01(intensity);
        float orbitRadius = Mathf.Lerp(BaseOrbitRadius, MaximumOrbitRadius, progress);
        return Mathf.Min(Mathf.Lerp(ObjectRadius, MaximumChargedObjectRadius, progress),
            GetObjectRadiusLimit(orbitRadius));
    }

    private float GetObjectRadiusLimit(float orbitRadius)
    {
        return orbitRadius * 0.65f
            * (ObjectCount == 1 ? 1f : Mathf.Sin(Mathf.PI / ObjectCount));
    }

    public override ShipMetaContract Clone()
    {
        return (OrbitShipMetaContract)MemberwiseClone();
    }
}
