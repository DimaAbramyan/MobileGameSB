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
            () => ship.MaximumHealthPoints,
            ship.SubscribeHealth,
            ship.UnsubscribeHealth,
            ()=>ship.CurrentHealthPoints);
        healthBar.UpdateMax(ship.MaximumHealthPoints); 
        ship.OnMaxHealthChanged += healthBar.UpdateMax;

        shieldBar.Setup(
            ship,
            () => ship.MaximumShieldPoints,
            ship.SubscribeShield,
            ship.UnsubscribeShield,
            () => ship.CurrentShieldPoints);

        shieldBar.UpdateMax(ship.MaximumShieldPoints); 
        ship.OnMaxShieldChanged += shieldBar.UpdateMax;

        ExtraHealthPassive extraHealth = ship.GetComponent<ExtraHealthPassive>();

        if (extraHealth != null)
        {
            extraHealthBar.Setup(
                ship,
                () => extraHealth.MaximumExtraHealth,
                extraHealth.SubscribeExtraHealth,
                extraHealth.UnsubscribeExtraHealth,
                () => extraHealth.ExtraHealth);
        }
        else
        {
            extraHealthBar.SetValue(0);
        }

    }

}
