using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public static class VerifyMultipleOriginRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        checks.Clear();
        VerifyCopyAndSchedule();
        foreach (string path in new[] { "Assets/Prefub/Enemies Prefab/Enemy.prefab",
            "Assets/Prefub/Enemies Prefab/DoubleShooter.prefab", "Assets/Prefub/Enemies Prefab/FourWayEnemy.prefab",
            "Assets/Prefub/EnemyShipsLegasy/FourWayEnemy.prefab" })
            VerifyPrefab(path);
        return new { passed = checks.Count, checks };
    }

    private static EnemyBurstAttackSettings Pair()
    {
        var root = new EnemyBurstAttackSettings();
        root.ConfigureLegacySingleAttack(1, 1f, 0f);
        root.Shooting.ConfigureRotation(Rotation(90f), true);
        root.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 1);
        root.EnableMultiplePatterns();
        var second = new EnemyAttackPatternSettings();
        second.CopyFrom(root.GetPattern(0));
        second.Shooting.Rotation.CopyFrom(Rotation(-90f));
        root.AddPattern(second);
        root.Validate();
        return root;
    }

    private static EnemyRotationSettings Rotation(float angle)
    {
        var rotation = new EnemyRotationSettings();
        rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle,
            90f, AnimationCurve.Linear(0, 0, 1, 1), angle, 0f, true);
        return rotation;
    }

    private static void VerifyCopyAndSchedule()
    {
        Assert(new EnemyBurstAttackSettings().AttackPatterns == EnemyAttackPatternsMode.Single, "Default mode is Single");
        var source = Pair();
        var copy = new EnemyBurstAttackSettings();
        copy.CopyFrom(source);
        copy.GetPattern(1).Shooting.Rotation.CopyFrom(Rotation(-180f));
        Assert(source.GetPattern(1).Shooting.Rotation.RotationAngle == -90f, "Copy does not share rotation settings");
        source.SetProjectileTypeForAllPatterns(EnemyProjectileType.Homing);
        source.SetRotationForAllPatterns(Rotation(30f));
        copy.CopyFrom(source);
        for (int i = 0; i < copy.PatternCount; i++)
            Assert(copy.GetPattern(i).Shooting.ProjectileType == EnemyProjectileType.Homing
                && copy.GetPattern(i).Shooting.Rotation.RotationAngle == 30f,
                "Point projectile/rotation overrides survive copying and cache rebuild");
        checks.Add("Single default, deep copy, projectile and rotation overrides on every list entry");

        var burst = new EnemyBurstAttackSettings();
        burst.ConfigureLegacyRepeatedBurst(3, 1f, .1f);
        Set(burst, "attackShotCount", 1); // FireRate 2 produces two bursts.
        Set(burst, "attackShotInterval", .25f);
        Set(burst, "useAttackStartDelay", true);
        Set(burst, "attackStartDelay", .4f);
        burst.Validate();
        var schedule = new EnemyAttackPatternSchedule();
        schedule.Begin(burst, 0f, 2f, true);
        Assert(!schedule.IsDue(.199f), "Per-pattern start delay respects FireRate");
        float[] times = { .2f, .3001f, .4002f, .6503f, .7504f, .8505f };
        for (int i = 0; i < times.Length; i++)
        {
            Assert(schedule.IsDue(times[i]), "Burst shot due " + i);
            schedule.Advance(burst, times[i], 2f);
        }
        Assert(schedule.IsComplete && Mathf.Abs(schedule.NextAttackTime - 1.3505f) < .0001f,
            "Six shots and scaled cooldown complete independently");
        checks.Add("Independent burst timing, start delay, FireRate and cooldown");
    }

    private static void VerifyPrefab(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var clone = UnityEngine.Object.Instantiate(asset);
        var waveObject = new GameObject("Multiple attack verification wave");
        var world = new World("Multiple attack verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world);
        Set(spawner, "entityManager", world.EntityManager);
        try
        {
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "projectileSpawner", spawner);
            Invoke(executor, "Awake");
            Set(executor, "autonomousAttack", false);
            var enemy = clone.GetComponent<Enemy>();
            var wave = waveObject.AddComponent<DirectedEnemySubWave>();
            Quaternion initial = clone.transform.rotation;
            var settings = new DirectedWaveAttackSettings();
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.PreserveLegacyForward();
            settings.WaveBurstSettings.CopyFrom(Pair());
            settings.Validate();

            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings);
                var active = Acquire(controller, enemy);
                Assert(ReferenceEquals(Get(executor, "waveAttackSettings"), settings.WaveBurstSettings), "Wave supplies full collection");
                Set(executor, "patternRotationTime", .5f);
                if (name == "DirectedWaveAttackController")
                    Assert((bool)Invoke(controller, "TryFireShot", enemy, active, settings.WaveBurstSettings), "Formation fires");
                else
                    Assert((bool)Invoke(controller, "TryFire", enemy), "Entrance fires");
                VerifyOppositeDirections(world, clone.transform.rotation);
                Invoke(executor, "Update");
                Assert(Quaternion.Angle(initial, clone.transform.rotation) < .001f, "Multiple never rotates ship");
                controller.GetType().GetMethod("Stop").Invoke(controller, null);
                Assert(Get(executor, "waveAttackSettings") == null, "Release removes entire wave collection");
                Clear(world);
                checks.Add(path + ": " + name + " opposite streams, fixed ship, release");
            }

            // Different shapes and shot counts must all finish inside one wave attack.
            var full = Pair();
            var entries = (List<EnemyAttackPatternSettings>)Get(full, "patterns");
            Set(entries[0], "attackShotCount", 2);
            Set(entries[0], "attackShotInterval", 0f);
            entries[1].Shooting.ConfigureLegacyFan(3, -15f, 15f);
            Set(entries[1], "burstAttack", EnemyBurstAttackMode.Enabled);
            Set(entries[1], "burstShotCount", 2);
            Set(entries[1], "burstShotInterval", 0f);
            Set(entries[1], "attackCooldown", 2f);
            full.Validate();
            settings.WaveBurstSettings.CopyFrom(full);
            object formation = Controller("DirectedWaveAttackController", wave, settings);
            var activeFormation = Acquire(formation, enemy);
            Set(executor, "patternRotationTime", .5f);
            Drain((IEnumerator)Invoke(formation, "FireFullAttack", enemy, activeFormation, settings.WaveBurstSettings));
            Assert(Count(world) == 8, "Parallel full attack emits 2 + 2*3 projectiles");
            var ready = (Dictionary<Enemy, float>)Get(formation, "nextAttackReadyTimes");
            Assert(ready[enemy] >= Time.time + 1.99f, "Collection cooldown includes slowest pattern");
            formation.GetType().GetMethod("Stop").Invoke(formation, null);
            Clear(world);
            checks.Add(path + ": full parallel wave attack = 8, all patterns finish, cooldown respected");

            var timed = Pair();
            entries = (List<EnemyAttackPatternSettings>)Get(timed, "patterns");
            Set(entries[0], "attackShotCount", 2);
            Set(entries[0], "attackShotInterval", .2f);
            Set(entries[1], "useAttackStartDelay", true);
            Set(entries[1], "attackStartDelay", .1f);
            Set(entries[1], "attackCooldown", .3f);
            timed.Validate();
            executor.ApplyBurstAttackSettingsOverride(timed);
            Set(executor, "patternRotationTime", .5f);
            float start = Time.time;
            float[] stepTimes = { 0f, .1001f, .2002f, .4003f, .8004f, 1.2105f };
            int[] expectedCounts = { 1, 2, 3, 4, 5, 7 };
            for (int i = 0; i < stepTimes.Length; i++)
            {
                Invoke(executor, "TickAutonomousAttack", start + stepTimes[i]);
                Assert(Count(world) == expectedCounts[i], "Independent autonomous schedules at " + stepTimes[i]);
            }
            Clear(world);
            checks.Add(path + ": autonomous patterns keep separate delay / intervals / cooldowns");

            settings.WaveBurstSettings.CopyFrom(new EnemyBurstAttackSettings());
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings);
                var active = Acquire(controller, enemy);
                var resolved = (EnemyBurstAttackSettings)Invoke(controller, "GetBurstSettings", enemy, active);
                Assert(ReferenceEquals(resolved, executor.BurstAttackSettings) && resolved.HasMultiplePatterns,
                    "Explicit point collection takes priority over shared Single");
                controller.GetType().GetMethod("Stop").Invoke(controller, null);
            }
            checks.Add(path + ": Multiple point override wins over Single Wave Override");

            VerifyInspectorSerialization(executor);
            checks.Add(path + ": Inspector initialize / duplicate / reorder / remove, serialization and defaults");
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
            UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(waveObject);
            world.Dispose();
        }
    }

    private static void VerifyInspectorSerialization(SimpleEnemyAttackExecutor executor)
    {
        var single = Pair().GetPattern(0);
        executor.ApplyBurstAttackSettingsOverride(single);
        var serialized = new SerializedObject(executor);
        var root = serialized.FindProperty("burstAttackSettings");
        root.FindPropertyRelative("attackPatterns").enumValueIndex = (int)EnemyAttackPatternsMode.Multiple;
        EnemyBurstAttackSettingsDrawer.InitializeMultiple(root);
        var list = root.FindPropertyRelative("patterns");
        EnemyBurstAttackSettingsDrawer.Duplicate(list, 0);
        list.GetArrayElementAtIndex(1).FindPropertyRelative("shooting").FindPropertyRelative("rotation")
            .FindPropertyRelative("rotationAngle").floatValue = -90f;
        serialized.ApplyModifiedProperties();
        Assert(executor.BurstAttackSettings.PatternCount == 2, "Inspector list has two patterns");
        Assert(executor.BurstAttackSettings.GetPattern(0).Shooting.Rotation.RotationAngle == 90f
            && executor.BurstAttackSettings.GetPattern(1).Shooting.Rotation.RotationAngle == -90f,
            "Inspector duplication produces independent entries");
        serialized.Update(); root = serialized.FindProperty("burstAttackSettings"); list = root.FindPropertyRelative("patterns");
        float height = new EnemyBurstAttackSettingsDrawer().GetPropertyHeight(root, new GUIContent("Attack"));
        Assert(height > 200f, "Inspector reserves space for all expanded fields");
        list.MoveArrayElement(1, 0);
        serialized.ApplyModifiedProperties();
        Assert(executor.BurstAttackSettings.GetPattern(0).Shooting.Rotation.RotationAngle == -90f, "Reorder retains values");
        string json = EditorJsonUtility.ToJson(executor);
        EditorJsonUtility.FromJsonOverwrite(json, executor);
        Assert(executor.BurstAttackSettings.PatternCount == 2 && executor.BurstAttackSettings.GetPattern(0).Shooting.Rotation.RotationAngle == -90f,
            "Editor serialization round trip preserves list and nested rotation");
        serialized.Update(); root = serialized.FindProperty("burstAttackSettings"); list = root.FindPropertyRelative("patterns");
        list.DeleteArrayElementAtIndex(1); serialized.ApplyModifiedProperties();
        Assert(executor.BurstAttackSettings.PatternCount == 1, "Remove one list entry");
        serialized.Update(); root = serialized.FindProperty("burstAttackSettings"); root.FindPropertyRelative("patterns").ClearArray();
        serialized.ApplyModifiedProperties();
        Assert(!executor.CanPerformWaveAttack, "Empty list is not silently replaced with a default shot");
    }

    private static void VerifyOppositeDirections(World world, Quaternion basis)
    {
        Assert(Count(world) == 2, "Two simultaneous streams");
        using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
        using (var data = query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
        {
            var expected = new[] { basis * Quaternion.Euler(0, 0, 45) * Vector3.up,
                basis * Quaternion.Euler(0, 0, -45) * Vector3.up };
            for (int i = 0; i < 2; i++)
            {
                var actual = new Vector3(data[i].Value.x, data[i].Value.y, 0f);
                Assert(Vector3.Angle(actual, expected[i]) < .05f, "Negative angle reverses firing rotation and matches preview");
            }
        }
    }

    private static object Controller(string name, DirectedEnemySubWave wave, DirectedWaveAttackSettings settings)
    {
        var type = typeof(DirectedEnemySubWave).Assembly.GetType(name, true);
        return Activator.CreateInstance(type, name == "DirectedWaveAttackController"
            ? new object[] { wave, settings, null } : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
    }
    private static IWaveAttackExecutor Acquire(object controller, Enemy enemy)
    {
        object[] args = { enemy, null };
        Assert((bool)controller.GetType().GetMethod("TryGetExecutor", Private).Invoke(controller, args), "Acquire executor");
        return (IWaveAttackExecutor)args[1];
    }
    private static void Drain(IEnumerator routine)
    {
        int iterations = 0;
        while (routine.MoveNext())
        {
            if (++iterations > 100) throw new Exception("Unexpected unbounded coroutine");
            if (routine.Current is IEnumerator nested) Drain(nested);
            else throw new Exception("Immediate wave test must not wait for time");
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
    private static void Assert(bool condition, string label) { if (!condition) throw new Exception(label); }
}
