using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePreviewDelayedSnapshots
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Flags).SetValue(value, data);
    private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Flags).Invoke(value, args);
    private static string State(EnemyProjectilePreviewWorld world)
    {
        var manager = world.SimulationWorld.EntityManager; var result = new List<string>();
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
            result.Add(JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileVelocity>(entity)));
        result.Sort(StringComparer.Ordinal); return string.Join("\n", result);
    }
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene(); var go = new GameObject("Snapshot camera") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 5; camera.aspect = 1; camera.transform.position = new Vector3(0, 0, -10);
        try
        {
            foreach (var origin in new[] { EnemyChildAttackOriginMode.CaptureOnStart, EnemyChildAttackOriginMode.FollowParent })
            using (var world = new EnemyProjectilePreviewWorld())
            {
                object spawner = typeof(EnemyProjectilePreviewWorld).GetProperty("SnapshotManagedRoot", Flags).GetValue(world);
                var child = new EnemyChildAttackSettings(); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent);
                Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached);
                child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Top);
                Set(child, "timeOffset", .8f); Set(child, "originMode", origin); Set(child, "destroyParent", true);
                Set(child, "repeatCount", 2); Set(child, "repeatInterval", .15f);
                Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .1f);
                child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .1f);
                child.Pattern.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .05f, .08f);
                var shooting = new EnemyShootingSettings(); shooting.SetChildAttack(child); shooting.SetProjectileBaseSpeedOverride(true, 10);
                Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 10f); shooting.Validate();
                var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
                var completion = new EnemyAttackCompletionTracker(); var requests = new List<EnemyProjectileSpawnRequest>();
                EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.zero, Vector3.zero, Vector3.up,
                    1, 10, 0, new EnemyProjectilePrefabSet(bullet), completionTracker: completion);
                world.Spawn(requests);
                void Advance(int from, int through)
                { for (int frame = from + 1; frame <= through; frame++) world.Step(frame / 60f, 1 / 60f, new Vector3(1000, 1000), camera); }
                Advance(0, 45);
                Check(world.Count == 1 && completion.PendingChildAttacks > 0, "Boundary delay is pending at snapshot");
                string before = State(world); int pending = completion.PendingChildAttacks;
                var nested = typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot", BindingFlags.NonPublic);
                using var native = (IDisposable)Activator.CreateInstance(nested, Flags, null, new object[] { world }, null);
                var graphType = typeof(WavePreviewPlayer).Assembly.GetType("WavePreviewStateGraph");
                object managed = graphType.GetMethod("Capture", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { new object[] { spawner, completion } });
                void Restore()
                {
                    object map = Call(native, "Restore", world); Call(managed, "Restore", map); Call(world, "RestoreClock", .75f);
                }
                Advance(45, 150); string final = State(world);
                Check(world.Count == 12 && completion.PendingChildAttacks == 0, "Delayed repeats and sequential sectors finish and destroy parent");
                Restore();
                Check(State(world) == before && completion.PendingChildAttacks == pending, "Pending event and shared completion tracker restored");
                Advance(45, 150);
                Check(State(world) == final && completion.PendingChildAttacks == 0, "Delayed child continuation matches with remapped parent entity");
                Restore(); Advance(45, 150);
                Check(State(world) == final, "Snapshot remains reusable after another continuation");
            }
            using (var world = new EnemyProjectilePreviewWorld())
            {
                _ = typeof(EnemyProjectilePreviewWorld).GetProperty("SnapshotManagedRoot", Flags).GetValue(world);
                var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
                var shooting = new EnemyShootingSettings(); shooting.SetProjectileBaseSpeedOverride(true, 1);
                var requests = new List<EnemyProjectileSpawnRequest>();
                EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.zero, Vector3.zero, Vector3.up,
                    1, 1, 0, new EnemyProjectilePrefabSet(bullet)); world.Spawn(requests);
                var manager = world.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                using (var entities = query.ToEntityArray(Allocator.Temp))
                    manager.AddComponentData(entities[0], new EnemyProjectileHoming { RemainingDuration = 10, TurnSpeedDegreesPerSecond = 360 });
                world.Step(.1f, .1f, new Vector3(0, 100), camera); string initial = State(world);
                var nested = typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot", BindingFlags.NonPublic);
                using var snapshot = (IDisposable)Activator.CreateInstance(nested, Flags, null, new object[] { world }, null);
                Check(State(world) == initial, "Snapshot capture does not tick homing projectiles");
                world.Step(.2f, .1f, new Vector3(100, 0), camera); string final = State(world);
                using (var entities = query.ToEntityArray(Allocator.Temp))
                    Check(manager.GetComponentData<EnemyProjectileVelocity>(entities[0]).Value.x > .1f, "Active homing follows newly moved target on resume");
                Call(snapshot, "Restore", world); Call(world, "RestoreClock", .1f);
                Check(State(world) == initial, "Homing native state restored");
                world.Step(.2f, .1f, new Vector3(100, 0), camera);
                Check(State(world) == final, "Homing continuation remains exact after restoration");
            }
        }
        finally { Object.DestroyImmediate(go); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "Native and managed delayed snapshots leave no worlds");
        return new { passed = true, checks, delayedBoundary = true, origins = 2, repeats = true, sequentialSector = true, cleanup = true };
    }
}
