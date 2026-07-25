using System.Collections.Generic;
using UnityEngine;

public sealed class ChronosTimeFractureField : MonoBehaviour
{
    [SerializeField] private LayerMask enemyLayers = ~0;
    [SerializeField] private LayerMask enemyProjectileLayers = ~0;
    [SerializeField] private LayerMask playerProjectileLayers = ~0;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.05f;

    private readonly Collider2D[] hits = new Collider2D[96];
    private readonly Dictionary<EnemyProjectile, float> affectedEnemyProjectiles = new();
    private readonly Dictionary<Projectile, ProjectileSnapshot> affectedPlayerProjectiles = new();
    private readonly HashSet<EnemyProjectile> seenEnemyProjectiles = new();
    private readonly HashSet<Projectile> seenPlayerProjectiles = new();

    private ContactFilter2D enemyFilter;
    private ContactFilter2D enemyProjectileFilter;
    private ContactFilter2D playerProjectileFilter;
    private ParentShip owner;
    private float duration;
    private float radius;
    private float enemySpeedMultiplier;
    private float enemyProjectileSpeedMultiplier;
    private float playerProjectileSpeedMultiplier;
    private float playerProjectileDamageMultiplier;
    private float collapseDamage;
    private float scanTimer;

    private struct ProjectileSnapshot
    {
        public float Speed;
        public float Damage;
    }

    public void Configure(
        float fieldDuration,
        float fieldRadius,
        float enemyMultiplier,
        float enemyProjectileMultiplier,
        float playerProjectileMultiplier,
        float playerProjectileDamageMult,
        float fieldCollapseDamage,
        ParentShip fieldOwner)
    {
        duration = Mathf.Max(0.1f, fieldDuration);
        radius = Mathf.Max(0.1f, fieldRadius);
        enemySpeedMultiplier = Mathf.Clamp(enemyMultiplier, 0.05f, 1f);
        enemyProjectileSpeedMultiplier = Mathf.Clamp(enemyProjectileMultiplier, 0.05f, 1f);
        playerProjectileSpeedMultiplier = Mathf.Max(1f, playerProjectileMultiplier);
        playerProjectileDamageMultiplier = Mathf.Max(1f, playerProjectileDamageMult);
        collapseDamage = Mathf.Max(0f, fieldCollapseDamage);
        owner = fieldOwner;
        ConfigureFilters();
        transform.localScale = Vector3.one * radius * 2f;
    }

    private void Awake()
    {
        ConfigureFilters();
    }

    private void FixedUpdate()
    {
        duration -= Time.fixedDeltaTime;
        scanTimer -= Time.fixedDeltaTime;

        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            ApplyFieldEffects();
        }

