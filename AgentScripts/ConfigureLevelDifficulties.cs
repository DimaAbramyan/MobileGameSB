using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class ConfigureLevelDifficulties
{
    const string CatalogPath = "Assets/Config/Levels/LevelCatalog.asset";
    const string MenuPrefabPath = "Assets/Prefub/UI Prefab/SelectLevel.prefab";
    const string MenuScenePath = "Assets/Scenes/NewMainMenu.unity";

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Editor must be idle outside Play Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MenuScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("NewMainMenu must be loaded before wiring its controls.");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("LevelCatalog is missing.");

        var normals = AssetDatabase.FindAssets("t:LevelConfig", new[] { "Assets/Config/Levels" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
            .Where(x => x != null && x.Difficulty == LevelDifficulty.Normal)
            .OrderBy(x => x.Id).ToArray();
        if (normals.Length == 0 || normals.Select(x => x.Id).Distinct().Count() != normals.Length)
            throw new InvalidOperationException("Normal configs must exist and have unique IDs.");

        var definitions = new Dictionary<LevelConfig, LevelDefinitionConfig>();
        var catalogObject = new SerializedObject(catalog);
        var oldDefault = catalogObject.FindProperty("legacyDefaultLevel").objectReferenceValue as LevelConfig;
        foreach (var normal in normals)
        {
            string normalPath = AssetDatabase.GetAssetPath(normal);
            string stem = Path.GetFileNameWithoutExtension(normalPath);
            string folder = Path.GetDirectoryName(normalPath).Replace('\\', '/');
            var hard = GetOrCreateVariant(normal, folder + "/" + stem + "_Hard.asset", LevelDifficulty.Hard);
            var extreme = GetOrCreateVariant(normal, folder + "/" + stem + "_Extreme.asset", LevelDifficulty.Extreme);
            string definitionPath = folder + "/" + stem + "_Definition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinitionConfig>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinitionConfig>();
                AssetDatabase.CreateAsset(definition, definitionPath);
                Undo.RegisterCreatedObjectUndo(definition, "Create level difficulty definition");
            }
            Undo.RecordObject(definition, "Configure level difficulties");
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("normal").objectReferenceValue = normal;
            serialized.FindProperty("hard").objectReferenceValue = hard;
            serialized.FindProperty("extreme").objectReferenceValue = extreme;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);
            definitions.Add(normal, definition);
        }
        Undo.RecordObject(catalog, "Migrate level catalog");
        catalogObject.Update();
        var entries = catalogObject.FindProperty("levelDefinitions");
        entries.arraySize = normals.Length;
        for (int i = 0; i < normals.Length; i++)
            entries.GetArrayElementAtIndex(i).objectReferenceValue = definitions[normals[i]];
        catalogObject.FindProperty("defaultLevelDefinition").objectReferenceValue =
            oldDefault != null && definitions.TryGetValue(oldDefault, out var defaultDefinition)
                ? defaultDefinition : definitions[normals[0]];
        catalogObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);

        int prefabLoaders = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || (path != MenuPrefabPath
                && prefab.GetComponentsInChildren<LoadLevelConfig>(true).Length == 0)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var loader in root.GetComponentsInChildren<LoadLevelConfig>(true))
                {
                    if (!WireLoader(loader, definitions)) continue;
                    changed = true;
                    prefabLoaders++;
                }
                if (path == MenuPrefabPath)
                {
                    WireController(root.GetComponent<NewMainMenuLevelSelectionController>());
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        int sceneLoaders = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var loader in root.GetComponentsInChildren<LoadLevelConfig>(true))
                if (WireLoader(loader, definitions)) sceneLoaders++;
            foreach (var controller in root.GetComponentsInChildren<NewMainMenuLevelSelectionController>(true))
                WireController(controller);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save NewMainMenu.");
        return new { definitions = definitions.Count, sceneLoaders, prefabLoaders, normalUnchanged = true };
    }

    static LevelConfig GetOrCreateVariant(LevelConfig source, string path, LevelDifficulty difficulty)
    {
        var existing = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
        if (existing != null)
        {
            if (existing.Id != source.Id || existing.Difficulty != difficulty)
                throw new InvalidOperationException("Existing variant has a different identity: " + path);
            return existing;
        }
        var variant = UnityEngine.Object.Instantiate(source);
        variant.name = Path.GetFileNameWithoutExtension(path);
        var serialized = new SerializedObject(variant);
        serialized.FindProperty("difficulty").enumValueIndex = (int)difficulty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(variant, path);
        Undo.RegisterCreatedObjectUndo(variant, "Create difficulty variant");
        return variant;
    }

    static bool WireLoader(LoadLevelConfig loader, Dictionary<LevelConfig, LevelDefinitionConfig> definitions)
    {
        var normal = loader.LevelConfig;
        if (normal == null || !definitions.TryGetValue(normal, out var definition)) return false;
        Undo.RecordObject(loader, "Assign level definition");
        var serialized = new SerializedObject(loader);
        serialized.FindProperty("levelDefinition").objectReferenceValue = definition;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(loader);
        if (PrefabUtility.IsPartOfPrefabInstance(loader))
            PrefabUtility.RecordPrefabInstancePropertyModifications(loader);
        return true;
    }

    static void WireController(NewMainMenuLevelSelectionController controller)
    {
        if (controller == null) throw new InvalidOperationException("SelectLevel controller is missing.");
        var root = controller.transform.Find("SelectDificulty");
        if (root == null) throw new InvalidOperationException("SelectDificulty is missing.");
        Undo.RecordObject(controller, "Wire difficulty buttons");
        var serialized = new SerializedObject(controller);
        WireButton(serialized, root, "Normal", "normalDifficultyButton", "Normal");
        WireButton(serialized, root, "Hard", "hardDifficultyButton", "Hard");
        WireButton(serialized, root, "Extrim", "extremeDifficultyButton", "Extreme");
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        if (PrefabUtility.IsPartOfPrefabInstance(controller))
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
    }

    static void WireButton(SerializedObject controller, Transform root, string childName, string field, string label)
    {
        var child = root.Find(childName);
        var button = child != null ? child.GetComponent<Button>() : null;
        if (button == null) throw new InvalidOperationException("Missing difficulty button " + childName);
        controller.FindProperty(field).objectReferenceValue = button;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) throw new InvalidOperationException("Missing button label " + childName);
        Undo.RecordObject(text, "Name difficulty");
        text.text = label;
        EditorUtility.SetDirty(text);
        if (PrefabUtility.IsPartOfPrefabInstance(text))
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }
}
