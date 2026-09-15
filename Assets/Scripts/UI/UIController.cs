using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIController : MonoBehaviour
{
    [System.Serializable]
    private sealed class ShipStatusBars
    {
        [SerializeField] private StatBar healthBar;
        [SerializeField] private StatBar shieldBar;
        [SerializeField] private UnityEngine.UI.Image shipPreviewFill;

        private ParentShip boundShip;

        public void Bind(ParentShip ship, Sprite shipIcon)
        {
            Unbind();

            boundShip = ship;
            if (boundShip == null)
                return;

            if (shipPreviewFill != null)
            {
                shipPreviewFill.sprite = shipIcon;
                shipPreviewFill.enabled = shipIcon != null;
            }

            if (healthBar == null || shieldBar == null)
            {
                Debug.LogError(
                    "Both health and shield bars must be configured for a ship status row.");
                boundShip = null;
                return;
            }

            healthBar.Setup(
                boundShip,
                () => boundShip.MaximumHealthPoints,
                boundShip.SubscribeHealth,
                boundShip.UnsubscribeHealth,
                () => boundShip.CurrentHealthPoints);
            healthBar.UpdateMax(boundShip.MaximumHealthPoints);
            boundShip.OnMaxHealthChanged += healthBar.UpdateMax;

            shieldBar.Setup(
                boundShip,
                () => boundShip.MaximumShieldPoints,
                boundShip.SubscribeShield,
                boundShip.UnsubscribeShield,
                () => boundShip.CurrentShieldPoints);
            shieldBar.UpdateMax(boundShip.MaximumShieldPoints);
            boundShip.OnMaxShieldChanged += shieldBar.UpdateMax;
            boundShip.OnDied += Clear;
        }

        public void Unbind()
        {
            if (boundShip != null)
            {
                if (healthBar != null)
                    boundShip.OnMaxHealthChanged -= healthBar.UpdateMax;
                if (shieldBar != null)
                    boundShip.OnMaxShieldChanged -= shieldBar.UpdateMax;
                boundShip.OnDied -= Clear;
            }

            healthBar?.Clear();
            shieldBar?.Clear();
            if (shipPreviewFill != null)
            {
                shipPreviewFill.sprite = null;
                shipPreviewFill.enabled = false;
            }
            boundShip = null;
        }

        private void Clear()
        {
            Unbind();
        }
    }

    [SerializeField]
    StatBar healthBar;
    [SerializeField]
    StatBar shieldBar;
    [SerializeField]
    StatBar extraHealthBar;
    [SerializeField] private PlayerController playerController;
    [Header("Fighting team status bars")]
    [SerializeField] private ShipStatusBars[] shipStatusBars;
    [SerializeField] private HullCatalog hullCatalog;
    private ParentShip boundShip;

    private bool HasTeamStatusBars => shipStatusBars != null
        && shipStatusBars.Length > 0;

    private void OnEnable()
    {
        if (playerController == null)
            return;

        if (!HasTeamStatusBars)
        {
            playerController.OnCurrentShipChanged += BindUI;
            BindCurrentShip();
        }
    }

    private void Start()
    {
        if (HasTeamStatusBars)
            BindTeamStatusBars();
        else
            BindCurrentShip();
    }

    private void OnDisable()
    {
        if (playerController != null && !HasTeamStatusBars)
            playerController.OnCurrentShipChanged -= BindUI;

        UnbindMaxValueEvents();

        if (shipStatusBars != null)
        {
            for (int i = 0; i < shipStatusBars.Length; i++)
                shipStatusBars[i].Unbind();
        }

        boundShip = null;
    }

    private void BindTeamStatusBars()
    {
        if (playerController == null || shipStatusBars == null)
            return;

        ParentShip[] ships =
            playerController.GetComponentsInChildren<ParentShip>(true);
        for (int i = 0; i < shipStatusBars.Length; i++)
        {
            ParentShip ship = i < ships.Length ? ships[i] : null;
            shipStatusBars[i].Bind(ship, GetHullIcon(ship));
        }
    }

    private Sprite GetHullIcon(ParentShip ship)
    {
        if (ship == null || ship.ShipData == null || hullCatalog == null)
            return null;

        var hulls = hullCatalog.Hulls;
        for (int i = 0; i < hulls.Count; i++)
        {
            HullContentDefinition hull = hulls[i];
            if (hull != null && hull.Data == ship.ShipData)
                return hull.Icon;
        }

        Debug.LogWarning(
            $"Hull icon for ship '{ship.name}' is not configured in the hull catalog.",
            ship);
        return null;
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
