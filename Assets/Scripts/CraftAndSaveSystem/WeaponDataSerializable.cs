using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class WeaponDataSerializable:MonoBehaviour
{
    public int ID;
    public Vector3 place;
}

[System.Serializable]
public class WeaponData
{
    public int ID;
    public Vector3 place;
    public WeaponData(int id, Vector3 position)
    {
        ID = id;
        place = position;
    }
    public WeaponData()
    {
        ID = 0;
        place = new Vector3(0,0,0);
    }
    public int GetID()
    { return ID; }
}
