using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class LevelProgressService
{
    private const string SaveFileName = "level_progress.json";

    private readonly string savePath;
    private readonly HashSet<int> completedLevelIds = new HashSet<int>();
    private bool loaded;

    public LevelProgressService()
    {
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public bool IsLevelCompleted(LevelConfig level)
    {
        return level != null && IsLevelCompleted(level.Id);
    }

    public bool IsLevelCompleted(int levelId)
    {
        EnsureLoaded();
        return completedLevelIds.Contains(levelId);
    }

    public bool CanStartLevel(LevelConfig level)
    {
        EnsureLoaded();

        if (level == null)
            return false;

        LevelConfig requiredLevel = level.RequiredLevel;
        return requiredLevel == null
            || completedLevelIds.Contains(requiredLevel.Id);
    }

    public void MarkLevelCompleted(LevelConfig level)
    {
        if (level == null)
            return;

        MarkLevelCompleted(level.Id);
    }

    public void MarkLevelCompleted(int levelId)
    {
        EnsureLoaded();

        if (levelId < 0)
            return;

        if (!completedLevelIds.Add(levelId))
            return;

        Save();
    }

    public int CompletedCount
    {
        get
        {
            EnsureLoaded();
            return completedLevelIds.Count;
        }
    }

    public IReadOnlyCollection<int> CompletedLevelIds
    {
        get
        {
            EnsureLoaded();
            return completedLevelIds;
        }
    }

    public void ResetProgress()
    {
        completedLevelIds.Clear();
        loaded = true;

        if (File.Exists(savePath))
            File.Delete(savePath);
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        completedLevelIds.Clear();

        if (!File.Exists(savePath))
            return;

        try
        {
            string json = File.ReadAllText(savePath);
            LevelProgressSaveData data =
                JsonUtility.FromJson<LevelProgressSaveData>(json);

            if (data?.completedLevelIds == null)
                return;

            for (int i = 0; i < data.completedLevelIds.Count; i++)
            {
                int id = data.completedLevelIds[i];
                if (id >= 0)
                    completedLevelIds.Add(id);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Cannot load level progress from {savePath}: {exception.Message}");
        }
    }

    private void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory)
                && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            LevelProgressSaveData data = new LevelProgressSaveData
            {
                completedLevelIds = new List<int>(completedLevelIds)
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Cannot save level progress to {savePath}: {exception.Message}");
        }
    }

    [Serializable]
    private sealed class LevelProgressSaveData
    {
        public List<int> completedLevelIds = new List<int>();
    }
}
