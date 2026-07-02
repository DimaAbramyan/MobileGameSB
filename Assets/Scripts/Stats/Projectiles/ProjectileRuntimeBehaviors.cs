using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[Serializable]
public class ProjectileRuntimeConfig
{
    public ProjectileFlightMode flightMode = ProjectileFlightMode.Straight;
    public ProjectileContactMode contactMode = ProjectileContactMode.DamageAndDestroy;

    public float homingRotationSpeed = 360f;
    public bool growDuringFlight;
    public Vector2 scaleGrowthPerSecond = Vector2.one * 0.5f;
    public float projectileLifetime = 10f;
    public bool disableColliderAfterFirstPhysicsStep;
    public bool fadeDuringLifetime;
    public float fadeDuration = 0.5f;
    public Explode explosionPrefab;
    public float explosionDamage = 30f;
    public float continuousDamageInterval = 0.25f;
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
    ExplodeAndSpawn
}

public interface IProjectileMovementBehavior
{
    void Tick(Projectile projectile);
}

public interface IProjectileContactBehavior
{
    void OnEnter(iDamagable target, Projectile projectile);
    void OnStay(iDamagable target, Projectile projectile);
}

public interface IProjectileTickBehavior
{
    void Tick(Projectile projectile);
}

public sealed class ProjectileRuntimeBehaviorSet
{
    private IProjectileMovementBehavior movementBehavior;
    private IProjectileContactBehavior contactBehavior;
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
                    dealDamageManager),
            _ => new DamageAndDestroyContactBehavior(dealDamageManager)
        };

        if (config.growDuringFlight)
            tickBehaviors.Add(
                new ScaleGrowthTickBehavior(config.scaleGrowthPerSecond));
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

    public void Reset()
    {
        movementBehavior = null;
        contactBehavior = null;
        tickBehaviors.Clear();
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

        Vector2 dir = target.transform.position - projectile.transform.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);

        projectile.transform.rotation = Quaternion.RotateTowards(
            projectile.transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );

        projectile.transform.position += projectile.transform.up * projectile.speed * Time.deltaTime;
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

public sealed class ExplodeAndSpawnContactBehavior
    : IProjectileContactBehavior
{
    private readonly Explode explosionPrefab;
    private readonly float explosionDamage;
    private readonly DealDamageManager dealDamageManager;

    public ExplodeAndSpawnContactBehavior(
        Explode explosionPrefab,
        float explosionDamage,
        DealDamageManager dealDamageManager)
    {
        this.explosionPrefab = explosionPrefab;
        this.explosionDamage = explosionDamage;
        this.dealDamageManager = dealDamageManager;
    }

    public void OnEnter(iDamagable target, Projectile projectile)
    {
        if (target != null)
            dealDamageManager.DealDamage(target, projectile);

        if (explosionPrefab != null)
        {
            Explode exp = UnityEngine.Object.Instantiate(explosionPrefab, projectile.transform.position, Quaternion.identity);
            exp.SetDamage(explosionDamage);
        }

        projectile.ReturnToPool();
    }

    public void OnStay(iDamagable target, Projectile projectile)
    {
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
