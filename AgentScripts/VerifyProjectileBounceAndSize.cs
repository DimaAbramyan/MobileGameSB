using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyProjectileBounceAndSize
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static readonly Vector3 Target = new(1000, 1000);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object value, string name, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var f = type.GetField(name, Flags); if (f != null) { f.SetValue(value, data); return; }
            var p = type.GetProperty(name, Flags); if (p != null) { p.SetValue(value, data); return; }
        }
        throw new Exception("Missing " + name);
    }
    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public EntityManager Manager => Preview.SimulationWorld.EntityManager;
        readonly UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public Context()
        {
            var go = new GameObject("Bounce test camera") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Camera = go.AddComponent<Camera>(); Camera.enabled = false; Camera.orthographic = true;
            Camera.orthographicSize = 5; Camera.aspect = 1; Camera.transform.position = new Vector3(0, 0, -10);
        }
        public Entity Spawn(Vector3 position, Vector3 direction, EnemyShootingSettings settings = null, float speed = 2)
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            settings ??= Shooting(); settings.SetProjectileBaseSpeedOverride(true, speed); settings.Validate();
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, settings, position, position, direction, 1, speed, 0, new EnemyProjectilePrefabSet(bullet));
            Preview.Spawn(requests);
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = query.ToEntityArray(Allocator.Temp);
            var entity = entities[entities.Length - 1];
            var data = Manager.GetComponentData<EnemyProjectileStaticData>(entity); data.Radius = .2f; Manager.SetComponentData(entity, data);
            return entity;
        }
        public float2 Position(Entity entity) => Manager.GetComponentData<LocalTransform>(entity).Position.xy;
        public float2 Velocity(Entity entity) => Manager.GetComponentData<EnemyProjectileVelocity>(entity).Value;
        public void Step(float time, float delta) => Preview.Step(time, delta, Target, Camera);
        public void Dispose() { Preview.Dispose(); UnityEngine.Object.DestroyImmediate(Camera.gameObject); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static EnemyShootingSettings Shooting(EnemyChildAttackBoundaries edges = EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right, bool destroy = true, bool bounce = true)
    {
        var settings = new EnemyShootingSettings(); settings.SetScreenBoundary(new EnemyProjectileScreenSettings(destroy, bounce: bounce, edges: edges));
        Set(settings, "overrideProjectileLifetime", true); Set(settings, "projectileLifetime", 100f); return settings;
    }
    static EnemyChildAttackSettings Child()
    {
        var child = EnemyChildAttackSettings.CreateDefault(); Set(child, "triggerValue", 0f); Set(child, "repeatInterval", .1f);
        child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .1f); return child;
    }
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        Size(); Walls(); Patterns(); BoundaryChild(); Defaults(); Limits();
        Check(World.All.Count == worlds, "Owned ECS worlds freed");
        return new { passed = true, checks, zeroSize = true, bounce = true, patterns = true, boundaryChildren = true };
    }
    public static object RunPlayProbe()
    { Check(Application.isPlaying, "Play Mode required"); return Run(); }
    public static object SeekProbe() => SeekProbeInternal(false);
    public static object SeekLimitProbe() => SeekProbeInternal(true);
    static object SeekProbeInternal(bool limited)
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Bounce snapshot verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var go = new GameObject("One moving projectile"); go.transform.SetParent(root.transform, false);
            var subwave = go.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "spawnInterval", 0f); Set(subwave, "settleDuration", 0f);
            var attack = go.GetComponent<DirectedWaveAttackBehaviour>() ?? go.AddComponent<DirectedWaveAttackBehaviour>();
            var settings = (DirectedWaveAttackSettings)attack.GetType().GetField("attackSettings", Flags).GetValue(attack);
            Set(attack, "allowAutonomousAttackDuringEntrance", false);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(135);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 20f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 1f, 20f);
            var shooting = settings.WaveBurstSettings.Shooting;
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(!limited, bounce: true, edges: EnemyChildAttackBoundaries.All, overrideBounceTime: limited));
            shooting.SetProjectileBaseSpeedOverride(true, 8f);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 20f);
            Set(shooting, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
            Set(shooting, "projectileSizeChangeY", 0f); Set(shooting, "projectileFinalSize", 0f); Set(shooting, "projectileSizeChangeSpeed", .3f);
            settings.Validate(); Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(go, 0) });
            using var player = new WavePreviewPlayer(wave, 8, Target); player.SnapshotInterval = .25f;
            void Seek(float time) { player.Seek(time); for (int i = 0; player.IsSeeking; i++) { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); } }
            string Capture()
            {
                var m = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = m.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp); var rows = new List<string>();
                foreach (var e in entities) rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileVelocity>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileScreenState>(e)));
                rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
            }
            Seek(3f); string expected = Capture();
            if (limited)
            {
                var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileScreenState));
                using var states = query.ToComponentDataArray<EnemyProjectileScreenState>(Allocator.Temp);
                Check(states.Length > 0 && states[0].CompletedBounces == 1, "Limited bounce count reached before snapshot rewind");
            }
            Check(expected.Length > 0, "Live bouncing/shrinking projectile captured");
            Seek(1f); Seek(3f); Check(Capture() == expected, "Snapshots restore exact position, direction, scale and boundary state");
            Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "Fast Seek removes zero-size projectile before Lifetime");
            Seek(2f); Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "No zero-size projectile resurrected after rewind");
            Check(player.RestoredSnapshots > 0 && !player.IsPlaying, "Actual snapshot restores preserve pause");
            return new { passed = true, checks, snapshotRestores = player.RestoredSnapshots, deterministicSeek = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Size()
    {
        using var c = new Context(); var shooting = Shooting(bounce: false, destroy: false);
        Set(shooting, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
        Set(shooting, "projectileSizeChangeY", 0f); Set(shooting, "projectileSizeChangeSpeed", 10f); Set(shooting, "projectileFinalSize", 0f);
        shooting.SetChildAttack(Child()); var parent = c.Spawn(Vector3.zero, Vector3.down, shooting);
        c.Step(.01f, .01f); Check(c.Preview.Count == 2, "Child emitted before shrink");
        c.Step(.12f, .11f); Check(!c.Manager.Exists(parent) && c.Preview.Count == 1, "Size Curve reaches zero and resolves parent immediately");
        c.Step(.5f, .05f); Check(c.Preview.Count == 1, "Shrink cancels further child emissions, existing child survives");
        var center = c.Manager.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectileVisualStaticData), typeof(EnemyProjectileResolution), typeof(EnemyCompositeCenter));
        c.Manager.SetComponentData(center, LocalTransform.FromScale(0));
        var service = c.Manager.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectileVisualStaticData), typeof(EnemyProjectileResolution), typeof(DisableRendering));
        c.Manager.SetComponentData(service, LocalTransform.FromScale(0));
        var burst = c.Manager.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectileVisualStaticData), typeof(EnemyProjectileResolution), typeof(EnemyProjectileBurstAtPoint));
        c.Manager.SetComponentData(burst, LocalTransform.FromScale(0));
        c.Manager.SetComponentData(burst, new EnemyProjectileBurstAtPoint { HasDetonated = 1 });
        c.Step(.6f, .1f); Check(c.Manager.Exists(center) && c.Manager.Exists(service) && c.Manager.Exists(burst), "Hidden controllers, detonated Burst and service entities survive zero visual size");
    }
    static void Walls()
    {
        foreach (var test in new (Vector3 Position, Vector3 Direction, EnemyChildAttackBoundaries Edges)[] { (Position: new Vector3(4.7f, 0), Direction: Vector3.right, Edges: EnemyChildAttackBoundaries.Right),
            (new Vector3(0, 4.7f), Vector3.up, EnemyChildAttackBoundaries.Top),
            (new Vector3(-4.7f, 0), Vector3.left, EnemyChildAttackBoundaries.Left),
            (new Vector3(0, -4.7f), Vector3.down, EnemyChildAttackBoundaries.Bottom) })
        {
            using var c = new Context(); var entity = c.Spawn(test.Position, test.Direction, Shooting(test.Edges));
            c.Step(.2f, .2f); Check(c.Manager.Exists(entity), "Bounce wins over Destroy Outside Screen");
            Check(math.distance(c.Position(entity), (float2)(new float3(test.Direction.x, test.Direction.y, 0).xy * 4.5f)) < .001f, "Radius and remaining travel respected: " + test.Edges);
            Check(math.distance(c.Velocity(entity), new float2(-test.Direction.x * 2, -test.Direction.y * 2)) < .001f, "Reflected component preserves speed");
            c.Step(.21f, .01f); Check(math.dot(c.Velocity(entity), new float2(test.Direction.x, test.Direction.y)) < 0, "No repeated bounce on contact");
        }
        using (var c = new Context())
        {
            var e = c.Spawn(new Vector3(4.7f, 4.7f), new Vector3(1, 1), Shooting(EnemyChildAttackBoundaries.All), Mathf.Sqrt(8));
            c.Step(.2f, .2f); Check(math.distance(c.Position(e), new float2(4.5f)) < .001f && math.all(c.Velocity(e) < 0), "Corner reflects both axes");
        }
        using (var c = new Context())
        {
            var e = c.Spawn(new Vector3(0, 4.7f), Vector3.up, speed: 4); c.Step(.2f, .2f);
            Check(!c.Manager.Exists(e), "Unselected top edge still destroys");
        }
        using (var c = new Context())
        {
            var e = c.Spawn(new Vector3(6, 0), Vector3.left); c.Step(.2f, .2f);
            Check(c.Manager.Exists(e) && c.Velocity(e).x < 0 && c.Position(e).x > 5, "Outside birth travels inward without bounce");
            c.Step(.7f, .5f); Check(c.Velocity(e).x < 0 && c.Position(e).x < 5, "Entry preserves inward direction");
        }
        using (var c = new Context())
        {
            var e = c.Spawn(Vector3.zero, Vector3.right, Shooting(EnemyChildAttackBoundaries.All), 100); c.Step(1f, 1f);
            Check(math.all(math.abs(c.Position(e)) <= 4.801f) && math.abs(math.length(c.Velocity(e)) - 100) < .001f, "Multiple high-speed bounces retain speed and distance");
        }
    }
    static void Patterns()
    {
        foreach (var kind in new[] { EnemyProjectileMovementPattern.Default, EnemyProjectileMovementPattern.AngularTurn,
            EnemyProjectileMovementPattern.Lateral, EnemyProjectileMovementPattern.Orbit })
        foreach (bool dna in kind == EnemyProjectileMovementPattern.Orbit ? new[] { false, true } : new[] { false })
        {
            using var c = new Context(); var e = c.Spawn(new Vector3(4.7f, 0), Vector3.right);
            var motion = c.Manager.GetComponentData<EnemyProjectileMovement>(e);
            motion.Pattern = kind; motion.ReferenceSpeed = 2; motion.ForwardDirection = new float2(1, 0);
            motion.LateralSpeed = kind == EnemyProjectileMovementPattern.Lateral ? .5f : 0;
            motion.AngularSpeedDegreesPerSecond = kind == EnemyProjectileMovementPattern.AngularTurn ? 15f : 0;
            motion.OrbitCenter = new float2(4, 0); motion.OrbitRadius = .7f; motion.OrbitRadialSpeed = 2; motion.OrbitAngleRadians = 0;
            if (dna) { motion.OrbitCenterMode = EnemyProjectileOrbitCenterMode.DnaLike; motion.OrbitCenter = new float2(4.7f, 0); motion.DnaMedianDirection = new float2(1, 0); motion.DnaHalfCycleDuration = 1; }
            c.Manager.SetComponentData(e, motion);
            c.Step(.2f, .2f); float x = c.Position(e).x;
            Check(c.Velocity(e).x < 0, "Motion reflects: " + kind + "/" + dna);
            c.Step(.25f, .05f); Check(c.Position(e).x < x && c.Velocity(e).x < 0, "Next pattern update keeps reflected direction: " + kind + "/" + dna);
        }
        using (var c = new Context())
        {
            var settings = Shooting(); Set(settings, "projectileSpeedBehavior", EnemyProjectileSpeedBehavior.Customized);
            var e = c.Spawn(new Vector3(4.7f, 0), Vector3.right, settings);
            c.Step(.2f, .2f); c.Step(.25f, .05f);
            Check(c.Velocity(e).x < 0 && c.Manager.GetComponentData<EnemyProjectileCustomSpeed>(e).Direction.x < 0, "Speed Curve retains reflected basis");
        }
    }
    static void BoundaryChild()
    {
        using var c = new Context(); var child = new EnemyChildAttackSettings();
        Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached);
        Set(child, "triggerOnce", false); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Right);
        child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        var settings = Shooting(); settings.SetChildAttack(child); c.Spawn(new Vector3(4.7f, 0), Vector3.right, settings);
        c.Step(.2f, .2f); Check(c.Preview.Count == 2, "Radius contact triggers child boundary event");
        c.Step(.21f, .01f); Check(c.Preview.Count == 2, "Boundary contact is not duplicated on next frame");
        using var distance = new Context(); var atDistance = new EnemyChildAttackSettings();
        Set(atDistance, "triggerMode", EnemyChildAttackTriggerMode.AfterDistance); Set(atDistance, "triggerValue", .3f);
        atDistance.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        var repeated = Shooting(); repeated.SetChildAttack(atDistance);
        var parent = distance.Spawn(new Vector3(4.7f, 0), Vector3.right, repeated); distance.Step(.2f, .2f);
        Check(distance.Preview.Count == 2, "After Distance includes travel before and after bounce");
        using var query = distance.Manager.CreateEntityQuery(typeof(EnemyProjectileVelocity));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities) if (entity != parent) Check(distance.Velocity(entity).x < 0, "Child inherits final reflected direction, not endpoint chord");
    }
    static void Limits()
    {
        var defaults = JsonUtility.FromJson<EnemyShootingSettings>("{}").ScreenBoundary;
        Check(!defaults.OverrideBaseBounceTime && defaults.MaximumBounces == 1, "Old data: unlimited, limit defaults to one");
        var limited = new EnemyProjectileScreenSettings(false, bounce: true, overrideBounceTime: true);
        var shooting = Shooting(destroy: false); shooting.SetScreenBoundary(limited);
        var copy = new EnemyShootingSettings(); copy.CopyProjectileConfigurationFrom(shooting);
        Check(copy.ScreenBoundary.OverrideBaseBounceTime && copy.ScreenBoundary.MaximumBounces == 1, "Preset copy keeps bounce limit");
        copy = JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(shooting));
        Check(copy.ScreenBoundary.OverrideBaseBounceTime && copy.ScreenBoundary.MaximumBounces == 1, "JSON keeps bounce limit");
        Check(new EnemyProjectileScreenSettings(true, overrideBounceTime: true, maximumBounces: 0).MaximumBounces == 1, "Invalid count clamps to one");
        foreach (bool destroy in new[] { false, true })
        {
            using var c = new Context();
            var settings = Shooting(destroy: destroy);
            settings.SetScreenBoundary(new EnemyProjectileScreenSettings(destroy, bounce: true, overrideBounceTime: true));
            var e = c.Spawn(new Vector3(4.7f, 0), Vector3.right, settings);
            c.Step(.2f, .2f);
            Check(c.Manager.Exists(e) && c.Velocity(e).x < 0 && math.abs(c.Position(e).x - 4.5f) < .001f, "First allowed bounce reflects");
            Check(c.Manager.GetComponentData<EnemyProjectileScreenState>(e).CompletedBounces == 1, "Count belongs to projectile");
            c.Step(5.2f, 5f);
            Check(c.Manager.Exists(e) == !destroy, "After budget, Destroy Outside Screen resumes");
            if (!destroy)
            {
                Check(c.Position(e).x < -5 && c.Velocity(e).x < 0, "After budget, projectile flies through next edge");
                var state = c.Manager.GetComponentData<EnemyProjectileScreenState>(e);
                Check(state.CompletedBounces == 1 && math.all(!math.isfinite(state.BounceContacts)), "No stale contact or extra bounce after exhaustion");
            }
        }
        for (int count = 1; count <= 3; count++)
        {
            float2 result = EnemyProjectileScreenBounce.ResolveLimited(float2.zero, new float2(100, 0), new float2(-4.8f), new float2(4.8f),
                EnemyChildAttackBoundaries.All, count, out var sign, out var contacts, out int completed);
            float expected = (count % 2 == 0 ? 1 : -1) * (100 - count * 9.6f);
            Check(completed == count && math.abs(result.x - expected) < .001f, "High speed respects finite budget and remaining travel: " + count);
            Check(sign.x == (count % 2 == 0 ? 1 : -1) && sign.y == 1, "Final reflected basis: " + count);
        }
        var corner = EnemyProjectileScreenBounce.ResolveLimited(new float2(4.7f), new float2(5.1f), new float2(-4.8f), new float2(4.8f),
            EnemyChildAttackBoundaries.All, 1, out var cornerSign, out _, out int cornerCount);
        Check(cornerCount == 1 && math.all(cornerSign == -1) && math.distance(corner, new float2(4.5f)) < .001f, "Corner consumes one bounce and reflects both axes");
        using (var c = new Context())
        {
            var settings = Shooting(); settings.SetScreenBoundary(limited);
            var child = new EnemyChildAttackSettings();
            Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached);
            Set(child, "triggerOnce", false); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Right);
            child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false)); settings.SetChildAttack(child);
            c.Spawn(new Vector3(4.7f, 0), Vector3.right, settings);
            c.Step(.2f, .2f); Check(c.Preview.Count == 2, "Final allowed bounce still triggers child");
            c.Step(.21f, .01f); Check(c.Preview.Count == 2, "Exhausted bounce does not repeat child event");
        }
    }
    static void Defaults()
    {
        var old = JsonUtility.FromJson<EnemyShootingSettings>("{}");
        Check(!old.ScreenBoundary.BounceOnScreenBounds && old.ScreenBoundary.BounceEdges == EnemyProjectileScreenSettings.DefaultBounceEdges, "Legacy defaults: bounce off, sides selected");
        var settings = Shooting(EnemyChildAttackBoundaries.All, false); var copy = new EnemyShootingSettings(); copy.CopyProjectileConfigurationFrom(settings);
        Check(copy.ScreenBoundary.BounceOnScreenBounds && copy.ScreenBoundary.BounceEdges == EnemyChildAttackBoundaries.All, "Projectile preset copy preserves bounce");
        copy = JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(settings));
        Check(copy.ScreenBoundary.BounceEdges == EnemyChildAttackBoundaries.All && !copy.ScreenBoundary.DestroyOutsideScreen, "JSON preserves combined settings");
    }
}
