using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MigrateEnemyShootingProfiles
{
    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Editor must be idle in Edit Mode.");
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefub" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && (prefab.GetComponentInChildren<SimpleEnemyAttackExecutor>(true) != null
                || prefab.GetComponentInChildren<FourWayEnemy>(true) != null))
                paths.Add(path);
        }
        // Configure variants before their bases, preserving their old serialized overrides.
        paths.Sort((a, b) => Depth(b).CompareTo(Depth(a)));
        var changed = new List<string>();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        foreach (string path in paths)
        {
            if (stage != null && stage.assetPath == path)
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (FourWayEnemy enemy in root.GetComponentsInChildren<FourWayEnemy>(true))
                {
                    Undo.RecordObject(enemy, "Migrate Four Way attack");
                    SimpleEnemyAttackExecutor shared = enemy.EnsureExecutorConfigured();
                    EditorUtility.SetDirty(enemy);
                    EditorUtility.SetDirty(shared);
                    if (shared.BurstAttackSettings.Shooting.ShootingType != EnemyShootingType.Rotation)
                        throw new Exception(path + ": Four Way profile migration failed.");
                }
                foreach (SimpleEnemyAttackExecutor shared in root.GetComponentsInChildren<SimpleEnemyAttackExecutor>(true))
                {
                    Undo.RecordObject(shared, "Migrate shared shooting profile");
                    shared.MigrateLegacyAttackMode();
                    EditorUtility.SetDirty(shared);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return new { migratedPrefabs = changed.Count, paths = changed };
    }

    private static int Depth(string path)
    {
        int depth = 0;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        while (asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
        {
            depth++;
            asset = PrefabUtility.GetCorrespondingObjectFromSource(asset);
        }
        return depth;
    }
}
