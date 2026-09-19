using System;

using System.Collections.Generic;
using UnityEngine;
using Zenject;

[Serializable]
public class ProjectileRuntimeConfig
{
    public ProjectileFlightMode flightMode = ProjectileFlightMode.Straight;
    public ProjectileContactMode contactMode = ProjectileContactMode.DamageAndDestroy;
    public EnemyDamageType damageType = EnemyDamageType.Kinetic;

    public float homingRotationSpeed = 360f;
    public bool growDuringFlight;
    public Vector2 scaleGrowthPerSecond = Vector2.one * 0.5f;
    public float projectileLifetime = 10f;
    public bool disableColliderAfterFirstPhysicsStep;
    public bool fadeDuringLifetime;
    public float fadeDuration = 0.5f;
    public Explode explosionPrefab;
    public float explosionDamage = 30f;
    public EnemyDamageType explosionDamageType = EnemyDamageType.Explosion;
    public bool explosionBypassesEnemyShield;
    public float explosionRadius;
    public bool explodeAtMaximumRange;
    public ProjectileData secondaryProjectile;
    public bool hasSecondaryProjectileRuntimeStats;
    public float secondaryProjectileDamage;
    public float secondaryProjectileRange;
    public float secondaryProjectileSpeed;
    public SecondaryProjectileSpawnTrigger secondarySpawnTrigger =
        SecondaryProjectileSpawnTrigger.OnEnemyContact;
    public float secondaryTravelDistance;
    public bool ignoreSecondaryProjectileTriggeringEnemy = true;
    public float continuousDamageInterval = 0.25f;
    public IReadOnlyList<EnemyDebuffApplication> enemyDebuffs =
        Array.Empty<EnemyDebuffApplication>();
    public float ballLightningAreaDamage;
    public float ballLightningAreaRadius;
    public float ballLightningAreaTickInterval = 0.5f;
    public LayerMask ballLightningAreaDamageLayers = ~0;
    public int circularChainHitsPerTarget = 3;
    public float circularChainDamagePerHit = 1f;
    public float circularChainHitInterval = 0.02f;
    public int circularChainMaximumTargets = 3;
    public float circularChainSearchConeAngle = 15f;
    public float circularChainSearchRange = 1.25f;
    public float circularChainRandomEscapeAngle = 15f;
}

public enum ProjectileFlightMode
{
    Straight,
    Homing
}

public enum ProjectileContactMode
{
    DamageAndDestroy,
    PierceOnce,
    PierceContinuous,
    ExplodeAndSpawn,
    BallLightning,
    CircularChain,
    ExplodeOnContact
}

public interface IProjectileMovementBehavior
{
    void Tick(Projectile projectile);
}

public interface IProjectileContactBehavior
{
    void OnEnter(iDamagable target, Projectile projectile);
    void OnStay(iDamagable target, Projectile projectile);
    void OnExit(iDamagable target, Projectile projectile);
}

public interface IProjectileTickBehavior
{
    void Tick(Projectile projectile);
}

public interface IProjectileMaximumRangeBehavior
{
    bool TryHandleMaximumRange(Projectile projectile);
}

public sealed class ProjectileRuntimeBehaviorSet
{
    private IProjectileMovementBehavior movementBehavior;
    private IProjectileContactBehavior contactBehavior;
    private IProjectileMaximumRangeBehavior maximumRangeBehavior;
    private readonly List<IProjectileTickBehavior> tickBehaviors = new();
    private readonly DealDamageManager dealDamageManager;
    private readonly EnemyManager enemyManager;

    public ProjectileRuntimeBehaviorSet(DealDamageManager dealDamageManager, EnemyManager enemyManager)
    {
        this.dealDamageManager = dealDamageManager;
        this.enemyManager = enemyManager;
    }

