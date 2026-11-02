using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyChildBoundarySelection
{
    private static int checks, scenarios;
    private static EnemyBullet bullet;
    private static readonly Vector3 Target = new(1000, 1000);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .002f, message + ": " + actual + " / " + expected);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld World = new();
        public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public EntityManager Manager => World.SimulationWorld.EntityManager;
        public Context()
        {
            var go = new GameObject("Boundary test camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, Scene);
            Camera = go.AddComponent<Camera>(); Camera.enabled = false;
            Camera.transform.position = new Vector3(0, 0, -10); Camera.orthographic = true;
            Camera.orthographicSize = 5; Camera.aspect = 1;
        }
        public Rect Bounds { get { Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera, 0, out var r), "Camera bounds available"); return r; } }
        public void Step(float time, float delta) => World.Step(time, delta, Target, Camera);
        public Vector3 Position(Entity entity) { var p = Manager.GetComponentData<LocalTransform>(entity).Position; return new Vector3(p.x, p.y, p.z); }
        public void Position(Entity entity, Vector3 position)
        { var pose = Manager.GetComponentData<LocalTransform>(entity); pose.Position = new float3(position.x, position.y, position.z); Manager.SetComponentData(entity, pose); }
        public List<Entity> Children(Entity parent)
        {
            using var q = Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var all = q.ToEntityArray(Allocator.Temp);
            var result = new List<Entity>(); foreach (var entity in all) if (entity != parent) result.Add(entity); return result;
        }
        public void Dispose() { World.Dispose(); UnityEngine.Object.DestroyImmediate(Camera.gameObject); EditorSceneManager.ClosePreviewScene(Scene); }
    }

    public static object Run()
    {
        checks = scenarios = 0;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        Geometry(); CameraViewport(); SpeedsAndEdges(); OutsideAndRearm(); DelayedOrigins(); Cancellation(); BurstSector(); Overlapping(); Motion(); Serialization(); RuntimeTick();
        Selections(); IgnoredSides(); MultipleCrossings(); SelectionMotion(); SelectionSerialization();
        return new { passed = true, scenarios, checks, masks = 16, tested = "all masks/offsets/speeds, ignored-edge continuation outside viewport, one event per swept step/corner, repeat/rearm/delay, all actual motion types, legacy selection/copy/preset/override, Runtime and Preview, old boundary regression" };
    }
    private static EnemyChildAttackSettings Child()
    {
        var child = new EnemyChildAttackSettings(); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent);
        Set(child, "eventType", EnemyChildAttackEvent.OnScreenBoundaryReached); Set(child, "boundary", EnemyChildAttackBoundary.Top);
        child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .01f);
        Set(child.Pattern.Shooting, "overrideProjectileLifetime", true); Set(child.Pattern.Shooting, "projectileLifetime", 40f);
        child.Validate(); return child;
    }
    private static Entity Spawn(Context c, EnemyChildAttackSettings child, Vector3 position, Vector3 direction, float speed = 1f,
        EnemyShootingSettings shooting = null, EnemyAttackCompletionTracker completion = null)
    {
        shooting ??= new EnemyShootingSettings(); shooting.SetChildAttack(child);
        // This fixture tests repeated boundary crossings; screen cleanup is covered separately.
        shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 40f); shooting.SetProjectileBaseSpeedOverride(true, speed); shooting.Validate();
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, position, Vector3.zero, direction,
            1f, speed, 0f, new EnemyProjectilePrefabSet(bullet), completionTracker: completion);
        c.World.Spawn(requests);
        using var q = c.Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = q.ToEntityArray(Allocator.Temp);
        return entities[0];
    }
    private static void Geometry()
    {
        var r = Rect.MinMaxRect(-2, -3, 2, 3);
        foreach (var edge in new[] { EnemyChildAttackBoundary.Top, EnemyChildAttackBoundary.Bottom, EnemyChildAttackBoundary.Left, EnemyChildAttackBoundary.Right })
        foreach (float offset in new[] { -1f, 0f, .5f })
        {
            scenarios++; var direction = edge == EnemyChildAttackBoundary.Top ? Vector3.up : edge == EnemyChildAttackBoundary.Bottom ? Vector3.down : edge == EnemyChildAttackBoundary.Left ? Vector3.left : Vector3.right;
            float length = (direction.y != 0 ? 3 : 2) - offset;
            Check(EnemyChildAttackBoundaryIntersection.TryCross(Vector3.zero, direction * 10, r, edge, offset, out float t, out var hit) && hit == edge, "Swept selected boundary");
            Near(t * 10, length, "Positive inward / negative outward");
            Check(EnemyChildAttackBoundaryIntersection.TryCross(direction * 10, Vector3.zero, r, edge, offset, out _, out _), "Incoming outside projectile reaches edge");
            Check(!EnemyChildAttackBoundaryIntersection.TryCross(direction * 10, direction * 11, r, edge, offset, out _, out _), "Outside spawn does not trigger");
        }
        scenarios++;
        Check(EnemyChildAttackBoundaryIntersection.TryCross(Vector3.left * 10, Vector3.right * 10, r, EnemyChildAttackBoundary.Any, 0, out float first, out var boundary) && boundary == EnemyChildAttackBoundary.Left, "Any picks entry before exit within one tick"); Near(first, .4f, "First crossing fraction");
        Check(!EnemyChildAttackBoundaryIntersection.TryCross(new Vector3(8, 2), new Vector3(8, 4), r, EnemyChildAttackBoundary.Top, 0, out _, out _), "No event on infinite edge extension");
        Check(EnemyChildAttackBoundaryIntersection.TryCross(Vector3.zero, Vector3.up * 3, r, EnemyChildAttackBoundary.Top, 0, out _, out _), "Exact arrival fires");
        Check(!EnemyChildAttackBoundaryIntersection.TryCross(Vector3.up * 3, Vector3.up * 4, r, EnemyChildAttackBoundary.Top, 0, out _, out _), "Leaving touched line does not double fire");
        Check(!EnemyChildAttackBoundaryIntersection.TryCross(Vector3.up * 3, Vector3.up * 3, r, EnemyChildAttackBoundary.Any, 0, out _, out _), "Stationary boundary does not fire");
        var rng = new System.Random(351);
        for (int i = 0; i < 1000; i++)
        {
            var end = new Vector3((float)rng.NextDouble() * 40 - 20, (float)rng.NextDouble() * 40 - 20);
            if (r.Contains(end)) continue;
            Check(EnemyChildAttackBoundaryIntersection.TryCross(Vector3.zero, end, r, EnemyChildAttackBoundary.Any, 0, out float t, out _), "Random fast segment finds exit");
            var p = end * t; Check(p.x >= -2.00001f && p.x <= 2.00001f && p.y >= -3.00001f && p.y <= 3.00001f, "Hit inside finite perimeter");
        }
    }
    private static void CameraViewport()
    {
        scenarios++;
        using var c = new Context(); c.Camera.rect = new Rect(.15f, .2f, .65f, .6f); c.Camera.aspect = .75f;
        c.Camera.transform.position = new Vector3(2, -1, -10); c.Camera.orthographicSize = 7;
        var bounds = c.Bounds; Check(Mathf.Abs(bounds.yMax - 6) < .02f, "Gameplay camera top (pixel-rounded viewport)"); Check(Mathf.Abs(bounds.width - 10.5f) < .02f, "Camera aspect respected");
        var child = Child(); Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
        var parent = Spawn(c, child, new Vector3(2, 5), Vector3.up, 100);
        c.Step(.1f, .1f); Near(c.Position(c.Children(parent)[0]).y, bounds.yMax, "Capture uses gameplay viewport edge");
        c.Camera.orthographic = false; c.Camera.fieldOfView = 60;
        Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(c.Camera, 0, out var perspective) && perspective.width > 0, "Perspective viewport supported");
    }
    private static void SpeedsAndEdges()
    {
        foreach (var edge in new[] { EnemyChildAttackBoundary.Top, EnemyChildAttackBoundary.Bottom, EnemyChildAttackBoundary.Left, EnemyChildAttackBoundary.Right })
        foreach (float speed in new[] { .1f, 5f, 1000f })
        foreach (float offset in new[] { -1f, 0f, 1f })
        {
            scenarios++;
            using var c = new Context(); var child = Child(); Set(child, "boundary", edge); Set(child, "boundaryOffset", offset);
            Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
            var direction = edge == EnemyChildAttackBoundary.Top ? Vector3.up : edge == EnemyChildAttackBoundary.Bottom ? Vector3.down : edge == EnemyChildAttackBoundary.Left ? Vector3.left : Vector3.right;
            var parent = Spawn(c, child, Vector3.zero, direction, speed);
            float arrival = (5 - offset) / speed;
            // Long projectile lifetime for the slowest cases.
            c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = 1000 });
            c.Step(arrival * .9f, arrival * .9f); Check(c.Children(parent).Count == 0, "No premature boundary event");
            c.Step(arrival * 1.1f, arrival * .2f); Check(c.Children(parent).Count == 1, "Slow/fast event not skipped");
            Near(Vector3.Dot(c.Position(c.Children(parent)[0]), direction), 5 - offset, "Captured precise crossing");
            c.Step(arrival * 1.2f, arrival * .1f); Check(c.Children(parent).Count == 1, "Trigger once");
        }
    }
    private static void OutsideAndRearm()
    {
        foreach (bool once in new[] { true, false })
        {
            scenarios++;
            using var c = new Context(); var child = Child(); Set(child, "triggerOnce", once);
            var parent = Spawn(c, child, Vector3.up * 8, Vector3.down);
            c.Step(0, 0); Check(c.Children(parent).Count == 0, "Outside birth not an event");
            c.Position(parent, Vector3.up * 6); c.Step(.1f, 0); Check(c.Children(parent).Count == 0, "Remaining outside not an event");
            c.Position(parent, Vector3.up * 5); c.Step(.2f, 0); Check(c.Children(parent).Count == 1, "Incoming touch fires");
            c.Step(.3f, 0); Check(c.Children(parent).Count == 1, "Resting on edge does not refire");
            c.Position(parent, Vector3.up * 4); c.Step(.4f, 0); Check(c.Children(parent).Count == 1, "Departing touched edge does not refire");
            c.Position(parent, Vector3.up * 6); c.Step(.5f, 0); Check(c.Children(parent).Count == (once ? 1 : 2), "New crossing respects Trigger Once");
            c.Position(parent, Vector3.up * 7); c.Step(.6f, 0); Check(c.Children(parent).Count == (once ? 1 : 2), "Outside does not spam");
        }
    }
    private static void DelayedOrigins()
    {
        foreach (var mode in new[] { EnemyChildAttackOriginMode.CaptureOnStart, EnemyChildAttackOriginMode.FollowParent })
        foreach (float delay in new[] { 0f, .2f, 1f })
        {
            scenarios++;
            using var c = new Context(); var child = Child(); Set(child, "timeOffset", delay); Set(child, "originMode", mode);
            Set(child, "spawnOffset", new Vector2(.5f, 1)); Set(child, "offsetSpace", EnemyChildAttackOffsetSpace.Local);
            var parent = Spawn(c, child, Vector3.up * 4, Vector3.up, 20);
            c.Step(.1f, .1f);
            if (delay > .05f)
            {
                Check(c.Children(parent).Count == 0, "Delay does not launch early");
                c.Step(.05f + delay - .01f, delay - .06f); Check(c.Children(parent).Count == 0, "Crossing-relative delay retained");
                c.Step(.05f + delay + .001f, .011f);
            }
            Check(c.Children(parent).Count == 1, "Delayed attack launches");
            var p = c.Position(c.Children(parent)[0]); Near(p.x, .5f, "Local spawn offset");
            Near(p.y, mode == EnemyChildAttackOriginMode.CaptureOnStart ? 6 : c.Position(parent).y + 1, "Captured crossing / live parent after delay");
        }
    }
    private static void Cancellation()
    {
        foreach (var kind in new[] { EnemyProjectileResolutionKind.Intercepted, EnemyProjectileResolutionKind.HitPlayer, EnemyProjectileResolutionKind.Expired })
        foreach (bool direct in new[] { false, true })
        {
            if (direct && kind != EnemyProjectileResolutionKind.Intercepted) continue;
            scenarios++;
            using var c = new Context(); var child = Child(); Set(child, "timeOffset", 1f); Set(child, "destroyParent", true);
            var completion = new EnemyAttackCompletionTracker();
            var parent = Spawn(c, child, Vector3.up * 4, Vector3.up, 20, completion: completion); c.Step(.1f, .1f);
            Check(c.Manager.Exists(parent) && completion.PendingChildAttacks > 0, "Scheduled work is tracked");
            if (direct) c.Manager.DestroyEntity(parent); else c.Manager.SetComponentData(parent, new EnemyProjectileResolution { Kind = kind });
            c.Step(.2f, .1f); c.Step(2, 1.8f);
            Check(c.World.Count == 0 && completion.PendingChildAttacks == 0, "Destroyed parent cancels delay and releases tracker");
        }
        scenarios++;
        using (var c = new Context())
        {
            var child = Child(); Set(child, "timeOffset", .3f); Set(child, "destroyParent", true);
            var parent = Spawn(c, child, Vector3.up * 4, Vector3.up, 20);
            c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = .05f });
            c.Step(.1f, .1f); Check(c.Manager.Exists(parent) && c.Manager.IsComponentEnabled<EnemyChildAttackLifetimeHold>(parent), "Destroy Parent owns lifetime throughout delay");
            c.Step(.2f, .1f); Check(c.Manager.Exists(parent), "Parent continues beyond screen during delay");
            c.Step(.36f, .16f); Check(!c.Manager.Exists(parent) && c.World.Count == 1, "Parent destroyed after child attack");
        }
    }
    private static void BurstSector()
    {
        scenarios++;
        using var c = new Context(); var child = Child(); Set(child, "timeOffset", .1f); Set(child, "destroyParent", true);
        Set(child, "repeatCount", 2); Set(child, "repeatInterval", .15f);
        Set(child.Pattern, "burstAttack", EnemyBurstAttackMode.Enabled); Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f);
        Set(child.Pattern, "burstShotCount", 2); Set(child.Pattern, "burstShotInterval", .1f);
        var layout = child.Pattern.Shooting.EmissionLayout; layout.Configure(EnemyEmissionLayoutType.Sector, 3);
        Set(layout, "launchMode", EnemySectorLaunchMode.Sequential); Set(layout, "startDelay", .15f); Set(layout, "launchInterval", .1f); child.Validate();
        var parent = Spawn(c, child, Vector3.up * 4, Vector3.up, 20); var manager = c.Manager;
        for (int i = 1; i <= 140; i++)
        {
            c.Step(i * .025f, .025f); int children = c.Children(parent).Count;
            if (children > 0 && children < 24) Check(manager.Exists(parent), "Destroy waits for every Burst/repeat/Sequential Sector launch");
        }
        Check(!manager.Exists(parent) && c.World.Count == 24, "Full existing attack pattern is reused");
    }
    private static void Overlapping()
    {
        scenarios++;
        using var c = new Context(); var child = Child(); Set(child, "triggerOnce", false); Set(child, "timeOffset", .5f);
        Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
        Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .5f); child.Validate();
        var parent = Spawn(c, child, Vector3.up * 4, Vector3.up);
        c.Position(parent, new Vector3(1, 6)); c.Step(.1f, 0);
        c.Position(parent, new Vector3(2, 4)); c.Step(.2f, 0);
        c.Position(parent, new Vector3(3, 6)); c.Step(.3f, 0);
        Check(c.Children(parent).Count == 0, "Three crossings retain independent delays");
        c.Step(.61f, 0); Check(c.Children(parent).Count == 1, "First crossing starts first");
        c.Step(.71f, 0); Check(c.Children(parent).Count == 2, "Second starts without waiting for first completion");
        c.Step(.81f, 0); Check(c.Children(parent).Count == 3, "Third delay retained");
        c.Step(1.4f, 0); Check(c.Children(parent).Count == 6, "All overlapping attacks finish their scheduled shots");
        var xs = new List<float>(); foreach (var entity in c.Children(parent)) { var p = c.Position(entity); Near(p.y, 5, "Each crossing captures boundary"); xs.Add(p.x); }
        xs.Sort(); Near(xs[0], .5f, "First capture retained"); Near(xs[2], 1.5f, "Second capture retained"); Near(xs[4], 2.5f, "Third capture retained");
    }
    private static void Motion()
    {
        foreach (var pattern in new[] { EnemyProjectileMovementPattern.Default, EnemyProjectileMovementPattern.AngularTurn, EnemyProjectileMovementPattern.Lateral, EnemyProjectileMovementPattern.Orbit })
        {
            scenarios++;
            using var c = new Context(); var child = Child(); var shooting = new EnemyShootingSettings();
            Set(shooting, "projectileMovementPattern", pattern); Set(shooting, "projectileAngularSpeed", -30f); Set(shooting, "projectileLateralSpeed", .5f);
            Set(shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.FixedWorldPoint); Set(shooting, "projectileOrbitRadius", 6f);
            Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
            var parent = Spawn(c, child, pattern == EnemyProjectileMovementPattern.Orbit ? Vector3.right * 6 : Vector3.up * 4, Vector3.up, bullet.GetComponent<EnemyProjectileAuthoring>().BaseSpeed, shooting);
            for (int i = 1; i <= 500 && c.Children(parent).Count == 0; i++) c.Step(i * .02f, .02f);
            Check(c.Children(parent).Count == 1, "Actual movement triggers event: " + pattern);
            Near(c.Position(c.Children(parent)[0]).y, 5, "Movement pattern crossing captured: " + pattern);
        }
        foreach (bool once in new[] { true, false })
        {
            scenarios++;
            using var c = new Context(); var child = Child(); Set(child, "triggerOnce", once);
            var shooting = new EnemyShootingSettings(); Set(shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
            Set(shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.FixedWorldPoint); Set(shooting, "projectileOrbitRadius", 6f); Set(shooting, "projectileAngularSpeed", -90f);
            var parent = Spawn(c, child, Vector3.right * 6, Vector3.up, bullet.GetComponent<EnemyProjectileAuthoring>().BaseSpeed, shooting);
            for (int i = 1; i <= 500; i++) c.Step(i * .02f, .02f);
            Check(once ? c.Children(parent).Count == 1 : c.Children(parent).Count >= 4 && c.Children(parent).Count <= 6, "Orbit triggers each crossing without per-frame spam");
        }
    }
    private static void Serialization()
    {
        scenarios++;
        Check((int)EnemyChildAttackEvent.BurstAtPoint == 4 && (int)EnemyChildAttackEvent.OnScreenBoundaryReached == 5, "Existing serialized event ids preserved");
        var child = Child(); Set(child, "boundary", EnemyChildAttackBoundary.Right); Set(child, "boundaryOffset", -.7f); Set(child, "timeOffset", .4f); Set(child, "triggerOnce", false);
        foreach (var copy in new[] { EnemyChildAttackSettings.Copy(child), JsonUtility.FromJson<EnemyChildAttackSettings>(JsonUtility.ToJson(child)) })
        { Check(copy.UsesScreenBoundary && copy.Boundary == EnemyChildAttackBoundary.Right && !copy.TriggerOnce, "Copy/JSON retains event settings"); Near(copy.BoundaryOffset, -.7f, "Offset persists"); Near(copy.TimeOffset, .4f, "Delay persists"); }
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>(); var roundtrip = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var settings = new DirectedWaveAttackSettings(); settings.WaveBurstSettings.Shooting.SetChildAttack(child); preset.SetAttackSettings(settings);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(preset), roundtrip);
            Check(roundtrip.AttackSettings.WaveBurstSettings.Shooting.ChildAttack.Boundary == EnemyChildAttackBoundary.Right, "Preset persists boundary event");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); UnityEngine.Object.DestroyImmediate(roundtrip); }
        Set(child, "timeOffset", -1f); Set(child, "boundaryOffset", float.NaN); child.Validate();
        Check(child.TimeOffset == 0 && child.BoundaryOffset == 0, "Invalid values sanitized");
        Check(new EnemyChildAttackSettings().TriggerOnce && !new EnemyChildAttackSettings().UsesScreenBoundary, "Defaults preserve old triggers");
    }
    private static void RuntimeTick()
    {
        scenarios++;
        using var c = new Context(); var child = Child(); var parent = Spawn(c, child, Vector3.up * 4, Vector3.up);
        c.Position(parent, Vector3.up * 6);
        var spawner = (EnemyProjectileEcsSpawner)typeof(EnemyProjectilePreviewWorld).GetField("spawner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c.World);
        spawner.UpdateChildAttacks(.1f, .1f, null, EnemyChildAttackSettings.MaximumLiveProjectiles, c.Camera);
        Check(c.Children(parent).Count == 1, "Runtime path works without a Preview target");
    }

    private static void Selections()
    {
        foreach (int bits in System.Linq.Enumerable.Range(0, 16))
        foreach (var edge in new[] { EnemyChildAttackBoundary.Left, EnemyChildAttackBoundary.Right, EnemyChildAttackBoundary.Top, EnemyChildAttackBoundary.Bottom })
        foreach (float offset in new[] { -1f, 0f, 1f })
        foreach (float speed in new[] { .1f, 1000f })
        {
            scenarios++; using var c = new Context(); var child = Child(); var mask = (EnemyChildAttackBoundaries)bits;
            child.SetTriggerBoundaries(mask); Set(child, "boundaryOffset", offset); Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
            bool selected = (mask & EnemyChildAttackBoundarySelection.FromLegacy(edge)) != 0;
            Vector3 direction = edge == EnemyChildAttackBoundary.Left ? Vector3.left : edge == EnemyChildAttackBoundary.Right ? Vector3.right : edge == EnemyChildAttackBoundary.Top ? Vector3.up : Vector3.down;
            var tracker = new EnemyAttackCompletionTracker(); var parent = Spawn(c, child, Vector3.zero, direction, speed, completion: tracker);
            if (bits == 0) Check(tracker.IsComplete, "No selected sides registers no impossible pending task");
            c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = 1000 });
            float arrival = (5 - offset) / speed;
            c.Step(arrival * .9f, arrival * .9f); Check(c.Children(parent).Count == 0, "No premature event for mask");
            c.Step(arrival * 1.1f, arrival * .2f);
            Check(c.Children(parent).Count == (selected ? 1 : 0), "Only selected edge fires: " + mask + " / " + edge);
            if (selected) Near(Vector3.Dot(c.Position(c.Children(parent)[0]), direction), 5 - offset, "Selected offset/capture correct at high and low speed");
        }
    }
    private static void IgnoredSides()
    {
        foreach (bool once in new[] { false, true }) foreach (float delay in new[] { 0f, .2f })
        {
            scenarios++; using var c = new Context(); var child = Child(); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right);
            Set(child, "triggerOnce", once); Set(child, "timeOffset", delay); Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
            var parent = Spawn(c, child, Vector3.zero, Vector3.up);
            c.Position(parent, new Vector3(0, 20)); c.Step(.1f, 0);
            Check(c.Children(parent).Count == 0 && c.Manager.Exists(parent), "Ignored top does not trigger, clean up or consume Trigger Once");
            c.Position(parent, new Vector3(6, 20)); c.Step(.2f, 0);
            if (delay > 0) { Check(c.Children(parent).Count == 0, "Ignored crossing did not start delay"); c.Step(.399f, 0); Check(c.Children(parent).Count == 0, "Delay starts only at selected side"); c.Step(.401f, 0); }
            Check(c.Children(parent).Count == 1, "Selected right remains reachable above ignored top");
            Near(c.Position(c.Children(parent)[0]).x, 5, "Capture on selected edge after ignored exit"); Near(c.Position(c.Children(parent)[0]).y, 20, "Ignored top does not clip selected edge");
            c.Position(parent, new Vector3(7, -20)); c.Step(.5f, 0); Check(c.Children(parent).Count == 1, "Ignored bottom cannot rearm/fire");
            c.Position(parent, new Vector3(4, -20)); c.Step(.6f, 0); c.Step(.801f, 0);
            Check(c.Children(parent).Count == (once ? 1 : 2), "Trigger Once / reentry survive ignored bottom");
            c.Position(parent, new Vector3(-6, -20)); c.Step(.9f, 0); c.Step(1.101f, 0);
            Check(c.Children(parent).Count == (once ? 1 : 3), "Selected left remains reachable after bottom exit");
        }
    }
    private static void MultipleCrossings()
    {
        foreach (var mask in new[] { EnemyChildAttackBoundaries.All, EnemyChildAttackBoundaries.Top | EnemyChildAttackBoundaries.Right,
            EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right })
        {
            scenarios++; using var c = new Context(); var child = Child(); child.SetTriggerBoundaries(mask); Set(child, "triggerOnce", false);
            var parent = Spawn(c, child, mask == (EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right) ? new Vector3(-10, 20) : Vector3.zero, Vector3.up);
            c.Position(parent, mask == (EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right) ? new Vector3(10, 20) : new Vector3(10, 10));
            c.Step(.1f, 0); Check(c.Children(parent).Count == 1, "Corner or two fast crossings launches one event");
            c.Step(.2f, 0); Check(c.Children(parent).Count == 1, "Resting outside never duplicates event");
        }
        scenarios++;
        using (var c = new Context())
        {
            var child = Child(); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right);
            Set(child, "repeatCount", 2); Set(child, "repeatInterval", .1f); Set(child, "timeOffset", .2f); Set(child, "destroyParent", true);
            Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .15f);
            child.Pattern.Shooting.EmissionLayout.ConfigureSector(-30, 30, 3, EnemySectorLaunchMode.Sequential, .05f, .05f); child.Validate();
            var parent = Spawn(c, child, Vector3.zero, Vector3.up);
            c.Position(parent, new Vector3(0, 20)); c.Step(.1f, 0); Check(c.Manager.Exists(parent), "Destroy Parent not applied at ignored boundary");
            c.Position(parent, new Vector3(6, 20)); c.Step(.2f, 0);
            c.Manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = .01f });
            for (int i = 1; i <= 100; i++)
            {
                c.Step(.2f + i * .02f, .02f); int children = c.Children(parent).Count;
                if (children < 12) Check(c.Manager.Exists(parent), "Parent survives delay, repeats and Sequential Sector");
            }
            Check(c.World.Count == 12 && !c.Manager.Exists(parent), "Repeat Count and Destroy Parent unchanged for mask");
        }
    }
    private static void SelectionMotion()
    {
        foreach (var movement in new[] { EnemyProjectileMovementPattern.AngularTurn, EnemyProjectileMovementPattern.Lateral, EnemyProjectileMovementPattern.Orbit })
        {
            scenarios++; using var c = new Context(); var child = Child(); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Right);
            Set(child, "originMode", EnemyChildAttackOriginMode.CaptureOnStart);
            var shooting = new EnemyShootingSettings(); Set(shooting, "projectileMovementPattern", movement);
            float speed = bullet.GetComponent<EnemyProjectileAuthoring>().BaseSpeed;
            Set(shooting, "projectileAngularSpeed", movement == EnemyProjectileMovementPattern.Orbit ? 90f : speed / (4f * Mathf.Deg2Rad));
            Set(shooting, "projectileLateralSpeed", speed * 2);
            Set(shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.FixedWorldPoint); Set(shooting, "projectileOrbitRadius", 6f);
            var parent = Spawn(c, child, movement == EnemyProjectileMovementPattern.Orbit ? Vector3.up * 6 : Vector3.up * 4.5f, Vector3.up, speed, shooting);
            for (int i = 1; i <= 1800 && c.Manager.Exists(parent) && c.Children(parent).Count == 0; i++) c.Step(i * .02f, .02f);
            Check(c.Children(parent).Count == 1, "Selected side fires after ignored top with real " + movement);
            Near(c.Position(c.Children(parent)[0]).x, 5, "Selected Right capture from curved movement");
        }
    }
    private static void SelectionSerialization()
    {
        foreach (EnemyChildAttackBoundary edge in Enum.GetValues(typeof(EnemyChildAttackBoundary)))
        {
            scenarios++; var old = JsonUtility.FromJson<EnemyChildAttackSettings>("{\"triggerMode\":2,\"eventType\":5,\"boundary\":" + (int)edge + "}");
            old.Validate(); var expected = EnemyChildAttackBoundarySelection.FromLegacy(edge);
            Check(old.TriggerBoundaries == expected, "Old Any / specific side resolves without asset rewrite");
            Check(EnemyChildAttackSettings.Copy(old).TriggerBoundaries == expected, "Copy retains inherited legacy choice");
            Set(old, "boundary", EnemyChildAttackBoundary.Right); Check(old.TriggerBoundaries == EnemyChildAttackBoundaries.Right, "Validation does not materialize legacy fallback or break overrides");
        }
        foreach (int bits in System.Linq.Enumerable.Range(0, 16))
        {
            scenarios++; var child = Child(); child.SetTriggerBoundaries((EnemyChildAttackBoundaries)bits); child.Validate();
            Check(child.TriggerBoundaries == (EnemyChildAttackBoundaries)bits, "Explicit mask");
            Check(EnemyChildAttackSettings.Copy(child).TriggerBoundaries == child.TriggerBoundaries, "Deep copy mask");
            Check(JsonUtility.FromJson<EnemyChildAttackSettings>(JsonUtility.ToJson(child)).TriggerBoundaries == child.TriggerBoundaries, "JSON mask");
            var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>(); var restored = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
            try
            {
                var settings = new DirectedWaveAttackSettings(); settings.WaveBurstSettings.Shooting.SetChildAttack(child); preset.SetAttackSettings(settings);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(preset), restored);
                Check(restored.AttackSettings.WaveBurstSettings.Shooting.ChildAttack.TriggerBoundaries == child.TriggerBoundaries, "Preset mask persists");
                var localCopy = new EnemyBurstAttackSettings(); localCopy.ApplyShootingOverrideFrom(settings.WaveBurstSettings);
                Check(localCopy.Shooting.ChildAttack.TriggerBoundaries == child.TriggerBoundaries, "Local Override mask copy");
            }
            finally { UnityEngine.Object.DestroyImmediate(preset); UnityEngine.Object.DestroyImmediate(restored); }
        }
        scenarios++;
        var legacyPreset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>(); var imported = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var child = Child(); Set(child, "boundary", EnemyChildAttackBoundary.Right);
            var settings = new DirectedWaveAttackSettings(); settings.WaveBurstSettings.Shooting.SetChildAttack(child); legacyPreset.SetAttackSettings(settings);
            string json = EditorJsonUtility.ToJson(legacyPreset);
            json = System.Text.RegularExpressions.Regex.Replace(json, "\\\"triggerBoundaries\\\"\\s*:\\s*-1\\s*,", "");
            Check(!json.Contains("triggerBoundaries"), "Native Unity legacy data has no mask field");
            EditorJsonUtility.FromJsonOverwrite(json, imported);
            Check(imported.AttackSettings.WaveBurstSettings.Shooting.ChildAttack.TriggerBoundaries == EnemyChildAttackBoundaries.Right, "Native Unity managed-reference legacy deserialization keeps selected side");
        }
        finally { UnityEngine.Object.DestroyImmediate(legacyPreset); UnityEngine.Object.DestroyImmediate(imported); }
        scenarios++;
        using (var c = new Context())
        {
            var child = Child(); child.SetTriggerBoundaries(EnemyChildAttackBoundaries.Right);
            var parent = Spawn(c, child, Vector3.zero, Vector3.up); c.Position(parent, new Vector3(6, 20));
            var spawner = (EnemyProjectileEcsSpawner)typeof(EnemyProjectilePreviewWorld).GetField("spawner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c.World);
            spawner.UpdateChildAttacks(.1f, .1f, null, EnemyChildAttackSettings.MaximumLiveProjectiles, c.Camera);
            Check(c.Children(parent).Count == 1, "Runtime no-preview-target path uses explicit selection above ignored top");
        }
    }
}

