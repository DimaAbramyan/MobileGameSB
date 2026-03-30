using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TailCreateAbility : ActiveAbility
{
    int tailsCreated;
    [SerializeField] GameObject tailPrefab;
    WeaponController controller;
    List<Weapon> weapons;
    ParentShip parentShip;
    public override bool Activate(ParentShip owner)
    {
        if (owner.GetLevel() < 4)
            return false;
        owner.SetLevel(0);

        Vector3 offset = tailsCreated*new Vector3(0,-0.5f,0);

        GameObject newTale = Instantiate(tailPrefab, transform);
        newTale.transform.position += offset;

        foreach (Weapon weapon in weapons)
        {
            GameObject newWeapon = Instantiate(weapon, transform).gameObject;
            newWeapon.transform.position += offset;
            controller.AddNewWeapon(weapon);
        }
        controller.UpdateWeapons();

        parentShip.AddMaxHealthPoints(parentShip.ShipData.maximumHealthPoints);
        parentShip.AddMaxShieldPoints(parentShip.ShipData.maximumHealthPoints);

        tailsCreated++;
        return true;
    }
    public void Init(Centipede owner)
    {
        weapons = owner.Weapons;
        parentShip = GetComponent<ParentShip>();
        controller = GetComponent<WeaponController>();
    }
}
