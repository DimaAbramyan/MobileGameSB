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
    public void Start()
    {
        if (TeamSave.Instance.AllSavesThatLoaded != null)
        {
            Debug.Log("Loading save with ID: " + TeamSave.Instance.AllSavesThatLoaded[id]);
            save = TeamSave.Instance.AllSavesThatLoaded[id].ConvertToSaveShip();
        }
    }
}
