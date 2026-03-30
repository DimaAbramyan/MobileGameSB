using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[CreateAssetMenu(menuName = "Impact/DestroyOnHit")]
public class DestroyOnHitSO : ImpactBehaviorSO
{
    public override void OnImpact(iDamagable target, Projectile projectile)
    {
        if (target != null)
            DealDamageManager.instanse.DealDamage(target, projectile);
        Destroy(projectile.gameObject);
    }
}
