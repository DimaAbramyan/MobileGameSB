using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using Zenject.Asteroids;

public class CreatePlayerShips : MonoBehaviour
{
    [Inject] private DiContainer _container;
    public event Action<PlayerController> OnPlayerSpawned;
    [SerializeField] PlayerController player;
    [SerializeField]
    private GameObject[] Ships; 
    [SerializeField]
    private GameObject[] Weapons;
    private void Awake()
    {
        SaveData[] saveData = FindObjectOfType<GotSaves>().allSaves.AllSavesThatLoaded;
        for (int i = 0; i < saveData.Length; i++)
        {
            BuildingShip(saveData[i], i);

        }
        OnPlayerSpawned?.Invoke(player);

    }
    private void BuildingShip(SaveData Ship, int j)
    {
        PlayerController shipInstance = _container
            .InstantiatePrefabForComponent<PlayerController>(Ships[Ship.shipId], player.transform.position, Quaternion.identity, player.transform);
        _container.InjectGameObject(shipInstance.gameObject);
        foreach (WeaponDataSer weaponData in Ship.WeaponData)
        {
            GameObject weaponInstance = _container
                .InstantiatePrefab(Weapons[weaponData.ID], shipInstance.transform.position, Quaternion.identity, shipInstance.transform);
            weaponInstance.transform.localPosition = weaponData.place / 400f;
            _container.InjectGameObject(weaponInstance.gameObject);
        }
    }
}
