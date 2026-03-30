using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IImpactBehavior
{
    void OnImpact(iDamagable target, Projectile projectile);
}
