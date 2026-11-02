using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class PlayerSaveMigration
{
    public static bool IsProfile(JObject document)
    {
        bool hasSection = false;
        foreach (string section in new[] { "settings", "onboarding", "progression", "runState" })
        {
            JToken value = document[section];
            if (value == null)
                continue; // Version zero may contain only some sections.
            if (value.Type != JTokenType.Object)
                return false;
            hasSection = true;
        }
        if (!hasSection)
            return false;
        Read(document, document["formatVersion"]?.Value<int>() ?? 0);
        return true;
    }

    public static PlayerSaveData Read(JObject document, int fromVersion)
    {
        // Version 0 is the unversioned JSON format. Future transformations go here in order.
        // FromJsonOverwrite leaves defaults in place for fields absent in old saves.
        while (fromVersion < JsonSaveFile.CurrentVersion)
        {
            switch (fromVersion)
            {
                case 0: fromVersion = 1; break;
                default: throw new InvalidDataException($"No save migration from version {fromVersion}.");
            }
        }
        var data = new PlayerSaveData();
        JsonUtility.FromJsonOverwrite(document.ToString(), data);
        Normalize(data);
        return data;
    }

    public static void Normalize(PlayerSaveData data)
    {
        data.settings ??= new();
        data.onboarding ??= new();
        data.progression ??= new();
        data.runState ??= new();
        data.settings.languageCode = string.IsNullOrWhiteSpace(data.settings.languageCode) ? "en" : data.settings.languageCode;
        data.settings.musicVolume = Mathf.Clamp(data.settings.musicVolume, 0, 100);
        data.settings.sfxVolume = Mathf.Clamp(data.settings.sfxVolume, 0, 100);
        data.onboarding.completedStepIds ??= new();
        data.progression.resources ??= new();
        data.progression.levels ??= new();
        data.progression.levels.completedLevelIds ??= new();
        data.progression.levels.completedDifficultyKeys ??= new();
        data.progression.content ??= new();
        data.progression.content.entries ??= new();
        if (!Enum.IsDefined(typeof(TutorialStatus), data.onboarding.tutorialStatus))
            throw new InvalidDataException("Unknown tutorial status.");
    }
}
