using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine;

[System.Serializable]
public class SaveShip
{
    public WeaponData[] weaponData;
    public int shipId;
    public string shipName;
    public string shipDescr;
    public SaveShip(int shipId, WeaponData[] weaponData, string name, string descr)
    {
        this.shipId = shipId;
        this.weaponData = weaponData;
        shipName = name;
        shipDescr = descr;
    }
}
public class SaveSystem : MonoBehaviour
{
    public static void SaveShipData(SaveShip saveData)
    {
        string directoryPath = Application.persistentDataPath + "/Saves";

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string filePath = Path.Combine(directoryPath, $"{saveData.shipName}.json");
        if (File.Exists(filePath))
        {
            Debug.Log("Данное название занято, переименуйте корабль");
            return;
        }
        if (saveData.shipName == "")
        {
            Debug.Log("Введите название корабля");
            return;
        }

        string json = JsonUtility.ToJson(saveData, true);

        File.WriteAllText(filePath, json);

        Debug.Log($"Сохранение выполнено: {filePath}");
        SceneManager.LoadScene("SelectMenu");
    }
}