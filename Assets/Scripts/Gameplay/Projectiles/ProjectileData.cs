using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProjectileDeliveryType
{
    Projectile,
    Beam
}

public enum ProjectileDamageTrigger
{
    Contact,
    Explosion,
    Periodic
}

[Serializable]
public sealed class ProjectileDamageSource
{
    [SerializeField] private string id = "source";
    [SerializeField] private ProjectileDamageTrigger trigger;
    [SerializeField, Min(0f)] private float damage;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Kinetic;
    [SerializeField] private bool bypassesShield;

    public string Id => id;
    public ProjectileDamageTrigger Trigger => trigger;
    public float Damage => Mathf.Max(0f, damage);
    public EnemyDamageType DamageType => damageType;
    public bool BypassesShield => bypassesShield;
}

[Serializable]
public abstract class ProjectileDataContract
{
    public abstract string DisplayName { get; }
}

public enum SecondaryProjectileSpawnTrigger
{
    OnEnemyContact,
    AfterTravelDistance
}

[Serializable]
public sealed class ProjectileFlightContract : ProjectileDataContract
{
    [SerializeField] private bool growDuringFlight;
    [SerializeField] private Vector2 scaleGrowthPerSecond = Vector2.one * 0.5f;

    public override string DisplayName => "Flight";
    public bool GrowDuringFlight => growDuringFlight;
    public Vector2 ScaleGrowthPerSecond => scaleGrowthPerSecond;
}

[Serializable]
public sealed class ProjectileHomingContract : ProjectileDataContract
{
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;

    public override string DisplayName => "Homing";
    public float RotationSpeed => Mathf.Max(0f, rotationSpeed);
}

[Serializable]
public sealed class ProjectileTargetingContract : ProjectileDataContract
{
    [SerializeField, Min(1)] private int maxTargets = 1;
    [SerializeField, Min(0f)] private float searchRadius;

    public override string DisplayName => "Targeting";
    public int MaxTargets => Mathf.Max(1, maxTargets);
    public float SearchRadius => Mathf.Max(0f, searchRadius);
}

[Serializable]
public sealed class ProjectileContactContract : ProjectileDataContract
{
    [SerializeField] private ProjectileContactMode contactMode =
        ProjectileContactMode.DamageAndDestroy;

    public override string DisplayName => "Contact Behaviour";
    public ProjectileContactMode ContactMode => contactMode;
}

[Serializable]
public sealed class ProjectileContinuousDamageContract : ProjectileDataContract
{
    [SerializeField, Min(0.02f)] private float damageTickInterval = 0.25f;

    public override string DisplayName => "Continuous Damage";
    public float DamageTickInterval => Mathf.Max(0.02f, damageTickInterval);
}

[Serializable]
public sealed class ProjectileMovementSlowContract : ProjectileDataContract
{
    [SerializeField, Range(0f, 100f)] private float slowPercentPerHit = 5f;
    [SerializeField, Range(0f, 100f)] private float maximumSlowPercent = 50f;

    public override string DisplayName => "Movement Slow";
    public float SlowPercentPerHit => Mathf.Clamp(slowPercentPerHit, 0f, 100f);
    public float MaximumSlowPercent => Mathf.Clamp(
        maximumSlowPercent,
        0f,
        100f);
}

[Serializable]
public sealed class ProjectileEnemyDebuffsContract : ProjectileDataContract
{
    [SerializeField] private List<EnemyDebuffApplication> debuffs = new();

    public override string DisplayName => "Enemy Debuffs";
    public IReadOnlyList<EnemyDebuffApplication> Debuffs => debuffs;

    public void AddDebuff(EnemyDebuffApplication application)
    {
        if (application == null)
            return;

        debuffs ??= new List<EnemyDebuffApplication>();
        debuffs.Add(application);
    }

    public void RemoveInvalidDebuffs()
    {
        debuffs?.RemoveAll(application => application == null
            || application.Debuff == null
            || !application.IsValid);
    }
}

