
using UnityEngine;
using Zenject;

[CreateAssetMenu(menuName = "Impact/RocketExplosion")]
public class RocketExplosionImpact : ImpactBehaviorSO
{
    [SerializeField] Explode explosionPrefab;
    public float explosionRadius = 3f;
    public float explosionDamage = 30f;
    public override void OnImpact(iDamagable target, Projectile projectile)
    {
        Explode exp = Container.InstantiatePrefabForComponent<Explode>(
            explosionPrefab,
            projectile.transform.position,
            Quaternion.identity,
            null);
        exp.SetDamage(explosionDamage);
        projectile.ReturnToPool();
    }
}