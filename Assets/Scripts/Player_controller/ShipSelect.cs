using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Transactions;
using JetBrains.Annotations;
using System.Linq;
public class ShipSelect : MonoBehaviour
{
    PlayerController playerRB;
    ParentShip allShips;
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


    List<ShipVisual> shipsVisual;
    void Awake()
    {
        playerRB = GetComponent<PlayerController>();
        shipsVisual = new List<ShipVisual>();
        CollectInfoAboutShips();
        InitFirstShip();
    }
    void CollectInfoAboutShips()
    {
        int i = 0;
        List<ParentShip> ships = FindAnyObjectByType<PlayerController>().GetComponentsInChildren<ParentShip>().ToList();
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
            child.GetComponent<WeaponController>().Init(child.GetComponent<ParentShip>());
            ParentShip ship = child.GetComponent<ParentShip>();
            if (ship == null)
                continue;
            if (i == 0)
            {
                playerRB.ChangeShipData(ship);
                shipsVisual[i].ShowShip();
                ship.ShowShip();
                ship.GetComponent<WeaponController>().ShowWeapons();
            }
            else
            {
                shipsVisual[i].HideShip();
                ship.HideShip();
                ship.GetComponent<WeaponController>().HideWeapons();
            }
            i++;
        }
    }
    public void SwitchShip()
    {
        int i = 0;
        foreach (Transform child in transform)
        {
            ParentShip ship = child.GetComponent<ParentShip>();
            if (ship == null)
                continue;
            if (!ship.IsVisible)
            {
                playerRB.ChangeShipData(ship);
                shipsVisual[i].ShowShip();
                ship.ShowShip();
                ship.GetComponent<WeaponController>().ShowWeapons();
            }
            else
            { 
                shipsVisual[i].HideShip();
                ship.HideShip();
                ship.GetComponent<WeaponController>().HideWeapons();
            }
            i++;
        }
    }
}