    public void Build(ProjectileRuntimeConfig config, Projectile projectile)
    {
        Reset();

        movementBehavior = config.flightMode switch
        {
            ProjectileFlightMode.Homing => new HomingMovementBehavior(config.homingRotationSpeed, enemyManager),
            _ => new StraightMovementBehavior()
        };

        if (config.contactMode == ProjectileContactMode.CircularChain)
        {
            CircularChainContactBehavior circularChain =
                new CircularChainContactBehavior(
                    config.circularChainHitsPerTarget,
                    config.circularChainDamagePerHit,
                    config.circularChainHitInterval,
                    config.circularChainMaximumTargets,
                    config.circularChainSearchConeAngle,
                    config.circularChainSearchRange,
                    config.circularChainRandomEscapeAngle,
                    dealDamageManager,
                    enemyManager);
            contactBehavior = circularChain;
            tickBehaviors.Add(circularChain);
        }
        else if (config.contactMode == ProjectileContactMode.BallLightning)
        {
            BallLightningContactBehavior ballLightningContact =
                new BallLightningContactBehavior(dealDamageManager);
            contactBehavior = ballLightningContact;
            tickBehaviors.Add(new BallLightningAreaTickBehavior(
                config.ballLightningAreaDamage,
                config.ballLightningAreaRadius,
                config.ballLightningAreaTickInterval,
                config.ballLightningAreaDamageLayers,
                dealDamageManager,
                ballLightningContact));
        }
        else
        {
            contactBehavior = config.contactMode switch
            {
                ProjectileContactMode.PierceOnce =>
                    new PierceOnceContactBehavior(dealDamageManager),
                ProjectileContactMode.PierceContinuous =>
                    new PierceContinuousContactBehavior(
                        config.continuousDamageInterval,
                        dealDamageManager),
                ProjectileContactMode.ExplodeAndSpawn =>
                    new ExplodeAndSpawnContactBehavior(
                        config.explosionPrefab,
                        config.explosionDamage,
                        config.explosionDamageType,
                        config.explosionBypassesEnemyShield,
                        config.explosionRadius,
                        dealDamageManager),
                ProjectileContactMode.ExplodeOnContact =>
                    new ExplodeOnContactBehavior(
                        config.explosionPrefab,
                        config.explosionDamage,
                        config.explosionDamageType,
                        config.explosionBypassesEnemyShield,
                        config.explosionRadius,
                        dealDamageManager),
                _ => new DamageAndDestroyContactBehavior(dealDamageManager)
            };
        }

        if (config.growDuringFlight)
        {
            tickBehaviors.Add(
                new ScaleGrowthTickBehavior(config.scaleGrowthPerSecond));
        }

        if (config.secondaryProjectile != null)
        {
            SecondaryProjectileSpawnBehavior secondarySpawn = new(
                config.secondarySpawnTrigger,
                config.secondaryTravelDistance,
                config.secondaryProjectile,
                config.ignoreSecondaryProjectileTriggeringEnemy);

            if (config.secondarySpawnTrigger
                == SecondaryProjectileSpawnTrigger.OnEnemyContact)
            {
                contactBehavior = new SecondaryProjectileContactDecorator(
                    contactBehavior,
                    secondarySpawn);
            }
            else
            {
                tickBehaviors.Add(secondarySpawn);
            }
        }

        if (config.explodeAtMaximumRange)
        {
            maximumRangeBehavior = new ExplodeAtMaximumRangeBehavior(
                config.explosionPrefab,
                config.explosionDamage,
                config.explosionDamageType,
                config.explosionBypassesEnemyShield,
                config.explosionRadius,
                dealDamageManager);
        }
    }

    public void Move(Projectile projectile)
    {
        movementBehavior?.Tick(projectile);
    }

    public void Tick(Projectile projectile)
    {
        for (int i = 0; i < tickBehaviors.Count; i++)
            tickBehaviors[i].Tick(projectile);
    }

    public void OnContactEnter(iDamagable target, Projectile projectile)
    {
        contactBehavior?.OnEnter(target, projectile);
    }

    public void OnContactStay(iDamagable target, Projectile projectile)
    {
        contactBehavior?.OnStay(target, projectile);
    }

    public void OnContactExit(iDamagable target, Projectile projectile)
    {
        contactBehavior?.OnExit(target, projectile);
    }

    public bool TryHandleMaximumRange(Projectile projectile)
    {
        return maximumRangeBehavior?.TryHandleMaximumRange(projectile) ?? false;
    }

