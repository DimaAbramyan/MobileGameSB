using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LevelProgressService
{
    private readonly SaveManager saveManager;
    private readonly HashSet<int> completedLevelIds = new HashSet<int>();
    private readonly HashSet<long> completedDifficultyKeys = new();
    private bool loaded;

    public LevelProgressService() : this(new SaveManager()) { }

    [Zenject.Inject]
    public LevelProgressService(SaveManager saveManager)
    {
        this.saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
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

        Save();
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        completedLevelIds.Clear();
        completedDifficultyKeys.Clear();

        LevelProgressSaveData data = saveManager.ReadProgression().levels;

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

    private void Save()
    {
        var data = new LevelProgressSaveData
        {
            completedLevelIds = new List<int>(completedLevelIds),
            completedDifficultyKeys = new List<long>(completedDifficultyKeys)
        };
        saveManager.UpdateProgression(progress => progress.levels = data, true);
    }

    private static long GetDifficultyKey(int levelId, LevelDifficulty difficulty)
    {
        return ((long)levelId << 32) | (uint)difficulty;
    }

}
