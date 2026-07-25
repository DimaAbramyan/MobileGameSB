using System;
using UnityEngine;
using Zenject;

public class CreatePlayerShips : MonoBehaviour
{
    [Inject] private DiContainer _container;
    [Inject] private TeamSave teamSave;

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

        foreach (WeaponDataSer weaponData in shipData.WeaponData)
        {
            GameObject weaponInstance = _container.InstantiatePrefab(
                Weapons[weaponData.ID],
                shipInstance.transform.position,
                Quaternion.identity,
                shipInstance.transform);

            weaponInstance.transform.localPosition = weaponData.place / 400f;
        }
    }
}