    public void Reset()
    {
        movementBehavior = null;
        contactBehavior = null;
        maximumRangeBehavior = null;
        tickBehaviors.Clear();
    }
}

public sealed class SecondaryProjectileContactDecorator : IProjectileContactBehavior
{
    private readonly IProjectileContactBehavior decoratedBehavior;
    private readonly SecondaryProjectileSpawnBehavior secondarySpawn;

    public SecondaryProjectileContactDecorator(
        IProjectileContactBehavior decoratedBehavior,
        SecondaryProjectileSpawnBehavior secondarySpawn)
    {
        this.decoratedBehavior = decoratedBehavior;
        this.secondarySpawn = secondarySpawn;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        secondarySpawn.TrySpawnFromContact(target, projectile);
        decoratedBehavior?.OnEnter(target, projectile);
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
        decoratedBehavior?.OnStay(target, projectile);
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
        decoratedBehavior?.OnExit(target, projectile);
    }
}

public sealed class SecondaryProjectileSpawnBehavior : IProjectileTickBehavior
{
    private readonly SecondaryProjectileSpawnTrigger spawnTrigger;
    private readonly float travelDistance;
    private readonly ProjectileData secondaryProjectile;
    private readonly bool ignoreTriggeringEnemy;
    private bool hasSpawned;

    public SecondaryProjectileSpawnBehavior(
        SecondaryProjectileSpawnTrigger spawnTrigger,
        float travelDistance,
        ProjectileData secondaryProjectile,
        bool ignoreTriggeringEnemy)
    {
        this.spawnTrigger = spawnTrigger;
        this.travelDistance = Mathf.Max(0f, travelDistance);
        this.secondaryProjectile = secondaryProjectile;
        this.ignoreTriggeringEnemy = ignoreTriggeringEnemy;
    }

    public void TrySpawnFromContact(iDamagable target, Projectile projectile)
    {
        if (spawnTrigger != SecondaryProjectileSpawnTrigger.OnEnemyContact
            || hasSpawned
            || target is not Enemy)
        {
            return;
        }

        Spawn(projectile, ignoreTriggeringEnemy ? target : null);
    }

    public void Tick(Projectile projectile)
    {
        if (spawnTrigger != SecondaryProjectileSpawnTrigger.AfterTravelDistance
            || hasSpawned
            || !projectile.HasReachedTravelDistance(travelDistance))
        {
            return;
        }

        Spawn(projectile, null);
        projectile.ReturnToPool();
    }

    private void Spawn(Projectile projectile, iDamagable ignoredTarget)
    {
        hasSpawned = true;
        projectile.TrySpawnSecondaryProjectile(secondaryProjectile, ignoredTarget);
    }
}

public sealed class StraightMovementBehavior : IProjectileMovementBehavior
{
    public void Tick(Projectile projectile)
    {
        projectile.transform.position += projectile.direction * projectile.speed * Time.deltaTime;
    }
}

public sealed class HomingMovementBehavior : IProjectileMovementBehavior
{
    private readonly float rotationSpeed;
    private Enemy target;
    private readonly EnemyManager enemyManager;

    public HomingMovementBehavior(float rotationSpeed, EnemyManager enemyManager)
    {
        this.rotationSpeed = rotationSpeed;
        this.enemyManager = enemyManager;
    }

    public void Tick(Projectile projectile)
    {
        if (target == null || !target.gameObject.activeSelf)
        {
            target = enemyManager?.FindNearestEnemy(projectile.transform.position);
        }

        if (target == null || !target.gameObject.activeSelf)
        {
            projectile.transform.position += projectile.direction * projectile.speed * Time.deltaTime;
            return;
        }

        Vector2 targetDirection =
            target.transform.position - projectile.transform.position;
        if (targetDirection.sqrMagnitude > Mathf.Epsilon)
        {
            Vector3 nextDirection = Vector3.RotateTowards(
                projectile.direction,
                targetDirection.normalized,
                rotationSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f);
            projectile.SetDirection(nextDirection);
        }

        projectile.transform.position += projectile.direction
            * projectile.speed
            * Time.fixedDeltaTime;
    }
}

