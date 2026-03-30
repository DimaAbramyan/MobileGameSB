using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[CreateAssetMenu(menuName = "Impact/FlyThrough")]
public class FlyThroughSO : ImpactBehaviorSO
{
    public override void OnImpact(iDamagable target, Projectile projectile)
    {
        if (target != null)
            DealDamageManager.instanse.DealDamage(target, projectile);
    }
}
