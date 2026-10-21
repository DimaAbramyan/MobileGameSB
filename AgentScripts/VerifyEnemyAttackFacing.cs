using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyEnemyAttackFacing
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();

    private sealed class MovingTarget : IEnemyAttackAimTarget
    {
        public Vector3 Position;
        public Vector3 AttackTargetPosition => Position;
    }

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle.");
        checks.Clear();
        VerifyIdentity();
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
        Set(root.Shooting, "directionCount", 1);
        root.EnableMultiplePatterns();
        var second = new EnemyAttackPatternSettings();
        second.CopyFrom(root.GetPattern(0));
        second.Shooting.Rotation.CopyFrom(Rotation(-90f));
        root.AddPattern(second);
        root.SetFacing(EnemyFacingMode.FollowPattern, 0, 0f);
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

    private static void VerifyIdentity()
    {
        var root = Pair();
        var entries = (List<EnemyAttackPatternSettings>)Get(root, "patterns");
        string selected = entries[0].PatternId;
        var copy = new EnemyBurstAttackSettings(); copy.CopyFrom(root);
        Assert(copy.FacingPatternIndex == 0 && ((List<EnemyAttackPatternSettings>)Get(copy, "patterns"))[0].PatternId == selected,
            "Copy preserves selection identity");
        entries.Reverse(); root.Validate();
        Assert(root.FacingPatternIndex == 1, "Reordering preserves selected pattern");
        root.AddPattern(entries[1]);
        Assert(entries[2].PatternId != selected, "Duplicating produces a new identity");
        entries.RemoveAt(1); root.Validate();
        Assert(root.FacingPatternIndex == -1, "Removing selected pattern is explicitly invalid");
        Assert(new EnemyBurstAttackSettings().FacingMode == EnemyFacingMode.Default, "Default retains legacy behavior");
        checks.Add("Stable selection across copy/reorder; duplicates independent; deleted selection invalid; legacy default");
    }

    private static void VerifyPrefab(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert(asset != null, "Prefab exists: " + path);
        var clone = UnityEngine.Object.Instantiate(asset);
        var waveObject = new GameObject("Facing verification wave");
        var parent = new GameObject("Facing verification parent");
        var playerObject = new GameObject("Facing verification target");
        var visual = new GameObject("Facing verification visual");
        visual.transform.SetParent(clone.transform, false);
        var muzzle = new GameObject("Facing verification muzzle");
        muzzle.transform.SetParent(visual.transform, false);
        muzzle.transform.localPosition = new Vector3(.4f, .7f, 0f);
        var world = new World("Facing verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "projectileSpawner", spawner); Invoke(executor, "Awake");
            Set(executor, "autonomousAttack", false);
            Set(executor, "projectileSpawnPoint", muzzle.transform);
            Quaternion initial = clone.transform.rotation;
            executor.ApplyBurstAttackSettingsOverride(Pair());
            Set(executor, "patternRotationTime", .5f);
            Assert(executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings), "Fire pair");
            VerifyPair(world, initial, 45f); Vector3 spawnBefore = FirstPosition(world); Clear(world);
            Invoke(executor, "TickFacing", .5f);
            Assert(Quaternion.Angle(clone.transform.rotation, initial * Quaternion.Euler(0, 0, 45)) < .05f, "Body follows first pattern");
            for (int i = 0; i < 20; i++)
            {
                Assert(executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings), "Repeat pair");
                VerifyPair(world, initial, 45f);
                Assert(Vector3.Distance(spawnBefore, FirstPosition(world)) < .0001f, "Muzzle does not feed visual rotation back into spawn position");
                Clear(world); Invoke(executor, "TickFacing", .1f);
            }
            Set(executor, "patternRotationTime", .75f);
            Invoke(executor, "TickFacing", .25f);
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings);
            VerifyPair(world, initial, 67.5f); Clear(world);
            checks.Add(path + ": selected body rotation; 20 ECS volleys and nested muzzle independent; opposite stream remains independent");

            var second = Pair(); second.SetFacing(EnemyFacingMode.FollowPattern, 1, 0f, 25f);
            executor.ApplyBurstAttackSettingsOverride(second); Set(executor, "patternRotationTime", .5f);
            Invoke(executor, "TickFacing", .5f);
            Assert(Quaternion.Angle(clone.transform.rotation, initial * Quaternion.Euler(0, 0, -20)) < .05f, "Selected second pattern and visual-only offset");
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings); VerifyPair(world, initial, 45f); Clear(world);
            checks.Add(path + ": second pattern selection and angle offset leave ECS firing unchanged");

            var slow = Pair(); slow.SetFacing(EnemyFacingMode.FollowPattern, 0, 30f);
            executor.ApplyBurstAttackSettingsOverride(slow); Set(executor, "patternRotationTime", .5f);
            Invoke(executor, "TickFacing", .5f);
            Quaternion expectedSlow = initial * Quaternion.Euler(0, 0, 15);
            Assert(Quaternion.Angle(clone.transform.rotation, expectedSlow) < .05f, "30 degrees/s visual speed");
            Assert(Quaternion.Angle(EnemyAttackFacing.Preview(slow, initial, initial, .5f), expectedSlow) < .05f, "Preview uses same angular rule");
            executor.PauseRotation(); float paused = (float)Get(executor, "patternRotationTime"); Invoke(executor, "Update");
            Assert(Mathf.Approximately(paused, (float)Get(executor, "patternRotationTime")), "Virtual rotation pauses");
            executor.ResumeRotation(); Assert((bool)Get(executor, "patternRotationRunning"), "Virtual rotation resumes");
            checks.Add(path + ": visual speed, shared preview and virtual pause/resume");

            Set(executor, "facingTarget", visual.transform);
            executor.ApplyBurstAttackSettingsOverride(Pair()); Set(executor, "patternRotationTime", .5f);
            Invoke(executor, "TickFacing", .5f);
            Assert(Quaternion.Angle(clone.transform.rotation, initial) < .05f, "Visual child leaves root/hitbox fixed");
            Assert(Quaternion.Angle(visual.transform.rotation, initial * Quaternion.Euler(0, 0, 45)) < .05f, "Visual child follows selected pattern");
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings); VerifyPair(world, initial, 45f); Clear(world);
            Set(executor, "facingTarget", null);
            checks.Add(path + ": optional visible child turns while enemy root remains fixed");

            var fixedSingle = Pair().GetPattern(0); fixedSingle.SetFacing(EnemyFacingMode.Fixed);
            executor.ApplyBurstAttackSettingsOverride(fixedSingle); Set(executor, "patternRotationTime", .5f);
            Invoke(executor, "TickFacing", .5f);
            Assert(Quaternion.Angle(clone.transform.rotation, initial) < .05f, "Fixed Single leaves body fixed");
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings);
            VerifyDirections(world, new[] { initial * Quaternion.Euler(0, 0, 45) * Vector3.up }); Clear(world);
            var aim = new EnemyBurstAttackSettings(); aim.ConfigureLegacySingleAttack(1, 1f, 0f);
            aim.Shooting.ConfigureLegacyFan(3, 0f, 60f); aim.SetFacing(EnemyFacingMode.FollowPattern, 0, 0f);
            executor.ApplyBurstAttackSettingsOverride(aim);
            Vector3 origin = (Vector3)Invoke(executor, "GetProjectileSpawnPosition", executor.BurstAttackSettings);
            executor.TryFireAt(origin + initial * Vector3.up * 10f, executor.BurstAttackSettings);
            Invoke(executor, "TickFacing", .1f);
            Assert(Quaternion.Angle(clone.transform.rotation, initial * Quaternion.Euler(0, 0, 30)) < .05f, "Body faces center of asymmetric fan");
            VerifyDirections(world, new[] { initial * Quaternion.Euler(0, 0, 60) * Vector3.up,
                initial * Quaternion.Euler(0, 0, 30) * Vector3.up, initial * Vector3.up }); Clear(world);
            var target = new MovingTarget { Position = clone.transform.position + Vector3.right * 10f };
            ((IEnemyAttackAimReceiver)executor).SetWaveAimTarget(target); Invoke(executor, "TickFacing", .1f);
            Vector3 direction = target.Position - (Vector3)Invoke(executor, "GetProjectileSpawnPosition", executor.BurstAttackSettings);
            Assert(Quaternion.Angle(clone.transform.rotation, EnemyAttackFacing.TargetRotation(aim.Shooting.GetFacingDirection(direction), 0f)) < .05f,
                "Wave target tracks before next shot");
            target.Position = clone.transform.position + Vector3.left * 10f; Invoke(executor, "TickFacing", .1f);
            direction = target.Position - (Vector3)Invoke(executor, "GetProjectileSpawnPosition", executor.BurstAttackSettings);
            Assert(Quaternion.Angle(clone.transform.rotation, EnemyAttackFacing.TargetRotation(aim.Shooting.GetFacingDirection(direction), 0f)) < .05f,
                "Moving target tracked between shots");
            ((IEnemyAttackAimReceiver)executor).SetWaveAimTarget(null);
            checks.Add(path + ": Fixed Single rotates only bullets; fan-center facing and moving wave aim target");

            clone.transform.SetParent(parent.transform, true);
            parent.transform.rotation = Quaternion.Euler(0, 0, 20);
            clone.transform.localScale = new Vector3(1.3f, .8f, 1);
            executor.ApplyBurstAttackSettingsOverride(Pair()); Set(executor, "patternRotationTime", .5f);
            Quaternion parentBasis = clone.transform.rotation;
            parent.transform.rotation = Quaternion.Euler(0, 0, 50);
            Quaternion movedBasis = Quaternion.Euler(0, 0, 30) * parentBasis;
            Invoke(executor, "TickFacing", .5f);
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings);
            VerifyPair(world, movedBasis, 45f); Clear(world);
            clone.transform.position += new Vector3(3f, -2f, 0);
            executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings); Vector3 afterMove = FirstPosition(world); Clear(world);
            Invoke(executor, "TickFacing", .2f); executor.TryFireInDirection(executor.AttackForward, executor.BurstAttackSettings);
            Assert(Vector3.Distance(afterMove, FirstPosition(world)) < .0001f, "Facing independent after movement and scaling"); Clear(world);
            Invoke(executor, "OnDisable");
            clone.transform.SetParent(null, true); clone.transform.rotation = initial; clone.transform.localScale = Vector3.one;
            Invoke(executor, "Awake");
            checks.Add(path + ": parent motion, enemy translation and scale preserve independent basis/spawn positions");

            var wave = waveObject.AddComponent<DirectedEnemySubWave>();
            var settings = new DirectedWaveAttackSettings(); Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
            settings.WaveBurstSettings.CopyFrom(Pair()); settings.Validate();
            // A plain forward second stream exposes feedback from body rotation immediately.
            var entries = (List<EnemyAttackPatternSettings>)Get(settings.WaveBurstSettings, "patterns");
            Set(entries[1].Shooting, "shootingType", EnemyShootingType.Default); settings.Validate();
            executor.ApplyBurstAttackSettingsOverride(new EnemyBurstAttackSettings()); Set(executor, "<HasAttackSettingsOverride>k__BackingField", false);
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings); object[] args = { clone.GetComponent<Enemy>(), null };
                Assert((bool)Invoke(controller, "TryGetExecutor", args), "Acquire wave executor");
                Set(executor, "patternRotationTime", .5f); Invoke(executor, "TickFacing", .5f);
                if (name == "DirectedWaveAttackController")
                    Assert((bool)Invoke(controller, "TryFireShot", clone.GetComponent<Enemy>(), args[1], settings.WaveBurstSettings), "Wave shot");
                else Assert((bool)Invoke(controller, "TryFire", clone.GetComponent<Enemy>()), "Entrance shot");
                VerifyDirections(world, new[] { initial * Quaternion.Euler(0, 0, 45) * Vector3.up, initial * Vector3.up }); Clear(world);
                controller.GetType().GetMethod("Stop").Invoke(controller, null);
                Assert(Get(executor, "waveAimTarget") == null && Quaternion.Angle(clone.transform.rotation, initial) < .05f,
                    "Releasing wave cleans aim and restores facing pose");
                checks.Add(path + ": " + name + " forward stream independent of body; wave facing release cleans up");
            }
            var player = playerObject.AddComponent<PlayerController>();
            Set(wave, "playerController", player);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
            var waveAim = new EnemyBurstAttackSettings(); waveAim.ConfigureLegacySingleAttack(1, 1f, 0f);
            waveAim.SetFacing(EnemyFacingMode.FollowPattern, 0, 0f);
            settings.WaveBurstSettings.CopyFrom(waveAim); settings.Validate();
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings); object[] args = { clone.GetComponent<Enemy>(), null };
                Assert((bool)Invoke(controller, "TryGetExecutor", args), "Acquire aimed wave executor");
                Assert(ReferenceEquals(Get(executor, "waveAimTarget"), wave), "Controller connects live wave aim source");
                foreach (Vector3 offset in new[] { Vector3.right * 10f, Vector3.left * 10f })
                {
                    playerObject.transform.position = clone.transform.position + offset;
                    Invoke(executor, "TickFacing", .1f);
                    Vector3 expectedDirection = playerObject.transform.position
                        - (Vector3)Invoke(executor, "GetProjectileSpawnPosition", settings.WaveBurstSettings);
                    Assert(Quaternion.Angle(clone.transform.rotation, EnemyAttackFacing.TargetRotation(expectedDirection, 0f)) < .05f,
                        "Aimed wave tracks moving player before firing");
                }
                controller.GetType().GetMethod("Stop").Invoke(controller, null);
                Assert(Get(executor, "waveAimTarget") == null, "Aimed wave release clears target");
                checks.Add(path + ": " + name + " live player aim before/between shots, release cleanup");
            }
            Set(settings, "fireMode", DirectedWaveAttackFireMode.ForwardWhenPlayerAhead);
            settings.WaveBurstSettings.CopyFrom(Pair());
            entries = (List<EnemyAttackPatternSettings>)Get(settings.WaveBurstSettings, "patterns");
            Set(entries[1].Shooting, "shootingType", EnemyShootingType.Default); settings.Validate();
            playerObject.transform.position = clone.transform.position + initial * Vector3.up * 10f;
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings); object[] args = { clone.GetComponent<Enemy>(), null };
                Assert((bool)Invoke(controller, "TryGetExecutor", args), "Acquire sector wave executor");
                Set(executor, "patternRotationTime", 1f); Invoke(executor, "TickFacing", .1f);
                if (name == "DirectedWaveAttackController")
                    Assert((bool)Invoke(controller, "TryFireShot", clone.GetComponent<Enemy>(), args[1], settings.WaveBurstSettings), "Forward sector uses independent basis");
                else Assert((bool)Invoke(controller, "TryFire", clone.GetComponent<Enemy>()), "Entrance sector uses independent basis");
                VerifyDirections(world, new[] { initial * Quaternion.Euler(0, 0, 90) * Vector3.up, initial * Vector3.up }); Clear(world);
                controller.GetType().GetMethod("Stop").Invoke(controller, null);
                checks.Add(path + ": " + name + " forward fire sector independent of visual 90 degree turn");
            }
            VerifySerialization(executor);
            checks.Add(path + ": Inspector fields, stable selection reorder, deep copy and JSON roundtrip");
        }
        finally
        {
            Clear(world);
            var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
            foreach (object v in visuals.Values)
            {
                UnityEngine.Object.DestroyImmediate((Material)v.GetType().GetField("Material").GetValue(v));
                UnityEngine.Object.DestroyImmediate((Mesh)v.GetType().GetField("Mesh").GetValue(v));
            }
            visuals.Clear(); spawner.Dispose();
            UnityEngine.Object.DestroyImmediate(clone); UnityEngine.Object.DestroyImmediate(waveObject);
            UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(playerObject); world.Dispose();
        }
    }

    private static void VerifySerialization(SimpleEnemyAttackExecutor executor)
    {
        executor.ApplyBurstAttackSettingsOverride(Pair());
        var so = new SerializedObject(executor); var root = so.FindProperty("burstAttackSettings");
        Assert(root.FindPropertyRelative("facingMode") != null && root.FindPropertyRelative("facingSpeed") != null
            && root.FindPropertyRelative("facingAngleOffset") != null, "Facing fields exist");
        string chosen = root.FindPropertyRelative("facingPatternId").stringValue;
        EnemyBurstAttackSettingsDrawer.Duplicate(root.FindPropertyRelative("patterns"), 0);
        Assert(root.FindPropertyRelative("patterns").GetArrayElementAtIndex(1).FindPropertyRelative("patternId").stringValue != chosen,
            "Inspector duplication creates independent identity");
        root.FindPropertyRelative("patterns").MoveArrayElement(0, 2); so.ApplyModifiedProperties();
        Assert(executor.BurstAttackSettings.FacingPatternIndex == 2, "Inspector reorder preserves facing selection");
        var copied = new EnemyBurstAttackSettings(); copied.CopyFrom(executor.BurstAttackSettings);
        Assert(copied.FacingPatternIndex == 2 && copied.FacingSpeed == 0f, "Point/wave profile copies facing values and selection");
        string json = EditorJsonUtility.ToJson(executor); EditorJsonUtility.FromJsonOverwrite(json, executor);
        Assert(executor.BurstAttackSettings.FacingPatternIndex == 2, "Serialized roundtrip preserves chosen identity");
        so.Update(); root = so.FindProperty("burstAttackSettings");
        float followHeight = new EnemyBurstAttackSettingsDrawer().GetPropertyHeight(root, GUIContent.none);
        root.FindPropertyRelative("facingMode").enumValueIndex = (int)EnemyFacingMode.Fixed;
        Assert(new EnemyBurstAttackSettingsDrawer().GetPropertyHeight(root, GUIContent.none) < followHeight, "Inspector reserves space for follow fields");
    }

    private static object Controller(string name, DirectedEnemySubWave wave, DirectedWaveAttackSettings settings)
    {
        Type type = typeof(DirectedEnemySubWave).Assembly.GetType(name, true);
        return Activator.CreateInstance(type, name == "DirectedWaveAttackController" ? new object[] { wave, settings, null }
            : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
    }
    private static void VerifyPair(World world, Quaternion basis, float angle) => VerifyDirections(world,
        new[] { basis * Quaternion.Euler(0, 0, angle) * Vector3.up, basis * Quaternion.Euler(0, 0, -angle) * Vector3.up });
    private static void VerifyDirections(World world, Vector3[] expected)
    {
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
        using (var v = q.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
        {
            Assert(v.Length == expected.Length, "ECS projectile count");
            for (int i = 0; i < expected.Length; i++)
                Assert(Vector3.Angle(new Vector3(v[i].Value.x, v[i].Value.y, 0), expected[i]) < .05f,
                    "ECS direction " + i + " does not include facing feedback");
        }
    }
    private static Vector3 FirstPosition(World world)
    {
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity), typeof(LocalTransform)))
        using (var v = q.ToComponentDataArray<LocalTransform>(Allocator.Temp)) return (Vector3)v[0].Position;
    }
    private static void Clear(World world)
    {
        using (var q = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity))) world.EntityManager.DestroyEntity(q);
    }
    private static void Set(object o, string n, object value) => o.GetType().GetField(n, Private).SetValue(o, value);
    private static object Get(object o, string n) => o.GetType().GetField(n, Private).GetValue(o);
    private static object Invoke(object o, string n, params object[] args) => o.GetType().GetMethod(n, Private).Invoke(o, args);
    private static void Assert(bool ok, string label) { if (!ok) throw new Exception(label); }
}
