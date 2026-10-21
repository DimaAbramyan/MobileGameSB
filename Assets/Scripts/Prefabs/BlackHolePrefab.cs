using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

public class BlackHolePrefab : MonoBehaviour, IEnemyProjectileDestructionAbility
{
    private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile;
    public EnemyProjectileDestructionPolicy ProjectileDestructionPolicy =>
        projectileDestructionPolicy;
    float liveTime;
    [SerializeField, HideInInspector]
    float damage = 50;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;
    [Inject] private DealDamageManager damageManager;
    private float runtimeDamage = -1f;
    private ParentShip owner;
    private float fallbackDamageMultiplier = 1f;
    private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    private CircleCollider2D purgeCollider;

    private void Awake()
    {
        purgeCollider = GetComponent<CircleCollider2D>();
    }

    public void Init(
        float lifeTime,
        float metaDamage = -1f,
        ParentShip sourceOwner = null,
        float damageMultiplier = 1f,
        EnemyProjectileCollisionRegistry collisionRegistry = null,
        EnemyProjectileDestructionPolicy destructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile)
    {
        liveTime = lifeTime;
        projectileDestructionPolicy = destructionPolicy;
        runtimeDamage = metaDamage;
        owner = sourceOwner;
        if (owner != null && owner.ShipData != null
            && owner.ShipData.GetMetaRuntimeStats(0).TryGetContract(out BlackHoleShipMetaContract contract))
            damageType = contract.DamageType;
        fallbackDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        projectileCollisionRegistry = collisionRegistry;
        projectileCollisionRegistry?.RegisterProjectilePurgeField(this);
        Destroy(gameObject, liveTime);
    }

    public bool TryGetProjectilePurgeField(
        out EnemyProjectilePurgeField field)
    {
        field = default;
        if (!isActiveAndEnabled || purgeCollider == null
            || !purgeCollider.isActiveAndEnabled)
        {
            return false;
        }

        Bounds bounds = purgeCollider.bounds;
        field = new EnemyProjectilePurgeField
        {
            Center = new float2(bounds.center.x, bounds.center.y),
            Radius = Mathf.Max(bounds.extents.x, bounds.extents.y),
            DestructionPolicy = ProjectileDestructionPolicy
        };
        return field.Radius > 0f;
    }

    private void OnDestroy()
    {
        projectileCollisionRegistry?.UnregisterProjectilePurgeField(this);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        EnemyProjectile proj = collision.gameObject.GetComponent<EnemyProjectile>();

        if (proj != null)
        {
            proj.TryDestroyByAbility((IEnemyProjectileDestructionAbility)this);
        }
        if (enemy != null)
        {
            float baseDamage = runtimeDamage >= 0f ? runtimeDamage : damage;
            damageManager.DealDamage(enemy, owner, baseDamage * GetCurrentDamageMultiplier(), damageType);
        }
    }

    private float GetCurrentDamageMultiplier()
    {
        return owner != null && owner.ShipData != null
            ? owner.ShipData.GetAbilityDamageMultiplier(owner.GetLevel())
            : fallbackDamageMultiplier;
    }
}