public sealed class DamageAndDestroyContactBehavior : IProjectileContactBehavior
{
    private readonly DealDamageManager dealDamageManager;

    public DamageAndDestroyContactBehavior(
        DealDamageManager dealDamageManager)
    {
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        if (target != null)
            dealDamageManager.DealDamage(target, projectile);

        projectile.ReturnToPool();
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }
}

public sealed class PierceOnceContactBehavior : IProjectileContactBehavior
{
    private readonly DealDamageManager dealDamageManager;
    private readonly HashSet<iDamagable> damagedTargets = new();

    public PierceOnceContactBehavior(DealDamageManager dealDamageManager)
    {
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        if (target == null || !damagedTargets.Add(target))
            return;

        dealDamageManager.DealDamage(target, projectile);
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }
}

public sealed class PierceContinuousContactBehavior
    : IProjectileContactBehavior
{
    private readonly float interval;
    private readonly DealDamageManager dealDamageManager;
    private readonly Dictionary<iDamagable, float> nextDamageTimes = new();

    public PierceContinuousContactBehavior(
        float interval,
        DealDamageManager dealDamageManager)
    {
        this.interval = Mathf.Max(0.02f, interval);
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        TryDealDamage(target, projectile);
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
        TryDealDamage(target, projectile);
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }

    private void TryDealDamage(
        iDamagable target,
        Projectile projectile)
    {
        if (target == null)
            return;

        if (nextDamageTimes.TryGetValue(target, out float nextDamageTime)
            && Time.time < nextDamageTime)
        {
            return;
        }

        dealDamageManager.DealDamage(target, projectile);
        nextDamageTimes[target] = Time.time + interval;
    }
}

public sealed class BallLightningContactBehavior : IProjectileContactBehavior
{
    private readonly DealDamageManager dealDamageManager;
    private readonly Dictionary<iDamagable, int> activeContactCounts = new();
    private readonly Dictionary<iDamagable, float> lastDirectDamageTimes = new();

    public BallLightningContactBehavior(DealDamageManager dealDamageManager)
    {
        this.dealDamageManager = dealDamageManager;
    }

    public bool IsReceivingDirectDamage(iDamagable target)
    {
        return target != null && activeContactCounts.ContainsKey(target);
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        RegisterContact(target);
        TryDealDirectDamage(target, projectile);
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
        RegisterContactIfMissing(target);
        TryDealDirectDamage(target, projectile);
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
        if (target == null
            || !activeContactCounts.TryGetValue(target, out int contactCount))
        {
            return;
        }

        if (contactCount > 1)
        {
            activeContactCounts[target] = contactCount - 1;
            return;
        }

        activeContactCounts.Remove(target);
        lastDirectDamageTimes.Remove(target);
    }

    private void RegisterContact(iDamagable target)
    {
        if (target == null)
            return;

        if (activeContactCounts.TryGetValue(target, out int contactCount))
        {
            activeContactCounts[target] = contactCount + 1;
            return;
        }

        activeContactCounts.Add(target, 1);
    }

    private void RegisterContactIfMissing(iDamagable target)
    {
        if (target == null || activeContactCounts.ContainsKey(target))
            return;

        activeContactCounts.Add(target, 1);
    }

    private void TryDealDirectDamage(
        iDamagable target,
        Projectile projectile)
    {
        if (target == null || projectile == null)
            return;

        float currentPhysicsTime = Time.fixedTime;
        if (lastDirectDamageTimes.TryGetValue(
                target,
                out float lastDamageTime)
            && Mathf.Approximately(lastDamageTime, currentPhysicsTime))
        {
            return;
        }

        dealDamageManager.DealDamage(target, projectile);
        lastDirectDamageTimes[target] = currentPhysicsTime;
    }
}

public sealed class BallLightningAreaTickBehavior : IProjectileTickBehavior
{
    private const int OverlapBufferCapacity = 32;

    private readonly float areaDamage;
    private readonly float areaRadius;
    private readonly float tickInterval;
    private readonly int damageLayers;
    private readonly ContactFilter2D damageFilter;
    private readonly DealDamageManager dealDamageManager;
    private readonly BallLightningContactBehavior directContactBehavior;
    private readonly List<Collider2D> overlapResults =
        new(OverlapBufferCapacity);
    private readonly HashSet<iDamagable> damagedTargets = new();

