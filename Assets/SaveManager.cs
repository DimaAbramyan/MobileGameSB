using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager
{
    public string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

    private List<SaveShip> savedShips = new List<SaveShip>();
    public IReadOnlyList<SaveShip> SavedShips => savedShips;

    public SaveManager()
    {
        LoadAllSaves();
    }

    public void LoadAllSaves()
    {
        savedShips.Clear();

        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);

        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        foreach (var file in files)
        {
            string json = File.ReadAllText(file);
            SaveShip ship = JsonUtility.FromJson<SaveShip>(json);
            savedShips.Add(ship);
        }
    }

    public void SaveShip(SaveShip ship)
    {
        string path = Path.Combine(SaveFolder, ship.shipName + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(ship));
        LoadAllSaves();
    }
}