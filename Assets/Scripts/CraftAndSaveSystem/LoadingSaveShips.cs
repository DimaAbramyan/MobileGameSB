using System;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public class CreatePlayerShips : MonoBehaviour
{

    [Inject] private DiContainer _container;
    [Inject] private TeamSave teamSave;
    [Inject] private ContentCatalogService contentCatalog;

    public event Action<PlayerController> OnPlayerSpawned;

    [SerializeField] private PlayerController player;
    // Только для старых чертежей, созданных до HullCatalog.
    // Новые чертежи всегда используют SaveData.hullContentId.
    [SerializeField, FormerlySerializedAs("Ships")]
    private GameObject[] legacyShipPrefabs;
    [SerializeField] private GameObject[] Weapons;

    private void Awake()
    {
        if (teamSave.AllSavesThatLoaded == null)
        {
            Debug.LogError("Cannot create player ships: TeamSave data is missing.");
            return;
        }

        SaveData[] saveData = teamSave.AllSavesThatLoaded;
        for (int i = 0; i < saveData.Length; i++)
            BuildShip(saveData[i]);

        ShipSelect shipSelect = player.GetComponent<ShipSelect>();
        if (shipSelect == null)
        {
            Debug.LogError("Cannot initialize player ships: ShipSelect is missing.");
            return;
        }

        shipSelect.InitializeShips();
        OnPlayerSpawned?.Invoke(player);
    }

    private void BuildShip(SaveData shipData)
    {
        if (!TryResolveHullPrefab(shipData, out GameObject hullPrefab))
            return;

        ParentShip shipInstance = _container.InstantiatePrefabForComponent<ParentShip>(
            hullPrefab,
            player.transform.position,
            Quaternion.identity,
            player.transform);

        WeaponController weaponController = shipInstance.GetComponent<WeaponController>();
        if (weaponController == null)
        {
            Debug.LogError(
                $"Cannot build weapons for '{shipInstance.name}': "
                + $"{nameof(WeaponController)} is missing.",
                shipInstance);
            return;
        }

        if (shipData.WeaponData == null)
            return;

        foreach (WeaponDataSer weaponData in shipData.WeaponData)
        {
            if (weaponData == null)
                continue;

            if (!TryResolveWeaponPrefab(
                    weaponData,
                    out GameObject weaponPrefab,
                    out _))
                continue;

            GameObject weaponInstance = _container.InstantiatePrefab(
                weaponPrefab,
                shipInstance.transform.position,
                Quaternion.identity,
                shipInstance.transform);

            weaponInstance.transform.localPosition = Vector3.zero;
            weaponInstance.transform.localRotation = Quaternion.identity;
        }
    }

    private bool TryResolveHullPrefab(
        SaveData shipData,
        out GameObject hullPrefab)
    {
        hullPrefab = null;
        if (shipData == null)
        {
            Debug.LogError("Cannot build player ship: save data is missing.", this);
            return false;
        }

        if (contentCatalog == null)
        {
            Debug.LogError("Cannot build player ship: HullCatalog is unavailable.", this);
            return false;
        }

        HullContentDefinition hull;
        if (!string.IsNullOrWhiteSpace(shipData.hullContentId))
        {
            if (!contentCatalog.TryGetHull(shipData.hullContentId, out hull)
                || hull.GameplayPrefab == null)
            {
                Debug.LogError(
                    $"Cannot build saved ship '{shipData.shipName}': hull content "
                    + $"'{shipData.hullContentId}' is not configured.",
                    this);
                return false;
            }

            hullPrefab = hull.GameplayPrefab;
            return true;
        }

        if (contentCatalog.TryGetHullByShipId(shipData.shipId, out hull)
            && hull.GameplayPrefab != null)
        {
            Debug.LogWarning(
                $"Saved ship '{shipData.shipName}' has no hull content id. "
                + "Resolve it by opening and saving the craft again.",
                this);
            hullPrefab = hull.GameplayPrefab;
            return true;
        }

        if (TryResolveLegacyHullPrefab(shipData.shipId, out hullPrefab))
        {
            Debug.LogWarning(
                $"Saved ship '{shipData.shipName}' has no hull content id. "
                + "It was created through the legacy compatibility path; save the craft again.",
                this);
            return true;
        }

        Debug.LogError(
            $"Cannot build saved ship '{shipData.shipName}': no unambiguous hull "
            + $"was found for legacy ship id {shipData.shipId}.",
            this);
        return false;
    }

    private bool TryResolveLegacyHullPrefab(int shipId, out GameObject hullPrefab)
    {
        hullPrefab = null;
        if (legacyShipPrefabs == null)
            return false;

        for (int i = 0; i < legacyShipPrefabs.Length; i++)
        {
            GameObject candidate = legacyShipPrefabs[i];
            if (candidate == null)
                continue;

            BodyData bodyData = candidate.GetComponentInChildren<BodyData>(true);
            if (bodyData != null && bodyData.ShipId == shipId)
            {
                hullPrefab = candidate;
                return true;
            }
        }

        if (shipId < 0 || shipId >= legacyShipPrefabs.Length)
            return false;

        hullPrefab = legacyShipPrefabs[shipId];
        return hullPrefab != null;
    }

    private bool TryResolveWeaponPrefab(
        WeaponDataSer weaponData,
        out GameObject weaponPrefab,
        out WeaponContentDefinition weaponContent)
    {
        weaponPrefab = null;
        weaponContent = null;

        if (!string.IsNullOrWhiteSpace(weaponData.contentId))
        {
            if (contentCatalog != null
                && contentCatalog.TryGetWeapon(weaponData.contentId, out WeaponContentDefinition weapon)
                && weapon.Prefab != null)
            {
                weaponPrefab = weapon.Prefab;
                weaponContent = weapon;
                return true;
            }

            Debug.LogError(
                $"Cannot build saved weapon: content id '{weaponData.contentId}' is not configured.",
                this);
            return false;
        }

        if (Weapons == null || weaponData.ID < 0 || weaponData.ID >= Weapons.Length)
        {
            Debug.LogError($"Cannot build saved weapon with legacy id {weaponData.ID}.", this);
            return false;
        }

        weaponPrefab = Weapons[weaponData.ID];
        return weaponPrefab != null;
    }

}