    private float nextPulseTime = -1f;

    public BallLightningAreaTickBehavior(
        float areaDamage,
        float areaRadius,
        float tickInterval,
        LayerMask damageLayers,
        DealDamageManager dealDamageManager,
        BallLightningContactBehavior directContactBehavior)
    {
        this.areaDamage = Mathf.Max(0f, areaDamage);
        this.areaRadius = Mathf.Max(0f, areaRadius);
        this.tickInterval = Mathf.Max(0.02f, tickInterval);
        this.damageLayers = damageLayers.value;
        damageFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = damageLayers,
            useTriggers = true
        };
        this.dealDamageManager = dealDamageManager;
        this.directContactBehavior = directContactBehavior;
    }

    public void Tick(Projectile projectile)
    {
        if (nextPulseTime < 0f)
        {
            nextPulseTime = Time.fixedTime + tickInterval;
            return;
        }

        if (Time.fixedTime < nextPulseTime)
            return;

        nextPulseTime = Time.fixedTime + tickInterval;

        if (projectile == null
            || areaDamage <= 0f
            || areaRadius <= 0f
            || damageLayers == 0)
        {
            return;
        }

        damagedTargets.Clear();
        overlapResults.Clear();
        int overlapCount = Physics2D.OverlapCircle(
            projectile.transform.position,
            areaRadius,
            damageFilter,
            overlapResults);

        for (int index = 0; index < overlapCount; index++)
        {
            Collider2D collider = overlapResults[index];

            if (collider == null
                || !collider.TryGetComponent<iDamagable>(out var target)
                || target == null
                || directContactBehavior.IsReceivingDirectDamage(target)
                || !damagedTargets.Add(target))
            {
                continue;
            }

            dealDamageManager.DealDamage(
                target,
                projectile.Owner,
                areaDamage,
                projectile.DamageType);
        }
    }
}

