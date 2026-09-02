using UnityEngine;

public class PrefabFactory
{
    public GameObject[] shipPrefabs;
    public GameObject[] weaponPrefabs;

    private Sprite[] fallbackShipSprites;

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

    public Sprite GetShipIcon(int shipId)
    {
        GameObject shipPrefab = GetShip(shipId);
        if (shipPrefab != null)
        {
            UnityEngine.UI.Image image = shipPrefab.GetComponentInChildren<UnityEngine.UI.Image>(true);
            if (image != null && image.sprite != null)
                return image.sprite;
        }

        if (fallbackShipSprites == null)
            fallbackShipSprites = Resources.LoadAll<Sprite>("UI/Ships");

        if (fallbackShipSprites.Length == 0)
            return null;

        int fallbackIndex = Mathf.Abs(shipId) % fallbackShipSprites.Length;
        return fallbackShipSprites[fallbackIndex];
    }
}
