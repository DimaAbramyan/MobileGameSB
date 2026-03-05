using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealCreate : ActiveAbility
{
    [SerializeField] HealBuff buff;

    public override bool Activate(ParentShip owner)
    {
        
        return true;
    }
}
