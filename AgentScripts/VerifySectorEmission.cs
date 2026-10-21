using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifySectorEmission
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private struct Shot { public Vector3 Position, Direction; public EnemyProjectileMovement Movement; }
    private sealed class Target : IEnemyAttackAimTarget { public Vector3 Position; public Vector3 AttackTargetPosition => Position; }
    private sealed class PendingExecutor : IEnemyBurstAttackExecutor, IEnemyPendingEmissionExecutor, IEnemyAttackSequenceExecutor
    {
        public bool Pending, Ended;
        public int Volleys;
        public bool CanPerformWaveAttack => true;
        public EnemyBurstAttackSettings BurstAttackSettings { get; set; }
        public void SetWaveAttackControl(bool enabled) { }
        public bool TryFireAt(Vector3 target, EnemyBurstAttackSettings settings) => TryFireInDirection(target, settings);
        public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) { Volleys++; Pending = true; return true; }
        public bool HasPendingEmissions(EnemyBurstAttackSettings settings) => Pending;
        public void BeginAttackSequence(EnemyBurstAttackSettings settings) { Ended = false; }
        public void EndAttackSequence(EnemyBurstAttackSettings settings) { Ended = true; }
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle.");
        checks = 0;
        Geometry();
        AimedGeometry();
        Runtime();
        WaveCompletion();
        return new { passed = true, checks, tested = "sector wrap/midpoint, launch timing/reverse/overlap/readiness/cancellation, real ECS Fan/Stack/DNA/Aimed/rotation, preview geometry, inspector, copy and full-attack completion" };
    }
    private static void Geometry()
    {
        var layout = new EnemyEmissionLayoutSettings();
        foreach (var sample in new[] { new Vector3(350, 10, 20), new Vector3(-30, 30, 60), new Vector3(170, -170, 20),
            new Vector3(0, 270, 270), new Vector3(0, 360, 360), new Vector3(45, 45, 0) })
        {
            layout.ConfigureSector(sample.x, sample.y, 5, EnemySectorLaunchMode.Sequential, .2f, .3f);
            for (int i = 0; i < 5; i++) Near(layout.GetAngle(i), sample.x + sample.z * i / 4f, "Sector includes both boundaries and wraps correctly.");
            Near(layout.LaunchDuration, 1.4f, "Duration includes initial delay and last launch.");
            layout.ConfigureSector(sample.x, sample.y, 1);
            Near(layout.GetAngle(0), sample.x + sample.z / 2, "One direction is the sector midpoint.");
        }
        layout.ConfigureSector(350, 10, 3, EnemySectorLaunchMode.Sequential, .2f, .3f, true, true);
        for (int order = 0; order < 3; order++)
        {
            Assert(layout.GetLaunchIndex(order) == 2 - order, "Reverse order.");
            Near(layout.GetLaunchDelay(2 - order), .2f + .3f * order, "Preview delay follows runtime launch order.");
        }
        var copy = new EnemyEmissionLayoutSettings(); copy.CopyFrom(layout);
        Assert(JsonUtility.ToJson(copy) == JsonUtility.ToJson(layout), "Sector fields are deep-copied.");
        var restored = JsonUtility.FromJson<EnemyEmissionLayoutSettings>(JsonUtility.ToJson(layout));
        Assert(restored.WaitUntilSectorReady && restored.ReverseOrder && restored.IsSequential, "Serialization retains Sector settings.");
        layout.ConfigureSector(20, 40, 0, EnemySectorLaunchMode.Sequential, -2, -3);
        Assert(layout.EmissionCount == 1 && layout.StartDelay == 0 && layout.LaunchInterval == 0, "Invalid counts and timing are clamped.");
        layout.Configure(EnemyEmissionLayoutType.Radial, 4, angle: 12);
        for (int i = 0; i < 4; i++) Near(layout.GetAngle(i), 12 + i * 90, "Radial geometry is unchanged.");
        Assert(!layout.IsSequential && layout.LaunchDuration == 0, "Hidden Sector values cannot affect other layouts.");
    }
    private static void AimedGeometry()
    {
        var shooting = Profile().Shooting;
        Vector3 aim = new Vector3(3, -7, 0).normalized;
        Quaternion basis = Quaternion.Euler(0, 0, 53);
        foreach (var sample in new[] { new Vector3(0, 60, 60), new Vector3(30, 90, 60), new Vector3(350, 10, 20),
            new Vector3(170, -170, 20), new Vector3(0, 270, 270), new Vector3(0, 360, 360), new Vector3(45, 45, 0) })
        foreach (int count in new[] { 1, 2, 3, 4, 5 })
        {
            shooting.EmissionLayout.ConfigureSector(sample.x, sample.y, count);
            var frame = new EnemyEmissionPreviewFrame { Scale = Vector3.one, PointBasis = basis, EnemyBasis = basis, Target = aim * 10 };
            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0 : sample.z * ((float)i / (count - 1) - .5f);
                Vector3 expected = Quaternion.Euler(0, 0, offset) * aim;
                Near(shooting.GetEmissionDirection(i, basis, 0, aim, true), expected, "Aimed centers the Sector on the target, independent of start angle and enemy facing.");
                var emitter = EnemyShootingPreviewGeometry.GetEmitter(shooting, i, frame, basis, 0, aimed: true);
                Near(emitter.BaseDirection, expected, "Scene preview centers Aimed Sector exactly as runtime.");
                if (i == 0)
                {
                    Near(EnemyShootingPreviewGeometry.SectorStartDirection(shooting, emitter), Quaternion.Euler(0, 0, -sample.z / 2) * aim, "Preview start boundary surrounds the target.");
                    Near(EnemyShootingPreviewGeometry.SectorEndDirection(shooting, emitter), Quaternion.Euler(0, 0, sample.z / 2) * aim, "Preview end boundary surrounds the target.");
                }
            }
        }
    }
    private static void Runtime()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
        var world = new World("Sector emission verification");
        var spawner = new EnemyProjectileEcsSpawner();
        Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            clone.transform.position = new Vector3(2, 3, 0);
            clone.transform.rotation = Quaternion.Euler(0, 0, 27);
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "projectileSpawner", spawner); Invoke(executor, "Awake"); Set(executor, "autonomousAttack", false);
            var profile = Profile();
            profile.Shooting.ConfigureLegacyFan(5, -10, 10);
            Set(profile.Shooting, "shotGunMode", EnemyProjectileShotGunMode.ShotGun);
            Set(profile.Shooting, "shotGunProjectileCount", 3); Set(profile.Shooting, "shotGunDirection", Vector2.up);
            Set(profile.Shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
            Set(profile.Shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
            profile.Validate();
            Reset(executor, profile);
            Assert(executor.TryFireInDirection(Vector3.up, profile), "Simultaneous Sector emits.");
            var shots = Read(world); Assert(shots.Length == 45, "Fan/Stack multiply per Sector direction.");
            for (int arm = 0; arm < 3; arm++) CheckDirection(shots, arm * 15, 15, Quaternion.Euler(0, 0, 27 + 350 + 10 * arm) * Vector3.up, true);
            Preview(executor, profile); Clear(world);

            foreach (var sample in new[] { new Vector3(0, 60, 1), new Vector3(30, 90, 2), new Vector3(350, 10, 3), new Vector3(170, -170, 4) })
            foreach (bool locked in new[] { false, true })
            {
                int count = (int)sample.z;
                profile.Shooting.EmissionLayout.ConfigureSector(sample.x, sample.y, count);
                Reset(executor, profile);
                Vector3 aimTarget = new Vector3(4, -8, 0);
                Vector3 aimDirection = (aimTarget - executor.GetWaveAimOrigin(profile)).normalized;
                Assert(locked ? executor.TryFireInAimedDirection(aimDirection, profile) : executor.TryFireAt(aimTarget, profile), "Centered Aimed Sector emits actual ECS projectiles.");
                shots = Read(world);
                Assert(shots.Length == count * 15, "Centered Aimed retains Fan and Stack counts.");
                var frame = EnemyShootingPreview.CreateFrame(executor, profile.Shooting, clone.transform.position, 0, null, out var basis);
                frame.Target = aimTarget;
                for (int arm = 0; arm < count; arm++)
                {
                    float offset = count == 1 ? 0 : profile.Shooting.EmissionLayout.SectorSweep * ((float)arm / (count - 1) - .5f);
                    Vector3 expected = Quaternion.Euler(0, 0, offset) * aimDirection;
                    CheckDirection(shots, arm * 15, 15, expected, true);
                    var emitter = EnemyShootingPreviewGeometry.GetEmitter(profile.Shooting, arm, frame, basis, 0, aimed: true);
                    for (int projectile = 0; projectile < 15; projectile++)
                        Near(EnemyShootingPreviewGeometry.GetProjectile(profile.Shooting, emitter, projectile).Direction, shots[arm * 15 + projectile].Direction, "Final centered Aimed Fan/Stack preview matches spawned projectiles.");
                }
                Clear(world);
            }

            profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 3, EnemySectorLaunchMode.Sequential, .2f, .3f);
            profile.Shooting.SetLayoutRotationEnabled(true);
            profile.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.Continuous,
                90, AnimationCurve.Linear(0, 0, 1, 1), 360, 0, true);
            Reset(executor, profile);
            var target = new Target { Position = new Vector3(4, -8, 0) }; executor.SetWaveAimTarget(target);
            Assert(executor.TryFireAt(target.Position, profile), "Sequential request is accepted before launch.");
            float start = PendingStart(executor);
            Assert(Read(world).Length == 0 && executor.HasPendingEmissions(profile), "Start Delay defers the first launch.");
            Tick(executor, start + .199f); Assert(Read(world).Length == 0, "No premature launch.");
            Tick(executor, start + .2001f); shots = Read(world); Assert(shots.Length == 15, "First direction emits one Fan/Stack.");
            CheckDirection(shots, 0, 15, Quaternion.Euler(0, 0, -10) * (target.Position - shots[0].Position).normalized, true); Clear(world);
            clone.transform.position += Vector3.right * 2;
            target.Position += Vector3.left * 3;
            Set(executor, "patternRotationTime", .5f);
            Tick(executor, start + .5001f); shots = Read(world); Assert(shots.Length == 15, "Second direction uses interval.");
            CheckDirection(shots, 0, 15, Quaternion.Euler(0, 0, 45) * (target.Position - shots[0].Position).normalized, true);
            Near(shots[0].Position, executor.GetEmissionPosition(profile, 1), "Delayed launch uses current emitter position."); Clear(world);
            Tick(executor, start + .8001f); shots = Read(world); Assert(shots.Length == 15 && !executor.HasPendingEmissions(profile), "Last direction finishes the volley."); Clear(world);
            Assert(!(bool)Invoke(executor, "CanRestartSectorAttack", profile, start + .8001f, 1f), "Cooldown starts after actual last launch.");
            Assert((bool)Invoke(executor, "CanRestartSectorAttack", profile, start + 1.8002f, 1f), "Attack can restart after cooldown.");

            profile.Shooting.SetLayoutRotationEnabled(false);
            profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 3, EnemySectorLaunchMode.Sequential, 0, .1f, reverse: true);
            Reset(executor, profile);
            Assert(executor.TryFireInDirection(Vector3.up, profile), "Reverse Sector accepted."); start = PendingStart(executor);
            shots = Read(world); CheckDirection(shots, 0, 15, Quaternion.Euler(0, 0, 27 + 370) * Vector3.up, true); Clear(world);
            Tick(executor, start + .2001f); shots = Read(world); Assert(shots.Length == 30, "Missed frames catch up without losing directions.");
            CheckDirection(shots, 0, 15, Quaternion.Euler(0, 0, 27 + 360) * Vector3.up, true);
            CheckDirection(shots, 15, 15, Quaternion.Euler(0, 0, 27 + 350) * Vector3.up, true); Clear(world);

            // Start/burst locks are retained independently by overlapping Sector volleys.
            profile.Shooting.EmissionLayout.ConfigureSector(30, 90, 3, EnemySectorLaunchMode.Sequential, .2f, .1f);
            Reset(executor, profile);
            var aim = new DirectedWaveAttackAimState();
            Vector3 firstTarget = executor.GetWaveAimOrigin(profile) + Vector3.down * 5;
            Assert(aim.TryFire(clone.GetComponent<Enemy>(), executor, profile, firstTarget, false), "Sector acquires attack aim.");
            start = PendingStart(executor);
            Vector3 nextTarget = executor.GetWaveAimOrigin(profile) + Vector3.right * 5;
            Assert(aim.TryFire(clone.GetComponent<Enemy>(), executor, profile, nextTarget, true), "Next burst acquires a fresh aim.");
            Tick(executor, start + .4001f); shots = Read(world); Assert(shots.Length == 90, "Both aimed volleys finish.");
            for (int arm = 0; arm < 3; arm++)
            {
                CheckDirection(shots, arm * 15, 15, Quaternion.Euler(0, 0, -30 + arm * 30) * Vector3.down, true);
                CheckDirection(shots, 45 + arm * 15, 15, Quaternion.Euler(0, 0, -30 + arm * 30) * Vector3.right, true);
            }
            Clear(world);

            profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 1, EnemySectorLaunchMode.Sequential, 0, .4f);
            Reset(executor, profile); Assert(executor.TryFireInDirection(Vector3.up, profile), "Single midpoint emits immediately.");
            shots = Read(world); Assert(shots.Length == 15 && !executor.HasPendingEmissions(profile), "Count one creates no extra sequence steps.");
            CheckDirection(shots, 0, 15, Quaternion.Euler(0, 0, 27 + 360) * Vector3.up, true); Clear(world);

            profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .2f, 0);
            Reset(executor, profile); executor.TryFireInDirection(Vector3.up, profile); start = PendingStart(executor);
            Tick(executor, start + .2001f); Assert(Read(world).Length == 45 && !executor.HasPendingEmissions(profile), "Zero interval launches all directions together after delay."); Clear(world);

            profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .2f, .1f, true);
            Reset(executor, profile); executor.SetFormationAttackReady(false);
            executor.TryFireInDirection(Vector3.up, profile); start = PendingStart(executor);
            Tick(executor, start + 10); Assert(Read(world).Length == 0 && executor.HasPendingEmissions(profile), "Ready flag waits without consuming Start Delay.");
            executor.SetFormationAttackReady(true); Tick(executor, start + 10);
            Assert(Read(world).Length == 0, "Start Delay begins upon readiness.");
            Tick(executor, start + 10.2001f); Assert(Read(world).Length == 15, "First launch after readiness plus delay."); Clear(world);
            Tick(executor, start + 10.4001f); Assert(Read(world).Length == 30 && !executor.HasPendingEmissions(profile), "Ready sequence completes normally."); Clear(world);
            profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .2f, .1f, false);
            Reset(executor, profile); executor.SetFormationAttackReady(false);
            executor.TryFireInDirection(Vector3.up, profile); start = PendingStart(executor); Tick(executor, start + .4001f);
            Assert(Read(world).Length == 45, "Ready flag off launches during formation."); Clear(world); executor.SetFormationAttackReady(true);

            Reset(executor, profile); executor.TryFireInDirection(Vector3.up, profile); executor.TryFireInDirection(Vector3.down, profile);
            start = PendingStart(executor); Tick(executor, start + .4001f);
            Assert(Read(world).Length == 90 && !executor.HasPendingEmissions(profile), "Overlapping volleys each retain all directions."); Clear(world);
            Reset(executor, profile); executor.SetWaveAttackControl(true); executor.TryFireInDirection(Vector3.up, profile);
            start = PendingStart(executor); executor.SetWaveAttackControl(false); Tick(executor, start + 10);
            Assert(Read(world).Length == 0 && !executor.HasPendingEmissions(profile), "Wave release cancels pending sectors.");
            Reset(executor, profile); executor.TryFireInDirection(Vector3.up, profile); start = PendingStart(executor);
            Invoke(executor, "OnDisable"); Tick(executor, start + 10);
            Assert(Read(world).Length == 0 && !executor.HasPendingEmissions(profile), "Disable cancels pending sectors.");
            // An explicit pivot must produce identical origins in runtime and preview.
            var pivot = new GameObject("Sector layout basis").transform;
            pivot.SetParent(clone.transform, false); pivot.localPosition = new Vector3(.4f, -.2f, 0);
            pivot.localRotation = Quaternion.Euler(0, 0, 40);
            Set(executor, "projectileSpawnPoint", null); Set(executor, "rotationTarget", pivot);
            profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3);
            Reset(executor, profile); executor.TryFireInDirection(Vector3.up, profile);
            Preview(executor, profile); Clear(world);
            Inspector(executor);
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
            visuals.Clear(); spawner.Dispose(); UnityEngine.Object.DestroyImmediate(clone); world.Dispose();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void WaveCompletion()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Sector attack completion verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>(); var enemy = root.AddComponent<Enemy>();
            var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            var profile = Profile(); profile.ConfigureLegacyRepeatedBurst(3, 1, 0);
            settings.WaveBurstSettings.CopyFrom(profile); settings.Validate();
            var executor = new PendingExecutor { BurstAttackSettings = profile };
            object controller = Activator.CreateInstance(typeof(DirectedEnemySubWave).Assembly.GetType("DirectedWaveAttackController", true), new object[] { wave, settings, null });
            var routine = (IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, profile);
            Assert(routine.MoveNext() && executor.Volleys == 3 && !executor.Ended, "Burst attack stays active until last Sector launch.");
            Assert(routine.MoveNext() && !executor.Ended, "Wait For Previous Attack observes pending emissions.");
            executor.Pending = false;
            Assert(!routine.MoveNext() && executor.Ended, "Attack completes only after draining directions.");
            profile.EnableMultiplePatterns(); profile.AddPattern(new EnemyAttackPatternSettings()); profile.Validate();
            executor.Volleys = 0; executor.Ended = false;
            var multiple = (IEnumerator)Invoke(controller, "FireMultiplePatterns", enemy, executor, profile);
            Assert(multiple.MoveNext() && executor.Pending, "Multiple pattern completion also waits for pending directions.");
            executor.Pending = false; Assert(!multiple.MoveNext(), "Multiple patterns finish after drain.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static EnemyBurstAttackSettings Profile()
    {
        var profile = new EnemyBurstAttackSettings(); profile.ConfigureLegacySingleAttack(1, 1, 0); profile.SetFacing(EnemyFacingMode.Fixed);
        profile.Shooting.SetProjectileBaseSpeedOverride(true, 3); profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 3); return profile;
    }
    private static void Inspector(SimpleEnemyAttackExecutor executor)
    {
        var profile = executor.BurstAttackSettings; profile.CopyFrom(Profile());
        profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .2f, .1f, true, true);
        var shooting = new SerializedObject(executor).FindProperty("burstAttackSettings").FindPropertyRelative("shooting");
        var layout = shooting.FindPropertyRelative("emissionLayout");
        var fields = (IEnumerable<string>)typeof(EnemyEmissionLayoutSettingsDrawer).GetMethod("Fields", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { layout });
        var names = new HashSet<string>(fields);
        foreach (string field in new[] { "startAngle", "endAngle", "directionsCount", "launchMode", "startDelay", "launchInterval", "waitUntilSectorReady", "reverseOrder" })
            Assert(names.Contains(field) && layout.FindPropertyRelative(field) != null, "Sector Inspector field: " + field);
        Near(profile.AttackDuration, .4f, "Attack summary includes Sector tail.");
        Assert(EnemyShootingSettingsDrawer.GetProjectilesPerVolley(shooting) == 3, "Inspector counts Sector directions.");
        Assert(new EnemyEmissionLayoutSettingsDrawer().GetPropertyHeight(layout, GUIContent.none) > 170, "Inspector allocates all required rows.");
        var local = new EnemyBurstAttackSettings(); local.ApplyShootingOverrideFrom(profile);
        Assert(JsonUtility.ToJson(local.Shooting.EmissionLayout) == JsonUtility.ToJson(profile.Shooting.EmissionLayout), "Local overrides retain every Sector option.");
    }
    private static void CheckDirection(Shot[] shots, int start, int count, Vector3 median, bool dna)
    {
        for (int i = 0; i < count; i++)
        {
            Near(shots[start + i].Direction, Quaternion.Euler(0, 0, Mathf.Lerp(10, -10, (i % 5) / 4f)) * median, "Fan is applied separately per direction.");
            if (dna) Near(new Vector3(shots[start + i].Movement.DnaMedianDirection.x, shots[start + i].Movement.DnaMedianDirection.y, 0), median, "DNA keeps each direction's own median.");
        }
    }
    private static void Preview(SimpleEnemyAttackExecutor executor, EnemyBurstAttackSettings profile)
    {
        var frame = EnemyShootingPreview.CreateFrame(executor, profile.Shooting, executor.transform.position, 0, null, out var basis);
        for (int i = 0; i < profile.Shooting.DirectionCount; i++)
            Near(profile.Shooting.GetEmissionPosition(i, frame.ShootingPoint, frame.EnemyPosition, basis, frame.PointBasis, frame.EnemyBasis, frame.Scale, 0), executor.GetEmissionPosition(profile, i), "Preview and runtime share Sector origins.");
    }
    private static void Reset(SimpleEnemyAttackExecutor executor, EnemyBurstAttackSettings profile)
    {
        executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile); executor.SetWaveAimTarget(null);
    }
    private static float PendingStart(SimpleEnemyAttackExecutor executor) => (float)Get(((IList)Get(executor, "pendingSectors"))[0], "StartTime");
    private static void Tick(SimpleEnemyAttackExecutor executor, float time) => Invoke(executor, "TickPendingSectors", time);
    private static Shot[] Read(World world)
    {
        using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity), typeof(LocalTransform), typeof(EnemyProjectileMovement));
        using var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp); using var movements = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
        var shots = new Shot[poses.Length]; for (int i = 0; i < shots.Length; i++) shots[i] = new Shot { Position = poses[i].Position,
            Direction = new Vector3(movements[i].ForwardDirection.x, movements[i].ForwardDirection.y, 0), Movement = movements[i] }; return shots;
    }
    private static void Clear(World world) { using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)); world.EntityManager.DestroyEntity(query); }
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Private | BindingFlags.Public).GetValue(obj);
    private static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    private static void Assert(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Assert(Mathf.Abs(actual - expected) < .002f, message + $" ({actual} != {expected})");
    private static void Near(Vector3 actual, Vector3 expected, string message) => Assert((actual - expected).sqrMagnitude < .00001f, message + $" ({actual} != {expected})");
}
