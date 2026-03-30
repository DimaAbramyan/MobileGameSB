using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingSaveShips : MonoBehaviour
{
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
            //GameObject ShipCreated = Instantiate(Ships[CurrentShipIBuild.save.shipId]);
            BuildingShip(saveData[i], i);
        }
    }
    private void BuildingShip(SaveData Ship, int j)
    {
        GameObject ShipCreated = Instantiate(Ships[Ship.shipId], player.transform);
        Debug.Log(Ship.WeaponData.Length);
        foreach (WeaponDataSer weaponData in Ship.WeaponData)
        {
            GameObject WeaponICreate = Instantiate(Weapons[weaponData.ID], ShipCreated.transform);
            WeaponICreate.transform.position = weaponData.place/400f;
        }
    }
}
