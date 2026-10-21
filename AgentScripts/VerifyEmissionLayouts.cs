using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

public static class VerifyEmissionLayouts
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    [Serializable] public class Document { public List<string> assets, waves; public List<Entry> entries; }
    [Serializable] public class Entry
    {
        public string asset, hierarchy, component, property, json;
        public int componentIndex, shootingType, directions, volley;
        public bool rotates;
        public List<Vector3> rays;
        public List<float> angles;
    }
    private struct Shot { public Vector3 Position, Direction; public EnemyProjectileMovement Movement; }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle.");
        checks = 0;
        var baseline = ReadBaseline();
        VerifyBaseline(baseline);
        VerifyRuntime();
        return new { passed = true, checks, profiles = baseline.entries.Count, waves = baseline.waves,
            tested = "legacy ray/parameter migration, Point offsets, Radial spacing, Circle emitters, per-emitter continuous/locked/burst aim, Fan/Stack/DNA, facing independence, preview geometry, copy/serialization" };
    }
    public static object Migrate()
    {
        var baseline = ReadBaseline();
        int saved = EnemyEmissionLayoutMigration.MigrateAssets(baseline.assets.ToArray());
        return new { saved, assets = baseline.assets };
    }
    public static object Idempotence()
    {
        var document = ReadBaseline();
        var hashes = new Dictionary<string, string>();
        foreach (string path in document.assets) hashes[path] = Hash(path);
        EnemyEmissionLayoutMigration.MigrateAssets(document.assets.ToArray());
        foreach (string path in document.assets)
            if (Hash(path) != hashes[path]) throw new Exception("Second migration changed asset bytes: " + path);
        return new { identicalAssets = hashes.Count };
    }
    private static string Hash(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    private static Document ReadBaseline() => JsonConvert.DeserializeObject<Document>(File.ReadAllText("AgentScripts/EmissionLayoutBaseline.json"));
    private static void VerifyBaseline(Document baseline)
    {
        int normal = 0, hard = 0, extreme = 0;
        foreach (string wave in baseline.waves)
        {
            if (wave.Contains("/1_Normal/")) normal++;
            if (wave.Contains("/1_Hard/")) hard++;
            if (wave.Contains("/1_Extreme/")) extreme++;
            Assert(AssetDatabase.LoadAssetAtPath<GameObject>(wave) != null, "Wave still exists: " + wave);
        }
        Assert(normal == 4 && hard == 4 && extreme == 4, "All three first-level difficulties include all four waves.");
        foreach (var entry in baseline.entries)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(entry.asset);
            UnityEngine.Object target = asset;
            if (asset is GameObject root)
            {
                Transform child = string.IsNullOrEmpty(entry.hierarchy) ? root.transform : root.transform.Find(entry.hierarchy);
                Type type = typeof(Enemy).Assembly.GetType(entry.component, true);
                target = child.GetComponents(type)[entry.componentIndex];
            }
            var serialized = new SerializedObject(target);
            var shooting = (EnemyShootingSettings)serialized.FindProperty(entry.property).boxedValue;
            shooting.Validate();
            Assert(shooting.EmissionLayout.Type == (entry.shootingType == 1 ? EnemyEmissionLayoutType.Radial : EnemyEmissionLayoutType.Point), "Layout migration: " + entry.asset);
            Assert(shooting.DirectionCount == entry.directions && shooting.ProjectilesPerVolley == entry.volley && shooting.Rotates == entry.rotates, "Counts/rotation preserved.");
            var old = JsonUtility.FromJson<EnemyShootingSettings>(entry.json);
            foreach (var field in typeof(EnemyShootingSettings).GetFields(Private))
            {
                if (field.Name == "emissionLayout" || field.Name == "emissionLayoutVersion" || field.Name == "layoutRotationEnabled") continue;
                if (!entry.json.Contains("\"" + field.Name + "\":")) continue;
                object a = field.GetValue(old), b = field.GetValue(shooting);
                Assert(a is AnimationCurve || a is EnemyRotationSettings
                    ? JsonUtility.ToJson(a) == JsonUtility.ToJson(b) : Equals(a, b), "Preserved field: " + field.Name + " / " + entry.asset);
            }
            int sample = 0, angleIndex = 0;
            foreach (float time in new[] { 0f, .37f, 1.4f, 8.5f })
            {
                Near(shooting.Rotation.EvaluateAngle(time), entry.angles[angleIndex++], "All legacy rotation modes/curves remain unchanged.");
                foreach (bool aimed in new[] { false, true })
                    for (int arm = 0; arm < shooting.DirectionCount; arm++)
                        for (int bullet = 0; bullet < shooting.ProjectilesPerDirection; bullet++)
                        {
                            var direction = shooting.GetEmissionDirection(arm, Quaternion.Euler(0, 0, 27), time, new Vector3(.4f, -.9f, 0), aimed);
                            Near(shooting.GetProjectileDirection(direction, 0, bullet, shooting.RandomPreviewAngle), entry.rays[sample++], "Legacy firing geometry preserved: " + entry.asset);
                        }
            }
            string roundTrip = JsonUtility.ToJson(shooting);
            shooting.Validate();
            Assert(JsonUtility.ToJson(shooting) == roundTrip, "Migration is idempotent.");
            var copy = new EnemyShootingSettings(); copy.CopyFrom(shooting);
            Assert(JsonUtility.ToJson(copy) == roundTrip, "Preset and local-override copies retain the layout.");
        }
    }
    private static void VerifyRuntime()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab");
        var clone = UnityEngine.Object.Instantiate(asset);
        var world = new World("Emission layout verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            clone.transform.position = new Vector3(2, 3, 0);
            clone.transform.rotation = Quaternion.Euler(0, 0, 27);
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "projectileSpawner", spawner); Invoke(executor, "Awake");
            Set(executor, "autonomousAttack", false);
            var spawn = new GameObject("Local shooting point").transform;
            spawn.SetParent(clone.transform, false); spawn.localPosition = new Vector3(.2f, -.3f, 0); spawn.localRotation = Quaternion.Euler(0, 0, 15);
            Set(executor, "projectileSpawnPoint", spawn);
            Set(executor, "rotationTarget", null);
            var profile = new EnemyBurstAttackSettings(); profile.Validate(); profile.SetFacing(EnemyFacingMode.Fixed);
            profile.Shooting.SetProjectileBaseSpeedOverride(true, 3f);
            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point, pointOffset: new Vector3(.4f, .6f, 0));
            executor.SetWaveAttackSettings(profile);
            Vector3 target = new Vector3(5, -8, 0);
            Assert(executor.TryFireAt(target, profile), "Point emits actual ECS projectiles.");
            var shots = Read(world);
            Near(shots[0].Position, spawn.position + spawn.rotation * new Vector3(.4f, .6f, 0), "Point offset uses shooting point local axes.");
            Near(shots[0].Direction, (target - shots[0].Position).normalized, "Point Aimed uses offset origin."); Clear(world);
            spawn.localScale = new Vector3(.5f, .7f, 1);
            executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile);
            Assert(executor.TryFireAt(target, profile), "Scaled shooting point emits."); shots = Read(world);
            Near(shots[0].Position, spawn.TransformPoint(new Vector3(.4f, .6f, 0)), "Point Offset respects the local shooting point scale.");
            VerifyPreview(executor, profile, shots); Clear(world); spawn.localScale = Vector3.one;

            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 5, angle: 22f);
            profile.Shooting.SetLayoutRotationEnabled(true);
            profile.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle,
                90, AnimationCurve.Linear(0, 0, 1, 1), 90, 0, true);
            executor.SetWaveAttackSettings(profile); Set(executor, "patternRotationTime", .5f);
            Assert(executor.TryFireInDirection(Vector3.down, profile), "Radial emits."); shots = Read(world);
            Assert(shots.Length == 5, "Radial count.");
            for (int i = 0; i < shots.Length; i++)
            {
                Near(shots[i].Position, spawn.position, "Modern Radial has one fixed shooting point.");
                Near(shots[i].Direction, Quaternion.Euler(0, 0, 27 + 22 + 45 + i * 72) * Vector3.up, "Radial angles cover 360 degrees evenly.");
            }
            Near(Quaternion.Angle(clone.transform.rotation, Quaternion.Euler(0, 0, 27)), 0, "Layout Rotation never turns enemy."); Clear(world);

            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 6, 1.2f, 12f, circleCenterOffset: new Vector3(.2f, -.5f, 0));
            profile.Shooting.SetLayoutRotationEnabled(false);
            executor.SetWaveAttackSettings(profile);
            Assert(executor.TryFireInDirection(Vector3.down, profile), "Circle emits outward."); shots = Read(world);
            Assert(shots.Length == 6, "Circle emitter count.");
            Vector3 center = clone.transform.position + clone.transform.rotation * new Vector3(.2f, -.5f, 0);
            for (int i = 0; i < 6; i++)
            {
                var outward = Quaternion.Euler(0, 0, 27 + 12 + i * 60) * Vector3.up;
                Near(shots[i].Position, center + outward * 1.2f, "Circle placement."); Near(shots[i].Direction, outward, "Circle direction is outward.");
            }
            VerifyPreview(executor, profile, shots); Clear(world);
            clone.transform.localScale = new Vector3(.7f, 1.4f, 1);
            Assert(executor.TryFireInDirection(Vector3.down, profile), "Circle survives non-uniform ship scale."); shots = Read(world);
            Vector3 scaledCenter = clone.transform.position + clone.transform.rotation
                * Vector3.Scale(new Vector3(.2f, -.5f, 0), clone.transform.localScale);
            foreach (var shot in shots) Near((shot.Position - scaledCenter).magnitude, 1.2f, "Radius keeps a real circle.");
            VerifyPreview(executor, profile, shots); Clear(world); clone.transform.localScale = Vector3.one;
            Assert(executor.TryFireAt(target, profile), "Circle continuously aims."); shots = Read(world);
            foreach (var shot in shots) Near(shot.Direction, (target - shot.Position).normalized, "Aimed is evaluated at each emitter."); Clear(world);

            DirectedWaveAttackAimState aim = default;
            Assert(aim.TryFire(clone.GetComponent<Enemy>(), executor, profile, target, false), "Circle acquires start aim.");
            var locked = Read(world); Clear(world);
            clone.transform.position += Vector3.right * 2f;
            profile.Shooting.SetLayoutRotationEnabled(true); Set(executor, "patternRotationTime", .7f);
            Assert(aim.TryFire(clone.GetComponent<Enemy>(), executor, profile, target + Vector3.left * 4, false), "Circle retains aim through attack.");
            shots = Read(world);
            for (int i = 0; i < 6; i++) Near(shots[i].Direction, locked[i].Direction, "Emitter direction stays locked while layout moves."); Clear(world);
            Assert(aim.TryFire(clone.GetComponent<Enemy>(), executor, profile, target + Vector3.left * 4, true), "Burst start refreshes emitter aim.");
            shots = Read(world);
            foreach (var shot in shots) Near(shot.Direction, (target + Vector3.left * 4 - shot.Position).normalized, "Burst re-acquires from current emitter positions."); Clear(world);

            profile.Shooting.ConfigureLegacyFan(5, -20, 30);
            Set(profile.Shooting, "shotGunMode", EnemyProjectileShotGunMode.ShotGun);
            Set(profile.Shooting, "shotGunProjectileCount", 3);
            Set(profile.Shooting, "shotGunDirection", Vector2.up);
            Set(profile.Shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
            Set(profile.Shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
            profile.Validate(); executor.SetWaveAttackSettings(profile); Set(executor, "patternRotationTime", .7f);
            Assert(executor.TryFireAt(target, profile), "Circle + Fan + Stack + DNA emits."); shots = Read(world);
            Assert(shots.Length == 90, "All modifiers multiply per Circle emitter.");
            for (int i = 0; i < 6; i++)
                for (int bullet = 0; bullet < 15; bullet++)
                {
                    var shot = shots[i * 15 + bullet];
                    float angle = Mathf.Lerp(30, -20, (bullet % 5) / 4f);
                    Vector3 directed = (target - shot.Position).normalized;
                    Near(shot.Direction, Quaternion.Euler(0, 0, angle) * directed, "Fan remains relative to emitter aim.");
                    Near(new Vector3(shot.Movement.DnaMedianDirection.x, shot.Movement.DnaMedianDirection.y, 0),
                        Quaternion.Euler(0, 0, 5) * directed, "Each emitter owns its DNA median.");
                }
            VerifyPreview(executor, profile, shots); Clear(world);

            profile.SetFacing(EnemyFacingMode.Rotation);
            profile.FacingRotation.Configure(FourWayEnemyRotationDirection.Clockwise, FourWayEnemyRotationMode.Continuous,
                40, AnimationCurve.Linear(0, 0, 1, 1), 360, 0, true);
            executor.SetWaveAttackSettings(profile); Set(executor, "patternRotationTime", .7f);
            Assert(executor.TryFireAt(target, profile), "Before facing rotation."); var before = Read(world); Clear(world);
            Set(executor, "facingRotationTime", 2f); Invoke(executor, "TickFacing", 0f);
            Assert(Quaternion.Angle(clone.transform.rotation, Quaternion.Euler(0, 0, 27)) > 79f, "Independent Enemy Facing rotates its model.");
            Assert(executor.TryFireAt(target, profile), "After facing rotation."); var after = Read(world); Clear(world);
            for (int i = 0; i < before.Length; i++) { Near(before[i].Position, after[i].Position, "Facing cannot move layout."); Near(before[i].Direction, after[i].Direction, "Facing cannot change emission directions."); }
            executor.PauseRotation(); executor.RestartRotation();
            Near((float)Get(executor, "facingRotationTime"), 2f, "Restarting layout never restarts independent facing.");
            VerifyInspector(executor);
        }
        finally
        {
            Clear(world);
            // Dispose is a Play Mode API. Test-owned visuals are released with
            // DestroyImmediate only after their ECS entities have been removed.
            var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
            foreach (object visual in visuals.Values)
            {
                UnityEngine.Object.DestroyImmediate((Material)visual.GetType().GetField("Material").GetValue(visual));
                UnityEngine.Object.DestroyImmediate((Mesh)visual.GetType().GetField("Mesh").GetValue(visual));
            }
            visuals.Clear();
            spawner.Dispose(); UnityEngine.Object.DestroyImmediate(clone); world.Dispose();
        }
    }
    private static void VerifyInspector(SimpleEnemyAttackExecutor executor)
    {
        executor.SetWaveAttackSettings(null);
        foreach (EnemyEmissionLayoutType type in Enum.GetValues(typeof(EnemyEmissionLayoutType)))
        {
            executor.BurstAttackSettings.Shooting.EmissionLayout.Configure(type, 5);
            executor.BurstAttackSettings.Shooting.SetLayoutRotationEnabled(true);
            var serialized = new SerializedObject(executor);
            var root = serialized.FindProperty("burstAttackSettings");
            var shooting = root.FindPropertyRelative("shooting");
            var fields = (IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod("Fields", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { shooting });
            foreach (string field in fields)
            {
                Assert(shooting.FindPropertyRelative(field) != null, "Inspector field exists: " + field);
                Assert(field != "shootingType" && field != "directionCount" && field != "rotate", "Old Rotation UI is replaced.");
            }
            Assert(new EnemyEmissionLayoutSettingsDrawer().GetPropertyHeight(shooting.FindPropertyRelative("emissionLayout"), GUIContent.none) > 40,
                "Layout drawer reserves its fields.");
            Assert(new EnemyShootingSettingsDrawer().GetPropertyHeight(shooting, GUIContent.none) > 200, "Shooting drawer includes layout and rotation.");
            Assert(EnemyShootingSettingsDrawer.GetProjectilesPerVolley(shooting) == executor.BurstAttackSettings.Shooting.ProjectilesPerVolley,
                "Inspector projectile totals match runtime for every layout.");
        }
    }
    private static void VerifyPreview(SimpleEnemyAttackExecutor executor, EnemyBurstAttackSettings profile, Shot[] shots)
    {
        float time = (float)Get(executor, "patternRotationTime");
        var frame = EnemyShootingPreview.CreateFrame(executor, profile.Shooting, executor.transform.position, time, null, out var basis);
        for (int i = 0; i < profile.Shooting.DirectionCount; i++)
            Near(profile.Shooting.GetEmissionPosition(i, frame.ShootingPoint, frame.EnemyPosition, basis, frame.PointBasis,
                frame.EnemyBasis, frame.Scale, time), shots[i * profile.Shooting.ProjectilesPerDirection].Position, "Runtime and Preview emitter positions match.");
    }
    private static Shot[] Read(World world)
    {
        using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity), typeof(LocalTransform), typeof(EnemyProjectileMovement));
        using var positions = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var movements = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
        var shots = new Shot[positions.Length];
        for (int i = 0; i < shots.Length; i++) shots[i] = new Shot { Position = positions[i].Position,
            Direction = new Vector3(movements[i].ForwardDirection.x, movements[i].ForwardDirection.y, 0), Movement = movements[i] };
        return shots;
    }
    private static void Clear(World world) { using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)); world.EntityManager.DestroyEntity(query); }
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Private).GetValue(obj);
    private static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    private static void Assert(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Assert(Mathf.Abs(actual - expected) < .002f, message + $" ({actual} != {expected})");
    private static void Near(Vector3 actual, Vector3 expected, string message) => Assert((actual - expected).sqrMagnitude < .00001f, message + $" ({actual} != {expected})");
}
