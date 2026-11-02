using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public partial class SaveManager
{
    private PlayerSaveData profile;
    private bool profileLoaded;
    private bool firstLaunch;
    private string persistedJson;

    public const int CurrentFormatVersion = JsonSaveFile.CurrentVersion;
    public string ProfilePath => Path.Combine(SaveFolder, "Profile", "player.json");
    public bool IsDirty { get; private set; }
    public bool IsReadOnly { get; private set; }
    public string LastError { get; private set; }
    public SaveLoadStatus LastLoadStatus { get; private set; }
    public bool IsFirstLaunch { get { InitializeProfile(); return firstLaunch; } }
    public event Action<string> SaveFailed;

    // Snapshots cannot silently change persisted state without a dirty flag.
    public PlayerSaveData ReadSnapshot() { InitializeProfile(); return Copy(profile); }
    public SettingsSaveData ReadSettings() { InitializeProfile(); return Copy(profile.settings); }
    public OnboardingSaveData ReadOnboarding() { InitializeProfile(); return Copy(profile.onboarding); }
    public ProgressionSaveData ReadProgression() { InitializeProfile(); return Copy(profile.progression); }
    public RunStateSaveData ReadRunState() { InitializeProfile(); return Copy(profile.runState); }

    public bool UpdateSettings(Action<SettingsSaveData> update, bool saveImmediately = false)
        => Update(data => update(data.settings), saveImmediately);
    public bool UpdateOnboarding(Action<OnboardingSaveData> update, bool saveImmediately = true)
        => Update(data => update(data.onboarding), saveImmediately);
    public bool UpdateProgression(Action<ProgressionSaveData> update, bool saveImmediately = false)
        => Update(data => update(data.progression), saveImmediately);
    public bool UpdateRunState(Action<RunStateSaveData> update, bool saveImmediately = false)
        => Update(data => update(data.runState), saveImmediately);

    public bool SetTutorialState(TutorialStatus status, string stepId = "", bool saveImmediately = true)
        => UpdateOnboarding(data => { data.tutorialStatus = status; data.currentStepId = stepId ?? string.Empty; }, saveImmediately);

    public void InitializeProfile()
    {
        if (profileLoaded)
            return;
        profileLoaded = true;
        profile = new PlayerSaveData();
        var result = JsonSaveFile.Read(ProfilePath, PlayerSaveMigration.IsProfile);
        LastLoadStatus = result.Status;
        if (result.HasData)
        {
            profile = PlayerSaveMigration.Read(result.Document, result.Version);
            IsDirty = result.Status == SaveLoadStatus.Recovered || result.Version < CurrentFormatVersion;
        }
        else if (result.Status == SaveLoadStatus.Missing)
        {
            bool imported = ImportLegacyProgression();
            // Legacy data proves a previous launch, but says nothing about tutorial completion.
            profile.onboarding.hasLaunched = imported || savedShips.Count > 0;
            IsDirty = !IsReadOnly;
        }
        else
        {
            IsReadOnly = true;
            ReportFailure(result.Error);
            return; // Preserve every candidate; do not replace unknown/damaged data with defaults.
        }

        persistedJson = JsonUtility.ToJson(profile);
        if (IsReadOnly)
            return;
        firstLaunch = !profile.onboarding.hasLaunched;
        if (firstLaunch)
        {
            profile.onboarding.hasLaunched = true;
            IsDirty = true;
        }
        if (IsDirty)
            Flush();
    }

    public bool Flush()
    {
        if (!profileLoaded)
            return true;
        if (IsReadOnly)
            return false;
        if (!IsDirty)
            return true;
        try
        {
            lock (JsonSaveFile.SyncRoot)
            {
                // Legacy fallback constructors may create another manager. Merge only our changes
                // so writing levels cannot roll back another manager's resources/settings.
                var latest = JsonSaveFile.Read(ProfilePath, PlayerSaveMigration.IsProfile);
                if (latest.Status == SaveLoadStatus.UnsupportedVersion || latest.Status == SaveLoadStatus.Unavailable)
                {
                    ReportFailure(latest.Error);
                    return false;
                }
                if (latest.HasData && persistedJson != null)
                {
                    var merged = JsonSaveFile.Parse(JsonUtility.ToJson(PlayerSaveMigration.Read(latest.Document, latest.Version)));
                    MergeChanges(JsonSaveFile.Parse(persistedJson), JsonSaveFile.Parse(JsonUtility.ToJson(profile)), merged);
                    profile = PlayerSaveMigration.Read(merged, CurrentFormatVersion);
                }
                JsonSaveFile.Write(ProfilePath, JsonUtility.ToJson(profile, true), PlayerSaveMigration.IsProfile);
                persistedJson = JsonUtility.ToJson(profile);
                IsDirty = false;
                LastError = null;
                return true;
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is System.Security.SecurityException)
        {
            ReportFailure(exception.Message);
            return false; // Keep dirty state so a later checkpoint can retry.
        }
    }

    private bool Update(Action<PlayerSaveData> update, bool saveImmediately)
    {
        if (update == null)
            throw new ArgumentNullException(nameof(update));
        InitializeProfile();
        if (IsReadOnly)
            return false;
        var candidate = Copy(profile);
        update(candidate);
        PlayerSaveMigration.Normalize(candidate);
        string json = JsonUtility.ToJson(candidate);
        if (json == JsonUtility.ToJson(profile))
            return !saveImmediately || Flush();
        // Detach references retained by the callback/caller.
        profile = JsonUtility.FromJson<PlayerSaveData>(json);
        IsDirty = true;
        return !saveImmediately || Flush();
    }

    private bool ImportLegacyProgression()
    {
        bool resources = ImportLegacy<ResourceSaveData>("player_resources.json",
            doc => doc["metal"] != null || doc["gold"] != null || doc["cores"] != null,
            data => profile.progression.resources = data);
        bool levels = ImportLegacy<LevelProgressSaveData>("level_progress.json",
            doc => doc["completedLevelIds"] != null || doc["completedDifficultyKeys"] != null,
            data => profile.progression.levels = data);
        bool content = ImportLegacy<ContentProgressSaveData>("content_progress.json",
            doc => doc["entries"] != null, data => profile.progression.content = data);
        PlayerSaveMigration.Normalize(profile);
        return resources || levels || content;
    }

    private bool ImportLegacy<T>(string fileName, Func<JObject, bool> marker, Action<T> apply) where T : class, new()
    {
        var result = JsonSaveFile.Read(Path.Combine(persistentRoot, fileName), doc =>
        {
            if (!marker(doc))
                return false;
            var value = new T();
            JsonUtility.FromJsonOverwrite(doc.ToString(), value);
            return true;
        });
        if (result.Status == SaveLoadStatus.Missing)
            return false;
        if (!result.HasData)
        {
            IsReadOnly = true;
            ReportFailure(result.Error);
            return false;
        }
        var data = new T();
        JsonUtility.FromJsonOverwrite(result.Document.ToString(), data);
        apply(data);
        return true;
    }

    private void ReportFailure(string error)
    {
        if (LastError == error)
            return;
        LastError = error;
        Debug.LogWarning($"SaveManager: {error}");
        SaveFailed?.Invoke(error);
    }

    private static T Copy<T>(T data) => JsonUtility.FromJson<T>(JsonUtility.ToJson(data));

    private static void MergeChanges(JObject before, JObject pending, JObject destination)
    {
        foreach (var property in pending.Properties())
        {
            JToken previous = before[property.Name];
            if (JToken.DeepEquals(previous, property.Value))
                continue;
            if (previous is JObject previousObject && property.Value is JObject changedObject
                && destination[property.Name] is JObject targetObject)
                MergeChanges(previousObject, changedObject, targetObject);
            else
                destination[property.Name] = property.Value.DeepClone();
        }
    }
}
