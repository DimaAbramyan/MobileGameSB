using System.Linq;
using UnityEngine;
using System.Collections.Generic;

using NUnit.Framework;

public class Centipede : ParentShip, iHaveAbilities
{
    private List<Weapon> weapons;

    public List<Weapon> Weapons
    {
        get { return weapons; }
        set { weapons = value; }
    }
    public void Start()
    {
        weapons = GetComponent<WeaponController>()
                    .weapons
                    .Take(2)
                    .ToList();
        Debug.Log(weapons.Count);
        GetComponent<TailCreateAbility>().Init(this);
    }
    public void Init()
    {

    }
    public void UltimateAbility()
    {

    }
    public void PassiveAbility()
    {

    }
}
