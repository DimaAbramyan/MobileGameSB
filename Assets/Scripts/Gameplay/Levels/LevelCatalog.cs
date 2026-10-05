using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelCatalog",
    menuName = "Game/Levels/Level Catalog")]
public sealed class LevelCatalog : ScriptableObject
{
    [SerializeField] private LevelDefinitionConfig defaultLevelDefinition;
    [SerializeField, InspectorName("Levels")]
    private LevelDefinitionConfig[] levelDefinitions = Array.Empty<LevelDefinitionConfig>();
    [SerializeField, HideInInspector, FormerlySerializedAs("defaultLevel")]
    private LevelConfig legacyDefaultLevel;
    [SerializeField, HideInInspector, FormerlySerializedAs("levels")]
    private LevelConfig[] legacyLevels = Array.Empty<LevelConfig>();

    public IReadOnlyList<LevelDefinitionConfig> Levels => levelDefinitions;

    public LevelConfig GetLevel(int id)
    {
        return GetLevel(id, LevelDifficulty.Normal);
    }

    public LevelConfig GetLevel(int id, LevelDifficulty difficulty)
    {
        LevelDefinitionConfig definition = GetDefinition(id);
        if (definition == null)
            return null;

        LevelConfig level = definition.GetDifficulty(difficulty);
        if (level == null)
            Debug.LogError($"Level {definition.Id} has no {difficulty} configuration.", definition);
        return level;
    }

    public LevelDefinitionConfig GetDefinition(int id)
    {
        foreach (LevelDefinitionConfig level in levelDefinitions)
        {
            if (level != null && level.Id == id)
                return level;
        }

        if (defaultLevelDefinition != null)
        {
            Debug.LogWarning(
                $"Level config with ID {id} was not found. "
                + $"Using default level {defaultLevelDefinition.Id}.");
            return defaultLevelDefinition;
        }

        Debug.LogError($"Level config with ID {id} was not found.");
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var usedIds = new HashSet<int>();

        foreach (LevelDefinitionConfig level in levelDefinitions)
        {
            if (level != null && !usedIds.Add(level.Id))
                Debug.LogError(
                    $"Duplicate level ID {level.Id} in {name}.",
                    this);
        }
    }
#endif
}
