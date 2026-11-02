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
        var saves = new SaveManager();
        if (!saves.TrySaveNewShip(saveData, out string error))
        {
            Debug.Log(error);
            return;
        }
        Debug.Log($"Сохранение выполнено: {saveData.shipName}");
        SceneManager.LoadScene("SelectMenu");
    }
}
