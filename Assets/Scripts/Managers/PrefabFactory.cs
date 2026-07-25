using UnityEngine;

public class PrefabFactory
{
    public GameObject[] shipPrefabs;
    public GameObject[] weaponPrefabs;

    public PrefabFactory(GameObject[] shipPrefabs, GameObject[] weaponPrefabs)
    {
        this.shipPrefabs = shipPrefabs;
        this.weaponPrefabs = weaponPrefabs;
    }

    public GameObject GetShip(int id)
    {
        if (shipPrefabs == null)
            return null;

        for (int i = 0; i < shipPrefabs.Length; i++)
        {
            if (shipPrefabs[i] == null)
                continue;

            BodyData bodyData =
                shipPrefabs[i].GetComponentInChildren<BodyData>(true);
            if (bodyData != null && bodyData.ShipId == id)
                return shipPrefabs[i];
        }

        if (id < 0 || id >= shipPrefabs.Length)
            return null;

        return shipPrefabs[id];
    }

    public GameObject GetWeapon(int id)
    {
        if (id < 0 || id >= weaponPrefabs.Length) return null;
        return weaponPrefabs[id];
    }
}
