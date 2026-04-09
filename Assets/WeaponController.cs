using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class WeaponController : MonoBehaviour
{
    [Inject] SoundManager soundManager;
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
                //soundManager.PlaySound(weapon.weaponData.AudioClipProjectileShot, transform.position);
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
            soundManager.StopContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
            weapon.HideWeapon();
        }
    }
    public void ShowWeapons()
    {
        foreach (Weapon weapon in weapons)
        {
            soundManager.PlayContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
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
            soundManager.StopContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
        }
        Debug.Log("Нельзя стрелять");
        yield return new WaitForSeconds(seconds);
        foreach (Weapon weapon in weapons)
        {
           weapon.AbleToShoot(true);
           soundManager.PlayContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
        }
        Debug.Log("Можно стрелять");
    }
}
