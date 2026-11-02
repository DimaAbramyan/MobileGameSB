using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zenject;

public static class VerifySaveFoundation
{
    private static string root;
    private static List<string> checks;

    public static object Run()
    {
        root = Path.GetFullPath(Path.Combine(Application.dataPath, "../AgentArtifacts/SaveFoundationTests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        checks = new List<string>();
        FirstLaunchAndApi();
        LegacyMigration();
        Recovery();
        InvalidAndFuture();
        CraftCompatibility();
        MultipleManagers();
        ServiceRegression();
        LifecycleAndIoFailure();
        return new { success = true, count = checks.Count, checks, testRoot = root, playerDataUntouched = true };
    }

    private static void FirstLaunchAndApi()
    {
        var saves = Case("fresh");
        Check(saves.IsFirstLaunch, "Fresh install is first launch");
        Check(saves.ReadOnboarding().hasLaunched, "First launch persisted explicitly");
        Check(saves.ReadOnboarding().tutorialStatus == TutorialStatus.NotStarted, "Fresh tutorial NotStarted");
        Check(saves.ReadSettings().languageCode == "en" && saves.ReadSettings().sfxVolume == 100, "Settings defaults");
        Check(!saves.ReadRunState().hasActiveRun, "RunState inactive by default");
        Check(File.Exists(saves.ProfilePath) && File.Exists(saves.ProfilePath + ".bak"), "First write and backup exist");
        string disk = File.ReadAllText(saves.ProfilePath);
        Check(disk.Contains("formatVersion") && disk.Contains("checksum"), "Version and integrity metadata");
        Check(disk.Contains("settings") && disk.Contains("onboarding") && disk.Contains("progression") && disk.Contains("runState"), "Four persisted sections");
        var second = Reopen(saves);
        Check(!second.IsFirstLaunch && second.ReadOnboarding().tutorialStatus == TutorialStatus.NotStarted, "Second launch is not tutorial completion");
        foreach (TutorialStatus status in Enum.GetValues(typeof(TutorialStatus)))
        {
            saves.SetTutorialState(status, "step-one");
            Check(Reopen(saves).ReadOnboarding().tutorialStatus == status, "Tutorial state persists: " + status);
        }
        saves.UpdateOnboarding(data => data.completedStepIds.Add("step-one"));
        Check(Reopen(saves).ReadOnboarding().completedStepIds.Contains("step-one"), "Tutorial step progress persists");
        disk = File.ReadAllText(saves.ProfilePath);
        for (int i = 0; i < 100; i++) saves.UpdateSettings(data => data.musicVolume = i);
        Check(saves.IsDirty && File.ReadAllText(saves.ProfilePath) == disk, "100 deferred changes do not write to disk");
        Check(saves.Flush() && !saves.IsDirty && Reopen(saves).ReadSettings().musicVolume == 99, "Deferred changes flush once");
        DateTime written = File.GetLastWriteTimeUtc(saves.ProfilePath);
        saves.UpdateSettings(data => data.musicVolume = 99, true);
        Check(File.GetLastWriteTimeUtc(saves.ProfilePath) == written && !saves.IsDirty, "No-op update avoids a write");
        var snapshot = saves.ReadSnapshot();
        snapshot.settings.musicVolume = 0;
        snapshot.onboarding.completedStepIds.Clear();
        Check(saves.ReadSettings().musicVolume == 99 && saves.ReadOnboarding().completedStepIds.Count == 1, "Read snapshots are detached");
        SettingsSaveData retained = null;
        saves.UpdateSettings(data => { data.languageCode = "zh-Hans"; retained = data; });
        retained.languageCode = "untracked";
        Check(saves.ReadSettings().languageCode == "zh-Hans", "Retained callback reference cannot mutate state");
        saves.UpdateRunState(data => { data.modeId = "test-mode"; data.stateVersion = 3; data.payloadJson = "{\"score\":7}"; }, true);
        Check(Reopen(saves).ReadRunState().payloadJson == "{\"score\":7}", "Future run payload round trip");
    }

    private static void LegacyMigration()
    {
        string path = NewRoot("legacy");
        string resources = "{\"metal\":123,\"gold\":17,\"cores\":9}";
        string levels = "{\"completedLevelIds\":[1,4]}";
        string content = "{\"entries\":[{\"contentId\":\"old-hull\",\"isOwned\":true,\"upgradeLevel\":2}]}";
        File.WriteAllText(Path.Combine(path, "player_resources.json"), resources);
        File.WriteAllText(Path.Combine(path, "level_progress.json"), levels);
        File.WriteAllText(Path.Combine(path, "content_progress.json"), content);
        var saves = new SaveManager(path);
        var data = saves.ReadSnapshot();
        Check(data.progression.resources.metal == 123 && data.progression.resources.gold == 17 && data.progression.resources.cores == 9, "Legacy resource amounts and cores key migrated");
        Check(data.progression.levels.completedLevelIds.Count == 2 && data.progression.levels.completedDifficultyKeys.Count == 0, "Legacy normal-only levels migrated");
        Check(data.progression.content.entries[0].contentId == "old-hull" && data.progression.content.entries[0].upgradeLevel == 2, "Legacy ownership/upgrades migrated");
        Check(!saves.IsFirstLaunch && data.onboarding.tutorialStatus == TutorialStatus.NotStarted, "Existing player is not assumed to have finished tutorial");
        Check(File.ReadAllText(Path.Combine(path, "player_resources.json")) == resources
            && File.ReadAllText(Path.Combine(path, "level_progress.json")) == levels
            && File.ReadAllText(Path.Combine(path, "content_progress.json")) == content, "All legacy originals retained byte-for-byte");
        File.WriteAllText(Path.Combine(path, "player_resources.json"), "{\"metal\":9999}");
        Check(Reopen(saves).ReadProgression().resources.metal == 123, "Migration is not repeated over a current profile");
        saves.UpdateProgression(data => data.resources = new ResourceSaveData(), true);
        Check(Reopen(saves).ReadProgression().resources.metal == 0, "Reset does not resurrect retained legacy resources");

        var versionZero = Case("version-zero");
        Directory.CreateDirectory(Path.GetDirectoryName(versionZero.ProfilePath));
        File.WriteAllText(versionZero.ProfilePath,
            "{\"settings\":{\"languageCode\":\"ru\"},\"onboarding\":{\"hasLaunched\":true,\"tutorialStatus\":1,\"currentStepId\":\"old-step\"}}");
        var migrated = Reopen(versionZero);
        Check(migrated.ReadSettings().languageCode == "ru" && migrated.ReadSettings().sfxVolume == 100, "Version zero retains fields and fills defaults");
        Check(migrated.ReadOnboarding().tutorialStatus == TutorialStatus.InProgress && migrated.ReadOnboarding().currentStepId == "old-step", "Version zero tutorial progress preserved");
        Check(File.ReadAllText(migrated.ProfilePath).Contains("formatVersion"), "Version zero upgraded on disk");

        var badLegacy = Case("bad-legacy");
        string corrupt = Path.Combine(Path.GetDirectoryName(badLegacy.SaveFolder), "player_resources.json");
        File.WriteAllText(corrupt, "{broken");
        badLegacy.InitializeProfile();
        Check(badLegacy.IsReadOnly && !File.Exists(badLegacy.ProfilePath), "Unrecoverable legacy data blocks migration instead of resetting");
        Check(File.ReadAllText(corrupt) == "{broken", "Corrupt legacy original preserved");
        var recoveredLegacy = Case("legacy-backup");
        string oldPath = Path.Combine(Path.GetDirectoryName(recoveredLegacy.SaveFolder), "player_resources.json");
        File.WriteAllText(oldPath, "{broken");
        File.WriteAllText(oldPath + ".bak", "{\"metal\":37,\"cores\":4}");
        Check(recoveredLegacy.ReadProgression().resources.metal == 37 && !recoveredLegacy.IsReadOnly, "Legacy backup recovered into consolidated profile");
    }

    private static void Recovery()
    {
        var saves = Case("backup-recovery");
        saves.InitializeProfile();
        saves.UpdateSettings(data => data.languageCode = "ru", true);
        saves.UpdateSettings(data => data.musicVolume = 41, true);
        File.WriteAllText(saves.ProfilePath, "{truncated");
        var recovered = Reopen(saves);
        Check(recovered.ReadSettings().languageCode == "ru" && recovered.LastLoadStatus == SaveLoadStatus.Recovered, "Corrupt primary falls back to last good backup");
        Check(!recovered.IsReadOnly && recovered.Flush() && !recovered.IsDirty, "Recovered profile repaired and writable");
        Check(Directory.GetFiles(Path.GetDirectoryName(saves.ProfilePath), "player.json.corrupt-*").Length > 0, "Corrupt primary bytes archived");
        Check(Reopen(saves).ReadSettings().languageCode == "ru", "Repaired profile survives another launch");

        var checksum = Case("checksum");
        checksum.InitializeProfile();
        checksum.UpdateProgression(data => data.resources.metal = 111, true);
        string original = File.ReadAllText(checksum.ProfilePath);
        string tampered = original.Replace("\"metal\": 111", "\"metal\": 999");
        Check(original != tampered, "Valid JSON corruption fixture");
        File.WriteAllText(checksum.ProfilePath, tampered);
        var verified = Reopen(checksum);
        Check(verified.ReadProgression().resources.metal == 0 && verified.LastLoadStatus == SaveLoadStatus.Recovered, "Checksum catches valid-JSON corruption");

        var versionCorruption = Case("version-corruption");
        versionCorruption.InitializeProfile();
        versionCorruption.UpdateSettings(data => data.languageCode = "ru", true);
        string versionedJson = File.ReadAllText(versionCorruption.ProfilePath);
        File.WriteAllText(versionCorruption.ProfilePath, versionedJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 0"));
        var guarded = Reopen(versionCorruption);
        Check(guarded.ReadSettings().languageCode == "en" && guarded.LastLoadStatus == SaveLoadStatus.Recovered,
            "Damaged version cannot bypass checksum by appearing legacy");

        var temporary = Case("interrupted-replace");
        temporary.InitializeProfile();
        temporary.UpdateSettings(data => data.languageCode = "zh-Hans", true);
        File.Copy(temporary.ProfilePath, temporary.ProfilePath + ".tmp");
        File.Delete(temporary.ProfilePath);
        var fromTemp = Reopen(temporary);
        Check(fromTemp.ReadSettings().languageCode == "zh-Hans" && fromTemp.LastLoadStatus == SaveLoadStatus.Recovered, "Flushed temp recovers interrupted replacement");

        var partial = Case("partial-temp");
        partial.InitializeProfile();
        File.WriteAllText(partial.ProfilePath + ".tmp", "{partial");
        File.Delete(partial.ProfilePath);
        Check(Reopen(partial).ReadSettings().languageCode == "en", "Partial temp skipped in favor of valid backup");
    }

    private static void InvalidAndFuture()
    {
        foreach (string invalid in new[] { "", "null", "[]", "{}", "{\"settings\":null}", "{\"settings\":{},\"settings\":{}}", "{\"settings\":{}} garbage",
            "{\"formatVersion\":1,\"settings\":{},\"checksum\":{}}" })
        {
            var saves = Case("invalid-" + checks.Count);
            Directory.CreateDirectory(Path.GetDirectoryName(saves.ProfilePath));
            File.WriteAllText(saves.ProfilePath, invalid);
            File.WriteAllText(saves.ProfilePath + ".bak", invalid);
            saves.InitializeProfile();
            Check(saves.IsReadOnly && !saves.IsFirstLaunch && !saves.UpdateSettings(data => data.languageCode = "ru", true)
                && File.ReadAllText(saves.ProfilePath) == invalid, "Invalid profile protected: " + checks.Count);
        }
        var future = Case("future");
        future.InitializeProfile();
        string json = "{\"formatVersion\":999,\"settings\":{\"languageCode\":\"future\"}}";
        File.WriteAllText(future.ProfilePath, json);
        var protectedSave = Reopen(future);
        protectedSave.InitializeProfile();
        Check(protectedSave.IsReadOnly && protectedSave.LastLoadStatus == SaveLoadStatus.UnsupportedVersion, "Newer primary is never downgraded to older backup");
        Check(!protectedSave.Flush() && File.ReadAllText(future.ProfilePath) == json, "Newer format cannot be overwritten");
    }

    private static void CraftCompatibility()
    {
        var saves = Case("crafts");
        var ship = new SaveShip(2, Array.Empty<WeaponDataSer>(), "Legacy craft", "original");
        string old = JsonUtility.ToJson(ship);
        string path = Path.Combine(saves.SaveFolder, ship.shipName + ".json");
        File.WriteAllText(path, old);
        saves.LoadAllSaves();
        Check(saves.SavedShips.Count == 1 && File.ReadAllText(path) == old, "Legacy craft loads without rewriting");
        ship.shipDescr = "updated";
        saves.SaveShip(ship);
        Check(saves.SavedShips.Count == 1 && saves.SavedShips[0].shipDescr == "updated", "Craft update uses shared safe writer");
        var legacyReader = JsonUtility.FromJson<SaveShip>(File.ReadAllText(path));
        Check(legacyReader.shipName == ship.shipName && legacyReader.shipDescr == "updated", "Flat versioned craft remains readable by old UI");
        saves.InitializeProfile();
        saves.LoadAllSaves();
        Check(saves.SavedShips.Count == 1, "Profile sections are not listed as crafts");
        File.WriteAllText(path, "{broken");
        saves.LoadAllSaves();
        Check(saves.SavedShips.Count == 1 && saves.SavedShips[0].shipDescr == "original", "Craft recovery uses previous blueprint");
        File.Delete(path);
        saves.LoadAllSaves();
        Check(saves.SavedShips.Count == 1 && File.Exists(path), "Orphan craft backup discovered and repaired");
        Check(saves.TryDeleteShip(ship.shipName, out _) && Reopen(saves).SavedShips.Count == 0, "Deleted craft does not resurrect from backup");
        Check(saves.TrySaveNewShip(ship, out _) && saves.SavedShips.Count == 1, "New craft immediately visible after closed/committed write");
        Check(!saves.TrySaveNewShip(ship, out _), "Duplicate craft is not overwritten");
        Check(!saves.TryValidateNewShipName("../unsafe", out _, out _), "Craft path traversal rejected");
    }

    private static void MultipleManagers()
    {
        var first = Case("independent-managers");
        first.InitializeProfile();
        var second = Reopen(first);
        second.InitializeProfile();
        first.UpdateProgression(data => data.resources.metal = 70, true);
        second.UpdateProgression(data => data.levels.completedLevelIds.Add(3), true);
        var current = Reopen(first).ReadProgression();
        Check(current.resources.metal == 70 && current.levels.completedLevelIds.Contains(3), "Fallback managers preserve unrelated progression changes");
        first.UpdateSettings(data => data.languageCode = "ru");
        second.UpdateOnboarding(data => data.currentStepId = "parallel-step");
        first.Flush();
        var all = Reopen(first).ReadSnapshot();
        Check(all.settings.languageCode == "ru" && all.onboarding.currentStepId == "parallel-step", "Deferred merge preserves another manager's section");
    }

    private static void ServiceRegression()
    {
        var saves = Case("services");
        var guids = AssetDatabase.FindAssets("t:ResourceCatalog");
        var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var container = new DiContainer();
        container.Bind<SaveManager>().FromInstance(saves);
        container.Bind<ResourceCatalog>().FromInstance(catalog);
        container.Bind<PlayerResourceWallet>().AsSingle();
        container.Bind<LevelProgressService>().AsSingle();
        container.Bind<ContentProgressService>().AsSingle();
        var wallet = container.Resolve<PlayerResourceWallet>();
        var levels = container.Resolve<LevelProgressService>();
        var content = container.Resolve<ContentProgressService>();
        Check(wallet.Metal == 0 && wallet.Chip == 0, "Zenject services share profile/defaults");
        int events = 0;
        wallet.OnResourcesChanged += (_, _, _) => events++;
        wallet.Add(100, 20);
        Check(saves.IsDirty && wallet.Metal == 100 && wallet.Chip == 20 && events == 1, "Wallet add behavior/events unchanged, write deferred");
        Check(wallet.TrySpend(10, 2) && Reopen(saves).ReadProgression().resources.metal == 90, "Direct resource spending is an immediate checkpoint");
        Check(!wallet.TrySpend(999, 0) && wallet.Metal == 90, "Insufficient funds rule unchanged");
        levels.MarkLevelCompleted(2, LevelDifficulty.Normal);
        levels.MarkLevelCompleted(2, LevelDifficulty.Hard);
        levels.MarkLevelCompleted(4, LevelDifficulty.Extreme);
        var reloadedLevels = new LevelProgressService(Reopen(saves));
        Check(reloadedLevels.CompletedCount == 1 && reloadedLevels.IsLevelCompleted(2, LevelDifficulty.Hard)
            && reloadedLevels.IsLevelCompleted(4, LevelDifficulty.Extreme) && !reloadedLevels.IsLevelCompleted(4), "Level/difficulty semantics unchanged");
        var hull = ScriptableObject.CreateInstance<HullContentDefinition>();
        try
        {
            JsonUtility.FromJsonOverwrite("{\"id\":\"save-test-hull\",\"purchasePrice\":20,\"purchaseCoresPrice\":3,\"maxUpgradeLevel\":1,\"upgradePrices\":[{\"metal\":10,\"cores\":2}]}", hull);
            Check(content.TryPurchase(hull) && wallet.Metal == 70 && wallet.Chip == 15, "Content purchase behavior unchanged");
            var purchased = Reopen(saves).ReadProgression();
            Check(purchased.resources.metal == 70 && purchased.content.entries[0].isOwned, "Ownership and purchase debit persisted in one profile commit");
            Check(content.TryUpgrade(hull) && content.GetUpgradeLevel(hull) == 1 && wallet.Metal == 60, "Upgrade behavior unchanged");
            var reloadedWallet = new PlayerResourceWallet(catalog, Reopen(saves));
            var reloadedContent = new ContentProgressService(reloadedLevels, reloadedWallet);
            Check(reloadedContent.GetUpgradeLevel(hull) == 1, "Legacy content constructor shares wallet persistence");
            wallet.Reset();
            Check(Reopen(saves).ReadProgression().resources.metal == 0 && Reopen(saves).ReadProgression().content.entries.Count == 1, "Resource reset affects only resources");
            levels.ResetProgress();
            Check(Reopen(saves).ReadProgression().levels.completedLevelIds.Count == 0 && content.IsOwned(hull), "Level reset affects only level progression");
            content.ResetProgress();
            Check(Reopen(saves).ReadProgression().content.entries.Count == 0, "Content reset does not import old files again");
        }
        finally { UnityEngine.Object.DestroyImmediate(hull); }
    }

    private static void LifecycleAndIoFailure()
    {
        var saves = Case("lifecycle");
        var go = new GameObject("Save Foundation Verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var host = go.AddComponent<SaveLifecycleHost>();
            host.Initialize(saves);
            saves.UpdateSettings(data => data.musicVolume = 51);
            InvokeLifecycle(host, "OnApplicationPause", true);
            Check(Reopen(saves).ReadSettings().musicVolume == 51 && !saves.IsDirty, "Android pause flushes pending data");
            saves.UpdateSettings(data => data.sfxVolume = 39);
            InvokeLifecycle(host, "OnApplicationFocus", false);
            Check(Reopen(saves).ReadSettings().sfxVolume == 39, "Focus loss flushes pending data");
            saves.UpdateSettings(data => data.musicMuted = true);
            InvokeLifecycle(host, "OnApplicationQuit");
            Check(Reopen(saves).ReadSettings().musicMuted, "Quit flushes pending data");
            saves.UpdateSettings(data => data.sfxMuted = true);
            // MonoBehaviour callbacks are not automatic outside Play Mode; invoke the same hook.
            InvokeLifecycle(host, "OnDestroy");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Check(Reopen(saves).ReadSettings().sfxMuted, "Lifecycle host destruction flushes pending data");

        var locked = Case("locked");
        locked.InitializeProfile();
        locked.UpdateSettings(data => data.languageCode = "ru");
        using (var stream = new FileStream(locked.ProfilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(!locked.Flush() && locked.IsDirty && !string.IsNullOrEmpty(locked.LastError), "I/O failure retains pending data and exposes error");
        Check(locked.Flush() && Reopen(locked).ReadSettings().languageCode == "ru", "Save retries successfully after I/O recovery");

        var fixture = new GameObject("Save Installer Verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProjectContext.prefab");
            var installer = fixture.AddComponent<ProjectManagerInstaller>();
            EditorUtility.CopySerialized(source.GetComponent<ProjectManagerInstaller>(), installer);
            var container = new DiContainer();
            container.Inject(installer);
            installer.InstallBindings();
            var isolated = Case("global-installer");
            container.Rebind<SaveManager>().FromInstance(isolated);
            var host = container.Resolve<SaveLifecycleHost>();
            Check(host.gameObject == fixture && File.Exists(isolated.ProfilePath), "Global installer attaches lifecycle host to its existing context");
            Check(ReferenceEquals(container.Resolve<SaveManager>(), isolated), "Lifecycle host uses the container's SaveManager");
            InvokeLifecycle(host, "OnDestroy");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    // SendMessage asserts ShouldRunBehaviour in Edit Mode after a domain reload.
    // Invoke the production hook directly without asking Unity to run a behaviour.
    private static void InvokeLifecycle(SaveLifecycleHost host, string method, params object[] arguments)
        => typeof(SaveLifecycleHost).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(host, arguments);

    private static SaveManager Case(string name) => new SaveManager(NewRoot(name));
    private static string NewRoot(string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }
    private static SaveManager Reopen(SaveManager saves) => new SaveManager(Path.GetDirectoryName(saves.SaveFolder));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Save verification failed: " + message);
        checks.Add(message);
    }
}
