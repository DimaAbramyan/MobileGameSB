using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldCreateAbility : ActiveAbility
{
    private float ShieldHealth;
    [SerializeField]
    ShieldAbilityPrefab shieldPrefab;
    public override bool Activate(ParentShip owner)
    {
        ShieldHealth = 0;
        ShieldHealth = owner.CurrentShieldPoints;
        if (ShieldHealth > 0)
        {
            ShieldAbilityPrefab shield = Instantiate(shieldPrefab);
            shield.Init(ShieldHealth);
            owner.SetShieldPoints(0);
            return true;
        }
        else
        {
            return false;
        }
    }
}
