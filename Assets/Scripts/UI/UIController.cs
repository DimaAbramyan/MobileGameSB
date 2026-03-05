using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIController : MonoBehaviour
{
    [SerializeField]
    StatBar healthBar;
    [SerializeField]
    StatBar shieldBar;
    [SerializeField]
    StatBar extraHealthBar;
    [SerializeField] private PlayerController playerController;

    private void OnEnable()
    {
        playerController.OnCurrentShipChanged += BindUI;
    }

    private void OnDisable()
    {
        playerController.OnCurrentShipChanged -= BindUI;
    }

    private void BindUI(ParentShip ship)
    {
        healthBar.Setup(
            ship,
            ship.ShipData.maximumHealthPoints,
            ship.SubscribeHealth,
            ship.UnsubscribeHealth,
            ship.CurrentHealthPoints);

        shieldBar.Setup(
            ship,
            ship.ShipData.maximumShieldPoints,
            ship.SubscribeShield,
            ship.UnsubscribeShield,
            ship.CurrentShieldPoints);

        ExtraHealthPassive extraHealth = ship.GetComponent<ExtraHealthPassive>();

        if (extraHealth != null)
        {
            extraHealthBar.Setup(
                ship,
                extraHealth.ExtraHealth,
                extraHealth.SubscribeExtraHealth,
                extraHealth.UnsubscribeExtraHealth,
                extraHealth.ExtraHealth);
        }
        else
        {
            extraHealthBar.SetValue(0);
        }
    }

}
