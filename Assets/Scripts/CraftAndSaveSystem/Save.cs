using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[System.Serializable]
public class Save : MonoBehaviour
{
    [Inject] private TeamSave teamSave;

    public SaveShip save;
    [SerializeField]
    private int id;

    public int SlotIndex => id;

    public void ErazeSave()
    {
        save.shipName = "";
    }
    public void Start()
    {
        if (teamSave.AllSavesThatLoaded != null
            && id >= 0
            && id < teamSave.AllSavesThatLoaded.Length)
        {
            Debug.Log("Loading save with ID: " + teamSave.AllSavesThatLoaded[id]);
            save = teamSave.AllSavesThatLoaded[id].ConvertToSaveShip();
        }
    }
}
