using UnityEngine;
using Zenject;

[CreateAssetMenu(menuName = "Impact/RocketExplosion")]
public class RocketExplosionImpact : ImpactBehaviorSO
{
    public float explosionRadius = 3f;
    public float explosionDamage = 30f;
    public override void OnImpact(iDamagable target, Projectile projectile)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            projectile.transform.position,
            explosionRadius
        );

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<iDamagable>(out var enemy))
            {
                if (!(hit.gameObject.layer == LayerMask.NameToLayer("Player")))
                    DealDamageManager.instanse.DealDamage(target, projectile);
            }
        }

        Destroy(projectile.gameObject);
    }
}