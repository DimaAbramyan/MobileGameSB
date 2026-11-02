using System;
using System.Collections.Generic;

public enum TutorialStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3
}

[Serializable]
public sealed class PlayerSaveData
{
    public SettingsSaveData settings = new();
    public OnboardingSaveData onboarding = new();
    public ProgressionSaveData progression = new();
    public RunStateSaveData runState = new();
}

[Serializable]
public sealed class SettingsSaveData
{
    public string languageCode = "en";
    public int musicVolume = 100;
    public int sfxVolume = 100;
    public bool musicMuted;
    public bool sfxMuted;
    // -1 preserves the project's graphics/frame-rate defaults when integrated later.
    public int qualityLevel = -1;
    public int targetFrameRate = -1;
}

[Serializable]
public sealed class OnboardingSaveData
{
    public bool hasLaunched;
    public TutorialStatus tutorialStatus;
    public int tutorialVersion = 1;
    public string currentStepId = string.Empty;
    public List<string> completedStepIds = new();
}

[Serializable]
public sealed class ProgressionSaveData
{
    public ResourceSaveData resources = new();
    public LevelProgressSaveData levels = new();
    public ContentProgressSaveData content = new();
    // Craft blueprints remain in Saves/*.json, owned by SaveManager.SavedShips.
}

[Serializable]
public sealed class ResourceSaveData
{
    public int metal;
    public int gold;
    // Preserve the original key: Chips were previously called Cores.
    public int cores;
}

[Serializable]
public sealed class LevelProgressSaveData
{
    public List<int> completedLevelIds = new();
    public List<long> completedDifficultyKeys = new();
}

[Serializable]
public sealed class ContentProgressSaveData
{
    public List<ContentProgressEntry> entries = new();
}

[Serializable]
public sealed class ContentProgressEntry
{
    public string contentId;
    public bool isOwned;
    public int upgradeLevel;
}

[Serializable]
public sealed class RunStateSaveData
{
    public bool hasActiveRun;
    public string modeId = string.Empty;
    public int stateVersion;
    // Future modes own their payload schema; no current mode is resumed automatically.
    public string payloadJson = string.Empty;
}