[Serializable]
public sealed class ProjectileCircularChainContract : ProjectileDataContract
{
    [SerializeField, Min(1)] private int hitsPerTarget = 3;
    [SerializeField, Min(0f)] private float damagePerHit = 1f;
    [SerializeField, Min(0.02f)] private float hitInterval = 0.02f;
    [SerializeField, Min(1)] private int maximumTargets = 3;
    [SerializeField, Range(0f, 360f)] private float searchConeAngle = 15f;
    [SerializeField, Min(0.01f)] private float searchRange = 1.25f;
    [SerializeField, Range(0f, 360f)] private float randomEscapeAngle = 15f;

    public override string DisplayName => "Circular Chain";
    public int HitsPerTarget => Mathf.Max(1, hitsPerTarget);
    public float DamagePerHit => Mathf.Max(0f, damagePerHit);
    public float HitInterval => Mathf.Max(0.02f, hitInterval);
    public int MaximumTargets => Mathf.Max(1, maximumTargets);
    public float SearchConeAngle => Mathf.Clamp(searchConeAngle, 0f, 360f);
    public float SearchRange => Mathf.Max(0.01f, searchRange);
    public float RandomEscapeAngle => Mathf.Clamp(randomEscapeAngle, 0f, 360f);
}

[Serializable]
public sealed class ProjectileLifetimeContract : ProjectileDataContract
{
    [SerializeField, Min(0.02f)] private float lifetime = 10f;
    [SerializeField] private bool disableColliderAfterFirstPhysicsStep;
    [SerializeField] private bool fadeBeforeDespawn;
    [SerializeField, Min(0.02f)] private float fadeDuration = 0.5f;

    public override string DisplayName => "Lifetime";
    public float Lifetime => Mathf.Max(0.02f, lifetime);
    public bool DisableColliderAfterFirstPhysicsStep =>
        disableColliderAfterFirstPhysicsStep;
    public bool FadeBeforeDespawn => fadeBeforeDespawn;
    public float FadeDuration => Mathf.Clamp(fadeDuration, 0.02f, Lifetime);
}

[Serializable]
public sealed class ProjectileExplosionContract : ProjectileDataContract
{
    [SerializeField] private Explode explosionPrefab;
    [SerializeField, Min(0f)] private float explosionRadius;
    [SerializeField] private bool explodeAtMaximumRange;

    public override string DisplayName => "Explosion";
    public Explode ExplosionPrefab => explosionPrefab;
    public float ExplosionRadius => Mathf.Max(0f, explosionRadius);
    public bool ExplodeAtMaximumRange => explodeAtMaximumRange;
}

[Serializable]
public sealed class SpawnSecondaryProjectileContract : ProjectileDataContract
{
    [SerializeField] private SecondaryProjectileSpawnTrigger spawnTrigger =
        SecondaryProjectileSpawnTrigger.OnEnemyContact;
    [SerializeField, Min(0f)] private float travelDistance = 1f;
    [SerializeField] private bool ignoreTriggeringEnemy = true;
    [SerializeField] private ProjectileData secondaryProjectile;

    public override string DisplayName => "Spawn Secondary Projectile";
    public SecondaryProjectileSpawnTrigger SpawnTrigger => spawnTrigger;
    public float TravelDistance => Mathf.Max(0f, travelDistance);
    public bool IgnoreTriggeringEnemy => ignoreTriggeringEnemy;
    public ProjectileData SecondaryProjectile => secondaryProjectile;
}

[Serializable]
public sealed class ProjectileDamageSourcesContract : ProjectileDataContract
{
    [SerializeField] private List<ProjectileDamageSource> sources = new();

    public override string DisplayName => "Damage Sources";
    public IReadOnlyList<ProjectileDamageSource> Sources => sources;
}

