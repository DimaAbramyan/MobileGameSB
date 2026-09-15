using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DirectedEnemySubWaveStructuredConfigurationMigration
{
    private const string WavesFolder = "Assets/Waves";

    public static string MigrateWavePrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { WavesFolder });
        int prefabCount = 0;
        int savedPrefabCount = 0;
        int subWaveCount = 0;
        int migratedSubWaveCount = 0;
        List<string> errors = new();

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (string.IsNullOrEmpty(prefabPath))
                continue;

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                DirectedEnemySubWave[] subWaves =
                    prefabRoot.GetComponentsInChildren<DirectedEnemySubWave>(true);
                if (subWaves.Length == 0)
                    continue;

                prefabCount++;
                bool needsSave = EditorUtility.IsDirty(prefabRoot);

                for (int subWaveIndex = 0;
                     subWaveIndex < subWaves.Length;
                     subWaveIndex++)
                {
                    DirectedEnemySubWave subWave = subWaves[subWaveIndex];
                    if (subWave == null)
                        continue;

                    subWaveCount++;
                    if (subWave.TryMigrateLegacyBehaviourConfiguration())
                    {
                        migratedSubWaveCount++;
                        EditorUtility.SetDirty(subWave);
                        needsSave = true;
                    }

                    if (EditorUtility.IsDirty(subWave))
                        needsSave = true;
                }

                if (!needsSave)
                    continue;

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath);
                if (savedPrefab == null)
                {
                    errors.Add($"{prefabPath}: Unity could not save the prefab.");
                    continue;
                }

                savedPrefabCount++;
            }
            catch (Exception exception)
            {
                errors.Add($"{prefabPath}: {exception.Message}");
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        string result = $"Prefabs: {prefabCount}; saved: {savedPrefabCount}; "
            + $"subwaves: {subWaveCount}; explicitly migrated: {migratedSubWaveCount}.";
        if (errors.Count == 0)
            return result;

        return result + " Errors: " + string.Join(" | ", errors);
    }
}
