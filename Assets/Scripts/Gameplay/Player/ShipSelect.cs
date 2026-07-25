using System.Collections;

using System.Collections.Generic;
using UnityEngine;
using System.Transactions;
using JetBrains.Annotations;
using System.Linq;
using Zenject;
public class ShipSelect : MonoBehaviour
{
    PlayerController playerRB;
    ParentShip allShips;
    public event System.Action<int> OnShipChanged;
    class ShipVisual
    {
        int number;
        int id;
        SpriteRenderer sprite;
        Collider2D collider;
        List<Weapon> weapons;
        public ShipVisual(int number,  int id, SpriteRenderer sprite, Collider2D collider, List<Weapon> weapons)
        {
            this.number = number;
            this.id = id;
            this.sprite = sprite;
            this.collider = collider;
            this.weapons = weapons;
        }
        public int GetId()
        {
            return id;
        }
        public void HideShip()
        {
            sprite.enabled = false;
            collider.enabled = false;
            foreach (var item in weapons)
            {
                item.gameObject.SetActive(false);
            }
        }
        public void ShowShip()
        {
            sprite.enabled = true;
            collider.enabled = true;
            foreach (var item in weapons)
            {
                item.gameObject.SetActive(true);
            }
        }
        public void PrintInfo()
        {
            Debug.Log(
                $"ShipVisual info:\n" +
                $"Number: {number}\n" +
                $"Id: {id}\n" +
                $"Sprite: {(sprite != null ? sprite.name : "NULL")}\n" +
                $"Collider: {(collider != null ? collider.GetType().Name : "NULL")}\n" +
                $"Weapons count: {(weapons != null ? weapons.Count : 0)}"
            );
        }
    }


    List<ShipVisual> shipsVisual = new List<ShipVisual>();

    void Awake()
    {
        playerRB ??= GetComponent<PlayerController>();
    }

    public void InitializeShips()
    {
        playerRB ??= GetComponent<PlayerController>();
        shipsVisual.Clear();
        CollectInfoAboutShips();

        if (shipsVisual.Count == 0)
        {
            Debug.LogError("ShipSelect cannot initialize: no player ships were created.");
            return;
        }

        InitFirstShip();
    }
    public void OnEnable()
    {
        
    }
    public void OnDisable()
    {

    }
    void CollectInfoAboutShips()
    {
        int i = 0;
        List<ParentShip> ships = GetComponentsInChildren<ParentShip>(true).ToList();
        foreach (ParentShip ship in ships)
        {
            shipsVisual.Add(new ShipVisual(i, ship.ShipData.shipId, ship.GetComponent<SpriteRenderer>(), ship.GetComponent<Collider2D>(), ship.GetComponentsInChildren<Weapon>().ToList()));
            shipsVisual[i].PrintInfo();
            i++;
        }
    }
    public void InitFirstShip()
    {
        int i = 0;
        foreach (Transform child in transform)
        {
            ParentShip ship = child.GetComponent<ParentShip>();
            if (ship == null)
                continue;

            WeaponController weaponController = child.GetComponent<WeaponController>();
            if (weaponController == null)
            {
                Debug.LogError($"WeaponController is missing on ship {ship.name}.");
                continue;
            }

            weaponController.Init(ship);
            if (i == 0)
            {
                OnShipChanged?.Invoke(ship.GetLevel());
                playerRB.ChangeShipData(ship);
                shipsVisual[i].ShowShip();
                ship.ShowShip();
                weaponController.ShowWeapons();
            }
            else
            {
                shipsVisual[i].HideShip();
                ship.HideShip();
                weaponController.HideWeapons();
            }
            i++;
        }
    }
    public void SwitchShip()
    {
        if (playerRB != null && playerRB.ShipSwitchLocked)
            return;

        WeaponController controller = null;
        int i = 0;
        foreach (Transform child in transform)
        {
            ParentShip ship = child.GetComponent<ParentShip>();
            if (ship == null)
                continue;
            if (!ship.IsVisible)
            {
                OnShipChanged?.Invoke(ship.GetLevel());
                playerRB.ChangeShipData(ship);
                shipsVisual[i].ShowShip();
                ship.ShowShip();
                controller = ship.GetComponent<WeaponController>();
            }
            else
            { 
                shipsVisual[i].HideShip();
                ship.HideShip();
                ship.GetComponent<WeaponController>().HideWeapons();
            }
            i++;
        }
        controller?.ShowWeapons();
    }
}