[Serializable]
public sealed class ProjectileBeamContract : ProjectileDataContract
{
    [SerializeField, Min(0.01f)] private float width = 0.1f;
    [SerializeField, Min(0.02f)] private float damageTickInterval = 0.1f;
    [SerializeField] private List<GameObject> impactEffects = new();

    public override string DisplayName => "Beam";
    public float Width => Mathf.Max(0.01f, width);
    public float DamageTickInterval => Mathf.Max(0.02f, damageTickInterval);
    public IReadOnlyList<GameObject> ImpactEffects => impactEffects;
}

public readonly struct ProjectileRuntimeStats
{
    public ProjectileRuntimeStats(float damage, float range, float speed)
    {
        Damage = Mathf.Max(0f, damage);
        Range = Mathf.Max(0f, range);
        Speed = Mathf.Max(0f, speed);
    }

    public float Damage { get; }
    public float Range { get; }
    public float Speed { get; }
}

[CreateAssetMenu(fileName = "ProjectileData", menuName = "Game/Projectile Data")]
public sealed class ProjectileData : ScriptableObject
{
    [Header("Delivery")]
    [SerializeField] private ProjectileDeliveryType deliveryType;

    [Header("Direct Damage")]
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Kinetic;

    [Header("Physical Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField, Min(0f)] private float range = 10f;
    [SerializeField, Min(0f)] private float speed = 10f;

    [SerializeReference] private List<ProjectileDataContract> contracts = new();

    public ProjectileDeliveryType DeliveryType => deliveryType;
    public Projectile ProjectilePrefab => projectilePrefab;
    public float Damage => Mathf.Max(0f, damage);
    public EnemyDamageType DamageType => damageType;
    public float Range => Mathf.Max(0f, range);
    public float Speed => Mathf.Max(0f, speed);
    public IReadOnlyList<ProjectileDataContract> Contracts => contracts;

    public ProjectileRuntimeStats GetRuntimeStats()
    {
        return new ProjectileRuntimeStats(Damage, Range, Speed);
    }

    public bool TryGetContract<TContract>(out TContract contract)
        where TContract : ProjectileDataContract
    {
        if (contracts != null)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                contract = contracts[index] as TContract;
                if (contract != null)
                    return true;
            }
        }

        contract = null;
        return false;
    }

