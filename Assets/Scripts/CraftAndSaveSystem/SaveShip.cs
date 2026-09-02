using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine;

[System.Serializable]
public class SaveShip
{
    public WeaponDataSer[] weaponData;
    public int shipId;
    public string hullContentId;
    public string shipName;
    public string shipDescr;
    public ShipColorPalette colors = new ShipColorPalette();
    public SaveShip(int shipId, WeaponDataSer[] weaponData, string name, string descr)
    {
        this.shipId = shipId;
        this.weaponData = weaponData;
        shipName = name;
        shipDescr = descr;
    }

    public void EnsureColorPalette()
    {
        if (colors == null)
            colors = new ShipColorPalette();
    }
}
public class SaveSystem : MonoBehaviour
{
    public static void SaveShipData(SaveShip saveData)
    {
        if (saveData == null)
            return;

        saveData.EnsureColorPalette();
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
