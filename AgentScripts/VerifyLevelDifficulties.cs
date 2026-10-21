using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyLevelDifficulties
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            throw new Exception("Editor must be idle outside Play Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Config/Levels/LevelCatalog.asset");
        foreach (var definition in catalog.Levels)
        {
            Check(definition != null, "Missing definition");
            foreach (LevelDifficulty difficulty in Enum.GetValues(typeof(LevelDifficulty)))
            {
                var config = definition.GetDifficulty(difficulty);
                Check(config != null && config.Id == definition.Id && config.Difficulty == difficulty, "Invalid identity");
                Check(config.Waves.SequenceEqual(definition.Normal.Waves), "Migration changed configured wave references");
                Check(catalog.GetLevel(definition.Id, difficulty) == config, "Incorrect catalog routing");
            }
            Check(definition.Normal != definition.Hard && definition.Hard != definition.Extreme, "Shared variant asset");
        }
        var sceneLoaders = Resources.FindObjectsOfTypeAll<LoadLevelConfig>()
            .Where(x => x.gameObject.scene.path == "Assets/Scenes/NewMainMenu.unity").ToArray();
        Check(sceneLoaders.Length == catalog.Levels.Count && sceneLoaders.All(x => x.LevelDefinition != null), "Missing scene loader definition");
        var sceneController = Resources.FindObjectsOfTypeAll<NewMainMenuLevelSelectionController>()
            .First(x => x.gameObject.scene.path == "Assets/Scenes/NewMainMenu.unity");
        foreach (string field in new[] { "normalDifficultyButton", "hardDifficultyButton", "extremeDifficultyButton" })
            Check(typeof(NewMainMenuLevelSelectionController).GetField(field, Fields).GetValue(sceneController) != null, "Missing scene difficulty button");

        string testDirectory = Path.Combine(Path.GetTempPath(), "CodexLevelDifficulty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        string save = Path.Combine(testDirectory, "progress.json");
        File.WriteAllText(save, "{\"completedLevelIds\":[" + string.Join(",", catalog.Levels.Select(x => x.Id)) + "]}");
        var originalLevel = LevelLoader.SelectedLevelConfig;
        int originalIndex = LevelLoader.LevelIndex;
        string originalName = LevelLoader.LevelName;
        var originalDifficulty = LevelLoader.SelectedDifficulty;
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject menu = null;
        GameObject loaderObject = null;
        LevelConfig requirementProbe = null;
        try
        {
            var progress = ProgressAt(save);
            var first = catalog.Levels[0];
            Check(progress.IsLevelCompleted(first.Normal), "Legacy Normal progress lost");
            Check(!progress.IsLevelCompleted(first.Hard) && !progress.IsLevelCompleted(first.Extreme), "Legacy save completed other difficulties");
            requirementProbe = UnityEngine.Object.Instantiate(catalog.Levels[1].Normal);
            var probe = new SerializedObject(requirementProbe);
            probe.FindProperty("requiredLevel").objectReferenceValue = first.Hard;
            probe.ApplyModifiedPropertiesWithoutUndo();
            Check(!progress.CanStartLevel(requirementProbe), "Normal unlocked a Hard prerequisite");
            progress.MarkLevelCompleted(first.Hard);
            Check(progress.CanStartLevel(requirementProbe), "Hard prerequisite not unlocked");
            Check(!progress.IsLevelCompleted(first.Extreme), "Hard completed Extreme");
            var reloaded = ProgressAt(save);
            Check(reloaded.IsLevelCompleted(first.Hard) && !reloaded.IsLevelCompleted(first.Extreme), "Difficulty progress not persisted");
            reloaded.MarkLevelCompleted(first.Extreme);
            Check(ProgressAt(save).IsLevelCompleted(first.Extreme), "Extreme progress not persisted");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/UI Prefab/SelectLevel.prefab");
            menu = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(menu, preview);
            var controller = menu.GetComponent<NewMainMenuLevelSelectionController>();
            var slotsField = typeof(NewMainMenuLevelSelectionController).GetField("shipSlots", Fields);
            slotsField.SetValue(controller, Array.CreateInstance(slotsField.FieldType.GetElementType(), 0));
            Set(controller, "selectedShipsService", new TeamSelectionService(null));
            Set(controller, "prefabFactory", new PrefabFactory(Array.Empty<GameObject>(), Array.Empty<GameObject>()));
            Set(controller, "tabsController", null);
            Set(controller, "studioController", null);
            typeof(NewMainMenuLevelSelectionController).GetMethod("RegisterButtons", Fields).Invoke(controller, null);
            loaderObject = new GameObject("Difficulty verification loader");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(loaderObject, preview);
            var loader = loaderObject.AddComponent<LoadLevelConfig>();
            Set(loader, "progressService", progress);
            int clicks = 0;
            foreach (var definition in catalog.Levels)
            {
                Set(loader, "levelDefinition", definition);
                controller.Show(definition.Normal, loader);
                Check(controller.SelectedLevel == definition.Normal, "Opening another level retained difficulty");
                foreach (var field in new[] { "hardDifficultyButton", "extremeDifficultyButton", "normalDifficultyButton" })
                {
                    var button = (Button)typeof(NewMainMenuLevelSelectionController).GetField(field, Fields).GetValue(controller);
                    Check(button != null && button.interactable, "Difficulty button unavailable");
                    button.onClick.Invoke();
                    var expectedDifficulty = field == "hardDifficultyButton" ? LevelDifficulty.Hard
                        : field == "extremeDifficultyButton" ? LevelDifficulty.Extreme : LevelDifficulty.Normal;
                    var expected = definition.GetDifficulty(expectedDifficulty);
                    Check(controller.SelectedLevel == expected && controller.SelectedDifficulty == expectedDifficulty, "Button selected incorrect variant");
                    LevelLoader.SelectLevel(controller.SelectedLevel);
                    Check(LevelLoader.GetSelectedLevel(catalog) == expected, "Battle receives wrong variant");
                    LevelLoader.SelectedLevelConfig = null;
                    Check(LevelLoader.GetSelectedLevel(catalog) == expected, "Catalog recovery lost selected difficulty");
                    clicks++;
                }
                controller.SelectHardDifficulty();
            }
            return new { definitions = catalog.Levels.Count, buttonClicks = clicks, legacyProgressPreserved = true, separateDifficultyProgress = true, battleConfigRouting = true, passed = true };
        }
        finally
        {
            LevelLoader.SelectedLevelConfig = originalLevel;
            LevelLoader.LevelIndex = originalIndex;
            LevelLoader.LevelName = originalName;
            typeof(LevelLoader).GetField("<SelectedDifficulty>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, originalDifficulty);
            if (requirementProbe != null) UnityEngine.Object.DestroyImmediate(requirementProbe);
            EditorSceneManager.ClosePreviewScene(preview);
            File.Delete(save);
            Directory.Delete(testDirectory);
        }
    }
    static LevelProgressService ProgressAt(string path)
    {
        var progress = new LevelProgressService();
        Set(progress, "savePath", path);
        return progress;
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