public sealed class CircularChainContactBehavior
    : IProjectileContactBehavior, IProjectileTickBehavior
{
    private readonly int hitsPerTarget;
    private readonly float damagePerHit;
    private readonly float hitInterval;
    private readonly int maximumTargets;
    private readonly float searchConeAngle;
    private readonly float searchRange;
    private readonly float randomEscapeAngle;
    private readonly DealDamageManager dealDamageManager;
    private readonly EnemyManager enemyManager;
    private readonly List<Enemy> visitedTargets;

      private Enemy currentTarget;
      private Vector3 attachmentLocalPosition;
      private bool isAttachedToTarget;
      private float nextHitTime;
    private float travelSpeed;
    private int hitsAppliedToCurrentTarget;

    public CircularChainContactBehavior(
        int hitsPerTarget,
        float damagePerHit,
        float hitInterval,
        int maximumTargets,
        float searchConeAngle,
        float searchRange,
        float randomEscapeAngle,
        DealDamageManager dealDamageManager,
        EnemyManager enemyManager)
    {
        this.hitsPerTarget = Mathf.Max(1, hitsPerTarget);
        this.damagePerHit = Mathf.Max(0f, damagePerHit);
        this.hitInterval = Mathf.Max(0.02f, hitInterval);
        this.maximumTargets = Mathf.Max(1, maximumTargets);
        this.searchConeAngle = Mathf.Clamp(searchConeAngle, 0f, 360f);
        this.searchRange = Mathf.Max(0.01f, searchRange);
        this.randomEscapeAngle = Mathf.Clamp(randomEscapeAngle, 0f, 360f);
        this.dealDamageManager = dealDamageManager;
        this.enemyManager = enemyManager;
        visitedTargets = new List<Enemy>(this.maximumTargets);
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        TryLatchToTarget(target as Enemy, projectile);
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
        TryLatchToTarget(target as Enemy, projectile);
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }

      public void Tick(Projectile projectile)
      {
          if (projectile == null)
              return;

          if (currentTarget == null)
          {
              if (isAttachedToTarget)
                  ContinueChain(projectile);

              return;
          }

          if (currentTarget.isDead || !currentTarget.isActiveAndEnabled)
          {
              ContinueChain(projectile);
              return;
          }

          projectile.transform.position = currentTarget.transform.TransformPoint(
              attachmentLocalPosition);
        if (Time.fixedTime < nextHitTime)
            return;

        nextHitTime = Time.fixedTime + hitInterval;
        if (damagePerHit > 0f && dealDamageManager != null)
        {
            dealDamageManager.DealDamage(
                currentTarget,
                projectile.Owner,
                damagePerHit,
                projectile.DamageType);
        }

        hitsAppliedToCurrentTarget++;
        if (hitsAppliedToCurrentTarget >= hitsPerTarget)
            ContinueChain(projectile);
    }

    private void TryLatchToTarget(Enemy target, Projectile projectile)
    {
        if (projectile == null
            || currentTarget != null
            || target == null
            || target.isDead
            || !target.isActiveAndEnabled
            || IsVisited(target))
        {
            return;
        }

        if (travelSpeed <= 0f)
            travelSpeed = projectile.speed;

          currentTarget = target;
          isAttachedToTarget = true;
          attachmentLocalPosition = target.transform.InverseTransformPoint(
              projectile.transform.position);
          visitedTargets.Add(target);
          hitsAppliedToCurrentTarget = 0;
          nextHitTime = Time.fixedTime;
          projectile.SetSpeed(0f);
          projectile.transform.position = target.transform.TransformPoint(
              attachmentLocalPosition);
      }

      private void ContinueChain(Projectile projectile)
      {
          currentTarget = null;
          isAttachedToTarget = false;
          hitsAppliedToCurrentTarget = 0;

        if (visitedTargets.Count >= maximumTargets)
        {
            projectile.ReturnToPool();
            return;
        }

        Enemy nextTarget = enemyManager?.FindNearestEnemyInCone(
            projectile.transform.position,
            projectile.direction,
            searchRange,
            searchConeAngle,
            visitedTargets);
        if (nextTarget != null)
        {
            LaunchTowards(projectile, nextTarget.transform.position);
            return;
        }

        LaunchInRandomDirection(projectile);
    }

    private void LaunchTowards(Projectile projectile, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - projectile.transform.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            LaunchInRandomDirection(projectile);
            return;
        }

        projectile.SetDirection(direction);
        projectile.SetSpeed(travelSpeed);
    }

    private void LaunchInRandomDirection(Projectile projectile)
    {
        Vector3 baseDirection = projectile.direction;
        if (baseDirection.sqrMagnitude <= Mathf.Epsilon)
            baseDirection = Vector3.up;

        float halfAngle = randomEscapeAngle * 0.5f;
        float angle = UnityEngine.Random.Range(-halfAngle, halfAngle);
        projectile.SetDirection(
            Quaternion.Euler(0f, 0f, angle) * baseDirection);
        projectile.SetSpeed(travelSpeed);
    }

    private bool IsVisited(Enemy target)
    {
        for (int index = 0; index < visitedTargets.Count; index++)
        {
            if (visitedTargets[index] == target)
                return true;
        }

        return false;
    }
}

public sealed class ExplodeAndSpawnContactBehavior
    : IProjectileContactBehavior
{
    private readonly Explode explosionPrefab;
    private readonly float explosionDamage;
    private readonly EnemyDamageType explosionDamageType;
    private readonly bool explosionBypassesEnemyShield;
    private readonly float explosionRadius;
    private readonly DealDamageManager dealDamageManager;

    public ExplodeAndSpawnContactBehavior(
        Explode explosionPrefab,
        float explosionDamage,
        EnemyDamageType explosionDamageType,
        bool explosionBypassesEnemyShield,
        float explosionRadius,
        DealDamageManager dealDamageManager)
    {
        this.explosionPrefab = explosionPrefab;
        this.explosionDamage = explosionDamage;
        this.explosionDamageType = explosionDamageType;
        this.explosionBypassesEnemyShield = explosionBypassesEnemyShield;
        this.explosionRadius = explosionRadius;
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        if (target != null)
            dealDamageManager.DealDamage(target, projectile);

        ProjectileExplosionSpawner.Spawn(
            explosionPrefab,
            explosionDamage,
            explosionDamageType,
            explosionBypassesEnemyShield,
            explosionRadius,
            projectile.transform.position,
            projectile.Owner,
            dealDamageManager);

        projectile.ReturnToPool();
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }
}

