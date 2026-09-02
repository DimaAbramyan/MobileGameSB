using System;
using UnityEngine;
using Zenject;

public class CreatePlayerShips : MonoBehaviour
{
    private const float LegacyWeaponPositionScale = 400f;

    [Inject] private DiContainer _container;
    [Inject] private TeamSave teamSave;
    [Inject] private ContentCatalogService contentCatalog;

    public event Action<PlayerController> OnPlayerSpawned;

    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject[] Ships;
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
        ParentShip shipInstance = _container.InstantiatePrefabForComponent<ParentShip>(
            Ships[shipData.shipId],
            player.transform.position,
            Quaternion.identity,
            player.transform);

        HullLoadoutDefinition loadout = shipInstance.GetComponent<HullLoadoutDefinition>();

        if (shipData.WeaponData == null)
            return;

        foreach (WeaponDataSer weaponData in shipData.WeaponData)
        {
            if (weaponData == null)
                continue;

            if (!TryResolveWeaponPrefab(
                    weaponData,
                    out GameObject weaponPrefab,
                    out WeaponContentDefinition weaponContent))
            continue;

            Transform weaponParent = shipInstance.transform;
            bool useWeaponMount = !string.IsNullOrWhiteSpace(weaponData.slotId);
            if (useWeaponMount
                && !TryResolveWeaponMount(
                    shipInstance,
                    loadout,
                    weaponData.slotId,
                    weaponContent,
                    out weaponParent))
            {
                continue;
            }

            GameObject weaponInstance = _container.InstantiatePrefab(
                weaponPrefab,
                shipInstance.transform.position,
                Quaternion.identity,
                weaponParent);

            if (useWeaponMount)
            {
                weaponInstance.transform.localPosition = Vector3.zero;
                weaponInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                weaponInstance.transform.localPosition = weaponData.usesShipLocalPosition
                    ? weaponData.place
                    : weaponData.place / LegacyWeaponPositionScale;
            }
        }
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

    private bool TryResolveWeaponMount(
        ParentShip shipInstance,
        HullLoadoutDefinition loadout,
        string slotId,
        WeaponContentDefinition weapon,
        out Transform weaponMount)
    {
        weaponMount = null;

        if (loadout != null)
        {
            if (!loadout.TryGetWeaponPlatform(slotId, out HullWeaponPlatform platform)
                || platform.WeaponMount == null)
            {
                Debug.LogError(
                    $"Cannot build saved weapon: platform '{slotId}' is not configured on '{shipInstance.name}'.",
                    this);
                return false;
            }

            if (weapon != null
                && !ShipBuildValidator.TryValidateWeaponPlatformTier(
                    weapon,
                    slotId,
                    loadout.GetMaxWeaponTier(slotId),
                    out string loadoutTierError))
            {
                Debug.LogError(loadoutTierError, this);
                return false;
            }

            weaponMount = platform.WeaponMount;
            return true;
        }

        CraftWeaponSlotAnchor[] legacyAnchors =
            shipInstance.GetComponentsInChildren<CraftWeaponSlotAnchor>(true);
        for (int i = 0; i < legacyAnchors.Length; i++)
        {
            CraftWeaponSlotAnchor anchor = legacyAnchors[i];
            if (anchor == null || anchor.SlotId != slotId || anchor.WeaponMount == null)
                continue;

            if (weapon != null
                && !ShipBuildValidator.TryValidateWeaponPlatformTier(
                    weapon,
                    slotId,
                    1,
                    out string legacyTierError))
            {
                Debug.LogError(legacyTierError, this);
                return false;
            }

            weaponMount = anchor.WeaponMount;
            return true;
        }

        Debug.LogError(
            $"Cannot build saved weapon: platform '{slotId}' is not configured on '{shipInstance.name}'.",
            this);
        return false;
    }
}
