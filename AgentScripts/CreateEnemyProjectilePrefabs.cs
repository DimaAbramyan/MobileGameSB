using UnityEditor;
using UnityEngine;

public static class CreateEnemyProjectilePrefabs
{
    private const string SourcePath = "Assets/Prefub/Enemy Projectiles/Bullet.prefab";
    private const string HomingPath =
        "Assets/Prefub/Enemy Projectiles/HomingEnemyProjectile.prefab";
    private const string BurstPath =
        "Assets/Prefub/Enemy Projectiles/BurstAtPointEnemyProjectile.prefab";

    public static string Execute()
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (sourcePrefab == null)
            return $"Missing source prefab: {SourcePath}";

        EnemyBullet sourceProjectile = sourcePrefab.GetComponent<EnemyBullet>();
        if (sourceProjectile == null)
            return $"Missing {nameof(EnemyBullet)} on {SourcePath}";

        GameObject homingPrefab = CreateOrLoad(HomingPath, sourcePrefab);
        GameObject burstPrefab = CreateOrLoad(BurstPath, sourcePrefab);
        ConfigureProjectile(
            homingPrefab,
            EnemyProjectileFlightMode.Homing,
            sourceProjectile);
        ConfigureProjectile(
            burstPrefab,
            EnemyProjectileFlightMode.BurstAtPoint,
            sourceProjectile);

        int configuredExecutors = ConfigureAttackExecutors(
            sourceProjectile,
            homingPrefab.GetComponent<EnemyBullet>(),
            burstPrefab.GetComponent<EnemyBullet>());
        return $"Created/updated: {HomingPath}, {BurstPath}; "
            + $"configured attack executors: {configuredExecutors}.";
    }

    private static GameObject CreateOrLoad(string path, GameObject sourcePrefab)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static void ConfigureProjectile(
        GameObject prefab,
        EnemyProjectileFlightMode flightMode,
        EnemyBullet normalProjectile)
    {
        EnemyProjectileAuthoring authoring = prefab.GetComponent<EnemyProjectileAuthoring>();
        if (authoring == null)
            throw new MissingComponentException(
                $"{prefab.name} needs {nameof(EnemyProjectileAuthoring)}.");

        var serialized = new SerializedObject(authoring);
        serialized.FindProperty("flightMode").enumValueIndex = (int)flightMode;
        serialized.FindProperty("scaleBehavior").enumValueIndex =
            (int)EnemyProjectileScaleBehavior.None;
        if (flightMode == EnemyProjectileFlightMode.BurstAtPoint)
            serialized.FindProperty("burstProjectilePrefab").objectReferenceValue =
                normalProjectile;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(authoring);
        AssetDatabase.SaveAssetIfDirty(authoring);
    }

    private static int ConfigureAttackExecutors(
        EnemyBullet defaultProjectile,
        EnemyBullet homingProjectile,
        EnemyBullet burstProjectile)
    {
        int configured = 0;
        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/Prefub" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            SimpleEnemyAttackExecutor[] executors = prefab
                .GetComponentsInChildren<SimpleEnemyAttackExecutor>(true);
            foreach (SimpleEnemyAttackExecutor executor in executors)
            {
                var serialized = new SerializedObject(executor);
                SerializedProperty defaultField = serialized.FindProperty(
                    "defaultProjectilePrefab");
                SerializedProperty homingField = serialized.FindProperty(
                    "homingProjectilePrefab");
                SerializedProperty burstField = serialized.FindProperty(
                    "burstAtPointProjectilePrefab");
                if (defaultField == null || homingField == null || burstField == null)
                    continue;

                bool changed = false;
                if (defaultField.objectReferenceValue == null)
                {
                    defaultField.objectReferenceValue = defaultProjectile;
                    changed = true;
                }

                if (homingField.objectReferenceValue == null)
                {
                    homingField.objectReferenceValue = homingProjectile;
                    changed = true;
                }

                if (burstField.objectReferenceValue == null)
                {
                    burstField.objectReferenceValue = burstProjectile;
                    changed = true;
                }

                if (!changed)
                    continue;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(executor);
                AssetDatabase.SaveAssetIfDirty(executor);
                configured++;
            }
        }

        return configured;
    }
}
