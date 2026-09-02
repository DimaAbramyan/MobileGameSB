using System;
using UnityEngine;

public enum ContentUnlockRequirementType
{
    None,
    PlayerLevel,
    CompletedLevel,
    CustomCondition
}

[Serializable]
public sealed class ContentUnlockRequirement
{
    [SerializeField] private ContentUnlockRequirementType type;
    [SerializeField, Min(1)] private int requiredPlayerLevel = 1;
    [SerializeField] private LevelConfig requiredCompletedLevel;
    [SerializeField] private string conditionId;

    public ContentUnlockRequirementType Type => type;
    public int RequiredPlayerLevel => requiredPlayerLevel;
    public LevelConfig RequiredCompletedLevel => requiredCompletedLevel;
    public string ConditionId => conditionId;
}
