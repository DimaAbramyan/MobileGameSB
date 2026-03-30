using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHoleActive : ActiveAbility
{
    [SerializeField]
    BlackHolePrefab blackHole;
    public override bool Activate(ParentShip owner)
    {
        float seconds = 3;
        BlackHolePrefab currentBlackHole = Instantiate(blackHole, transform);
        currentBlackHole.Init(seconds);
        GetComponent<WeaponController>().StopShootingForSeconds(seconds);
        return true;
    }
}
