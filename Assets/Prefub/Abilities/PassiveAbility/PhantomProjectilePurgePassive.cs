using System.Collections.Generic;
using UnityEngine;

public sealed class PhantomProjectilePurgePassive : PassiveAbility
{
    [SerializeField, Min(0f)] private float purgeRadius = 2.5f;
    [SerializeField] private LayerMask projectileLayers = ~0;

    private readonly Collider2D[] hits = new Collider2D[64];
    private readonly HashSet<EnemyProjectile> purgedProjectiles = new();
    private ContactFilter2D projectileFilter;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void On()
    {
        base.On();
        PurgeNow();
    }

    public void PurgeNow()
    {
        Transform center = owner != null ? owner.transform : transform;
        purgedProjectiles.Clear();

        ConfigureProjectileFilter();

        int count = Physics2D.OverlapCircle(
            center.position,
            purgeRadius,
            projectileFilter,
            hits);

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
                continue;

            EnemyProjectile enemyProjectile =
                hits[i].GetComponentInParent<EnemyProjectile>();

            if (enemyProjectile == null
                || !purgedProjectiles.Add(enemyProjectile))
            {
                continue;
            }

            Destroy(enemyProjectile.gameObject);
        }
    }

    private void ConfigureProjectileFilter()
    {
        projectileFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = projectileLayers,
            useTriggers = true
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.45f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, purgeRadius);
    }
#endif
}
