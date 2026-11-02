using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public partial class SaveManager
{
    private const int MaxShipNameLength = 48;

    private readonly string persistentRoot;
    public string SaveFolder => Path.Combine(persistentRoot, "Saves");

    private List<SaveShip> savedShips = new List<SaveShip>();
    public IReadOnlyList<SaveShip> SavedShips => savedShips;

    [Zenject.Inject]
    public SaveManager() : this(Application.persistentDataPath) { }

    // Explicit root enables isolated verification without touching player data.
    public SaveManager(string persistentDataRoot)
    {
        persistentRoot = Path.GetFullPath(persistentDataRoot ?? throw new ArgumentNullException(nameof(persistentDataRoot)));
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
                var result = JsonSaveFile.Read(file, IsCraft);
                if (!result.HasData)
                {
                    Debug.LogWarning(result.Error);
                    continue;
                }
                SaveShip ship = JsonUtility.FromJson<SaveShip>(result.Document.ToString());
                ship.EnsureColorPalette();
                savedShips.Add(ship);
                if (result.Status == SaveLoadStatus.Recovered)
                    JsonSaveFile.Write(file, JsonUtility.ToJson(ship), IsCraft);
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
            JsonSaveFile.Write(savePath, JsonUtility.ToJson(ship), IsCraft, false);
            LoadAllSaves();
            Flush();
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
            JsonSaveFile.Write(GetSavePath(normalizedName), JsonUtility.ToJson(ship), IsCraft);
            LoadAllSaves();
            Flush();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Не удалось обновить крафт '{normalizedName}': {exception.Message}");
        }
    }

    public bool TryDeleteShip(string shipName, out string error)
    {
        if (!TryNormalizeExistingShipName(shipName, out string normalizedName, out error))
            return false;

        string savePath = GetSavePath(normalizedName);
        if (!File.Exists(savePath))
        {
            error = $"Сохранение крафта '{normalizedName}' не найдено.";
            return false;
        }

        try
        {
            JsonSaveFile.Delete(savePath);
            LoadAllSaves();
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Не удалось удалить крафт '{normalizedName}': {exception.Message}";
            return false;
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
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(SaveFolder))
            {
                string candidate = path;
                if (path.EndsWith(".json.bak.tmp", StringComparison.OrdinalIgnoreCase))
                    candidate = path.Substring(0, path.Length - 8);
                else if (path.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".json.tmp", StringComparison.OrdinalIgnoreCase))
                    candidate = path.Substring(0, path.Length - 4);
                if (IsSaveFile(candidate))
                    paths.Add(candidate);
            }
            files = new string[paths.Count];
            paths.CopyTo(files);
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

    private static bool IsCraft(JObject document)
    {
        if (document["shipName"]?.Type != JTokenType.String
            || string.IsNullOrWhiteSpace(document["shipName"].Value<string>())
            || document["shipId"]?.Type != JTokenType.Integer)
            return false;
        var ship = JsonUtility.FromJson<SaveShip>(document.ToString());
        return ship != null;
    }
}
