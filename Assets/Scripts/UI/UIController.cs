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
    private ParentShip boundShip;

    private void OnEnable()
    {
        if (playerController == null)
            return;

        playerController.OnCurrentShipChanged += BindUI;
        BindCurrentShip();
    }

    private void Start()
    {
        BindCurrentShip();
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnCurrentShipChanged -= BindUI;

        UnbindMaxValueEvents();
        boundShip = null;
    }

    private void BindCurrentShip()
    {
        if (playerController != null && playerController.CurrentShip != null)
            BindUI(playerController.CurrentShip);
    }

    private void BindUI(ParentShip ship)
    {
        if (ship == null || ship == boundShip)
            return;

        UnbindMaxValueEvents();
        boundShip = ship;

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

    private void UnbindMaxValueEvents()
    {
        if (boundShip == null)
            return;

        boundShip.OnMaxHealthChanged -= healthBar.UpdateMax;
        boundShip.OnMaxShieldChanged -= shieldBar.UpdateMax;
    }

}
