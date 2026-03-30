using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public ParentShip parentShip { get; private set; }
    public List<Weapon> weapons { get; private set; }
    public float reloadMultiplier = 1f;
    public void Init(ParentShip ship)
    {
        parentShip = ship;
        UpdateWeapons();
    }
    public void UpdateWeapons()
    {
        weapons = new List<Weapon>();
        weapons = parentShip.GetComponentsInChildren<Weapon>().ToList();
        foreach (Weapon weapon in weapons)
        {
            weapon.SetOwner(parentShip);
        }
    }
    private void FixedUpdate()
    {
        foreach (Weapon weapon in weapons)
        {
            if (weapon == null) continue;
            if (weapon.TryToShoot())
            {
                weapon.Reload(reloadMultiplier);
            }
        }
    }
    public void SetReloadMultiplier(float newReloadMultiplier)
    {
        reloadMultiplier = newReloadMultiplier;
    }
    public void HideWeapons()
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.HideWeapon();
        }
    }
    public void ShowWeapons()
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.ShowWeapon();
        }
    }
    public void AddNewWeapon(Weapon weapon)
    {
        weapons.Add(weapon);
    }
    public void StopShootingForSeconds(float seconds)
    {
        StartCoroutine(StopShootingCoroutine(seconds));
    }
    private IEnumerator StopShootingCoroutine(float seconds)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.AbleToShoot(false);
        }
        Debug.LogError("Нельзя стрелять");
        yield return new WaitForSeconds(seconds);
        foreach (Weapon weapon in weapons)
        {
            weapon.AbleToShoot(true);
        }
        Debug.LogError("Можно стрелять");
    }
}
