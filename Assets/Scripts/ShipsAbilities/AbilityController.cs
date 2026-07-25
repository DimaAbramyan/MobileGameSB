using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class AbilityController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private PlayerController playerController;

    public void ActivateCurrentAbility()
    {
        ParentShip ship = playerController.CurrentShip;

        if (ship == null)
            return;

        ship.UseAbility();
    }

    public void ReleaseCurrentAbility()
    {
        ParentShip ship = playerController.CurrentShip;

        if (ship == null)
            return;

        ship.ReleaseAbility();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ActivateCurrentAbility();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseCurrentAbility();
    }
}
