using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CollectAllSaves : MonoBehaviour
{
    [SerializeField] int sceneID;
    [SerializeField] bool needToCheck;
    [SerializeField] GameObject WarningToShow;
    [SerializeField]
    public GameObject SaveThatChecked;

    private void Start()
    {
        SaveThatChecked = GameObject.Find("SaveHandler");
    }
    public void Collecting()
    {
        Save[] AllSaves = FindObjectsOfType<Save>();

        if (!needToCheck || CheckingAllSaves(AllSaves))
        {
            var saveDataArray = AllSaves.Select(save => new SaveData
            {
                shipId = save.save.shipId,
                shipName = save.save.shipName,
                WeaponData = save.save.weaponData
            }).ToArray();
            if (SaveThatChecked == null)
            {
                Debug.LogError("œ»«ƒ¿¿¿");
            }
            SaveThatChecked.GetComponent<GotSaves>().allSaves.AllSavesThatLoaded = saveDataArray;
            DontDestroyOnLoad(SaveThatChecked.gameObject);
            SceneManager.LoadScene(sceneID);
        }
        else
        {
            WarningToShow.SetActive(true);
        }
    }

    bool CheckingAllSaves(Save[] saves)
    {
        return ((saves[0].save.shipId != saves[1].save.shipId) &&
                saves[0].save.shipName != "" && saves[1].save.shipName != "" && saves[1].save.shipName != "");
    }
}
