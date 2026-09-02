using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager
{
    private const int MaxShipNameLength = 48;

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

        if (!TryGetSaveFiles(out string[] files, out _))
            return;

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            if (!IsSaveFile(file))
                continue;

            try
            {
                string json = File.ReadAllText(file);
                SaveShip ship = JsonUtility.FromJson<SaveShip>(json);
                if (ship == null)
                {
                    Debug.LogWarning($"Save file '{Path.GetFileName(file)}' does not contain a craft.");
                    continue;
                }

                ship.EnsureColorPalette();
                savedShips.Add(ship);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not load craft save '{Path.GetFileName(file)}': {exception.Message}");
            }
        }
    }

    public bool TryValidateNewShipName(
        string requestedName,
        out string normalizedName,
        out string error)
    {
        normalizedName = requestedName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "Введите название крафта.";
            return false;
        }

        if (normalizedName.Length > MaxShipNameLength)
        {
            error = $"Название должно быть не длиннее {MaxShipNameLength} символов.";
            return false;
        }

        if (normalizedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            error = "Введите название без расширения .json.";
            return false;
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalizedName.IndexOf(Path.DirectorySeparatorChar) >= 0
            || normalizedName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            error = "Название содержит недопустимые символы.";
            return false;
        }

        if (!TryGetSaveFiles(out string[] files, out error))
            return false;

        for (int i = 0; i < files.Length; i++)
        {
            if (!IsSaveFile(files[i]))
                continue;

            string existingName = Path.GetFileNameWithoutExtension(files[i]);
            if (string.Equals(existingName, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                error = "Крафт с таким названием уже сохранён.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TrySaveNewShip(SaveShip ship, out string error)
    {
        if (ship == null)
        {
            error = "Нет данных для сохранения крафта.";
            return false;
        }

        if (!TryValidateNewShipName(ship.shipName, out string normalizedName, out error))
            return false;

        ship.shipName = normalizedName;
        ship.EnsureColorPalette();

        try
        {
            string savePath = GetSavePath(normalizedName);
            using FileStream stream = new FileStream(
                savePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            using StreamWriter writer = new StreamWriter(stream);
            writer.Write(JsonUtility.ToJson(ship));
            LoadAllSaves();
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = File.Exists(GetSavePath(normalizedName))
                ? "Крафт с таким названием уже сохранён."
                : $"Не удалось сохранить крафт: {exception.Message}";
            return false;
        }
    }

    public void SaveShip(SaveShip ship)
    {
        if (ship == null)
            return;

        if (!TryNormalizeExistingShipName(ship.shipName, out string normalizedName, out string error))
        {
            Debug.LogWarning(error);
            return;
        }

        ship.EnsureColorPalette();
        ship.shipName = normalizedName;

        try
        {
            File.WriteAllText(GetSavePath(normalizedName), JsonUtility.ToJson(ship));
            LoadAllSaves();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Не удалось обновить крафт '{normalizedName}': {exception.Message}");
        }
    }

    private bool TryNormalizeExistingShipName(
        string requestedName,
        out string normalizedName,
        out string error)
    {
        normalizedName = requestedName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "Невозможно сохранить крафт без названия.";
            return false;
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalizedName.IndexOf(Path.DirectorySeparatorChar) >= 0
            || normalizedName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            error = "Название крафта содержит недопустимые символы.";
            return false;
        }

        return EnsureSaveFolder(out error);
    }

    private bool EnsureSaveFolder(out string error)
    {
        try
        {
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось открыть папку сохранений: {exception.Message}";
            return false;
        }
    }

    private bool TryGetSaveFiles(out string[] files, out string error)
    {
        files = Array.Empty<string>();
        if (!EnsureSaveFolder(out error))
            return false;

        try
        {
            files = Directory.GetFiles(SaveFolder);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось прочитать папку сохранений: {exception.Message}";
            return false;
        }
    }

    private static bool IsSaveFile(string path)
    {
        return string.Equals(
            Path.GetExtension(path),
            ".json",
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetSavePath(string shipName)
    {
        return Path.Combine(SaveFolder, shipName + ".json");
    }
}
