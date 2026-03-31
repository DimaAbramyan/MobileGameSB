using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BlackHoleActive : ActiveAbility
{
    [SerializeField]
    BlackHolePrefab blackHole;
    public override bool Activate(ParentShip owner)
    {
        float seconds = 3;
        BlackHolePrefab currentBlackHole = Instantiate(blackHole, transform);
        currentBlackHole.Init(seconds);
        Debug.Log(audioManager);
        Debug.Log(audioDatabase);
        audioManager.PlayOneShot(audioDatabase.blackHole, this.transform.position);
        GetComponent<WeaponController>().StopShootingForSeconds(seconds);
        return true;
    }
}
