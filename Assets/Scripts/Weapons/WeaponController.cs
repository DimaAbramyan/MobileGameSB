using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class WeaponController : MonoBehaviour
{
    [Inject] SoundManager soundManager;
    public ParentShip parentShip { get; private set; }
    public List<Weapon> weapons { get; private set; } = new List<Weapon>();
    public float reloadMultiplier = 1f;
    private int shootingSuppressionRequests;
    private readonly List<Weapon> externalWeapons = new List<Weapon>();
    public void Init(ParentShip ship)
    {
        parentShip = ship;
        UpdateWeapons();
    }
    public void UpdateWeapons()
    {
        externalWeapons.RemoveAll(weapon => weapon == null);

        weapons = parentShip
            .GetComponentsInChildren<Weapon>(true)
            .Where(weapon => weapon != null)
            .Concat(externalWeapons)
            .Where(weapon => weapon != null)
            .Distinct()
            .ToList();

        RefreshWeaponOwners();
    }

    public void RefreshWeaponOwners()
    {
        foreach (Weapon weapon in weapons)
        {
            if (weapon != null)
                weapon.SetOwner(parentShip);
        }
    }
    private void FixedUpdate()
    {
        if (weapons == null)
            return;

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
            if (weapon == null)
                continue;

            soundManager.StopContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
            weapon.HideWeapon();
        }
    }
    public void ShowWeapons()
    {
        foreach (Weapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            soundManager.PlayContiniousSound(weapon.weaponData.AudioClipDefault, transform.position);
            weapon.ShowWeapon();
        }
    }
    public void AddNewWeapon(Weapon weapon)
    {
        RegisterExternalWeapon(weapon);
    }

    public void RegisterExternalWeapon(Weapon weapon)
    {
        if (weapon == null || externalWeapons.Contains(weapon))
            return;

        externalWeapons.Add(weapon);

        if (!weapons.Contains(weapon))
            weapons.Add(weapon);

        weapon.SetOwner(parentShip);
    }

    public void UnregisterExternalWeapon(Weapon weapon)
    {
        if (weapon == null)
            return;

        externalWeapons.Remove(weapon);
        weapons.Remove(weapon);
    }
    public void StopShootingForSeconds(float seconds)
    {
        StartCoroutine(StopShootingCoroutine(seconds));
    }

    public void BeginShootingSuppression()
    {
        shootingSuppressionRequests++;
        SetWeaponsAbleToShoot(false);
    }

    public void EndShootingSuppression()
    {
        if (shootingSuppressionRequests <= 0)
            return;

        shootingSuppressionRequests--;

        if (shootingSuppressionRequests == 0)
            SetWeaponsAbleToShoot(true);
    }

    private IEnumerator StopShootingCoroutine(float seconds)
    {
        BeginShootingSuppression();
        Debug.Log("Нельзя стрелять");
        yield return new WaitForSeconds(seconds);
        EndShootingSuppression();
        Debug.Log("Можно стрелять");
    }

    private void SetWeaponsAbleToShoot(bool ableToShoot)
    {
        foreach (Weapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            weapon.AbleToShoot(ableToShoot);

            if (ableToShoot)
                soundManager.PlayContiniousSound(
                    weapon.weaponData.AudioClipDefault,
                    transform.position);
            else
                soundManager.StopContiniousSound(
                    weapon.weaponData.AudioClipDefault,
                    transform.position);
        }
    }
}
