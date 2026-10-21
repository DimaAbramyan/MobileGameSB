using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyPreviewRotationPriority
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        const string path = "Assets/Waves/1/1_Extreme/1_4_extrime.prefab";
        string before = Convert.ToBase64String(File.ReadAllBytes(path));
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var wave = stage != null && stage.assetPath == path ? stage.prefabContentsRoot.GetComponent<Wave>()
            : AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Wave>();
        DirectedEnemySubWave sub = null;
        foreach (var item in wave.GetComponentsInChildren<DirectedEnemySubWave>(true)) if (item.name == "FourWay") { sub = item; break; }
        Check(sub != null, "Actual FourWay group found");
        string authored = EditorJsonUtility.ToJson(sub) + EditorJsonUtility.ToJson(sub.AttackPatternBehaviour);
        bool dirty = wave.gameObject.scene.isDirty;
        var local = sub.GetConfiguredEnemyOverrideForSlot(0); string localBefore = EditorJsonUtility.ToJson(local);
        var settings = new DirectedWaveAttackSettings(); sub.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(0, false, settings, out _);
        var scene = EditorSceneManager.NewPreviewScene(); var clone = Object.Instantiate(sub.GetConfiguredEnemyPrefabForSlot(0).gameObject);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
        float nativeSpeed = 0, previewSpeed = 0, legacySpeed = 0, clock = 0;
        try
        {
            using var native = new EnemyProjectilePreviewWorld();
            var enemy = clone.GetComponent<Enemy>(); var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
            Call(executor, "Awake"); Set(executor, "projectileSpawner", Get(native, "spawner")); Set(executor, "autonomousAttack", false);
            // Actual spawn override followed by the runtime wave resolver and receiver.
            local.ApplyTo(enemy); var receiver = (IWaveAttackExecutor)clone.GetComponent<FourWayEnemy>();
            var profile = EnemyAttackSettingsResolver.ResolveForWave(enemy, receiver, settings, local,
                new Dictionary<Enemy, EnemyBurstAttackSettings>(), out bool bind);
            ((IEnemyAttackPatternSettingsReceiver)receiver).SetWaveAttackSettings(bind ? profile : null); receiver.SetWaveAttackControl(true);
            using var preview = new WaveShootingPreviewSession(sub, true, false, 0, new Vector3(100, 100));
            var slot = ((IList)Get(preview, "slots"))[0];
            var previewProfile = (EnemyBurstAttackSettings)Get(slot, "Profile");
            var rotationOverride = (EnemyRotationSettings)Get(slot, "RotationOverride");
            nativeSpeed = profile.Shooting.Rotation.RotationSpeedDegreesPerSecond;
            legacySpeed = local.RotationOverride.RotationSpeedDegreesPerSecond;
            previewSpeed = (rotationOverride ?? previewProfile.Shooting.Rotation).RotationSpeedDegreesPerSecond;
            Check(Mathf.Abs(nativeSpeed - 8.3f) < .001f && Mathf.Abs(legacySpeed - 22.5f) < .001f, "Authored conflicting speeds reproduced");
            Check(previewSpeed == nativeSpeed, "Preview must use active Wave Pattern speed (8.3), not legacy base rotation (22.5)");
            Check(rotationOverride == null, "Wave Override has no extra legacy rotation layer");
            var pattern = ((Array)Get(slot, "Patterns")).GetValue(0);
            var basis = (Quaternion)Get(pattern, "Basis");
            // Drive the real Runtime Update clock; never assign patternRotationTime.
            for (int frame = 0; frame < 20000 && clock < 7.5f; frame++)
            {
                float previous = (float)Get(executor, "patternRotationTime");
                float delta = (float)executor.GetType().GetProperty("PatternRotationDeltaTime", Flags).GetValue(executor);
                Call(executor, "Update"); clock = (float)Get(executor, "patternRotationTime");
                Check(Mathf.Abs(clock - previous - delta) < .00001f, "Actual runtime rotation clock advances by its effective delta");
                if (frame % 50 != 0) continue;
                executor.BeginAttackSequence(profile);
                var stationary = local.ResolveStationaryFireSettings(settings.StationaryFireSettings);
                Check(executor.TryFireStationary(stationary.Direction, false, profile), "Runtime FourWay emits actual projectiles");
                using var query = native.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                using var values = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
                var shooting = previewProfile.Shooting;
                Vector3 requested = stationary.Direction;
                Vector3 direction = shooting.GetEmissionDirection(0, basis, clock, requested, false, rotationOverride, true);
                Vector3 expected = shooting.GetProjectileDirection(direction, 0, 0, 0);
                Vector3 actual = new(values[0].ForwardDirection.x, values[0].ForwardDirection.y);
                Check((expected.normalized - actual).sqrMagnitude < .000001f, "Actual runtime and Preview agree at the same evolved rotation time");
                executor.EndAttackSequence(profile); native.SimulationWorld.EntityManager.DestroyEntity(query);
            }
            Check(clock >= 7.5f, "Runtime clock progressed through a full PingPong cycle and return");
            var enemySettings = new DirectedWaveAttackSettings(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings), enemySettings);
            Set(enemySettings, "burstSettingsSource", DirectedWaveBurstSettingsSource.EnemySettings);
            var inherited = Call(preview, "BuildSlot", 0, false, null, false, enemySettings, null, 1f);
            Check(((EnemyRotationSettings)Get(inherited, "RotationOverride")).RotationSpeedDegreesPerSecond == legacySpeed,
                "Enemy Settings retains the legacy base override");
            var sequence = Call(preview, "BuildSlot", 0, false, null, false, settings, null, 2f);
            Check(Get(sequence, "RotationOverride") == null && Mathf.Abs(((EnemyBurstAttackSettings)Get(sequence, "Profile")).Shooting.Rotation.RotationSpeedDegreesPerSecond - nativeSpeed * 2) < .001f,
                "Sequence multiplier scales the active pattern once");
        }
        finally { Object.DestroyImmediate(clone); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "All test worlds disposed");
        Check(Convert.ToBase64String(File.ReadAllBytes(path)) == before, "Prefab bytes unchanged");
        Check(EditorJsonUtility.ToJson(sub) + EditorJsonUtility.ToJson(sub.AttackPatternBehaviour) == authored && EditorJsonUtility.ToJson(local) == localBefore,
            "Wave and Local Override values unchanged");
        Check(wave.gameObject.scene.isDirty == dirty, "Source dirty state unchanged");
        return new { passed = true, checks, nativeSpeed, previewSpeed, legacySpeed, actualRuntimeClock = clock, assetsUnchanged = true };
    }
}