        if (duration <= 0f)
            Collapse();
    }

    private void ApplyFieldEffects()
    {
        SlowEnemies();
        SlowEnemyProjectiles();
        BoostPlayerProjectiles();
        RestoreExitedProjectiles();
    }

    private void SlowEnemies()
    {
        int count = Physics2D.OverlapCircle(
            transform.position,
            radius,
            enemyFilter,
            hits);

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hits[i] != null
                ? hits[i].GetComponentInParent<Enemy>()
                : null;
            Rigidbody2D body = enemy != null ? enemy.GetComponent<Rigidbody2D>() : null;

            if (body != null)
                body.linearVelocity *= enemySpeedMultiplier;
        }
    }

    private void SlowEnemyProjectiles()
    {
        seenEnemyProjectiles.Clear();
        int count = Physics2D.OverlapCircle(
            transform.position,
            radius,
            enemyProjectileFilter,
            hits);

        for (int i = 0; i < count; i++)
        {
            EnemyProjectile projectile = hits[i] != null
                ? hits[i].GetComponentInParent<EnemyProjectile>()
                : null;

            if (projectile == null)
                continue;

            seenEnemyProjectiles.Add(projectile);

            if (affectedEnemyProjectiles.ContainsKey(projectile))
                continue;

            affectedEnemyProjectiles[projectile] = 1f;
            projectile.SetMultiplier(enemyProjectileSpeedMultiplier);
        }
    }

    private void BoostPlayerProjectiles()
    {
        seenPlayerProjectiles.Clear();
        int count = Physics2D.OverlapCircle(
            transform.position,
            radius,
            playerProjectileFilter,
            hits);

        for (int i = 0; i < count; i++)
        {
            Projectile projectile = hits[i] != null
                ? hits[i].GetComponentInParent<Projectile>()
                : null;

            if (projectile == null)
                continue;

            seenPlayerProjectiles.Add(projectile);

            if (affectedPlayerProjectiles.ContainsKey(projectile))
                continue;

            ProjectileSnapshot snapshot = new ProjectileSnapshot
            {
                Speed = projectile.GetSpeed(),
                Damage = projectile.GetDamage()
            };

            affectedPlayerProjectiles[projectile] = snapshot;
            projectile.SetSpeed(snapshot.Speed * playerProjectileSpeedMultiplier);
            projectile.SetDamage(snapshot.Damage * playerProjectileDamageMultiplier);
        }
    }

    private void RestoreExitedProjectiles()
    {
        RestoreExitedEnemyProjectiles();
        RestoreExitedPlayerProjectiles();
    }

    private void RestoreExitedEnemyProjectiles()
    {
        List<EnemyProjectile> toRestore = null;

        foreach (EnemyProjectile projectile in affectedEnemyProjectiles.Keys)
        {
            if (projectile == null || seenEnemyProjectiles.Contains(projectile))
                continue;

            toRestore ??= new List<EnemyProjectile>();
            toRestore.Add(projectile);
        }

        if (toRestore == null)
            return;

        foreach (EnemyProjectile projectile in toRestore)
        {
            if (projectile != null)
                projectile.SetMultiplier(1f);

            affectedEnemyProjectiles.Remove(projectile);
        }
    }

    private void RestoreExitedPlayerProjectiles()
    {
        List<Projectile> toRestore = null;

        foreach (Projectile projectile in affectedPlayerProjectiles.Keys)
        {
            if (projectile == null || seenPlayerProjectiles.Contains(projectile))
                continue;

            toRestore ??= new List<Projectile>();
            toRestore.Add(projectile);
        }

        if (toRestore == null)
            return;

        foreach (Projectile projectile in toRestore)
        {
            RestorePlayerProjectile(projectile);
            affectedPlayerProjectiles.Remove(projectile);
        }
    }

    private void Collapse()
    {
        DealCollapseDamage();
        RestoreAllProjectiles();
        Destroy(gameObject);
    }

    private void DealCollapseDamage()
    {
        int count = Physics2D.OverlapCircle(
            transform.position,
            radius,
            enemyFilter,
            hits);

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hits[i] != null
                ? hits[i].GetComponentInParent<Enemy>()
                : null;

            if (enemy == null || enemy.isDead)
                continue;

            enemy.TakeDamage(collapseDamage);
            owner?.NotifyDamageDealt(collapseDamage);
        }
    }

    private void RestoreAllProjectiles()
    {
        foreach (EnemyProjectile projectile in affectedEnemyProjectiles.Keys)
        {
            if (projectile != null)
                projectile.SetMultiplier(1f);
        }

        foreach (Projectile projectile in affectedPlayerProjectiles.Keys)
            RestorePlayerProjectile(projectile);

        affectedEnemyProjectiles.Clear();
        affectedPlayerProjectiles.Clear();
    }

    private void RestorePlayerProjectile(Projectile projectile)
    {
        if (projectile == null
            || !affectedPlayerProjectiles.TryGetValue(projectile, out ProjectileSnapshot snapshot))
        {
            return;
        }

        projectile.SetSpeed(snapshot.Speed);
        projectile.SetDamage(snapshot.Damage);
    }

    private void ConfigureFilters()
    {
        enemyFilter = CreateFilter(enemyLayers);
        enemyProjectileFilter = CreateFilter(enemyProjectileLayers);
        playerProjectileFilter = CreateFilter(playerProjectileLayers);
    }

    private static ContactFilter2D CreateFilter(LayerMask layerMask)
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = layerMask,
            useTriggers = true
        };
    }

    private void OnDestroy()
    {
        RestoreAllProjectiles();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ConfigureFilters();
    }
#endif
}
