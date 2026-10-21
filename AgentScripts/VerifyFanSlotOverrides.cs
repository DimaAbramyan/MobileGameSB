using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class VerifyFanSlotOverrides
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            throw new Exception("Editor must be idle in Edit Mode.");
        const string path = "Assets/Waves/1_Hard/1_4_hard.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var assetWave = asset.transform.Find("GameObject").GetComponent<DirectedEnemySubWave>();
        var waveClone = UnityEngine.Object.Instantiate(assetWave.gameObject);
        var wave = waveClone.GetComponent<DirectedEnemySubWave>();
        var settings = new DirectedWaveAttackSettings();
        if (!wave.AttackPatternBehaviour.CopyPreviewAttackSettingsTo(0, false, settings))
            throw new Exception("Missing attack configuration.");
        if (settings.UsesEnemyBurstSettings || settings.WaveBurstSettings.Shooting.ShootingMode != EnemyProjectileFireMode.Default)
            throw new Exception("This regression requires the original shared Wave Override / Default.");
        // Isolate projectile shape from the unrelated scene player injection.
        Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
        var world = new World("Fan slot override verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world);
        Set(spawner, "entityManager", world.EntityManager);
        var results = new List<object>();
        try
        {
            for (int slot = 0; slot < 3; slot++)
            {
                Enemy prefab = assetWave.GetConfiguredEnemyPrefabForSlot(slot);
                var point = assetWave.GetConfiguredEnemyOverrideForSlot(slot);
                var clone = UnityEngine.Object.Instantiate(prefab.gameObject);
                try
                {
                    var shared = clone.GetComponent<SimpleEnemyAttackExecutor>();
                    Set(shared, "projectileSpawner", spawner);
                    Invoke(shared, "Awake");
                    var enemy = clone.GetComponent<Enemy>();
                    var controllerTypes = new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" };
                    foreach (string name in controllerTypes)
                    {
                        object controller = Controller(name, wave, settings);
                        var executor = Acquire(controller, enemy);
                        var resolved = (EnemyBurstAttackSettings)Invoke(controller, "GetBurstSettings", enemy, executor);
                        Assert(ReferenceEquals(resolved, settings.WaveBurstSettings), "Unconfigured enemy must use shared Wave Override");
                        Fire(controller, name, enemy, executor, resolved);
                        Assert(Count(world) == 1, "Shared Default must stay single before point override");
                        Clear(world);
                        controller.GetType().GetMethod("Stop").Invoke(controller, null);
                    }
                    point.ApplyTo(enemy); // Same call as DirectedEnemySubWave.SpawnEnemy.
                    Assert(shared.HasAttackSettingsOverride, "Point profile must be marked as an explicit override");
                    foreach (string name in controllerTypes)
                    {
                        object controller = Controller(name, wave, settings);
                        var executor = Acquire(controller, enemy);
                        var resolved = (EnemyBurstAttackSettings)Invoke(controller, "GetBurstSettings", enemy, executor);
                        Assert(ReferenceEquals(resolved, shared.BurstAttackSettings), "Point Attack Pattern must take priority");
                        var configured = point.AttackSettingsOverride;
                        Assert(resolved.AttackShotCount == configured.AttackShotCount
                            && resolved.BurstShotCount == configured.BurstShotCount
                            && resolved.AttackCooldown == configured.AttackCooldown,
                            "Preserve the configured point timing and shot counts");
                        Assert(Get(shared, "waveShootingSettings") == null, "Rotation must use the same resolved profile as projectiles");
                        Fire(controller, name, enemy, executor, resolved);
                        int expected = configured.ProjectilesPerShot;
                        Assert(Count(world) == expected, "Configured point projectile count");
                        if (configured.Shooting.ShootingMode == EnemyProjectileFireMode.Fan)
                            VerifyFanAngles(world, clone.transform.up, resolved.Shooting);
                        results.Add(new { slot, controller = name, mode = resolved.Shooting.ShootingMode.ToString(),
                            projectiles = Count(world), timingPreserved = true });
                        Clear(world);
                        controller.GetType().GetMethod("Stop").Invoke(controller, null);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            return new { prefab = path, overridePriority = "Point > Wave > Enemy Prefab", checks = results };
        }
        finally
        {
            Clear(world);
            var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
            foreach (object visual in visuals.Values)
            {
                UnityEngine.Object.DestroyImmediate((Material)visual.GetType().GetField("Material").GetValue(visual));
                UnityEngine.Object.DestroyImmediate((Mesh)visual.GetType().GetField("Mesh").GetValue(visual));
            }
            visuals.Clear();
            spawner.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(waveClone);
        }
    }

    private static object Controller(string name, DirectedEnemySubWave wave, DirectedWaveAttackSettings settings)
    {
        Type type = typeof(DirectedEnemySubWave).Assembly.GetType(name, true);
        return Activator.CreateInstance(type, name == "DirectedWaveAttackController"
            ? new object[] { wave, settings, null }
            : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
    }
    private static IWaveAttackExecutor Acquire(object controller, Enemy enemy)
    {
        object[] args = { enemy, null };
        Assert((bool)Invoke(controller, "TryGetExecutor", args), "Controller must acquire the real prefab executor");
        return (IWaveAttackExecutor)args[1];
    }
    private static void Fire(object controller, string name, Enemy enemy, IWaveAttackExecutor executor, EnemyBurstAttackSettings settings)
    {
        bool fired = name == "DirectedWaveAttackController"
            ? (bool)Invoke(controller, "TryFireShot", enemy, executor, settings)
            : (bool)Invoke(controller, "TryFire", enemy);
        Assert(fired, "Controller must spawn real ECS projectiles");
    }
    private static void VerifyFanAngles(World world, Vector3 forward, EnemyShootingSettings shooting)
    {
        using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
        using (var velocities = query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
        {
            Assert(velocities.Length == shooting.ProjectilesPerVolley, "Fan must use the current point projectile count");
            float[] actual = new float[velocities.Length];
            float[] expected = new float[velocities.Length];
            float min = (float)Get(shooting, "fanMinAngle");
            float max = (float)Get(shooting, "fanMaxAngle");
            for (int i = 0; i < velocities.Length; i++)
            {
                actual[i] = Vector3.SignedAngle(forward, new Vector3(velocities[i].Value.x, velocities[i].Value.y, 0f), Vector3.forward);
                int perArm = shooting.ProjectilesPerDirection;
                float arm = (i / perArm) * (360f / shooting.DirectionCount);
                float fan = perArm > 1 ? Mathf.Lerp(Mathf.Max(min, max), Mathf.Min(min, max), (i % perArm) / (float)(perArm - 1)) : 0f;
                expected[i] = Mathf.DeltaAngle(0f, arm + fan);
            }
            var matched = new bool[expected.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < expected.Length; j++)
                {
                    if (matched[j] || Mathf.Abs(Mathf.DeltaAngle(actual[i], expected[j])) >= .05f) continue;
                    matched[j] = true; found = true; break;
                }
                Assert(found, "Fan must respect the configured point angles and radial arms");
            }
        }
    }
    private static int Count(World world)
    {
        using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity))) return query.CalculateEntityCount();
    }
    private static void Clear(World world)
    {
        using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity))) world.EntityManager.DestroyEntity(query);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
}
