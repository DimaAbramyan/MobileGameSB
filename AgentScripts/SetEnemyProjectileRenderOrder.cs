using System;
using System.Collections;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class SetEnemyProjectileRenderOrder
{
    private const string BasePrefab = "Assets/Prefub/Enemy Projectiles/Bullet.prefab";
    public static object Apply()
    {
        RequireIdle();
        var root = PrefabUtility.LoadPrefabContents(BasePrefab);
        try
        {
            var renderer = root.GetComponent<EnemyProjectileAuthoring>().VisualRenderer;
            int before = renderer.sortingOrder;
            if (before != 99)
            {
                Undo.RecordObject(renderer, "Enemy ECS projectile order 99");
                renderer.sortingOrder = 99;
                PrefabUtility.SaveAsPrefabAsset(root, BasePrefab);
            }
            return new { prefab = BasePrefab, before, after = renderer.sortingOrder };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static object Verify()
    {
        RequireIdle();
        using (var world = new World("Enemy render priority verification"))
        using (var spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world))
        {
            int prefabs = 0;
            foreach (string name in new[] { "Bullet", "HomingEnemyProjectile", "BurstAtPointEnemyProjectile" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/" + name + ".prefab");
                if (root.GetComponent<EnemyProjectileAuthoring>().VisualRenderer.sortingOrder != 99)
                    throw new Exception("Wrong sortingOrder: " + name);
                if (!spawner.TrySpawn(root.GetComponent<EnemyBullet>(), Vector3.zero, Vector3.down, 1))
                    throw new Exception("Failed ECS spawn: " + name);
                prefabs++;
            }
            var visuals = (IDictionary)typeof(EnemyProjectileEcsSpawner).GetField("visualsByConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner);
            foreach (object visual in visuals.Values)
            {
                var material = (Material)visual.GetType().GetField("Material").GetValue(visual);
                if (material.renderQueue != 4099) throw new Exception("Wrong ECS render queue");
            }
            return new { prefabs, sortingOrder = 99, ecsRenderQueue = 4099, materials = visuals.Count };
        }
    }
    private static void RequireIdle()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
    }
}
