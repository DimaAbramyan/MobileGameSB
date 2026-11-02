using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyChildAttackPattern
{
    private static int checks;
    private static EnemyBullet bullet;
    private static readonly Vector3 Target = new(10f, 10f, 0f);
    public static object Run()
    {
        checks = 0;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        foreach (var trigger in new[] { EnemyChildAttackTriggerMode.AfterTime, EnemyChildAttackTriggerMode.AfterDistance }) TestTrigger(trigger);
        TestOffsets(); TestCapture(); TestBurstSector(); TestLifetimeHold(); TestEvents(); TestNested(); TestCopies(); TestDefaults();
        return new { passed = checks, scenarios = "time, distance, local/world offset, captured/follow origin, Burst, repeated sequential Sector, destruction ordering, resolution events, nested branching, JSON/copy/presets, old defaults" };
    }
    private static EnemyChildAttackSettings Child(float trigger = 0f)
    {
        var value = new EnemyChildAttackSettings(); Set(value, "triggerValue", trigger);
        value.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .01f);
        Set(value.Pattern.Shooting, "overrideProjectileLifetime", true); Set(value.Pattern.Shooting, "projectileLifetime", 20f);
        value.Pattern.Validate();
        return value;
    }
    private static Entity Spawn(EnemyProjectilePreviewWorld preview, EnemyChildAttackSettings child, Vector3? direction = null)
    {
        var shooting = new EnemyShootingSettings(); shooting.SetChildAttack(child);
        Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 30f);
        shooting.SetProjectileBaseSpeedOverride(true, 1f); shooting.Validate();
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, Vector3.zero, Vector3.zero, direction ?? Vector3.right, 1f, 1f, 0f, new EnemyProjectilePrefabSet(bullet));
        preview.Spawn(requests);
        using var q = preview.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
        using var entities = q.ToEntityArray(Allocator.Temp);
        Check(entities.Length == 1, "Parent spawn"); return entities[0];
    }
    private static Vector3 ChildPosition(EnemyProjectilePreviewWorld preview, Entity parent)
    {
        var manager = preview.SimulationWorld.EntityManager;
        using var q = manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = q.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities) if (entity != parent) { var p = manager.GetComponentData<LocalTransform>(entity).Position; return new Vector3(p.x, p.y, p.z); }
        throw new Exception("No child projectile.");
    }
    private static void TestTrigger(EnemyChildAttackTriggerMode trigger)
    {
        using var preview = new EnemyProjectilePreviewWorld(); var child = Child(.3f); Set(child, "triggerMode", trigger); var parent = Spawn(preview, child);
        preview.Step(.1f, .1f, Target); preview.Step(.2f, .1f, Target); Check(preview.Count == 1, "Does not trigger too early: " + trigger);
        preview.Step(.3f, .1f, Target); Check(preview.Count == 2, "Triggers at threshold: " + trigger);
        Check(Mathf.Abs(ChildPosition(preview, parent).x - .3f) < .001f, "Spawn at actual parent pose");
        preview.Step(.4f, .1f, Target); Check(preview.Count == 2, "Single execution by default");
    }
    private static void TestOffsets()
    {
        foreach (var space in new[] { EnemyChildAttackOffsetSpace.World, EnemyChildAttackOffsetSpace.Local })
        {
            using var preview = new EnemyProjectilePreviewWorld(); var child = Child(); Set(child, "offsetSpace", space); Set(child, "spawnOffset", new Vector2(2f, 3f));
            var parent = Spawn(preview, child); preview.Step(.1f, .1f, Target);
            Vector3 expected = space == EnemyChildAttackOffsetSpace.World ? new Vector3(2.1f, 3f) : new Vector3(3.1f, -2f);
            Check(Vector3.Distance(ChildPosition(preview, parent), expected) < .001f, "Offset: " + space);
        }
    }
    private static void TestCapture()
    {
        foreach (var mode in new[] { EnemyChildAttackOriginMode.FollowParent, EnemyChildAttackOriginMode.CaptureOnStart })
        {
            using var preview = new EnemyProjectilePreviewWorld(); var child = Child(); Set(child, "originMode", mode);
            Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f); child.Pattern.Validate();
            Spawn(preview, child); preview.Step(.1f, .1f, Target); preview.Step(.2f, .1f, Target); preview.Step(.31f, .11f, Target);
            Check(preview.Count == 3, "Two delayed shots: " + mode);
            var manager = preview.SimulationWorld.EntityManager; using var q = manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = q.ToEntityArray(Allocator.Temp);
            var last = manager.GetComponentData<LocalTransform>(entities[entities.Length - 1]).Position;
            Check(Mathf.Abs(last.x - (mode == EnemyChildAttackOriginMode.FollowParent ? .31f : .1f)) < .002f, "Origin retention: " + mode);
        }
    }
    private static void TestBurstSector()
    {
        using var preview = new EnemyProjectilePreviewWorld(); var child = Child();
        Set(child, "destroyParent", true); Set(child, "repeatCount", 2); Set(child, "repeatInterval", .2f);
        Set(child.Pattern, "burstAttack", EnemyBurstAttackMode.Enabled); Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f);
        Set(child.Pattern, "burstShotCount", 2); Set(child.Pattern, "burstShotInterval", .1f);
        var layout = child.Pattern.Shooting.EmissionLayout; layout.Configure(EnemyEmissionLayoutType.Sector, 3);
        Set(layout, "launchMode", EnemySectorLaunchMode.Sequential); Set(layout, "startDelay", .15f); Set(layout, "launchInterval", .1f); Set(layout, "reverseOrder", true);
        child.Pattern.Validate(); var parent = Spawn(preview, child);
        bool sawChildrenBeforeDestruction = false;
        for (int i = 1; i <= 80; i++)
        {
            preview.Step(i * .025f, .025f, Target);
            int descendants = preview.Count - (preview.SimulationWorld.EntityManager.Exists(parent) ? 1 : 0);
            if (descendants > 0 && descendants < 24)
            { Check(preview.SimulationWorld.EntityManager.Exists(parent), "Parent lives until all scheduled Sector launches finish"); sawChildrenBeforeDestruction = true; }
        }
        Check(sawChildrenBeforeDestruction, "Delayed emission observed"); Check(!preview.SimulationWorld.EntityManager.Exists(parent), "Parent destroyed after completion"); Check(preview.Count == 24, "2 repeats x 2 bursts x 2 shots x 3 sector directions");
    }
    private static void TestEvents()
    {
        foreach (var kind in new[] { EnemyProjectileResolutionKind.Expired, EnemyProjectileResolutionKind.Intercepted, EnemyProjectileResolutionKind.HitPlayer })
        {
            using var preview = new EnemyProjectilePreviewWorld(); var child = Child(); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent);
            Set(child, "eventType", kind == EnemyProjectileResolutionKind.Expired ? EnemyChildAttackEvent.Expired : kind == EnemyProjectileResolutionKind.Intercepted ? EnemyChildAttackEvent.Intercepted : EnemyChildAttackEvent.HitPlayer);
            Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f); child.Pattern.Validate(); var parent = Spawn(preview, child);
            preview.SimulationWorld.EntityManager.SetComponentData(parent, new EnemyProjectileResolution { Kind = kind });
            preview.Step(.1f, .1f, Target); Check(!preview.SimulationWorld.EntityManager.Exists(parent) && preview.Count == 1, "Event fires before cleanup: " + kind);
            preview.Step(.4f, .3f, Target); Check(preview.Count == 1, "Death event cancels delayed shots: " + kind);
        }
    }
    private static void TestLifetimeHold()
    {
        foreach (float lifetime in new[] { .1f, .15f })
        {
            using var preview = new EnemyProjectilePreviewWorld(); var child = Child(.1f);
            Set(child, "destroyParent", true); Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .3f); child.Pattern.Validate();
            var parent = Spawn(preview, child); var manager = preview.SimulationWorld.EntityManager;
            manager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = lifetime });
            preview.Step(.1f, .1f, Target);
            Check(manager.Exists(parent) && manager.IsComponentEnabled<EnemyChildAttackLifetimeHold>(parent), "Destroy Parent owns natural expiry after activation: " + lifetime);
            preview.Step(.2f, .1f, Target); preview.Step(.3f, .1f, Target);
            Check(manager.Exists(parent) && preview.Count == 2, "Parent survives lifetime until last child shot");
            preview.Step(.4f, .1f, Target);
            Check(!manager.Exists(parent) && preview.Count == 2, "Parent expires after final child shot");
        }
        using (var preview = new EnemyProjectilePreviewWorld())
        {
            var child = Child(); Set(child, "destroyParent", true); Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .3f); child.Pattern.Validate();
            var parent = Spawn(preview, child); preview.Step(.1f, .1f, Target);
            preview.SimulationWorld.EntityManager.SetComponentData(parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Intercepted });
            preview.Step(.2f, .1f, Target); preview.Step(.5f, .3f, Target);
            Check(!preview.SimulationWorld.EntityManager.Exists(parent) && preview.Count == 1, "Lifetime hold does not prevent interception or restart cancelled shots");
        }
        using (var preview = new EnemyProjectilePreviewWorld())
        {
            var child = Child(); Set(child, "destroyParent", true); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.Expired);
            Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .3f); child.Pattern.Validate();
            var parent = Spawn(preview, child); preview.SimulationWorld.EntityManager.SetComponentData(parent, new EnemyProjectileRemainingLifetime { Value = .1f });
            preview.Step(.1f, .1f, Target); Check(!preview.SimulationWorld.EntityManager.Exists(parent) && preview.Count == 1, "Expired event resolves parent normally");
            preview.Step(.4f, .3f, Target); Check(preview.Count == 1, "Expired event cancels delayed attack after parent removal");
        }
    }
    private static void TestNested()
    {
        using var preview = new EnemyProjectilePreviewWorld(); var first = Child(); var second = Child(); second.Pattern.Shooting.ConfigureLegacyFan(3, -30f, 30f); second.Pattern.Validate();
        first.Pattern.Shooting.SetChildAttack(second); first.Pattern.Validate(); Spawn(preview, first);
        preview.Step(.1f, .1f, Target); Check(preview.Count == 2, "Only one generation per step"); preview.Step(.2f, .1f, Target); Check(preview.Count == 5, "Nested child fan branches");
    }
    private static void TestCopies()
    {
        var root = new EnemyBurstAttackSettings(); var child = Child(.4f); child.Pattern.Shooting.SetChildAttack(Child(.8f)); root.Shooting.SetChildAttack(child);
        var copy = new EnemyBurstAttackSettings(); copy.CopyFrom(root);
        Check(copy.Shooting.ChildAttack != child && copy.Shooting.ChildAttack.Pattern.Shooting.ChildAttack != child.Pattern.Shooting.ChildAttack, "Nested preset copies are independent");
        var json = JsonUtility.ToJson(root); var roundtrip = JsonUtility.FromJson<EnemyBurstAttackSettings>(json); roundtrip.Validate();
        Check(roundtrip.Shooting.ChildAttack != null && Mathf.Abs(roundtrip.Shooting.ChildAttack.TriggerValue - .4f) < .00001f, "JSON keeps child trigger");
        Check(roundtrip.Shooting.ChildAttack.Pattern.Shooting.ChildAttack != null && Mathf.Abs(roundtrip.Shooting.ChildAttack.Pattern.Shooting.ChildAttack.TriggerValue - .8f) < .00001f, "JSON keeps grandchildren");
        copy.EnableMultiplePatterns(); Check(copy.GetPattern(0).Shooting.ChildAttack != null, "Single to Multiple keeps child pattern");
    }
    private static void TestDefaults()
    {
        var restored = JsonUtility.FromJson<EnemyShootingSettings>("{}"); restored.Validate(); Check(restored.ChildAttack == null, "Old configurations stay disabled");
        using var preview = new EnemyProjectilePreviewWorld(); Spawn(preview, null); preview.Step(.1f, .1f, Target); preview.Step(2f, 1.9f, Target); Check(preview.Count == 1, "Ordinary projectile unchanged");
    }
    private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (field == null) throw new Exception("Missing field: " + name); field.SetValue(target, value); }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
}
