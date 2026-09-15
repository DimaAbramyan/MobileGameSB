using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class WeaponController : MonoBehaviour
{
    [Inject] SoundManager soundManager;

    [Header("Battle Weapon Placement")]
    [Tooltip("The common mount used by every equipped weapon during battle.")]
    [SerializeField] private Transform battleWeaponMount;

    public ParentShip parentShip { get; private set; }
    public List<Weapon> weapons { get; private set; } = new List<Weapon>();
    public float reloadMultiplier = 1f;
    private int shootingSuppressionRequests;
    private float temporaryFireRateMultiplier = 1f;
    private float temporaryFireRateMultiplierUntil;
    private bool battleWeaponsConfigured;
    private readonly List<Weapon> battleWeapons = new List<Weapon>();
    private readonly List<Weapon> externalWeapons = new List<Weapon>();
    private BeamVisualBlendController beamVisualBlendController;

    public Transform BattleWeaponMount => battleWeaponMount;

    public void Init(ParentShip ship)
    {
        parentShip = ship;
        ConfigureBattleWeapons();
        UpdateWeapons();
        EnsureBeamVisualBlendController();
    }

    public void UpdateWeapons()
    {
        externalWeapons.RemoveAll(weapon => weapon == null);

        var updatedWeapons = new List<Weapon>(
            battleWeapons.Count + externalWeapons.Count);
        AddValidWeapons(updatedWeapons, battleWeapons);
        AddValidWeapons(updatedWeapons, externalWeapons);
        weapons = updatedWeapons;

        RefreshWeaponOwners();
        beamVisualBlendController?.SetWeapons(weapons);
    }

    private void ConfigureBattleWeapons()
    {
        if (battleWeaponsConfigured || parentShip == null)
            return;

        battleWeaponsConfigured = true;

        Weapon[] equippedWeapons =
            parentShip.GetComponentsInChildren<Weapon>(true);
        var primaryWeapons = new Dictionary<WeaponData, Weapon>();
        var weaponCounts = new Dictionary<WeaponData, int>();

        for (int index = 0; index < equippedWeapons.Length; index++)
        {
            Weapon weapon = equippedWeapons[index];
            if (weapon == null)
                continue;

            WeaponData data = weapon.weaponData;
            if (data == null)
            {
                Debug.LogError(
                    $"Equipped weapon '{weapon.name}' has no WeaponData assigned.",
                    weapon);
                battleWeapons.Add(weapon);
                PlaceBattleWeapon(weapon, Vector3.zero);
                continue;
            }

            if (!primaryWeapons.TryGetValue(data, out Weapon primaryWeapon))
            {
                primaryWeapons.Add(data, weapon);
                weaponCounts.Add(data, 1);
                battleWeapons.Add(weapon);
                continue;
            }

            weaponCounts[data]++;
            Destroy(weapon.gameObject);
        }

        foreach (KeyValuePair<WeaponData, Weapon> entry in primaryWeapons)
        {
            Weapon weapon = entry.Value;
            if (weapon == null)
                continue;

            weapon.SetIdenticalWeaponCount(weaponCounts[entry.Key]);
            PlaceBattleWeapon(weapon, entry.Key.BattleOffset);
        }
    }

    private void PlaceBattleWeapon(Weapon weapon, Vector3 offset)
    {
        if (weapon == null || parentShip == null)
            return;

        Transform mount = battleWeaponMount != null
            ? battleWeaponMount
            : parentShip.transform;

        weapon.transform.SetParent(mount, false);
        weapon.transform.localPosition = offset;
        weapon.transform.localRotation = Quaternion.identity;
    }

    private void EnsureBeamVisualBlendController()
    {
        if (beamVisualBlendController == null)
            TryGetComponent(out beamVisualBlendController);
        if (beamVisualBlendController == null)
            beamVisualBlendController = gameObject.AddComponent<
                BeamVisualBlendController>();

        beamVisualBlendController.SetWeapons(weapons);
    }

    private static void AddValidWeapons(
        List<Weapon> destination,
        List<Weapon> source)
    {
        for (int index = 0; index < source.Count; index++)
        {
            Weapon weapon = source[index];
            if (weapon != null && !destination.Contains(weapon))
                destination.Add(weapon);
        }
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
                weapon.Reload(
                    reloadMultiplier /
                    (weapon.IdenticalWeaponFireRateMultiplier
                        * CurrentFireRateMultiplier));
                //soundManager.PlaySound(weapon.weaponData.AudioClipProjectileShot, transform.position);
            }
        }
    }
    public void SetReloadMultiplier(float newReloadMultiplier)
    {
        reloadMultiplier = newReloadMultiplier;
    }

    private float CurrentFireRateMultiplier =>
        Time.time < temporaryFireRateMultiplierUntil
            ? temporaryFireRateMultiplier
            : 1f;

    public float BeamVisualTransitionRate =>
        CurrentFireRateMultiplier / Mathf.Max(0.01f, reloadMultiplier);

    public void ActivateFireRateMultiplier(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        temporaryFireRateMultiplier =
            Time.time < temporaryFireRateMultiplierUntil
                ? Mathf.Max(temporaryFireRateMultiplier, multiplier)
                : multiplier;
        temporaryFireRateMultiplierUntil = Mathf.Max(
            temporaryFireRateMultiplierUntil,
            Time.time + duration);
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
        beamVisualBlendController?.SetWeapons(weapons);
    }

    public void UnregisterExternalWeapon(Weapon weapon)
    {
        if (weapon == null)
            return;

        externalWeapons.Remove(weapon);
        weapons.Remove(weapon);
        beamVisualBlendController?.SetWeapons(weapons);
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
