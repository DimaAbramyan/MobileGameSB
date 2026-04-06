using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public abstract class ImpactBehaviorSO : ScriptableObject, IImpactBehavior
{
    protected DiContainer Container;

    [Inject]
    public void Construct(DiContainer container)
    {
        Container = container;
    }
    public abstract void OnImpact(iDamagable target, Projectile projectile);
}
