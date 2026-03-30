using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IContiniousImpactBehavior
{
    void OnImpact(iDamagable target, Projectile projectile);
}
