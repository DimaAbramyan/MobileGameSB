using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Save : MonoBehaviour
{
    public SaveShip save;
    [SerializeField]
    private int id; 

    public void ErazeSave()
    {
        save.shipName = "";
    }
    public void Awake()
    {
        if (TeamSave.Instance.AllSavesThatLoaded[id] != null)
        {
            save = TeamSave.Instance.AllSavesThatLoaded[id].ConvertToSaveShip();
        }
    }
}
