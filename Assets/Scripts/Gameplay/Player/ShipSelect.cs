using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShipSelect : MonoBehaviour
{
    private PlayerController playerController;
    private readonly List<ShipVisual> shipVisuals = new List<ShipVisual>();
    private readonly HashSet<ParentShip> defeatedShips = new HashSet<ParentShip>();

    public event Action<int> OnShipChanged;

    private sealed class ShipVisual
    {
        public ParentShip Ship { get; }
        private readonly SpriteRenderer sprite;
        private readonly Collider2D collider;
        private readonly List<Weapon> weapons;

        public ShipVisual(ParentShip ship)
        {
            Ship = ship;
            sprite = ship.GetComponent<SpriteRenderer>();
            collider = ship.GetComponent<Collider2D>();
            weapons = ship.GetComponentsInChildren<Weapon>(true).ToList();
        }

        public void Hide()
        {
            if (sprite != null)
                sprite.enabled = false;
            if (collider != null)
                collider.enabled = false;

            foreach (Weapon weapon in weapons)
            {
                if (weapon != null)
                    weapon.gameObject.SetActive(false);
            }
        }

        public void Show()
        {
            if (sprite != null)
                sprite.enabled = true;
            if (collider != null)
                collider.enabled = true;

            foreach (Weapon weapon in weapons)
            {
                if (weapon != null)
                    weapon.gameObject.SetActive(true);
            }
        }
    }

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    public void InitializeShips()
    {
        playerController ??= GetComponent<PlayerController>();
        shipVisuals.Clear();
        defeatedShips.Clear();

        foreach (ParentShip ship in GetComponentsInChildren<ParentShip>(true))
            shipVisuals.Add(new ShipVisual(ship));

        List<ParentShip> availableShips = GetAvailableShips();
        if (availableShips.Count == 0)
        {
            Debug.LogError("ShipSelect cannot initialize: no player ships were created.");
            return;
        }

        foreach (ParentShip ship in availableShips)
        {
            WeaponController weaponController = ship.GetComponent<WeaponController>();
            if (weaponController == null)
            {
                Debug.LogError($"WeaponController is missing on ship {ship.name}.", ship);
                continue;
            }

            weaponController.Init(ship);
        }

        ActivateShip(availableShips[0]);
    }

    public void SwitchShip()
    {
        if (playerController == null || playerController.ShipSwitchLocked)
            return;

        List<ParentShip> availableShips = GetAvailableShips();
        if (availableShips.Count < 2)
            return;

        int currentIndex = availableShips.IndexOf(playerController.CurrentShip);
        int nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + 1) % availableShips.Count;
        ActivateShip(availableShips[nextIndex]);
    }

    public int LevelUpAllShips()
    {
        int upgradedShips = 0;
        foreach (ParentShip ship in GetAvailableShips())
        {
            int levelBefore = ship.GetLevel();
            ship.LevelUp();
            if (ship.GetLevel() > levelBefore)
                upgradedShips++;
        }

        return upgradedShips;
    }

    public bool HandleShipDeath(ParentShip ship)
    {
        if (ship == null)
            return false;

        defeatedShips.Add(ship);
        GetShipVisual(ship)?.Hide();
        ship.HideShip();
        ship.GetComponent<WeaponController>()?.HideWeapons();

        if (playerController == null || playerController.CurrentShip != ship)
            return playerController != null && playerController.CurrentShip != null;

        List<ParentShip> availableShips = GetAvailableShips();
        if (availableShips.Count == 0)
            return false;

        ActivateShip(availableShips[0]);
        return true;
    }

    private List<ParentShip> GetAvailableShips()
    {
        List<ParentShip> availableShips = new List<ParentShip>();
        foreach (Transform child in transform)
        {
            ParentShip ship = child.GetComponent<ParentShip>();
            if (ship == null || defeatedShips.Contains(ship))
                continue;

            availableShips.Add(ship);
        }

        return availableShips;
    }

    private void ActivateShip(ParentShip ship)
    {
        foreach (ParentShip otherShip in GetAvailableShips())
        {
            if (otherShip == ship)
                continue;

            GetShipVisual(otherShip)?.Hide();
            otherShip.HideShip();
            otherShip.GetComponent<WeaponController>()?.HideWeapons();
        }

        GetShipVisual(ship)?.Show();
        ship.ShowShip();
        ship.GetComponent<WeaponController>()?.ShowWeapons();
        playerController.ChangeShipData(ship);
        OnShipChanged?.Invoke(ship.GetLevel());
    }

    private ShipVisual GetShipVisual(ParentShip ship)
    {
        return shipVisuals.FirstOrDefault(visual => visual.Ship == ship);
    }
}
