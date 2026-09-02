using System;

public readonly struct ContentAvailability
{
    public ContentAvailability(bool isAvailable, string reason)
    {
        IsAvailable = isAvailable;
        Reason = reason ?? string.Empty;
    }

    public bool IsAvailable { get; }
    public string Reason { get; }
}

public sealed class ContentAvailabilityService
{
    private readonly LevelProgressService levelProgress;

    public ContentAvailabilityService(LevelProgressService levelProgress)
    {
        this.levelProgress = levelProgress
            ?? throw new ArgumentNullException(nameof(levelProgress));
    }

    public ContentAvailability GetAvailability(CraftContentDefinition content)
    {
        if (content == null)
            return new ContentAvailability(false, "Контент не назначен.");

        if (content.IsStarterUnlocked)
            return new ContentAvailability(true, "Доступно");

        ContentUnlockRequirement requirement = content.UnlockRequirement;
        if (requirement == null
            || requirement.Type == ContentUnlockRequirementType.None)
        {
            return new ContentAvailability(true, "Доступно");
        }

        switch (requirement.Type)
        {
            case ContentUnlockRequirementType.PlayerLevel:
                return GetPlayerLevelAvailability(requirement);

            case ContentUnlockRequirementType.CompletedLevel:
                return GetCompletedLevelAvailability(requirement);

            case ContentUnlockRequirementType.CustomCondition:
                return GetCustomConditionAvailability(requirement);

            default:
                return new ContentAvailability(
                    false,
                    "Для контента задано неизвестное условие разблокировки.");
        }
    }

    private ContentAvailability GetPlayerLevelAvailability(
        ContentUnlockRequirement requirement)
    {
        int playerLevel = Math.Max(1, levelProgress.CompletedCount + 1);
        int requiredLevel = Math.Max(1, requirement.RequiredPlayerLevel);
        return playerLevel >= requiredLevel
            ? new ContentAvailability(true, "Доступно")
            : new ContentAvailability(
                false,
                $"Требуется уровень игрока: {requiredLevel}. Текущий: {playerLevel}.");
    }

    private ContentAvailability GetCompletedLevelAvailability(
        ContentUnlockRequirement requirement)
    {
        LevelConfig requiredLevel = requirement.RequiredCompletedLevel;
        if (requiredLevel == null)
        {
            return new ContentAvailability(
                false,
                "Не задан уровень, необходимый для разблокировки.");
        }

        return levelProgress.IsLevelCompleted(requiredLevel)
            ? new ContentAvailability(true, "Доступно")
            : new ContentAvailability(
                false,
                $"Требуется пройти уровень: {requiredLevel.DisplayName}.");
    }

    private static ContentAvailability GetCustomConditionAvailability(
        ContentUnlockRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.ConditionId))
        {
            return new ContentAvailability(
                false,
                "Не настроено пользовательское условие разблокировки.");
        }

        return new ContentAvailability(
            false,
            $"Требуется условие: {requirement.ConditionId}.");
    }
}
