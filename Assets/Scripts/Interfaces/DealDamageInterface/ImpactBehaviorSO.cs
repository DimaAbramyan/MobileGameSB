using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ImpactBehaviorSO : ScriptableObject, IImpactBehavior
{
    public abstract void OnImpact(iDamagable target, Projectile projectile);
}
