using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BlackHoleActive : ActiveAbility
{
    [SerializeField]
    BlackHolePrefab blackHole;
    [SerializeField] float duration;
    public override bool Activate(ParentShip owner)
    {
        BlackHolePrefab currentBlackHole = Instantiate(blackHole, transform);
        currentBlackHole.Init(duration);
        audioManager.PlaySound(audioDatabase.blackHole, transform.position);
        GetComponent<WeaponController>().StopShootingForSeconds(duration);
        return true;
    }
}
