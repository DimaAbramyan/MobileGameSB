using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class VerifyEnemyShootingProfiles
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();

    public static object Run()
    {
        checks.Clear();
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle.");
        VerifyRotation();
        VerifyMigrationAndCopy();
        string[] prefabs = {
            "Assets/Prefub/Enemies Prefab/Enemy.prefab",
            "Assets/Prefub/Enemies Prefab/DoubleShooter.prefab",
            "Assets/Prefub/Enemies Prefab/FourWayEnemy.prefab",
            "Assets/Prefub/EnemyShipsLegasy/FourWayEnemy.prefab"
        };
        foreach (string path in prefabs)
            VerifyPrefab(path);
        return new { passed = checks.Count, checks };
    }

    private static void VerifyPrefab(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject clone = UnityEngine.Object.Instantiate(asset);
        GameObject waveObject = new GameObject("Attack verification wave");
        var world = new World("Isolated shooting verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world);
        Set(spawner, "entityManager", world.EntityManager);
        try
        {
            var shared = clone.GetComponent<SimpleEnemyAttackExecutor>();
            if (shared == null)
                throw new Exception(path + ": missing executor");
            Set(shared, "projectileSpawner", spawner);
            Invoke(shared, "Awake");
            var enemy = clone.GetComponent<Enemy>();
            var wave = waveObject.AddComponent<DirectedEnemySubWave>();
            var settings = new DirectedWaveAttackSettings();
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            settings.WaveBurstSettings.CopyFrom(Pattern(EnemyShootingType.Default, 1, EnemyProjectileFireMode.Default));
            settings.Validate();

            foreach (string controllerName in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                Type type = typeof(DirectedEnemySubWave).Assembly.GetType(controllerName, true);
                object[] constructorArgs = controllerName == "DirectedWaveAttackController"
                    ? new object[] { wave, settings, null }
                    : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() };
                object controller = Activator.CreateInstance(type, constructorArgs);
                object[] args = { enemy, null };
                if (!(bool)type.GetMethod("TryGetExecutor", Private).Invoke(controller, args))
                    throw new Exception(path + ": failed to acquire " + controllerName);
                var executor = (IWaveAttackExecutor)args[1];
                VerifyVolley(world, () => executor.TryFireAt(Vector3.right * 10f, settings.WaveBurstSettings), 1);
                var runtime = (EnemyAttackRotation)Get(shared, "rotation");
                Quaternion before = clone.transform.rotation;
                runtime.Tick(0.5f);
                Assert(Quaternion.Angle(before, clone.transform.rotation) < 0.01f, "Default override pauses rotation");
                type.GetMethod("Stop").Invoke(controller, null);
                checks.Add(path + ": " + controllerName + " Default = 1");
            }

            var radial = Pattern(EnemyShootingType.Rotation, 6, EnemyProjectileFireMode.Default);
            shared.SetWaveAttackControl(true);
            shared.SetWaveShootingSettings(radial.Shooting);
            VerifyVolley(world, () => shared.TryFireAt(Vector3.right * 10f, radial), 6);
            VerifyEvenDirections(world, 6);
            ClearProjectiles(world);
            radial.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise,
                FourWayEnemyRotationMode.Continuous, 180f, AnimationCurve.Linear(0, 0, 1, 1), 360f, 0f, true);
            shared.RestartRotation();
            Quaternion start = clone.transform.rotation;
            ((EnemyAttackRotation)Get(shared, "rotation")).Tick(0.5f);
            Assert(Mathf.Abs(Quaternion.Angle(start, clone.transform.rotation) - 90f) < 0.05f, "Wave rotation speed override");
            shared.PauseRotation();
            Quaternion paused = clone.transform.rotation;
            ((EnemyAttackRotation)Get(shared, "rotation")).Tick(0.5f);
            Assert(Quaternion.Angle(paused, clone.transform.rotation) < 0.01f, "Pause rotation");
            shared.ResumeRotation();
            ((EnemyAttackRotation)Get(shared, "rotation")).Tick(0.5f);
            Assert(Mathf.Abs(Quaternion.Angle(paused, clone.transform.rotation) - 90f) < 0.05f, "Resume rotation");
            checks.Add(path + ": Rotation = 6, speed / pause / resume");

            var pivotObject = new GameObject("Verification weapon pivot");
            pivotObject.transform.SetParent(clone.transform, false);
            pivotObject.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            shared.SetRotationTarget(pivotObject.transform);
            Quaternion shipBeforePivotRotation = clone.transform.rotation;
            ((EnemyAttackRotation)Get(shared, "rotation")).Tick(0.5f);
            Assert(Quaternion.Angle(shipBeforePivotRotation, clone.transform.rotation) < 0.01f, "Child pivot leaves ship rotation unchanged");
            VerifyVolley(world, () => shared.TryFireAt(Vector3.right * 10f, radial), 6);
            using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
            using (var v = q.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
                Assert(Vector3.Angle(pivotObject.transform.up, new Vector3(v[0].Value.x, v[0].Value.y, 0f)) < 0.01f, "Radial volley follows child pivot");
            ClearProjectiles(world);
            shared.SetRotationTarget(null);
            checks.Add(path + ": child pivot rotation and projectile basis");

            var fan = Pattern(EnemyShootingType.Rotation, 4, EnemyProjectileFireMode.Fan);
            VerifyVolley(world, () => shared.TryFireInDirection(Vector3.up, fan), 12);
            ClearProjectiles(world);
            var shotgun = Pattern(EnemyShootingType.Default, 1, EnemyProjectileFireMode.ShotGun);
            Set(shotgun.Shooting, "shotGunProjectileCount", 5);
            Set(shotgun.Shooting, "shotGunInitialSpeed", 7f);
            Set(shotgun.Shooting, "projectileSpeedBehavior", EnemyProjectileSpeedBehavior.Customized);
            shotgun.Validate();
            VerifyVolley(world, () => shared.TryFireInDirection(Vector3.up, shotgun), 5);
            using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity), typeof(EnemyProjectileCustomSpeed)))
            using (var velocities = query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
            {
                Assert(velocities.Length == 5, "ShotGun custom speed data");
                foreach (var v in velocities)
                    Assert(Mathf.Abs(math.length(v.Value) - 7f) < 0.001f, "ShotGun initial speed");
            }
            ClearProjectiles(world);
            shared.SetWaveAttackControl(false);
            Assert(Get(shared, "waveShootingSettings") == null, "Release restores local shooting profile");
            checks.Add(path + ": radial fan = 12, ShotGun = 5, custom speed, restore local profile");

            // Exercise the autonomous scheduler with two bursts of three single shots.
            var burst = Pattern(EnemyShootingType.Default, 1, EnemyProjectileFireMode.Default);
            Set(burst, "burstAttack", EnemyBurstAttackMode.Enabled);
            Set(burst, "attackShotCount", 2);
            Set(burst, "burstShotCount", 3);
            Set(burst, "attackShotInterval", 0.25f);
            Set(burst, "burstShotInterval", 0.1f);
            Set(burst, "attackCooldown", 1.6f);
            burst.Validate();
            shared.ApplyBurstAttackSettingsOverride(burst);
            Set(shared, "nextAttackTime", 0f);
            foreach (float t in new[] { 0f, 0.05f, 0.1f, 0.2f, 0.45f, 0.55f, 0.65f, 0.75f, 1f })
                Invoke(shared, "TickAutonomousAttack", t);
            Assert(Count(world) == 6, "Two bursts produce six shot events and respect cooldown");
            checks.Add(path + ": autonomous burst schedule = 6, cooldown respected");
        }
        finally
        {
            ClearProjectiles(world); // Entities must die before runtime Mesh / Material.
            // The gameplay spawner disposes with Destroy in Play Mode. These visuals
            // belong only to this Edit Mode test, so release them immediately.
            var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
            foreach (object visual in visuals.Values)
            {
                UnityEngine.Object.DestroyImmediate((Material)visual.GetType().GetField("Material").GetValue(visual));
                UnityEngine.Object.DestroyImmediate((Mesh)visual.GetType().GetField("Mesh").GetValue(visual));
            }
            visuals.Clear();
            spawner.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(waveObject);
        }
    }

    private static EnemyBurstAttackSettings Pattern(EnemyShootingType type, int directions, EnemyProjectileFireMode mode)
    {
        var pattern = new EnemyBurstAttackSettings();
        Set(pattern, "serializedVersion", 2);
        Set(pattern.Shooting, "shootingType", type);
        Set(pattern.Shooting, "directionCount", directions);
        Set(pattern.Shooting, "shootingMode", mode);
        pattern.Validate();
        return pattern;
    }

    private static void VerifyRotation()
    {
        var r = new EnemyRotationSettings();
        var linear = AnimationCurve.Linear(0, 0, 1, 1);
        r.Configure(FourWayEnemyRotationDirection.Clockwise, FourWayEnemyRotationMode.Continuous, 90f, linear, 360f, 0f, true);
        Assert(Mathf.Abs(r.EvaluateAngle(5f) + 450f) < 0.01f, "Continuous angle");
        r.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle, 90f, linear, 180f, 0f, true);
        Assert(Mathf.Abs(r.EvaluateAngle(1f) - 90f) < 0.01f && Mathf.Abs(r.EvaluateAngle(5f) - 180f) < 0.01f, "By Angle stops at target");
        r.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.PingPongByAngle, 90f, linear, 90f, -90f, true);
        Assert(Mathf.Abs(r.EvaluateAngle(1f) + 90f) < 0.01f, "Ping Pong initial pass");
        Assert(Mathf.Abs(r.EvaluateAngle(3f) - 90f) < 0.01f && Mathf.Abs(r.EvaluateAngle(5f) + 90f) < 0.01f, "Ping Pong bounds");
        var curved = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 0.25f), new Keyframe(1, 1));
        r.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.Continuous, 90f, curved, 360f, 0f, true);
        Assert(Mathf.Abs(r.EvaluateAngle(2f) - 90f) < 0.01f, "Rotation progress curve");
        var target = new GameObject("Rotation completion verification");
        try
        {
            target.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            r.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle, 90f, linear, 180f, 0f, true);
            var runtime = new EnemyAttackRotation();
            runtime.Bind(target.transform, r);
            runtime.Restart();
            runtime.Tick(5f);
            Assert(runtime.IsComplete && Quaternion.Angle(target.transform.localRotation, Quaternion.Euler(0, 0, 225f)) < 0.01f, "By Angle runtime completion");
            runtime.Restart();
            Assert(!runtime.IsComplete && Quaternion.Angle(target.transform.localRotation, Quaternion.Euler(0, 0, 45f)) < 0.01f, "Restart restores initial rotation");
            checks.Add("By Angle completion and restart preserve initial orientation");
        }
        finally { UnityEngine.Object.DestroyImmediate(target); }
        checks.Add("Continuous / By Angle / Ping Pong / direction / curve");
    }

    private static void VerifyMigrationAndCopy()
    {
        var legacy = new EnemyBurstAttackSettings();
        Set(legacy, "useAreaAttack", true);
        Set(legacy, "areaAttackProjectileCount", 5);
        Set(legacy, "areaAttackMinAngle", -30f);
        Set(legacy, "areaAttackMaxAngle", 30f);
        legacy.Validate();
        Assert(legacy.Shooting.ShootingMode == EnemyProjectileFireMode.Fan && legacy.ProjectilesPerShot == 5, "Old wave fan migration");
        var copy = new EnemyBurstAttackSettings();
        copy.CopyFrom(legacy);
        copy.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.Clockwise, FourWayEnemyRotationMode.Continuous,
            240f, AnimationCurve.Linear(0, 0, 1, 1), 360f, 0f, true);
        Assert(legacy.Shooting.Rotation.RotationSpeedDegreesPerSecond == 90f, "Independent profile copy");
        checks.Add("Legacy Area Attack migration and independent preset profile copy");
    }

    private static void VerifyVolley(World world, Func<bool> fire, int expected)
    {
        ClearProjectiles(world);
        Assert(fire(), "ECS spawn succeeded");
        Assert(Count(world) == expected, "Projectile count " + expected);
    }

    private static void VerifyEvenDirections(World world, int expected)
    {
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
        using (var data = q.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
        {
            for (int i = 0; i < data.Length; i++)
                for (int j = i + 1; j < data.Length; j++)
                    Assert(math.distance(math.normalize(data[i].Value), math.normalize(data[j].Value)) > 0.1f, "Distinct radial directions");
            float2 sum = float2.zero;
            foreach (var v in data)
                sum += math.normalize(v.Value);
            Assert(math.length(sum) < 0.001f && data.Length == expected, "Symmetric radial volley");
        }
    }

    private static int Count(World world)
    {
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
            return q.CalculateEntityCount();
    }
    private static void ClearProjectiles(World world)
    {
        if (!world.IsCreated) return;
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
            world.EntityManager.DestroyEntity(q);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Assert(bool condition, string label) { if (!condition) throw new Exception(label); }
}