public sealed class ExplodeOnContactBehavior : IProjectileContactBehavior
{
    private readonly Explode explosionPrefab;
    private readonly float explosionDamage;
    private readonly EnemyDamageType explosionDamageType;
    private readonly bool explosionBypassesEnemyShield;
    private readonly float explosionRadius;
    private readonly DealDamageManager dealDamageManager;

    public ExplodeOnContactBehavior(
        Explode explosionPrefab,
        float explosionDamage,
        EnemyDamageType explosionDamageType,
        bool explosionBypassesEnemyShield,
        float explosionRadius,
        DealDamageManager dealDamageManager)
    {
        this.explosionPrefab = explosionPrefab;
        this.explosionDamage = explosionDamage;
        this.explosionDamageType = explosionDamageType;
        this.explosionBypassesEnemyShield = explosionBypassesEnemyShield;
        this.explosionRadius = explosionRadius;
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        ProjectileExplosionSpawner.Spawn(
            explosionPrefab,
            explosionDamage,
            explosionDamageType,
            explosionBypassesEnemyShield,
            explosionRadius,
            projectile.transform.position,
            projectile.Owner,
            dealDamageManager);
        projectile.ReturnToPool();
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
    }

    public void OnExit(iDamagable target, Projectile projectile)
    {
    }
}

public sealed class ExplodeAtMaximumRangeBehavior
    : IProjectileMaximumRangeBehavior
{
    private readonly Explode explosionPrefab;
    private readonly float explosionDamage;
    private readonly EnemyDamageType explosionDamageType;
    private readonly bool explosionBypassesEnemyShield;
    private readonly float explosionRadius;
    private readonly DealDamageManager dealDamageManager;

    public ExplodeAtMaximumRangeBehavior(
        Explode explosionPrefab,
        float explosionDamage,
        EnemyDamageType explosionDamageType,
        bool explosionBypassesEnemyShield,
        float explosionRadius,
        DealDamageManager dealDamageManager)
    {
        this.explosionPrefab = explosionPrefab;
        this.explosionDamage = explosionDamage;
        this.explosionDamageType = explosionDamageType;
        this.explosionBypassesEnemyShield = explosionBypassesEnemyShield;
        this.explosionRadius = explosionRadius;
        this.dealDamageManager = dealDamageManager;
    }

    public bool TryHandleMaximumRange(Projectile projectile)
    {
        if (explosionPrefab == null)
            return false;

        ProjectileExplosionSpawner.Spawn(
            explosionPrefab,
            explosionDamage,
            explosionDamageType,
            explosionBypassesEnemyShield,
            explosionRadius,
            projectile.transform.position,
            projectile.Owner,
            dealDamageManager);
        projectile.ReturnToPool();
        return true;
    }
}

public static class ProjectileExplosionSpawner
{
    public static void Spawn(
        Explode explosionPrefab,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield,
        float radius,
        Vector3 position,
        ParentShip owner,
        DealDamageManager dealDamageManager)
    {
        if (explosionPrefab == null)
            return;

        Explode explosion = UnityEngine.Object.Instantiate(
            explosionPrefab,
            position,
            Quaternion.identity);
        explosion.SetDamage(
            damage,
            damageType,
            bypassesEnemyShield,
            owner,
            dealDamageManager);
        explosion.SetRadius(radius);
    }
}

public sealed class ScaleGrowthTickBehavior : IProjectileTickBehavior
{
    private readonly Vector2 growthPerSecond;

    public ScaleGrowthTickBehavior(Vector2 growthPerSecond)
    {
        this.growthPerSecond = growthPerSecond;
    }

    public void Tick(Projectile projectile)
    {
        projectile.transform.localScale += new Vector3(
            growthPerSecond.x,
            growthPerSecond.y,
            0f) * Time.fixedDeltaTime;
    }
}
