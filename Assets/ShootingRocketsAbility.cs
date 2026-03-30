using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraHealAbility : ActiveAbility
{
    [SerializeField] HealBuff healBuff;
    private ExtraHealthPassive extraHealthPassive;
    public override bool Activate(ParentShip owner)
    {
        extraHealthPassive = owner.GetComponent<ExtraHealthPassive>();
        if (extraHealthPassive.ExtraHealth <= 0)
        {
            return false;
        }
        Instantiate(healBuff, transform.position + new Vector3(0,7.5f,0),Quaternion.identity);
        healBuff.Init(owner, extraHealthPassive.ExtraHealth);
        extraHealthPassive.SetExtraHealth(0);
        return true;
    }
}
