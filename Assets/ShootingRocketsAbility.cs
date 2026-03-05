using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraHealAbility : ActiveAbility
{
    [SerializeField] float degreesFrom;
    [SerializeField] float degreesTo;
    GameObject rocket;
    public override bool Activate(ParentShip owner)
    {
        Instantiate(rocket);
        return true;
    }
}
