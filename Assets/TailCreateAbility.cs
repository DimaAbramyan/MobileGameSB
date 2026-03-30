using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TailCreateAbility : ActiveAbility
{
    float baseMaxHealth;
    float baseMaxShield;
    int tailsCreated;
    [SerializeField] GameObject tailPrefab;
    WeaponController controller;
    List<Weapon> weapons;
    ParentShip parentShip;
    public override bool Activate(ParentShip owner)
    {
        tailsCreated++;
        if (owner.GetLevel() < 4)
            return false;
        owner.SetLevel(0);

        Vector3 offset = tailsCreated*new Vector3(0,-0.5f,0);

        GameObject newTale = Instantiate(tailPrefab, transform);
        newTale.transform.position += offset;

        foreach (Weapon weapon in weapons.Take(2))
        {
            GameObject newWeapon = Instantiate(weapon, transform).gameObject;
            newWeapon.transform.position += offset;
            controller.AddNewWeapon(weapon);
        }
        controller.UpdateWeapons();

        parentShip.AddMaxHealthPoints(baseMaxHealth);
        parentShip.AddMaxShieldPoints(baseMaxShield);

        return true;
    }
    public void Init(Centipede owner)
    {
        weapons = owner.Weapons;
        parentShip = GetComponent<ParentShip>();
        controller = GetComponent<WeaponController>();
        baseMaxHealth = parentShip.MaximumHealthPoints;
        baseMaxShield = parentShip.MaximumShieldPoints;
        Debug.Log(weapons.Count);
        
    }
}
