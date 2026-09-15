using UnityEngine;

public sealed class DictatorTurret : MonoBehaviour
{
    private ParentShip owner;
    private ProjectilePoolController projectilePoolController;
    private EnemyManager enemyManager;
    private DictatorShipMetaContract contract;
    private float expiresAt;
    private float reloadRemaining;

    public void Initialize(
        ParentShip sourceOwner,
        ProjectilePoolController poolController,
        EnemyManager manager,
        DictatorShipMetaContract metaContract)
    {
        owner = sourceOwner;
        projectilePoolController = poolController;
        enemyManager = manager;
        contract = metaContract;
        expiresAt = Time.time + contract.TurretLifetime;
        reloadRemaining = 0f;
    }

    private void Update()
    {
        if (contract == null || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        reloadRemaining = Mathf.Max(0f, reloadRemaining - Time.deltaTime);
        if (reloadRemaining > 0f)
            return;

        TryFireAtNearestEnemy();
    }

    private void TryFireAtNearestEnemy()
    {
        if (projectilePoolController == null
            || enemyManager == null
            || contract.TurretProjectilePrefab == null)
        {
            return;
        }

        Enemy target = enemyManager.FindNearestEnemy(
            transform.position,
            contract.TurretTargetingRange,
            null);
        if (target == null)
            return;

        Vector3 direction = target.transform.position - transform.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        direction.Normalize();
        transform.up = direction;

        Projectile projectile = projectilePoolController.Spawn(
            contract.TurretProjectilePrefab,
            transform.position,
            transform.rotation);
        if (projectile == null)
            return;

        projectile.Init(
            new ProjectileParams
            {
                speed = contract.TurretProjectileSpeed,
                damage = contract.TurretProjectileDamage,
                maxLength = contract.TurretTargetingRange,
                maxAngle = 0f,
                direction = direction
            },
            new ProjectileRuntimeConfig
            {
                flightMode = ProjectileFlightMode.Straight,
                contactMode = ProjectileContactMode.DamageAndDestroy,
                damageType = contract.TurretDamageType,
                projectileLifetime = Mathf.Max(
                    0.02f,
                    contract.TurretTargetingRange
                        / contract.TurretProjectileSpeed + 0.25f)
            },
            owner);

        reloadRemaining = contract.TurretReloadTime;
    }
}
