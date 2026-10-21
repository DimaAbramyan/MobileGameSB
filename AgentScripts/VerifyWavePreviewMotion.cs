using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePreviewMotion
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object obj, string field, object value)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var member = type.GetField(field, Flags);
            if (member != null) { member.SetValue(obj, value); return; }
            var property = type.GetProperty(field, Flags);
            if (property != null) { property.SetValue(obj, value); return; }
        }
        throw new Exception("Missing field " + field);
    }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    private static DirectedEnemySubWave Subwave(GameObject root, Enemy prefab, DirectedWaveAttackSettings settings)
    {
        var sub = root.AddComponent<DirectedEnemySubWave>(); Set(sub, "enemyPrefab", prefab); Set(sub, "enemyCount", 1);
        Set(sub, "settleDuration", 0f); Set(sub, "spawnInterval", 0f);
        var attack = sub.AttackPatternBehaviour ?? root.AddComponent<DirectedWaveAttackBehaviour>();
        Set(attack, "attackSettings", settings); Set(attack, "allowAutonomousAttackDuringEntrance", false); return sub;
    }
    public static object Visuals()
    {
        checks = 0; int worlds = World.All.Count; var scene = EditorSceneManager.NewPreviewScene();
        var roots = new List<GameObject>();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/EnemyShipsLegasy/FourWayEnemy.prefab");
            var source = Object.Instantiate(prefab); roots.Add(source); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, scene);
            foreach (bool nested in new[] { false, true })
            {
                if (nested)
                {
                    source.transform.localScale = new Vector3(-.7f, 1.3f, 1); source.transform.rotation = Quaternion.Euler(0, 0, 23);
                    var parent = new GameObject("Nonuniform parent"); parent.transform.SetParent(source.transform, false);
                    parent.transform.localRotation = Quaternion.Euler(0, 0, 35); parent.transform.localScale = new Vector3(.4f, 2f, 1);
                    var child = new GameObject("Nested mirrored sprite"); child.transform.SetParent(parent.transform, false);
                    child.transform.localRotation = Quaternion.Euler(0, 0, -17); child.transform.localScale = new Vector3(1, -.3f, 1);
                    var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = source.GetComponentInChildren<SpriteRenderer>().sprite;
                    renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(2, 3);
                }
                var root = new GameObject("Visual verification"); roots.Add(root); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var sub = Subwave(root, source.GetComponent<Enemy>(), new DirectedWaveAttackSettings());
                using var session = new WaveShootingPreviewSession(sub, true, true, 0, new Vector3(100, 100));
                var visualRoot = (GameObject)Get(session, "visualsRoot");
                var actual = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
                var expected = source.GetComponentsInChildren<SpriteRenderer>(true);
                Check(actual.Length == expected.Length, "All source sprites copied");
                for (int i = 0; i < actual.Length; i++)
                {
                    Check((actual[i].bounds.size - expected[i].bounds.size).sqrMagnitude < .00001f, "Sprite bounds match authored mirrored hierarchy: " + expected[i].name);
                    Check(actual[i].drawMode == expected[i].drawMode && actual[i].size == expected[i].size, "Sprite rendering size and mode preserved");
                    var offset = actual[i].transform.root.position - expected[i].transform.root.position;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var point = new Vector3((corner & 1) == 0 ? -.3f : .3f, (corner & 2) == 0 ? -.5f : .5f);
                        var a = actual[i].transform.TransformPoint(point); var e = expected[i].transform.TransformPoint(point);
                        // Compare vectors, independent of formation translation.
                        var aVector = a - actual[i].transform.TransformPoint(Vector3.zero);
                        var eVector = e - expected[i].transform.TransformPoint(Vector3.zero);
                        Check((aVector - eVector).sqrMagnitude < .00001f, "Nested transform retains reflection/shear");
                    }
                }
            }
        }
        finally { foreach (var root in roots) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "Visual test cleans up worlds"); return new { passed = true, checks };
    }
    public static object Rotation()
    {
        checks = 0; int worlds = World.All.Count; var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Rotation verification"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab");
        var runtime = Object.Instantiate(asset); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(runtime, scene);
        try
        {
            var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
            Set(settings, "attacksPerSecond", 10f); Set(settings, "delayAfterAttack", 0f);
            settings.StationaryFireSettings.ConfigureAngle(180); Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, .1f, 0);
            var shooting = settings.WaveBurstSettings.Shooting; shooting.SetProjectileBaseSpeedOverride(true, 1);
            shooting.SetLayoutRotationEnabled(true); Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 10f);
            var sub = Subwave(root, asset.GetComponent<Enemy>(), settings);
            using var native = new EnemyProjectilePreviewWorld(); var executor = runtime.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "projectileSpawner", Get(native, "spawner")); Call(executor, "Awake"); Set(executor, "autonomousAttack", false);
            executor.SetWaveAttackControl(true);
            foreach (var mode in new[] { FourWayEnemyRotationMode.Continuous, FourWayEnemyRotationMode.ByAngle, FourWayEnemyRotationMode.PingPongByAngle })
            {
                shooting.Rotation.Configure(FourWayEnemyRotationDirection.Clockwise, mode, 90, AnimationCurve.Linear(0, 0, 1, 1), 45, -20, true);
                settings.Validate(); executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(settings.WaveBurstSettings);
                using var session = new WaveShootingPreviewSession(sub, true, true, 0, new Vector3(100, 100));
                var world = (EnemyProjectilePreviewWorld)Get(session, "projectiles"); var manager = world.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                var seen = new HashSet<Entity>(); float maxAngle = 0, firstShotTime = -1; int shots = 0;
                for (int frame = 1; frame <= 120; frame++)
                {
                    session.AdvanceTo(frame * WaveShootingPreviewSession.StepInterval, 1000);
                    using var entities = query.ToEntityArray(Allocator.Temp);
                    foreach (var entity in entities)
                    {
                        if (!seen.Add(entity)) continue;
                        float emissionTime = (frame - 1) * WaveShootingPreviewSession.StepInterval;
                        if (firstShotTime < 0) firstShotTime = emissionTime;
                        float elapsed = emissionTime - firstShotTime;
                        Set(executor, "patternRotationTime", elapsed);
                        executor.BeginAttackSequence(settings.WaveBurstSettings);
                        Check((float)Get(executor, "patternRotationTime") == elapsed, "Runtime preserves rotation clock between attacks");
                        Check(executor.TryFireStationary(Vector3.down, false, settings.WaveBurstSettings), "Actual runtime executor fires");
                        using var runtimeQuery = native.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                        using var runtimeValues = runtimeQuery.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
                        var actual = manager.GetComponentData<EnemyProjectileMovement>(entity).ForwardDirection;
                        Check(Unity.Mathematics.math.lengthsq(actual - runtimeValues[0].ForwardDirection) < .00001f, "Preview emission matches runtime rotation across cooldowns: " + mode + " at " + elapsed);
                        maxAngle = Mathf.Max(maxAngle, Vector3.Angle(Vector3.down, new Vector3(actual.x, actual.y)));
                        executor.EndAttackSequence(settings.WaveBurstSettings); native.SimulationWorld.EntityManager.DestroyEntity(runtimeQuery); shots++;
                    }
                }
                Check(shots > 8 && maxAngle > 10, "Repeated single-shot attacks actually turn: " + mode);
            }
        }
        finally { Object.DestroyImmediate(runtime); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "Rotation test cleans up worlds"); return new { passed = true, checks, modes = 3, actualRuntimeCompared = true };
    }

    public static object Existing()
    {
        checks = 0; int worlds = World.All.Count;
        const string path = "Assets/Waves/1/1_Extreme/1_4_extrime.prefab";
        string bytes = Convert.ToBase64String(File.ReadAllBytes(path));
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Wave>();
        string authored = EditorJsonUtility.ToJson(wave);
        bool dirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty;
        var clocks = new Dictionary<object, float>(); int rotating = 0; float largest = 0;
        using (var player = new WavePreviewPlayer(wave, 20, new Vector3(0, -3)))
        {
            void Seek(float time) { player.Seek(time); while (player.IsSeeking) player.Update(0, 1000); }
            string State()
            {
                var values = new List<string>(); var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities) values.Add(JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                    + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity)));
                values.Sort(StringComparer.Ordinal); return string.Join("\n", values);
            }
            foreach (float time in new[] { 5f, 10f, 20f })
            {
                Seek(time);
                for (int cue = 0; cue < player.Simulation.CueCount; cue++)
                {
                    var session = player.Simulation.GetSession(cue); if (session == null) continue;
                    foreach (object slot in (System.Collections.IEnumerable)Get(session, "slots"))
                    {
                        var profile = (EnemyBurstAttackSettings)Get(slot, "Profile");
                        if (!profile.Shooting.Rotates || !(bool)Get(slot, "RotationStarted")) continue;
                        float start = (float)Get(slot, "RotationStart"); rotating++;
                        if (clocks.TryGetValue(slot, out float previous)) Check(previous == start, "Authored rotation clock persists across repeated attacks");
                        clocks[slot] = start;
                    }
                    var visuals = (GameObject)Get(session, "visualsRoot");
                    foreach (var renderer in visuals.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        largest = Mathf.Max(largest, renderer.bounds.size.magnitude);
                        Check(renderer.bounds.size.magnitude < 10, "Existing wave model stays within authored scale");
                    }
                }
            }
            Check(rotating > 0, "Existing rotating FourWay group exercised");
            string final = State(); int events = player.Events.Count;
            Seek(4); Seek(20); Check(State() == final && player.Events.Count == events, "Existing wave native state and rotation clock survive snapshot rewind");
            player.ClearSnapshotCache(); Seek(0); Seek(20);
            Check(State() == final && player.Events.Count == events, "Existing wave from-zero replay matches cached state");
        }
        Check(World.All.Count == worlds, "Existing wave frees live and snapshot worlds");
        Check(Convert.ToBase64String(File.ReadAllBytes(path)) == bytes && EditorJsonUtility.ToJson(wave) == authored, "Existing wave asset remains unchanged");
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty == dirty, "Real scene remains unchanged");
        return new { passed = true, checks, wave = wave.name, rotating, largestModel = largest, replay = true, assetsUnchanged = true };
    }
}
