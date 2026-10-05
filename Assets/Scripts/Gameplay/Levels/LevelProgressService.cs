using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class LevelProgressService
{
    private const string SaveFileName = "level_progress.json";

    private readonly string savePath;
    private readonly HashSet<int> completedLevelIds = new HashSet<int>();
    private readonly HashSet<long> completedDifficultyKeys = new();
    private bool loaded;

    public LevelProgressService()
    {
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public bool IsLevelCompleted(LevelConfig level)
    {
        return level != null && IsLevelCompleted(level.Id, level.Difficulty);
    }

    public bool IsLevelCompleted(int levelId)
    {
        return IsLevelCompleted(levelId, LevelDifficulty.Normal);
    }

    public bool IsLevelCompleted(int levelId, LevelDifficulty difficulty)
    {
        EnsureLoaded();
        return difficulty == LevelDifficulty.Normal
            ? completedLevelIds.Contains(levelId)
            : completedDifficultyKeys.Contains(GetDifficultyKey(levelId, difficulty));
    }

    public bool CanStartLevel(LevelConfig level)
    {
        EnsureLoaded();

        if (level == null)
            return false;

        LevelConfig requiredLevel = level.RequiredLevel;
        return requiredLevel == null
            || IsLevelCompleted(requiredLevel);
    }

    public void MarkLevelCompleted(LevelConfig level)
    {
        if (level == null)
            return;

        MarkLevelCompleted(level.Id, level.Difficulty);
    }

    public void MarkLevelCompleted(int levelId)
    {
        MarkLevelCompleted(levelId, LevelDifficulty.Normal);
    }

    public void MarkLevelCompleted(int levelId, LevelDifficulty difficulty)
    {
        EnsureLoaded();

        if (levelId < 0 || difficulty < LevelDifficulty.Normal || difficulty > LevelDifficulty.Extreme)
            return;

        bool added = difficulty == LevelDifficulty.Normal
            ? completedLevelIds.Add(levelId)
            : completedDifficultyKeys.Add(GetDifficultyKey(levelId, difficulty));
        if (!added)
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
        completedDifficultyKeys.Clear();
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
        completedDifficultyKeys.Clear();

        if (!File.Exists(savePath))
            return;

        try
        {
            string json = File.ReadAllText(savePath);
            LevelProgressSaveData data =
                JsonUtility.FromJson<LevelProgressSaveData>(json);

            if (data == null)
                return;

            // Older saves contain only level IDs; those remain Normal completions.
            data.completedLevelIds ??= new List<int>();
            for (int i = 0; i < data.completedLevelIds.Count; i++)
            {
                int id = data.completedLevelIds[i];
                if (id >= 0)
                    completedLevelIds.Add(id);
            }

            if (data.completedDifficultyKeys != null)
            {
                for (int i = 0; i < data.completedDifficultyKeys.Count; i++)
                {
                    long key = data.completedDifficultyKeys[i];
                    uint difficulty = (uint)key;
                    if (key >= 0 && difficulty >= (uint)LevelDifficulty.Hard
                        && difficulty <= (uint)LevelDifficulty.Extreme)
                        completedDifficultyKeys.Add(key);
                }
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
                completedLevelIds = new List<int>(completedLevelIds),
                completedDifficultyKeys = new List<long>(completedDifficultyKeys)
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

    private static long GetDifficultyKey(int levelId, LevelDifficulty difficulty)
    {
        return ((long)levelId << 32) | (uint)difficulty;
    }

    [Serializable]
    private sealed class LevelProgressSaveData
    {
        public List<int> completedLevelIds = new List<int>();
        public List<long> completedDifficultyKeys = new();
    }
}
