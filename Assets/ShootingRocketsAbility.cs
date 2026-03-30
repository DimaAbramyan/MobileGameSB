using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExtraHealAbility : ActiveAbility
{
    [SerializeField] HealBuff healBuff;
    private ExtraHealthPassive extraHealthPassive;
    public override bool Activate(ParentShip owner)
    {
        extraHealthPassive = owner.GetComponent<ExtraHealthPassive>();
        if (extraHealthPassive == null || extraHealthPassive.ExtraHealth <= 0)
            return false;

        ShipSelect shipSelect = owner.GetComponentInParent<ShipSelect>();
        ParentShip[] ships = shipSelect.GetComponentsInChildren<ParentShip>();

        ParentShip otherShip = ships.FirstOrDefault(ship => ship != owner);
        if (otherShip == null)
            return false;

        float missingHP = otherShip.MaximumHealthPoints - otherShip.CurrentHealthPoints;
        if (missingHP <= 0)
            return false;

        float healAmount = Mathf.Min(extraHealthPassive.ExtraHealth, missingHP);

        otherShip.HealHealth(healAmount);
        extraHealthPassive.SetExtraHealth(extraHealthPassive.ExtraHealth - healAmount);

        return true;
    }
}
