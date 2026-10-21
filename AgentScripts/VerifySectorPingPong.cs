using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifySectorPingPong
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks, scenarios;
    private static EnemyBullet bullet;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < .002f, message + ": " + a + " / " + b);
    private sealed class Fixture : IDisposable
    {
        public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
        public readonly World World = new("PingPong executor verification");
        public readonly EnemyProjectileEcsSpawner Spawner;
        public readonly GameObject Root;
        public readonly SimpleEnemyAttackExecutor Executor;
        public readonly Enemy Enemy;
        public float Time;
        public Fixture()
        {
            Root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));
            SceneManager.MoveGameObjectToScene(Root, Scene); Root.transform.rotation = Quaternion.identity;
            Executor = Root.GetComponent<SimpleEnemyAttackExecutor>(); Enemy = Root.GetComponent<Enemy>();
            Spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(World); Set(Executor, "projectileSpawner", Spawner);
            Call(Executor, "Awake"); Set(Executor, "autonomousAttack", false); Clock(0);
        }
        public void Clock(float time)
        {
            Time = time; object clock = new EnemyAttackClock(); Set(clock, "initialized", true);
            Set(clock, "previousTime", UnityEngine.Time.time); Set(clock, "attackTime", time); Set(Enemy, "attackClock", clock);
        }
        public void Tick(float time) { Clock(time); Call(Executor, "TickPendingSectors", time); }
        public List<Vector3> ReadAndClear()
        {
            using var query = World.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
            using var values = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
            var result = new List<Vector3>(); foreach (var value in values) result.Add(new Vector3(value.ForwardDirection.x, value.ForwardDirection.y));
            World.EntityManager.DestroyEntity(query); return result;
        }
        public IList Pending => (IList)Get(Executor, "pendingSectors");
        public bool NextReverse => ((EnemySectorLaunchTicket)Get(Pending[Pending.Count - 1], "LaunchOrder")).Reversed;
        public void Begin(EnemyBurstAttackSettings profile) { Executor.SetWaveAttackSettings(profile); Executor.BeginAttackSequence(profile); Executor.BeginEmissionOriginAttack(profile); }
        public List<Vector3> Sector(EnemyBurstAttackSettings profile)
        {
            Clock(Time + 1); float start = Time;
            Check(Executor.TryFireInDirection(Vector3.up, profile), "Sector accepted");
            var result = ReadAndClear();
            for (int i = 0; i < profile.Shooting.DirectionCount; i++)
            { Tick(start + profile.Shooting.EmissionLayout.StartDelay + i * profile.Shooting.EmissionLayout.LaunchInterval + .0001f); result.AddRange(ReadAndClear()); }
            Check(!Executor.HasPendingEmissions(profile), "Sector finishes after last direction"); return result;
        }
        public void Dispose() { Call(Executor, "OnDisable"); UnityEngine.Object.DestroyImmediate(Root); Spawner.Dispose(); World.Dispose(); EditorSceneManager.ClosePreviewScene(Scene); }
    }
    private static EnemyBurstAttackSettings Profile(bool reset = true, bool reverse = false)
    {
        var profile = new EnemyBurstAttackSettings(); profile.ConfigureLegacySingleAttack(1, 1, 0); profile.SetFacing(EnemyFacingMode.Fixed);
        profile.Shooting.SetProjectileBaseSpeedOverride(true, .01f);
        Set(profile.Shooting, "overrideProjectileLifetime", true); Set(profile.Shooting, "projectileLifetime", 40f);
        profile.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .05f, .05f);
        profile.Shooting.EmissionLayout.ConfigureLaunchOrder(EnemySectorLaunchOrder.PingPong,
            reverse ? EnemySectorInitialDirection.Reverse : EnemySectorInitialDirection.Forward, reset);
        return profile;
    }
    private static void Directions(List<Vector3> values, EnemyShootingSettings shooting, int sectors, Func<int, bool> reversed, Quaternion? basis = null)
    {
        int per = shooting.ProjectilesPerDirection; Check(values.Count == sectors * shooting.DirectionCount * per, "Expected Sector/Fan/Stack counts");
        for (int v = 0; v < sectors; v++) for (int order = 0; order < shooting.DirectionCount; order++) for (int p = 0; p < per; p++)
        {
            int arm = shooting.EmissionLayout.GetLaunchIndex(order, reversed(v));
            Vector3 baseDirection = shooting.GetEmissionDirection(arm, basis ?? Quaternion.identity, 0, Vector3.up, false);
            Near(values[(v * shooting.DirectionCount + order) * per + p], shooting.GetProjectileDirection(baseDirection, 0, p, 0), "Sector order preserves geometry");
        }
    }
    public static object Run()
    {
        checks = scenarios = 0;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        State(); Runtime(); Cancellation(); CopiesAndSources(); Children(); BoundaryOverlaps(); SequenceController(); Preview();
        return new { passed = true, checks, scenarios, tested = "completed-sector alternation, epochs/cancellation/overlap, initial direction/reset, physical executor, independent enemies/patterns/sources, Burst/Fan/Stack, child repeats, Sequence runtime and Preview, old bool serialization" };
    }
    private static void State()
    {
        foreach (bool reset in new[] { true, false }) foreach (bool initial in new[] { true, false })
        {
            scenarios++; var layout = Profile(reset, initial).Shooting.EmissionLayout; var registry = new EnemySectorLaunchOrderRegistry();
            for (int attack = 0; attack < 4; attack++)
            {
                registry.BeginAttack(layout);
                for (int sector = 0; sector < 3; sector++)
                {
                    var ticket = registry.Reserve(layout); bool expected = initial ^ (((reset ? sector : attack * 3 + sector) & 1) != 0);
                    Check(ticket.Reversed == expected, "Attack start reset / persistent parity"); ticket.Complete(); ticket.Complete();
                }
            }
            registry.BeginAttack(layout); var cancelled = registry.Reserve(layout); bool before = cancelled.Reversed; cancelled.Cancel(); cancelled.Cancel();
            var replacement = registry.Reserve(layout); Check(replacement.Reversed == before, "Cancelled sector never advances"); replacement.Complete();
        }
        scenarios++;
        var settings = Profile(false).Shooting.EmissionLayout; var orders = new EnemySectorLaunchOrderRegistry(); orders.BeginAttack(settings);
        var a = orders.Reserve(settings); var b = orders.Reserve(settings); var c = orders.Reserve(settings);
        Check(!a.Reversed && b.Reversed && !c.Reversed, "Overlapping reservations alternate"); a.Complete(); b.Cancel(); c.Cancel();
        var next = orders.Reserve(settings); Check(next.Reversed, "Only completed work commits parity"); next.Cancel();
        settings.ConfigureLaunchOrder(EnemySectorLaunchOrder.PingPong, EnemySectorInitialDirection.Reverse, true);
        orders.BeginAttack(settings); var old = orders.Reserve(settings); orders.BeginAttack(settings); old.Complete();
        var fresh = orders.Reserve(settings); Check(fresh.Reversed, "Old epoch completion cannot flip a restarted attack"); fresh.Cancel();
        var legacy = JsonUtility.FromJson<EnemyEmissionLayoutSettings>("{\"type\":3,\"directionsCount\":3,\"launchMode\":1,\"reverseOrder\":true}");
        Check(legacy.LaunchOrder == EnemySectorLaunchOrder.Reverse && legacy.GetLaunchIndex(0) == 2, "Old Reverse Order stays Reverse");
        Check(JsonUtility.FromJson<EnemyEmissionLayoutSettings>("{}").LaunchOrder == EnemySectorLaunchOrder.Forward, "Old Forward remains default");
        var variant = JsonUtility.FromJson<EnemyEmissionLayoutSettings>("{\"launchOrder\":1,\"reverseOrder\":false}");
        Check(variant.LaunchOrder == EnemySectorLaunchOrder.Forward, "Existing false Reverse Order variant override remains authoritative");
    }
    private static void Runtime()
    {
        foreach (bool reset in new[] { true, false }) foreach (bool reverse in new[] { true, false })
        {
            scenarios++; using var fixture = new Fixture(); var profile = Profile(reset, reverse); var all = new List<Vector3>();
            for (int attack = 0; attack < 2; attack++)
            {
                fixture.Begin(profile);
                for (int v = 0; v < 3; v++) all.AddRange(fixture.Sector(profile));
                fixture.Executor.EndAttackSequence(profile);
            }
            Directions(all, profile.Shooting, 6, v => reverse ^ (((reset ? v % 3 : v) & 1) != 0));
        }
        scenarios++;
        using (var left = new Fixture()) using (var right = new Fixture())
        {
            var shared = Profile(false); left.Begin(shared); right.Begin(shared);
            left.Sector(shared); left.Sector(shared); left.Sector(shared);
            Directions(right.Sector(shared), shared.Shooting, 1, _ => false);
        }
        scenarios++;
        using (var fixture = new Fixture())
        {
            var profile = Profile(false); profile.Shooting.ConfigureLegacyFan(2, -5, 5);
            Set(profile.Shooting, "shotGunMode", EnemyProjectileShotGunMode.ShotGun); Set(profile.Shooting, "shotGunProjectileCount", 2); Set(profile.Shooting, "shotGunDirection", Vector2.up); profile.Validate();
            fixture.Begin(profile); var result = fixture.Sector(profile); result.AddRange(fixture.Sector(profile));
            Directions(result, profile.Shooting, 2, v => v == 1);
        }
    }
    private static void Cancellation()
    {
        scenarios++; using var fixture = new Fixture(); var profile = Profile(false); fixture.Begin(profile);
        fixture.Clock(1); fixture.Executor.TryFireInDirection(Vector3.up, profile); fixture.Tick(1.0501f);
        Check(fixture.ReadAndClear().Count == 1, "Partial sector emitted");
        Call(fixture.Executor, "CancelPendingEmissions"); fixture.Begin(profile);
        Directions(fixture.Sector(profile), profile.Shooting, 1, _ => false);
        fixture.Clock(3); fixture.Executor.TryFireInDirection(Vector3.up, profile); fixture.Executor.TryFireInDirection(Vector3.up, profile);
        Check(((EnemySectorLaunchTicket)Get(fixture.Pending[0], "LaunchOrder")).Reversed && !fixture.NextReverse, "Overlap reserves alternating turns after completed sector");
        Call(fixture.Executor, "CancelPendingEmissions"); fixture.Begin(profile);
        Directions(fixture.Sector(profile), profile.Shooting, 1, _ => true);
        Call(fixture.Executor, "OnDisable"); Call(fixture.Executor, "OnEnable"); fixture.Begin(profile);
        Directions(fixture.Sector(profile), profile.Shooting, 1, _ => false);
        fixture.Clock(10); fixture.Executor.TryFireInDirection(Vector3.up, profile); fixture.Tick(10.0501f); fixture.ReadAndClear();
        fixture.Enemy.isDead = true; Call(fixture.Executor, "Update");
        Check(!fixture.Executor.HasPendingEmissions(profile), "Enemy death cancels pending Sector");
        fixture.Enemy.isDead = false; fixture.Begin(profile);
        Directions(fixture.Sector(profile), profile.Shooting, 1, _ => true);
    }
    private static void CopiesAndSources()
    {
        scenarios++; var source = Profile(false); var registry = new EnemySectorLaunchOrderRegistry();
        var copy = new EnemyBurstAttackSettings(); copy.CopyFrom(source); registry.BeginAttack(copy.Shooting.EmissionLayout);
        var first = registry.Reserve(copy.Shooting.EmissionLayout); first.Complete();
        var other = new EnemyBurstAttackSettings(); other.CopyFrom(source); registry.BeginAttack(other.Shooting.EmissionLayout);
        var next = registry.Reserve(other.Shooting.EmissionLayout); Check(next.Reversed, "Runtime copies retain source identity"); next.Complete();
        var separate = Profile(false); registry.BeginAttack(separate.Shooting.EmissionLayout); var independent = registry.Reserve(separate.Shooting.EmissionLayout);
        Check(!independent.Reversed, "Identical but distinct patterns are independent"); independent.Complete();
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var settings = new DirectedWaveAttackSettings(); settings.WaveBurstSettings.CopyFrom(source); preset.SetAttackSettings(settings);
            var x = new DirectedWaveAttackPattern(); var y = new DirectedWaveAttackPattern(); Set(x, "attackPreset", preset); Set(y, "attackPreset", preset);
            var dx = new DirectedWaveAttackSettings(); x.CopyResolvedSettingsTo(dx); var tx = registry.Reserve(dx.WaveBurstSettings.Shooting.EmissionLayout); tx.Complete();
            var dy = new DirectedWaveAttackSettings(); y.CopyResolvedSettingsTo(dy); var ty = registry.Reserve(dy.WaveBurstSettings.Shooting.EmissionLayout);
            Check(!ty.Reversed, "Different sources using one preset are independent"); ty.Cancel();
            var dx2 = new DirectedWaveAttackSettings(); x.CopyResolvedSettingsTo(dx2); registry.BeginAttack(dx2.WaveBurstSettings.Shooting.EmissionLayout);
            var tx2 = registry.Reserve(dx2.WaveBurstSettings.Shooting.EmissionLayout); Check(tx2.Reversed, "Re-resolving same source resumes order"); tx2.Cancel();
            Check(JsonUtility.ToJson(source.Shooting.EmissionLayout) == JsonUtility.ToJson(copy.Shooting.EmissionLayout), "Copies retain all serialized fields");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
        scenarios++;
        using (var fixture = new Fixture())
        {
            var local = fixture.Root.AddComponent<DirectedWaveEnemyOverride>(); Set(local, "attackOverrideVersion", 4);
            Set(local, "overrideAttackPattern", true); Set(local, "attackPattern", Profile(false));
            var sx = Profile(false); var sy = Profile(false); var scoped = new EnemySectorLaunchOrderRegistry();
            var cx = EnemyAttackSettingsResolver.CreateRuntimeCopy(sx, local, null); scoped.BeginAttack(cx.Shooting.EmissionLayout);
            var tx = scoped.Reserve(cx.Shooting.EmissionLayout); tx.Complete();
            var cy = EnemyAttackSettingsResolver.CreateRuntimeCopy(sy, local, null); scoped.BeginAttack(cy.Shooting.EmissionLayout);
            var ty = scoped.Reserve(cy.Shooting.EmissionLayout); Check(!ty.Reversed, "Shared Local Override keeps distinct attack sources independent"); ty.Cancel();
            var cx2 = EnemyAttackSettingsResolver.CreateRuntimeCopy(sx, local, null); scoped.BeginAttack(cx2.Shooting.EmissionLayout);
            var tx2 = scoped.Reserve(cx2.Shooting.EmissionLayout); Check(tx2.Reversed, "Local Override runtime copies preserve source continuity"); tx2.Cancel();
        }
    }
    private static void Children()
    {
        foreach (bool reset in new[] { true, false }) foreach (bool burst in new[] { false, true })
        {
            scenarios++; using var world = new EnemyProjectilePreviewWorld(); var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", 0f); Set(child, "repeatCount", 2); Set(child, "repeatInterval", .1f);
            var profile = Profile(reset); Set(profile, "attackShotCount", burst ? 1 : 3); Set(profile, "attackShotInterval", .3f);
            if (burst) { Set(profile, "burstAttack", EnemyBurstAttackMode.Enabled); Set(profile, "burstShotCount", 3); Set(profile, "burstShotInterval", .3f); }
            child.Pattern.CopyFrom(profile);
            var root = new EnemyShootingSettings(); root.SetChildAttack(child); var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, root, Vector3.zero, Vector3.zero, Vector3.up, 1, .01f, 0, new EnemyProjectilePrefabSet(bullet));
            world.Spawn(requests); var seen = new HashSet<Entity>(); using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
            using (var entities = query.ToEntityArray(Allocator.Temp)) foreach (var entity in entities) seen.Add(entity);
            var result = new List<Vector3>();
            for (int i = 1; i <= 250; i++)
            {
                world.Step(i * .01f, .01f, Vector3.right * 100);
                using var entities = query.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities) if (seen.Add(entity)) { var direction = world.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileMovement>(entity).ForwardDirection; result.Add(new Vector3(direction.x, direction.y)); }
            }
            Directions(result, profile.Shooting, 6, v => ((reset ? v % 3 : v) & 1) != 0);
        }
    }
    private static void SequenceController()
    {
        foreach (bool reset in new[] { true, false }) foreach (bool burst in new[] { false, true })
        {
            scenarios++; using var fixture = new Fixture(); var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
            try
            {
                var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var step = new DirectedWaveAttackSequenceStep();
                var pattern = new DirectedWaveAttackPattern(); Set(step, "pattern", pattern); var authored = (DirectedWaveAttackSettings)Get(pattern, "attackSettings");
                authored.WaveBurstSettings.CopyFrom(Profile(reset)); Set(authored.WaveBurstSettings, "attackShotCount", burst ? 1 : 3); Set(authored.WaveBurstSettings, "attackShotInterval", .3f);
                if (burst) { Set(authored.WaveBurstSettings, "burstAttack", EnemyBurstAttackMode.Enabled); Set(authored.WaveBurstSettings, "burstShotCount", 3); Set(authored.WaveBurstSettings, "burstShotInterval", .3f); }
                Set(authored, "fireMode", DirectedWaveAttackFireMode.Default); Set(authored, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
                var result = new List<Vector3>();
                for (int pass = 0; pass < 2; pass++)
                {
                    var settings = new DirectedWaveAttackSettings(); step.CopySettingsTo(settings);
                    var controller = Activator.CreateInstance(typeof(DirectedEnemySubWave).Assembly.GetType("DirectedWaveAttackController"), new object[] { wave, settings, null });
                    Call(controller, "BeginSingleCycle", 1f, new EnemyAttackCompletionTracker());
                    var profile = (EnemyBurstAttackSettings)Call(controller, "GetBurstSettings", fixture.Enemy, fixture.Executor);
                    fixture.Executor.SetWaveAttackSettings(profile); var iter = (IEnumerator)Call(controller, "FireFullAttack", fixture.Enemy, fixture.Executor, profile);
                    var stack = new Stack<IEnumerator>(); stack.Push(iter); float time = fixture.Time;
                    for (int i = 0; i < 300 && stack.Count > 0; i++)
                    {
                        fixture.Tick(time + i * .01f); result.AddRange(fixture.ReadAndClear()); Advance(stack);
                    }
                    Check(stack.Count == 0, "Native Sequence controller drains Sector before completing"); Call(controller, "Stop");
                }
                Directions(result, authored.WaveBurstSettings.Shooting, 6, v => ((reset ? v % 3 : v) & 1) != 0);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    private static void BoundaryOverlaps()
    {
        foreach (bool reset in new[] { true, false })
        {
            scenarios++; using var fixture = new Fixture(); using var world = new EnemyProjectilePreviewWorld();
            var cameraObject = new GameObject("PingPong boundary camera"); SceneManager.MoveGameObjectToScene(cameraObject, fixture.Scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.transform.position = Vector3.back * 10;
            camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
            var child = new EnemyChildAttackSettings(); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent);
            Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached); Set(child, "boundary", EnemyChildAttackBoundary.Top);
            Set(child, "triggerOnce", false); var profile = Profile(reset); Set(profile, "attackShotCount", 3); Set(profile, "attackShotInterval", .3f); child.Pattern.CopyFrom(profile);
            var shooting = new EnemyShootingSettings(); shooting.SetChildAttack(child); var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.up * 4, Vector3.zero, Vector3.up, 1, .01f, 0, new EnemyProjectilePrefabSet(bullet)); world.Spawn(requests);
            var manager = world.SimulationWorld.EntityManager; using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement));
            Entity parent; using (var entities = query.ToEntityArray(Allocator.Temp)) parent = entities[0];
            void Cross(float y, float time)
            { var pose = manager.GetComponentData<LocalTransform>(parent); pose.Position.y = y; manager.SetComponentData(parent, pose); world.Step(time, 0, Vector3.right * 100, camera); }
            Cross(6, .1f); Cross(4, .11f);
            var spawner = (EnemyProjectileEcsSpawner)Get(world, "spawner"); var runner = Get(spawner, "childAttacks");
            void VerifyTurns(bool second)
            {
                var turns = new List<bool>();
                foreach (var state in (IEnumerable)Get(runner, "parents"))
                {
                    if (Get(state, "BoundaryOwner") == null) continue;
                    var volleys = (IList)Get(state, "Volleys");
                    Check(volleys.Count == 1, "Each overlapping event owns one pending Sector");
                    turns.Add(((EnemySectorLaunchTicket)Get(volleys[0], "LaunchOrder")).Reversed);
                }
                Check(turns.Count == 2, "Two independent active event attacks");
                Check(turns[0] == (reset && second) && turns[1] == (reset ? second : true), "Overlapping child attack epochs keep their own PingPong direction");
            }
            VerifyTurns(false); world.Step(.3f, 0, Vector3.right * 100, camera); world.Step(.411f, 0, Vector3.right * 100, camera); VerifyTurns(true);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
    private static void Advance(Stack<IEnumerator> stack)
    {
        for (int guard = 0; guard < 100 && stack.Count > 0; guard++)
        { var iter = stack.Peek(); if (!iter.MoveNext()) { stack.Pop(); continue; } if (iter.Current is IEnumerator nested) stack.Push(nested); else return; }
    }
    private static void Preview()
    {
        foreach (bool sequence in new[] { false, true }) foreach (bool reset in new[] { false, true }) foreach (bool reverse in new[] { false, true })
        {
            scenarios++; var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
            try
            {
                var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var behaviour = wave.AttackPatternBehaviour;
                Set(behaviour, "attackPreset", null); ((IList)Get(behaviour, "attackPatterns")).Clear();
                Set(behaviour, "executionMode", sequence ? DirectedWaveAttackExecutionMode.Sequence : DirectedWaveAttackExecutionMode.Patterns);
                var authored = (DirectedWaveAttackSettings)Get(behaviour, "attackSettings");
                if (sequence)
                {
                    var step = new DirectedWaveAttackSequenceStep(); var pattern = new DirectedWaveAttackPattern(); Set(step, "pattern", pattern);
                    authored = (DirectedWaveAttackSettings)Get(pattern, "attackSettings"); behaviour.AttackSequence.steps = new() { step };
                    behaviour.AttackSequence.sequenceMode = DirectedWaveAttackSequenceMode.Loop; behaviour.AttackSequence.loopCount = 0;
                }
                Set(authored, "fireMode", DirectedWaveAttackFireMode.Default); Set(authored, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
                Set(authored, "attackStartDelay", 0f); Set(authored, "attacksPerSecond", 30f);
                authored.WaveBurstSettings.CopyFrom(Profile(reset, reverse)); Set(authored.WaveBurstSettings, "attackShotCount", 3); Set(authored.WaveBurstSettings, "attackShotInterval", .3f); Set(authored.WaveBurstSettings, "attackCooldown", .05f);
                using var preview = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.up * 100);
                var seen = new HashSet<Entity>(); var result = new List<Vector3>(); using var query = preview.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                Quaternion basis = Quaternion.identity;
                for (int i = 1; i <= 150 && result.Count < 18; i++)
                {
                    preview.AdvanceTo(i * WaveShootingPreviewSession.StepInterval, 1000);
                    using var entities = query.ToEntityArray(Allocator.Temp);
                    foreach (var entity in entities) if (seen.Add(entity)) { var d = preview.ProjectileWorld.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileMovement>(entity).ForwardDirection; result.Add(new Vector3(d.x, d.y)); }
                    foreach (var slot in (IEnumerable)Get(preview, "slots"))
                    { var patterns = (Array)Get(slot, "Patterns"); if (patterns.Length > 0) basis = (Quaternion)Get(patterns.GetValue(0), "Basis"); }
                }
                Directions(result, authored.WaveBurstSettings.Shooting, 6, v => reverse ^ (((reset ? v % 3 : v) & 1) != 0), basis);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
