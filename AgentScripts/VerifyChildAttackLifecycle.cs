using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyChildAttackLifecycle
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static readonly Vector3 Target = new(1000, 1000);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }
            var property = type.GetProperty(name, Flags);
            if (property != null) { property.SetValue(target, value); return; }
        }
        throw new Exception("Missing " + name);
    }
    static EnemyChildAttackSettings Child(bool infinite = false)
    {
        var child = infinite ? EnemyChildAttackSettings.CreateDefault() : new EnemyChildAttackSettings();
        Set(child, "triggerValue", 0f); Set(child, "repeatCount", 3); Set(child, "repeatInterval", .2f);
        child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .1f);
        child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        Set(child.Pattern.Shooting, "overrideProjectileLifetime", true); Set(child.Pattern.Shooting, "projectileLifetime", 20f);
        child.Validate(); return child;
    }
    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public EntityManager Manager => Preview.SimulationWorld.EntityManager;
        readonly UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public readonly EnemyProjectileEcsSpawner Spawner;
        public Context()
        {
            var go = new GameObject("Child lifecycle test camera") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Camera = go.AddComponent<Camera>(); Camera.enabled = false; Camera.orthographic = true;
            Camera.orthographicSize = 5; Camera.aspect = 1; Camera.transform.position = new Vector3(0, 0, -10);
            Spawner = (EnemyProjectileEcsSpawner)typeof(EnemyProjectilePreviewWorld).GetField("spawner", Flags).GetValue(Preview);
        }
        public Entity Spawn(EnemyChildAttackSettings child, bool screen = false, EnemyAttackCompletionTracker completion = null)
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var shooting = new EnemyShootingSettings(); shooting.SetChildAttack(child);
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(screen));
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 100f);
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.zero, Vector3.zero,
                Vector3.right, 1, 1, 0, new EnemyProjectilePrefabSet(bullet), completionTracker: completion);
            Preview.Spawn(requests);
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            using var entities = query.ToEntityArray(Allocator.Temp); return entities[entities.Length - 1];
        }
        public void Step(float time, float delta = .05f) => Preview.Step(time, delta, Target, Camera);
        public void RuntimeStep(float time)
        {
            var world = Preview.SimulationWorld;
            world.SetTime(new Unity.Core.TimeData(time, .05f));
            world.GetOrCreateSystem<ProjectileLifetimeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystemManaged<EnemyChildAttackSystem>().Update();
            world.GetOrCreateSystemManaged<EnemyChildAttackCleanupSystem>().Update();
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileResolution));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                if (Manager.GetComponentData<EnemyProjectileResolution>(entity).Kind != EnemyProjectileResolutionKind.None) Manager.DestroyEntity(entity);
        }
        public void Move(Entity entity, Vector3 position)
        { var pose = Manager.GetComponentData<LocalTransform>(entity); pose.Position = new float3(position.x, position.y, position.z); Manager.SetComponentData(entity, pose); }
        public void Dispose() { Preview.Dispose(); UnityEngine.Object.DestroyImmediate(Camera.gameObject); EditorSceneManager.ClosePreviewScene(scene); }
    }
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        DefaultsAndInspector(); Repetition(); Cancellation(); DelayedPattern(); BoundaryDelay(); DeathEvent(); Nested(); Unbounded(); RuntimeParity();
        Check(World.All.Count == worlds, "All owned ECS worlds disposed");
        return new { passed = true, checks, repeatModes = true, cancellation = true, nested = true, beyond2048Repetitions = true, runtimeSystemParity = true };
    }
    public static object RunPlayProbe()
    { Check(Application.isPlaying, "Play Mode required"); checks = 0; RuntimeParity(); Cancellation(); BoundaryDelay(); return new { passed = true, checks, playMode = true }; }
    public static object SeekProbe(bool infinite = true)
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Child seek verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var go = new GameObject("Single emitter"); go.transform.SetParent(root.transform, false);
            var subwave = go.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "spawnInterval", 0f); Set(subwave, "settleDuration", 0f);
            var behaviour = go.GetComponent<DirectedWaveAttackBehaviour>() ?? go.AddComponent<DirectedWaveAttackBehaviour>();
            Set(behaviour, "allowAutonomousAttackDuringEntrance", false);
            var settings = (DirectedWaveAttackSettings)behaviour.GetType().GetField("attackSettings", Flags).GetValue(behaviour);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(180);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 10f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 1f, 10f);
            var shooting = settings.WaveBurstSettings.Shooting;
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            shooting.SetProjectileBaseSpeedOverride(true, 1f);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", .35f);
            var child = Child(infinite); Set(child.Pattern, "attackShotCount", 4); Set(child.Pattern, "attackShotInterval", .15f);
            child.Validate(); shooting.SetChildAttack(child); settings.Validate();
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(go, 0) });
            using var player = new WavePreviewPlayer(wave, 8, Target); player.SnapshotInterval = .25f;
            void Seek(float time) { player.Seek(time); for (int i = 0; player.IsSeeking; i++) { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); } }
            string Capture()
            {
                var m = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = m.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp); var rows = new List<string>();
                foreach (var entity in entities) rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(entity)) + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(entity)));
                rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
            }
            Seek(4f); float spawn = -1;
            foreach (var e in player.Events) if (e.Kind == WavePreviewEventKind.ProjectileSpawn) { spawn = e.Time; break; }
            Check(spawn >= 0, "Actual parent spawn observed");
            Seek(spawn + .2f); Check(player.Simulation.ProjectileWorld.Count > 1, "Snapshot interval contains live parent and child work");
            Seek(spawn + .8f); string expected = Capture();
            int count = player.Simulation.ProjectileWorld.Count;
            Check(count > 0, "Children survive after parent lifetime");
            var spawner = (EnemyProjectileEcsSpawner)typeof(EnemyProjectilePreviewWorld).GetField("spawner", Flags).GetValue(player.Simulation.ProjectileWorld);
            Check(spawner.ActiveChildAttackParents == 0, "No dead-parent work after Fast Seek");
            Seek(spawn + .2f); Seek(spawn + .8f);
            Check(Capture() == expected && spawner.ActiveChildAttackParents == 0, "Rewind restores exact children without ghost launches");
            Check(player.RestoredSnapshots > 0 && !player.IsPlaying, "Snapshot restore and paused transport exercised");
            return new { passed = true, checks, infinite, childrenAfterDeath = count, snapshotRestores = player.RestoredSnapshots, fastSeek = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void DefaultsAndInspector()
    {
        var legacy = JsonUtility.FromJson<EnemyChildAttackSettings>("{\"repeatCount\":7,\"repeatInterval\":0.6}");
        Check(legacy.RepeatMode == EnemyChildAttackRepeatMode.FixedCount && legacy.RepeatCount == 7, "Missing repeat mode preserves old fixed count");
        var fresh = EnemyChildAttackSettings.CreateDefault();
        Check(fresh.RepeatsUntilParentDestroyed, "New authoring defaults to until destroyed");
        Check(EnemyChildAttackSettings.Copy(fresh).RepeatsUntilParentDestroyed, "Copy preserves mode");
        Check(JsonUtility.FromJson<EnemyChildAttackSettings>(JsonUtility.ToJson(fresh)).RepeatsUntilParentDestroyed, "JSON preserves mode");
        var preset = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            var settings = new EnemyShootingSettings(); settings.SetChildAttack(fresh);
            // SerializedObject verifies the actual managed-reference drawer field list.
            var holder = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
            try
            {
                var attack = new DirectedWaveAttackSettings(); attack.WaveBurstSettings.Shooting.SetChildAttack(fresh); holder.SetAttackSettings(attack);
                var serialized = new SerializedObject(holder);
                var iterator = serialized.GetIterator(); SerializedProperty child = null;
                while (iterator.Next(true)) if (iterator.name == "childAttack" && iterator.propertyType == SerializedPropertyType.ManagedReference && iterator.managedReferenceValue != null) { child = iterator.Copy(); break; }
                Check(child != null, "Preset contains child settings");
                var fields = typeof(EnemyChildAttackSettingsDrawer).GetMethod("Fields", BindingFlags.Static | BindingFlags.NonPublic);
                bool HasCount() { foreach (string field in (IEnumerable)fields.Invoke(null, new object[] { child })) if (field == "repeatCount") return true; return false; }
                Check(!HasCount(), "Infinite Inspector hides Repeat Count");
                child.FindPropertyRelative("repeatMode").intValue = 0;
                Check(HasCount(), "Fixed Inspector displays Repeat Count");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
    }
    static void Repetition()
    {
        foreach (bool infinite in new[] { false, true })
        {
            using var c = new Context(); c.Spawn(Child(infinite));
            for (int i = 1; i <= 21; i++) c.Step(i * .05f);
            Check(infinite ? c.Preview.Count > 4 : c.Preview.Count == 4, "Fixed/infinite repetition while alive: " + infinite);
        }
    }
    static void Cancellation()
    {
        foreach (bool infinite in new[] { false, true })
        foreach (int cause in new[] { 0, 1, 2, 3, 4 })
        {
            using var c = new Context(); var tracker = new EnemyAttackCompletionTracker();
            var parent = c.Spawn(Child(infinite), completion: tracker); c.Step(.05f);
            Check(c.Preview.Count == 2, "Initial child created");
            using var query = c.Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            using var entities = query.ToEntityArray(Allocator.Temp);
            Entity survivor = entities[0] == parent ? entities[1] : entities[0];
            float3 before = c.Manager.GetComponentData<LocalTransform>(survivor).Position;
            if (cause == 0) c.Manager.DestroyEntity(parent);
            else if (cause == 1) c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = .01f });
            else c.Manager.SetComponentData(parent, new EnemyProjectileResolution { Kind = cause == 2 ? EnemyProjectileResolutionKind.HitPlayer : cause == 3 ? EnemyProjectileResolutionKind.Intercepted : EnemyProjectileResolutionKind.Expired });
            c.Step(.1f); c.Step(.6f);
            Check(!c.Manager.Exists(parent) && c.Preview.Count == 1, "No emission after parent destruction: " + infinite + "/" + cause);
            Check(math.distance(before, c.Manager.GetComponentData<LocalTransform>(survivor).Position) > .001f, "Previously emitted child continues moving");
            Check(c.Spawner.ActiveChildAttackParents == 0 && tracker.IsComplete, "Queues and completion released");
        }
    }
    static void DelayedPattern()
    {
        foreach (bool infinite in new[] { false, true })
        {
            using var c = new Context(); var child = Child(infinite);
            Set(child.Pattern, "attackShotCount", 3); Set(child.Pattern, "attackShotInterval", .2f);
            child.Pattern.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, 0f, .1f);
            child.Validate(); var parent = c.Spawn(child); c.Step(.05f);
            Check(c.Preview.Count == 2, "First sequential direction emitted");
            c.Manager.DestroyEntity(parent); c.Step(.2f); c.Step(1f);
            Check(c.Preview.Count == 1 && c.Spawner.ActiveChildAttackParents == 0, "Remaining shots and sequential directions cancelled");
        }
        using (var c = new Context())
        {
            var child = Child(true); Set(child, "destroyParent", true); var parent = c.Spawn(child);
            c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = .1f });
            c.Step(.05f); c.Step(.1f); c.Step(.5f);
            Check(!c.Manager.Exists(parent) && c.Preview.Count == 1, "Infinite mode never holds parent lifetime indefinitely");
        }
    }
    static void BoundaryDelay()
    {
        using (var c = new Context())
        {
            var parent = c.Spawn(Child(true), true); c.Step(.05f); c.Move(parent, new Vector3(9, 0)); c.Step(.1f);
            int children = c.Preview.Count; c.Step(.5f); c.Step(1f);
            Check(!c.Manager.Exists(parent) && c.Preview.Count == children && c.Spawner.ActiveChildAttackParents == 0, "Screen exit cancels pending repeat in same step");
        }
        using (var c = new Context())
        {
            var child = Child(true); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached);
            child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Right); Set(child, "timeOffset", .5f);
            var parent = c.Spawn(child, true); c.Step(.05f); c.Move(parent, new Vector3(7, 0)); c.Step(.1f);
            Check(c.Spawner.ActiveChildAttackParents == 2 && c.Preview.Count == 1, "Boundary observer and delayed occurrence pending");
            c.Manager.SetComponentData(parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Intercepted });
            c.Step(.2f); c.Step(1f);
            Check(c.Preview.Count == 0 && c.Spawner.ActiveChildAttackParents == 0, "Death during Time Offset cancels boundary event");
        }
    }
    static void DeathEvent()
    {
        using var c = new Context(); var child = Child(true);
        Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.Expired);
        Set(child.Pattern, "attackShotCount", 3); Set(child.Pattern, "attackShotInterval", .2f); child.Validate();
        var parent = c.Spawn(child, true); c.Step(.05f); c.Move(parent, new Vector3(9, 0)); c.Step(.1f);
        Check(!c.Manager.Exists(parent) && c.Preview.Count == 1, "Expired on screen exit emits immediate shot before cleanup");
        c.Step(.5f); c.Step(1f);
        Check(c.Preview.Count == 1 && c.Spawner.ActiveChildAttackParents == 0, "Expired never leaves delayed work");
    }
    static void Nested()
    {
        using var c = new Context(); var child = Child(); var grandchild = Child();
        Set(grandchild, "triggerValue", .2f); Set(grandchild, "repeatCount", 1);
        child.Pattern.Shooting.SetChildAttack(grandchild); var parent = c.Spawn(child); c.Step(.05f);
        c.Manager.DestroyEntity(parent); c.Step(.1f); c.Step(.3f);
        Check(c.Preview.Count == 2, "Already emitted child's own attack survives ancestor death");
    }
    static void Unbounded()
    {
        using var c = new Context(); var child = Child(true); Set(child, "repeatInterval", 0f);
        Set(child.Pattern.Shooting, "projectileLifetime", .01f); c.Spawn(child);
        for (int i = 1; i <= 2100; i++) c.Step(i * .02f, .02f);
        Check(c.Spawner.PerformanceSpawnedCount > 2048 && c.Spawner.ActiveChildAttackParents == 1, "Infinite emitter continues beyond old family/count cap");
        Check(c.Preview.Count <= 3, "Expired descendants do not accumulate");
    }
    static void RuntimeParity()
    {
        foreach (bool infinite in new[] { false, true })
        {
            using var c = new Context(); var parent = c.Spawn(Child(infinite));
            c.RuntimeStep(.05f); Check(c.Preview.Count == 2, "Runtime ECS system emits first child");
            c.RuntimeStep(.3f); c.RuntimeStep(.55f); c.RuntimeStep(.8f); c.RuntimeStep(1.05f);
            Check(infinite ? c.Preview.Count > 4 : c.Preview.Count == 4, "Runtime repeat modes while parent alive");
            int children = c.Preview.Count - 1;
            c.Manager.SetComponentData(parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
            c.RuntimeStep(1.1f); c.RuntimeStep(1.5f);
            Check(c.Preview.Count == children && c.Spawner.ActiveChildAttackParents == 0, "Runtime ECS system cancels as Preview");
        }
    }
}
