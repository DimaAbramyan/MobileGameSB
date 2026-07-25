using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelCatalog",
    menuName = "Game/Levels/Level Catalog")]
public sealed class LevelCatalog : ScriptableObject
{
    [SerializeField] private LevelConfig defaultLevel;
    [SerializeField] private LevelConfig[] levels = Array.Empty<LevelConfig>();

    public IReadOnlyList<LevelConfig> Levels => levels;

    public LevelConfig GetLevel(int id)
    {
        foreach (LevelConfig level in levels)
        {
            if (level != null && level.Id == id)
                return level;
        }

        if (defaultLevel != null)
        {
            Debug.LogWarning(
                $"Level config with ID {id} was not found. "
                + $"Using default level {defaultLevel.Id}.");
            return defaultLevel;
        }

        Debug.LogError($"Level config with ID {id} was not found.");
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var usedIds = new HashSet<int>();

        foreach (LevelConfig level in levels)
        {
            if (level != null && !usedIds.Add(level.Id))
                Debug.LogError(
                    $"Duplicate level ID {level.Id} in {name}.",
                    this);
        }
    }
#endif
}
