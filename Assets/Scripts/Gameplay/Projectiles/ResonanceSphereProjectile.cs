using System.Collections.Generic;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(Projectile))]
public sealed class ResonanceSphereProjectile : MonoBehaviour,
    iDamagable,
    IProjectileRuntimeLifecycleHandler,
    IProjectileDamageReceiver,
    IEntityProjectileDamageReceiver
{
    [Inject] private DealDamageManager dealDamageManager;
    [Inject] private EnemyManager enemyManager;
    [Inject] private PlayerProjectileCollisionRegistry playerProjectileCollisionRegistry;

    [SerializeField] private SpriteRenderer sphereRenderer;

    private readonly HashSet<Enemy> damagedEnemies = new();

    private Projectile projectile;
    private Vector3 initialScale;
    private Color initialColor;
    private float spriteDiameter;
    private float maximumStoredDamage;
    private float explosionRadius;
    private float waveSpeed;
    private float storedDamage;
    private float currentWaveRadius;
    private bool isConfigured;
    private bool isDetonating;

    private void Awake()
    {
        projectile = GetComponent<Projectile>();
        if (sphereRenderer == null)
            sphereRenderer = GetComponent<SpriteRenderer>();

        initialScale = transform.localScale;
        initialColor = sphereRenderer != null ? sphereRenderer.color : Color.white;
        spriteDiameter = sphereRenderer != null && sphereRenderer.sprite != null
            ? Mathf.Max(0.01f, sphereRenderer.sprite.bounds.size.x)
            : 1f;
    }

    public void Initialize(
        Projectile ownerProjectile,
        ProjectileRuntimeConfig runtimeConfig)
    {
        isConfigured = runtimeConfig != null && runtimeConfig.isResonanceSphere;
        isDetonating = false;
        storedDamage = 0f;
        currentWaveRadius = 0f;
        damagedEnemies.Clear();

        if (!isConfigured)
            return;

        maximumStoredDamage = Mathf.Max(
            0f,
            runtimeConfig.resonanceSphereMaximumStoredDamage);
        explosionRadius = Mathf.Max(
            0f,
            runtimeConfig.resonanceSphereExplosionRadius);
        waveSpeed = Mathf.Max(0.01f, runtimeConfig.resonanceSphereWaveSpeed);

        if (sphereRenderer != null)
            sphereRenderer.color = initialColor;

        if (isConfigured)
            playerProjectileCollisionRegistry?.RegisterDamageReceiver(this);
    }

    public bool CanReceiveProjectileDamage(Projectile incomingProjectile)
    {
        return isConfigured
            && !isDetonating
            && incomingProjectile != null
            && incomingProjectile.Owner != null
            && incomingProjectile.DamageType != EnemyDamageType.Resonance;
    }

    public void ReceiveProjectileDamage(
        Projectile incomingProjectile,
        float damage)
    {
        if (!CanReceiveProjectileDamage(incomingProjectile) || damage <= 0f)
            return;

        storedDamage = Mathf.Min(maximumStoredDamage, storedDamage + damage);
    }

    public bool CanReceiveEntityProjectileDamage(
        ParentShip owner,
        EnemyDamageType damageType)
    {
        return isConfigured
            && !isDetonating
            && owner != null
            && damageType != EnemyDamageType.Resonance;
    }

    public void ReceiveEntityProjectileDamage(
        ParentShip owner,
        EnemyDamageType damageType,
        float damage)
    {
        if (!CanReceiveEntityProjectileDamage(owner, damageType)
            || damage <= 0f)
        {
            return;
        }

        storedDamage = Mathf.Min(maximumStoredDamage, storedDamage + damage);
    }

    public void TakeDamage(float damage)
    {
        // Projectile damage is routed through IProjectileDamageReceiver to retain
        // its source type and to exclude Resonance damage.
    }

    public void Dying()
    {
    }

    public bool TryHandleLifetimeExpired(Projectile ownerProjectile)
    {
        if (!isConfigured || isDetonating || ownerProjectile != projectile)
            return false;

        isDetonating = true;
        currentWaveRadius = 0f;
        projectile.SuspendRuntimeForExternalLifecycle();
        return true;
    }

    public void ResetProjectile()
    {
        playerProjectileCollisionRegistry?.UnregisterDamageReceiver(this);
        isConfigured = false;
        isDetonating = false;
        storedDamage = 0f;
        currentWaveRadius = 0f;
        damagedEnemies.Clear();
        transform.localScale = initialScale;

        if (sphereRenderer != null)
            sphereRenderer.color = initialColor;
    }

    private void Update()
    {
        if (!isDetonating)
            return;

        currentWaveRadius = Mathf.MoveTowards(
            currentWaveRadius,
            explosionRadius,
            waveSpeed * Time.deltaTime);
        ApplyWaveVisual();
        DamageReachedEnemies();

        if (currentWaveRadius >= explosionRadius)
            projectile.ReturnToPool();
    }

    private void ApplyWaveVisual()
    {
        float diameter = Mathf.Max(0.02f, currentWaveRadius * 2f);
        float scaleMultiplier = diameter / spriteDiameter;
        transform.localScale = initialScale * scaleMultiplier;

        if (sphereRenderer == null)
            return;

        Color color = initialColor;
        float progress = explosionRadius <= Mathf.Epsilon
            ? 1f
            : currentWaveRadius / explosionRadius;
        color.a *= 1f - Mathf.Clamp01(progress);
        sphereRenderer.color = color;
    }

    private void DamageReachedEnemies()
    {
        if (storedDamage <= 0f
            || dealDamageManager == null
            || enemyManager?.enemyList == null)
        {
            return;
        }

        float radiusSqr = currentWaveRadius * currentWaveRadius;
        for (int index = enemyManager.enemyList.Count - 1; index >= 0; index--)
        {
            Enemy enemy = enemyManager.enemyList[index];
            if (enemy == null
                || enemy.isDead
                || !enemy.isActiveAndEnabled
                || damagedEnemies.Contains(enemy)
                || (enemy.transform.position - transform.position).sqrMagnitude
                    > radiusSqr)
            {
                continue;
            }

            damagedEnemies.Add(enemy);
            dealDamageManager.DealDamage(
                enemy,
                projectile.Owner,
                storedDamage,
                EnemyDamageType.Resonance);
        }
    }
}
