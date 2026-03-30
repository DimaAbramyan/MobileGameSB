using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ContiniousImpactBehaviorSO : ScriptableObject, IContiniousImpactBehavior
{
    public abstract void OnImpact(iDamagable target, Projectile projectile);
}
