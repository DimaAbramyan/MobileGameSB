using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ContiniousBehaviour/ContiniousDealDamage")]
public class ContiniousDealDamage : ContiniousImpactBehaviorSO
{
    public override void OnImpact(iDamagable target, Projectile projectile)
    {
        if (target != null)
        {
           // DealDamageManager.instanse.DealDamage(target, projectile);
        }
    }
}
