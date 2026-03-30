using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShipWeaponsController : MonoBehaviour
{
    private int currentLevel = 0;

    private ParentShip ship;

    private List<Weapon> weapons = new List<Weapon>();

    private void Awake()
    {
        ship = GetComponent<ParentShip>();
        if ( ship != null )
        weapons = ship.GetComponentsInChildren<Weapon>().ToList();
    }
    public void ShipLevelUp()
    {
        currentLevel++;
        foreach (Weapon weapon in weapons)
        {
            //weapon.LevelUp();
        }
    }
}