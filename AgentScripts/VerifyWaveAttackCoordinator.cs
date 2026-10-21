using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyWaveAttackCoordinator
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private sealed class Member : IWaveAttackCoordinatorParticipant
    {
        public WaveAttackParticipantState State { get; set; } = WaveAttackParticipantState.Ready;
        public int CompletedCycles { get; set; }
        public bool IsIdle { get; set; } = true;
        public WaveAttackPermissions Permissions = new();
        public int Restarts, Resumes, Limit;
        public void SetPermission(object owner, WaveAttackPermission state) => Permissions.Set(owner, state);
        public void ReleasePermission(object owner) => Permissions.Release(owner);
        public void SetCycleLimit(int value) => Limit = value;
        public void Reactivate(WaveAttackReactivation mode)
        { if (mode == WaveAttackReactivation.Restart) { Restarts++; CompletedCycles = 0; } else Resumes++; }
    }
    public static object Run()
    {
        checks = 0;
        Pure(); Preview(); RuntimeBurst();
        return new { passed = true, checks, ownership = true, overlap = true, preview = true, runtimeInterruptResume = true };
    }
    private static WaveAttackCoordinatorGroup Group(WaveAttackGroupCompletion mode, params int[] members)
    {
        var group = new WaveAttackCoordinatorGroup { completionMode = mode, duration = 1f };
        foreach (int index in members) group.controlledSubWaves.Add(new WaveAttackSubWaveReference { scheduleIndex = index });
        return group;
    }
    private static void Pure()
    {
        var a = new Member(); var b = new Member(); var c = new Member(); var members = new[] { a, b, c };
        var settings = new WaveAttackCoordinatorSettings { loopSequence = false, switchMode = WaveAttackSwitchMode.Interrupt };
        var first = Group(WaveAttackGroupCompletion.Timed, 0, 1); first.overlapDuration = .3f;
        settings.groups = new() { first, Group(WaveAttackGroupCompletion.Timed, 1, 2) };
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => i >= 0 && i < members.Length ? members[i] : null))
        {
            player.Begin(0); Check(!a.Permissions.AllowNewAttacks && !c.Permissions.AllowNewAttacks, "All controlled participants initially wait");
            player.Tick(0); Check(a.Permissions.AllowNewAttacks && b.Permissions.AllowNewAttacks && !c.Permissions.AllowNewAttacks, "First group simultaneous");
            player.Tick(.69f); Check(!c.Permissions.AllowNewAttacks, "No premature next group");
            player.Tick(.71f); Check(c.Permissions.AllowNewAttacks && a.Permissions.AllowNewAttacks, "Timed overlap starts next group");
            Check(b.Restarts == 1, "Shared participant is not restarted during overlap");
            player.Tick(1.01f); Check(a.Permissions.Interrupted && b.Permissions.AllowNewAttacks, "Outgoing lease cannot block shared participant");
            player.Tick(1.8f); Check(player.IsComplete, "Finite sequence completes");
        }
        Check(a.Permissions.AllowNewAttacks && c.Permissions.AllowNewAttacks, "Dispose releases all permissions");
        var blocker = new object(); a.Permissions.Set(blocker, WaveAttackPermission.InterruptAttacks);
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        { player.Begin(0); player.Tick(0); Check(a.Permissions.Interrupted && b.Permissions.AllowNewAttacks, "Independent owner block wins"); }
        Check(a.Permissions.Interrupted, "Coordinator cleanup preserves another owner's block"); a.Permissions.Release(blocker);

        first = Group(WaveAttackGroupCompletion.AttackCycles, 0, 1); first.attackCyclesCount = 2;
        settings.groups = new() { first, Group(WaveAttackGroupCompletion.Timed, 2) };
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        {
            player.Begin(0); player.Tick(0); Check(a.Limit == 2 && b.Limit == 2, "Per participant finite cycle budget");
            a.CompletedCycles = 2; b.CompletedCycles = 1; player.Tick(.1f); Check(!c.Permissions.AllowNewAttacks, "Waits for every participant's cycles");
            b.State = WaveAttackParticipantState.Gone; player.Tick(.2f); Check(c.Permissions.AllowNewAttacks, "Dead participant cannot block completion");
        }
        b.State = WaveAttackParticipantState.Ready;
        first.completionMode = WaveAttackGroupCompletion.FirstCompleted;
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        { player.Begin(0); player.Tick(0); b.CompletedCycles = 1; player.Tick(.1f); Check(c.Permissions.AllowNewAttacks, "First completed participant advances"); }
        first.completionMode = WaveAttackGroupCompletion.AllCompleted;
        first.waitUntilReady = true; first.maxWaitDuration = .5f; a.State = WaveAttackParticipantState.Pending;
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        {
            player.Begin(0); player.Tick(0); Check(!a.Permissions.AllowNewAttacks, "Wait Until Ready holds group");
            player.Tick(.51f); Check(player.Timeouts == 1 && c.Permissions.AllowNewAttacks, "Readiness timeout advances safely");
        }
        a.State = WaveAttackParticipantState.Ready; a.IsIdle = false;
        first = Group(WaveAttackGroupCompletion.Timed, 0); first.duration = .1f; first.overlapDuration = .3f;
        settings.switchMode = WaveAttackSwitchMode.FinishCurrentAttack;
        settings.groups = new() { first, Group(WaveAttackGroupCompletion.Timed, 2) };
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        {
            player.Begin(0); player.Tick(0); player.Tick(.11f);
            Check(a.Permissions.State == WaveAttackPermission.BlockNewAttacks && c.Permissions.AllowNewAttacks, "Draining old burst overlaps next participant");
            player.Tick(.42f); Check(a.Permissions.Interrupted, "Overlap has a bounded drain timeout");
        }
        a.IsIdle = true;
        settings.groups = new() { Group(WaveAttackGroupCompletion.Timed, 0) };
        settings.loopSequence = false; settings.loopCount = 2; settings.reactivation = WaveAttackReactivation.Resume;
        settings.switchMode = WaveAttackSwitchMode.Interrupt;
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        { player.Begin(0); player.Tick(0); player.Tick(1.1f); player.Tick(2.2f); Check(player.IsComplete && a.Resumes == 2, "Finite loop count and Resume reactivation"); }
        var clock = new EnemyAttackClock(); clock.Tick(0, 1); Check(clock.Tick(1, 1) == 1, "Normal attack clock");
        Check(clock.Tick(10, 0) == 1 && clock.Tick(11, 1) == 2, "Interrupt freezes clock without catchup");
        var restored = JsonUtility.FromJson<WaveAttackCoordinatorSettings>(JsonUtility.ToJson(settings));
        Check(restored.groups[0].controlledSubWaves[0].scheduleIndex == 0 && restored.loopCount == 2, "Settings round trip");
    }
    private static void Preview()
    {
        int worldCount = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Coordinator verification"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var conductor = root.AddComponent<Wave>(); conductor.SubWavesToCreate = new();
            var waves = new List<DirectedEnemySubWave>();
            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("SubWave " + i); child.transform.SetParent(root.transform);
                var wave = child.AddComponent<DirectedEnemySubWave>(); waves.Add(wave);
                var attack = child.AddComponent<DirectedWaveAttackBehaviour>();
                Set(wave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
                Set(wave, "enemyCount", 1); Set(wave, "spawnInterval", 0f); Set(wave, "postStartDelay", 0f);
                Set(Get(wave, "phaseEntry"), "enabled", false); Set(Get(wave, "postBehaviour"), "enabled", false);
                var settings = (DirectedWaveAttackSettings)Get(attack, "attackSettings");
                Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(180);
                Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
                settings.WaveBurstSettings.ConfigureLegacySingleAttack(3, .2f, .1f);
                settings.WaveBurstSettings.Shooting.ConfigureLegacyFan(1, 0, 0);
                settings.WaveBurstSettings.Shooting.SetProjectileBaseSpeedOverride(true, .1f);
                Set(settings.WaveBurstSettings.Shooting, "overrideProjectileLifetime", true); Set(settings.WaveBurstSettings.Shooting, "projectileLifetime", 100f);
                settings.Validate(); conductor.SubWavesToCreate.Add(child);
            }
            var coordinator = root.AddComponent<WaveAttackCoordinator>();
            coordinator.Settings.loopSequence = false; coordinator.Settings.switchMode = WaveAttackSwitchMode.Interrupt;
            coordinator.Settings.groups = new() { Group(WaveAttackGroupCompletion.AttackCycles, 0, 1), Group(WaveAttackGroupCompletion.Timed, 2) };
            using (var preview = new WaveConductorShootingPreview(conductor, Vector3.down * 4))
            {
                Advance(preview, .2f); Check(preview.GetSession(0).ProjectileCount > 0, "Conductor creates actual projectiles; " + preview.GetSession(0).CoordinatorStatus + "; post=" + Get(preview.GetSession(0), "postStart") + "; warning=" + preview.Warning + "; active=" + preview.CoordinatorPlayer.ActiveGroups[0].Status);
                Check(preview.GetSession(2).CoordinatorStatus.Contains("waiting"), "Later participant waits");
                Advance(preview, 2f); Check(preview.CoordinatorPlayer.IsComplete, "Native group cycle completion drives conductor Preview");
                Check(preview.GetSession(0).CompletedCycles == 1 && preview.GetSession(1).CompletedCycles == 1, "Every group participant executes exactly one full cycle");
            }
            var wave0 = waves[0]; var owner = new object();
            using (var preview = new WaveShootingPreviewSession(wave0, true, true, 0, Vector3.down * 4))
            {
                preview.SetCycleLimit(1); preview.SetPermission(owner, WaveAttackPermission.AllowAttack);
                Advance(preview, .1f); int count = preview.ProjectileCount;
                Vector3 before = ProjectilePosition(preview.ProjectileWorld);
                preview.SetPermission(owner, WaveAttackPermission.InterruptAttacks); Advance(preview, 1.1f);
                Check(preview.ProjectileCount == count, "Interrupted Preview emits no future burst shots and preserves existing projectiles");
                Check((ProjectilePosition(preview.ProjectileWorld) - before).sqrMagnitude > .001f, "Released projectiles keep moving during interruption");
                preview.SetPermission(owner, WaveAttackPermission.AllowAttack); Advance(preview, 1.8f);
                Check(preview.CompletedCycles == 1 && preview.ProjectileCount == 3, "Resume completes remaining shots without restarting");
            }
            var attack0 = wave0.AttackPatternBehaviour;
            Set(attack0, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            var step = new DirectedWaveAttackSequenceStep();
            var pattern = (DirectedWaveAttackSettings)Get(attack0, "attackSettings");
            Set(step.pattern, "attackSettings", pattern);
            attack0.AttackSequence.steps = new() { step };
            foreach (var mode in new[] { DirectedWaveAttackSequenceMode.Once, DirectedWaveAttackSequenceMode.Loop })
            {
                attack0.AttackSequence.sequenceMode = mode; attack0.AttackSequence.loopCount = 0;
                using var preview = new WaveShootingPreviewSession(wave0, true, true, 0, Vector3.down * 4);
                preview.SetCycleLimit(3); preview.SetPermission(owner, WaveAttackPermission.AllowAttack);
                Advance(preview, 4f);
                Check(preview.CompletedCycles == 3 && preview.IsIdle, "Three bounded Sequence traversals: " + mode);
                Check(preview.ProjectileCount == 9, "Sequence preserves three volleys per traversal: " + mode);
            }
            Set(attack0, "executionMode", DirectedWaveAttackExecutionMode.Patterns);
            pattern.AttackOrder.enabled = true; pattern.AttackOrder.loopOrder = false;
            using (var preview = new WaveShootingPreviewSession(wave0, true, true, 0, Vector3.down * 4))
            {
                preview.SetCycleLimit(2); preview.SetPermission(owner, WaveAttackPermission.AllowAttack); Advance(preview, 3f);
                Check(preview.CompletedCycles == 2 && preview.ProjectileCount == 6, "Coordinator explicit cycles work with non-looping Attack Order");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worldCount, "Repeated Preview cleanup leaves no ECS worlds");
    }
    private sealed class Executor : IEnemyBurstAttackExecutor, IEnemyPendingEmissionExecutor
    {
        public EnemyBurstAttackSettings BurstAttackSettings { get; } = new();
        public bool CanPerformWaveAttack => true;
        public int Shots; public bool Pending = false;
        public void SetWaveAttackControl(bool value) { }
        public bool TryFireAt(Vector3 position, EnemyBurstAttackSettings settings) { Shots++; return true; }
        public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) { Shots++; return true; }
        public bool HasPendingEmissions(EnemyBurstAttackSettings settings) => Pending;
    }
    private static void RuntimeBurst()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        var actor = new GameObject("Coordinator runtime verification"); actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, root.scene);
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var attack = wave.AttackPatternBehaviour;
            var enemy = actor.AddComponent<Enemy>(); Set(enemy, "AttackPermissions", attack.AttackPermissions);
            var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
            var type = typeof(Wave).Assembly.GetType("DirectedWaveAttackController");
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null });
            var executor = new Executor(); executor.BurstAttackSettings.ConfigureLegacySingleAttack(3, .5f, .2f);
            var routine = (IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, executor.BurstAttackSettings);
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            Clock(enemy, 0); Advance(stack); Check(executor.Shots == 1, "Runtime first burst shot");
            var owner = new object(); attack.SetExternalAttackPermission(owner, WaveAttackPermission.InterruptAttacks);
            Clock(enemy, .3f); Advance(stack); Check(executor.Shots == 1, "Runtime interrupt preserves iterator and blocks next shot");
            attack.SetExternalAttackPermission(owner, WaveAttackPermission.AllowAttack); Advance(stack);
            Check(executor.Shots == 2, "Runtime resume emits next shot instead of restarting");
            attack.SetExternalAttackPermission(owner, WaveAttackPermission.BlockNewAttacks);
            Clock(enemy, .6f); Advance(stack); Check(executor.Shots == 3 && stack.Count == 0, "Block New Attacks lets the current burst finish");
            var ready = (Dictionary<Enemy, float>)Get(controller, "nextAttackReadyTimes");
            Check(Mathf.Abs(ready[enemy] - 1.1f) < .001f, "Authored cooldown unchanged");
            actor.SetActive(true); Set(controller, "cyclePrepared", true); Invoke(controller, "SetCycleLimit", 1);
            Check((int)type.GetProperty("CompletedCycles").GetValue(controller) == 0, "Cycle includes cooldown");
            Clock(enemy, 1.2f); Check((int)type.GetProperty("CompletedCycles").GetValue(controller) == 1, "Cycle is counted once after actual completion");
            attack.ReleaseExternalAttackPermission(owner); Invoke(controller, "Stop");
            using (var world = new EnemyProjectilePreviewWorld())
            {
                actor.SetActive(false);
                var simple = actor.AddComponent<SimpleEnemyAttackExecutor>();
                Set(simple, "defaultProjectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>());
                Set(simple, "projectileSpawner", Get(world, "spawner")); Invoke(simple, "Awake"); actor.SetActive(true);
                var profile = new EnemyBurstAttackSettings(); profile.Shooting.ConfigureLegacyFan(1, 0, 0);
                var layout = profile.Shooting.EmissionLayout; layout.Configure(EnemyEmissionLayoutType.Sector, 3);
                Set(layout, "launchMode", EnemySectorLaunchMode.Sequential); Set(layout, "startDelay", .1f); Set(layout, "launchInterval", .2f);
                Clock(enemy, 0f); simple.BeginEmissionOriginAttack(profile); simple.TryFireInDirection(Vector3.down, profile);
                Check(world.Count == 0 && simple.HasPendingEmissions(profile), "Sequential Sector starts with its authored delay");
                Clock(enemy, .2f); Invoke(simple, "Update"); Check(world.Count == 1, "First delayed Sector launch");
                attack.SetExternalAttackPermission(owner, WaveAttackPermission.InterruptAttacks);
                Clock(enemy, 1f); Invoke(simple, "Update"); Check(world.Count == 1 && simple.HasPendingEmissions(profile), "Interrupt holds queued Sector launches");
                attack.SetExternalAttackPermission(owner, WaveAttackPermission.AllowAttack);
                Clock(enemy, .4f); Invoke(simple, "Update"); Check(world.Count == 2, "Resume preserves Sector cursor");
                Clock(enemy, .6f); Invoke(simple, "Update"); Check(world.Count == 3 && !simple.HasPendingEmissions(profile), "Sector finishes without recreating its projectiles");
                attack.ReleaseExternalAttackPermission(owner);
            }
            Set(attack, "executionMode", DirectedWaveAttackExecutionMode.Patterns);
            Set(attack, "attackPatterns", new List<DirectedWaveAttackPattern> { new(), new() });
            var controllers = (IList)Get(attack, "attackPatternControllers"); controllers.Clear();
            var x = Activator.CreateInstance(type, new object[] { wave, settings, null });
            var y = Activator.CreateInstance(type, new object[] { wave, settings, null });
            Set(x, "completedCycles", 3); Set(y, "completedCycles", 1); controllers.Add(x); controllers.Add(y);
            attack.ReactivateCoordinatedAttack(WaveAttackReactivation.Resume);
            Check(attack.CompletedAttackCycles == 0, "Resume starts a new observation epoch for every independent Attack Group");
            Set(x, "completedCycles", 4); Set(y, "completedCycles", 2);
            Check(attack.CompletedAttackCycles == 1, "Multiple Attack Groups each contribute a fresh full cycle");
            attack.SetCoordinatedCycleLimit(2);
            Check((int)Get(x, "cycleLimit") == 5 && (int)Get(y, "cycleLimit") == 3, "Per-group budgets retain different saved progress");
            controllers.Clear();
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Advance(WaveConductorShootingPreview preview, float time) { while (!preview.AdvanceTo(time, 1000d)) { } }
    private static void Advance(WaveShootingPreviewSession preview, float time) { while (!preview.AdvanceTo(time, 1000d)) { } }
    private static void Advance(Stack<IEnumerator> stack)
    { for (int i = 0; i < 100 && stack.Count > 0; i++) { var current = stack.Peek(); if (!current.MoveNext()) { stack.Pop(); continue; } if (current.Current is IEnumerator nested) stack.Push(nested); else return; } }
    private static void Clock(Enemy enemy, float time)
    { object clock = new EnemyAttackClock(); Set(clock, "initialized", true); Set(clock, "previousTime", Time.time); Set(clock, "attackTime", time); Set(enemy, "attackClock", clock); }
    private static Vector3 ProjectilePosition(EnemyProjectilePreviewWorld world)
    { using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(EnemyProjectileMovement)); using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp); return transforms.Length > 0 ? (Vector3)transforms[0].Position : Vector3.zero; }
    private static object Get(object value, string name)
    { for (Type type = value.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, Flags); if (field != null) return field.GetValue(value); var property = type.GetProperty(name, Flags); if (property != null) return property.GetValue(value); } throw new Exception(name); }
    private static void Set(object value, string name, object data)
    { for (Type type = value.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, Flags); if (field != null) { field.SetValue(value, data); return; } var property = type.GetProperty(name, Flags); if (property != null) { property.SetValue(value, data); return; } } throw new Exception(name); }
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
}
