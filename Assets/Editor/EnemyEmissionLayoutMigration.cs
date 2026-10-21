using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runtime validation also migrates old profiles. This command persists the new
// schema through Unity serialization, keeping prefab GUIDs and nested instances.
public static class EnemyEmissionLayoutMigration
{
    [MenuItem("Tools/Enemy Attacks/Migrate Selected Emission Layouts")]
    private static void MigrateSelection()
    {
        var paths = new List<string>();
        foreach (var asset in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
        }
        int count = MigrateAssets(paths.ToArray());
        Debug.Log($"Emission Layout migration saved {count} assets.");
    }

    public static int MigrateAssets(string[] paths)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new System.InvalidOperationException("Wait for an idle Editor in Edit Mode before migrating emission layouts.");
        int saved = 0;
        foreach (string path in paths)
        {
            if (path.EndsWith(".prefab"))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                bool inStage = stage != null && stage.assetPath == path;
                GameObject root = inStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component != null) changed |= MigrateObject(component);
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success) throw new System.InvalidOperationException($"Could not save migrated prefab: {path}");
                    if (inStage) EditorSceneManager.MarkSceneDirty(stage.scene);
                    saved++;
                }
                finally { if (!inStage) PrefabUtility.UnloadPrefabContents(root); }
            }
            else if (AssetDatabase.LoadMainAssetAtPath(path) is ScriptableObject asset && MigrateObject(asset))
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                saved++;
            }
        }
        return saved;
    }

    private static bool MigrateObject(UnityEngine.Object target)
    {
        var serialized = new SerializedObject(target);
        var iterator = serialized.GetIterator();
        bool hasSettings = false;
        Undo.RecordObject(target, "Migrate Emission Layout");
        while (iterator.Next(true))
        {
            if (iterator.type == nameof(EnemyBurstAttackSettings))
            {
                var settings = (EnemyBurstAttackSettings)iterator.boxedValue;
                settings.Validate();
                iterator.boxedValue = settings;
                hasSettings = true;
            }
            else if (iterator.type == nameof(EnemyShootingSettings))
            {
                var settings = (EnemyShootingSettings)iterator.boxedValue;
                settings.Validate();
                iterator.boxedValue = settings;
                hasSettings = true;
            }
        }
        if (hasSettings)
        {
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
        return hasSettings;
    }
}