    public ProjectileRuntimeConfig CreateRuntimeConfig()
    {
        ProjectileRuntimeConfig runtimeConfig = new()
        {
            damageType = damageType
        };

        if (TryGetContract(out ProjectileFlightContract flight))
        {
            runtimeConfig.growDuringFlight = flight.GrowDuringFlight;
            runtimeConfig.scaleGrowthPerSecond = flight.ScaleGrowthPerSecond;
        }

        if (TryGetContract(out ProjectileHomingContract homing))
        {
            runtimeConfig.flightMode = ProjectileFlightMode.Homing;
            runtimeConfig.homingRotationSpeed = homing.RotationSpeed;
        }

        if (TryGetContract(out ProjectileContactContract contact))
            runtimeConfig.contactMode = contact.ContactMode;

        if (TryGetContract(out ProjectileContinuousDamageContract continuousDamage))
            runtimeConfig.continuousDamageInterval = continuousDamage.DamageTickInterval;

        if (TryGetContract(out ProjectileEnemyDebuffsContract enemyDebuffs))
        {
            runtimeConfig.enemyDebuffs = enemyDebuffs.Debuffs;
        }

        if (TryGetContract(out ProjectileCircularChainContract circularChain))
        {
            runtimeConfig.contactMode = ProjectileContactMode.CircularChain;
            runtimeConfig.circularChainHitsPerTarget =
                circularChain.HitsPerTarget;
            runtimeConfig.circularChainDamagePerHit =
                circularChain.DamagePerHit;
            runtimeConfig.circularChainHitInterval = circularChain.HitInterval;
            runtimeConfig.circularChainMaximumTargets =
                circularChain.MaximumTargets;
            runtimeConfig.circularChainSearchConeAngle =
                circularChain.SearchConeAngle;
            runtimeConfig.circularChainSearchRange = circularChain.SearchRange;
            runtimeConfig.circularChainRandomEscapeAngle =
                circularChain.RandomEscapeAngle;
        }

        if (TryGetContract(out ProjectileLifetimeContract lifetime))
        {
            runtimeConfig.projectileLifetime = lifetime.Lifetime;
            runtimeConfig.disableColliderAfterFirstPhysicsStep =
                lifetime.DisableColliderAfterFirstPhysicsStep;
            runtimeConfig.fadeDuringLifetime = lifetime.FadeBeforeDespawn;
            runtimeConfig.fadeDuration = lifetime.FadeDuration;
        }

        if (TryGetContract(out ProjectileExplosionContract explosion))
        {
            runtimeConfig.explosionPrefab = explosion.ExplosionPrefab;
            runtimeConfig.explosionRadius = explosion.ExplosionRadius;
            runtimeConfig.explodeAtMaximumRange =
                explosion.ExplodeAtMaximumRange;
            if (TryGetDamageSource(
                    ProjectileDamageTrigger.Explosion,
                    out ProjectileDamageSource damageSource))
            {
                runtimeConfig.explosionDamage = damageSource.Damage;
                runtimeConfig.explosionDamageType = damageSource.DamageType;
                runtimeConfig.explosionBypassesEnemyShield =
                    damageSource.BypassesShield;
            }
        }

        if (TryGetContract(out SpawnSecondaryProjectileContract secondary))
        {
            runtimeConfig.secondaryProjectile = secondary.SecondaryProjectile;
            runtimeConfig.secondarySpawnTrigger = secondary.SpawnTrigger;
            runtimeConfig.secondaryTravelDistance = secondary.TravelDistance;
            runtimeConfig.ignoreSecondaryProjectileTriggeringEnemy =
                secondary.IgnoreTriggeringEnemy;
        }

        return runtimeConfig;
    }

    public float GetDamageForTrigger(ProjectileDamageTrigger trigger)
    {
        return TryGetDamageSource(trigger, out ProjectileDamageSource source)
            ? source.Damage
            : 0f;
    }

    public bool TryGetDamageSource(
        ProjectileDamageTrigger trigger,
        out ProjectileDamageSource damageSource)
    {
        if (TryGetContract(out ProjectileDamageSourcesContract damageSources)
            && damageSources.Sources != null)
        {
            for (int index = 0; index < damageSources.Sources.Count; index++)
            {
                ProjectileDamageSource source = damageSources.Sources[index];
                if (source != null && source.Trigger == trigger)
                {
                    damageSource = source;
                    return true;
                }
            }
        }

        damageSource = null;
        return false;
    }

#if UNITY_EDITOR
    public bool ContainsContract(Type contractType)
    {
        if (contractType == null || contracts == null)
            return false;

        for (int index = 0; index < contracts.Count; index++)
        {
            if (contracts[index] != null
                && contracts[index].GetType() == contractType)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAddContract(Type contractType)
    {
        if (contractType == null
            || contractType.IsAbstract
            || !typeof(ProjectileDataContract).IsAssignableFrom(contractType)
            || ContainsContract(contractType)
            || Activator.CreateInstance(contractType) is not ProjectileDataContract contract)
        {
            return false;
        }

        contracts ??= new List<ProjectileDataContract>();
        contracts.Add(contract);
        return true;
    }

    public bool TryRemoveContract(Type contractType)
    {
        if (contractType == null || contracts == null)
            return false;

        bool removed = false;
        for (int index = contracts.Count - 1; index >= 0; index--)
        {
            if (contracts[index] != null
                && contracts[index].GetType() == contractType)
            {
                contracts.RemoveAt(index);
                removed = true;
            }
        }

        return removed;
    }
#endif
}
