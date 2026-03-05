using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;

    public void ActivateCurrentAbility()
    {
        ParentShip ship = playerController.CurrentShip;

        if (ship == null)
            return;

        ship.UseAbility();
    }
}
