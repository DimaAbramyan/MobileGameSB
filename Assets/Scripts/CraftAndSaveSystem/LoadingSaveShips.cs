using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

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
    {    // Проверяем видимость AudioDatabase
        Debug.Log($"AudioDatabase bound in current container: {_container.HasBinding<AudioDatabase>()}");
        Debug.Log($"AudioDatabase bound in parent: {_container.ParentContainers.Count() > 0 && _container.ParentContainers[0].HasBinding<AudioDatabase>()}");

        // Пытаемся получить AudioDatabase
        try
        {
            var audioDb = _container.Resolve<AudioDatabase>();
            Debug.Log($"AudioDatabase resolved successfully: {audioDb != null}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to resolve AudioDatabase: {e.Message}");
        }
        SaveData[] saveData = FindObjectOfType<GotSaves>().allSaves.AllSavesThatLoaded;
        for (int i = 0; i < saveData.Length; i++)
        {
            BuildingShip(saveData[i], i);

        }
        OnPlayerSpawned?.Invoke(player);

    }
    private void BuildingShip(SaveData Ship, int j)
    {
        ParentShip shipInstance = _container
            .InstantiatePrefabForComponent<ParentShip>(Ships[Ship.shipId], player.transform.position, Quaternion.identity, player.transform);
        foreach (WeaponDataSer weaponData in Ship.WeaponData)
        {
            GameObject weaponInstance = _container
                .InstantiatePrefab(Weapons[weaponData.ID], shipInstance.transform.position, Quaternion.identity, shipInstance.transform);
            weaponInstance.transform.localPosition = weaponData.place / 400f;
        }
    }
}
