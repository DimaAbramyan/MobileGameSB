using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public int shipId;
    public string shipName;
    public WeaponDataSer[] WeaponData;

    public SaveShip ConvertToSaveShip ()
    {
        return new SaveShip (shipId, WeaponData, shipName, "");
    }
}

