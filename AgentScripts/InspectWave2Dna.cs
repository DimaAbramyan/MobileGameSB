using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class InspectWave2Dna
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Get(object o, string field) => o.GetType().GetField(field, Flags).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Editor must be idle");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var root = stage != null && stage.assetPath == "Assets/Waves/2/2_1_extrime.prefab" ? stage.prefabContentsRoot : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_1_extrime.prefab");
        var results = new List<object>();
        foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            if (!wave.name.Trim().EndsWith("right") && !wave.name.Trim().EndsWith("left")) continue;
            var resolved = new DirectedWaveAttackSettings();
            if (!wave.GetComponent<DirectedWaveAttackBehaviour>().TryCopyResolvedAttackSettingsTo(0, false, resolved)) throw new Exception("No resolved profile");
            var profile = resolved.WaveBurstSettings;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var clone = UnityEngine.Object.Instantiate(wave.GetConfiguredEnemyPrefabForSlot(0));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone.gameObject, scene);
            var world = new World("Actual wave DNA inspection");
            var spawner = new EnemyProjectileEcsSpawner();
            Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
            try
            {
                var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
                Set(executor, "projectileSpawner", spawner);
                executor.GetType().GetMethod("Awake", Flags).Invoke(executor, null);
                Set(executor, "autonomousAttack", false);
                executor.SetWaveAttackSettings(profile); executor.SetWaveAimTarget(null);
                if (!executor.TryFireInDirection(Vector3.up, profile)) throw new Exception("Emission failed");
                using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(EnemyProjectileVelocity));
                int first = query.CalculateEntityCount();
                using var movements = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
                using var velocities = query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp);
                var pending = (IList)Get(executor, "pendingSectors");
                float start = (float)Get(pending[0], "StartTime");
                executor.GetType().GetMethod("TickPendingSectors", Flags).Invoke(executor, new object[] { start + 7.01f });
                int all = query.CalculateEntityCount();
                float halfCycle = profile.Shooting.GetDnaHalfCycleDuration();
                float speed = math.length(velocities[0].Value);
                EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), math.radians(5), halfCycle, halfCycle * .5f, speed, out var offset, out var direction, out var angular);
                var projectile = (EnemyBullet)executor.GetType().GetMethod("GetSelectedProjectilePrefab", Flags).Invoke(executor, new object[] { profile.Shooting.ProjectileType });
                var renderer = projectile.GetComponentInChildren<SpriteRenderer>(true);
                results.Add(new { wave = wave.name, firstLaunchCount = first, wholeSectorCount = all, dnaHalfCycleSeconds = halfCycle, baseSpeed = speed, maximumFanWidth = math.abs(offset.x) * 2, projectile = AssetDatabase.GetAssetPath(projectile), spriteWidth = renderer == null ? 0 : renderer.bounds.size.x, dnaMedian = movements[0].DnaMedianDirection.ToString() });
                if (first != 4 || all != 32) throw new Exception("Unexpected projectile count");
            }
            finally
            {
                using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity))) world.EntityManager.DestroyEntity(query);
                var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
                foreach (object visual in visuals.Values)
                {
                    UnityEngine.Object.DestroyImmediate((Material)visual.GetType().GetField("Material").GetValue(visual));
                    UnityEngine.Object.DestroyImmediate((Mesh)visual.GetType().GetField("Mesh").GetValue(visual));
                }
                visuals.Clear(); spawner.Dispose(); UnityEngine.Object.DestroyImmediate(clone.gameObject); world.Dispose();
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        return results;
    }
}
