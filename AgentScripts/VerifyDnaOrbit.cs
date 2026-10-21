using System;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyDnaOrbit
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        checks = 0;
        var settings = new EnemyShootingSettings();
        Set(settings, "fanMode", EnemyProjectileFanMode.Fan);
        Set(settings, "fanProjectileCount", 7);
        Set(settings, "fanMinAngle", -20f);
        Set(settings, "fanMaxAngle", 70f);
        Set(settings, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
        Set(settings, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
        Set(settings, "projectileAngularSpeed", 90f);
        var median = settings.GetDnaMedianDirection(Vector3.up, 0, 0f);
        Check(Vector3.Angle(median, Quaternion.Euler(0f, 0f, 25f) * Vector3.up) < .01f,
            "Asymmetric fan median follows its real min/max angles.");
        float halfCycle = settings.GetDnaHalfCycleDuration();
        Near(halfCycle, 1f, "Angular Velocity controls crossing interval.");
        Set(settings, "projectileDnaWeaveSpeed", 2f);
        Near(settings.GetDnaHalfCycleDuration(), halfCycle * .5f, "Weave speed doubles frequency.");
        var copy = new EnemyShootingSettings(); copy.CopyFrom(settings);
        Near(copy.DnaWeaveSpeed, 2f, "Preset/local copying preserves DNA speed.");
        Check((int)EnemyProjectileOrbitCenterMode.SourcePosition == 0
            && (int)EnemyProjectileOrbitCenterMode.FixedWorldPoint == 1,
            "Existing serialized Orbit modes retain their values.");
        foreach (float degrees in new[] { -150f, -95f, -70f, -35f, -3f, 0f, 11f, 50f, 80f, 120f, 175f })
        {
            float angle = math.radians(degrees);
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, 0f, 3f,
                out var start, out var direction, out var angular);
            Near(start, float2.zero, "DNA starts at the authored spawn position.");
            Near(direction, new float2(-math.sin(angle), math.cos(angle)), "Initial direction matches Fan.");
            for (int crossing = 1; crossing <= 6; crossing++)
            {
                EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, crossing, 3f,
                    out var point, out _, out _);
                Near(point, new float2(0, 3f * crossing), "All fan angles cross at exactly the same time and point.");
            }
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, .5f, 3f,
                out var firstSide, out _, out _);
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, 1.5f, 3f,
                out var secondSide, out _, out _);
            Near(firstSide.x, -secondSide.x, "Consecutive arcs weave on opposite sides.");
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, .999f, 3f,
                out _, out _, out var before);
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, 1.001f, 3f,
                out _, out _, out var after);
            Near(before, -after, "Angular Velocity changes sign at the median.");
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, .4f, 3f,
                out var p0, out var tangent, out _);
            EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), angle, 1f, .4001f, 3f,
                out var p1, out _, out _);
            Check(math.dot(math.normalizesafe(p1 - p0), tangent) > .999f, "Trajectory tangent agrees with bullet direction.");
        }
        VerifyWorld();
        return new { passed = true, checks, tested = "asymmetric Fan, crossing synchronization, six crossings, signs, tangents, real ECS movement, large steps, legacy Orbit, spawner, copying" };
    }

    private static void VerifyWorld()
    {
        var helper = new GameObject("DNA Orbit verification"); helper.SetActive(false);
        var authoring = helper.AddComponent<EnemyProjectileAuthoring>();
        using (var world = new World("DNA Orbit verification"))
        {
            try
            {
                var em = world.EntityManager;
                var system = world.GetOrCreateSystem<ProjectileMovementSystem>();
                var entities = new Entity[5];
                for (int i = 0; i < entities.Length; i++)
                {
                    float angle = math.radians(-50f + i * 25f);
                    var movement = CreateMovement(authoring, EnemyProjectileOrbitCenterMode.DnaLike, angle, out var spawn);
                    Near(new float2(spawn.x, spawn.y), new float2(2, 3), "DNA ignores legacy orbit radius and does not teleport at spawn.");
                    entities[i] = Projectile(em, spawn, movement);
                }
                world.SetTime(new TimeData(0, .025f));
                for (int tick = 1; tick <= 120; tick++)
                {
                    system.Update(world.Unmanaged);
                    if (tick % 40 != 0) continue;
                    foreach (var e in entities)
                        Near(em.GetComponentData<LocalTransform>(e).Position.xy,
                            new float2(2, 3 + 3 * (tick / 40)), "Real ECS fan meets at every crossing.");
                }
                var large = Projectile(em, new Vector3(2, 3, 0), CreateMovement(authoring, EnemyProjectileOrbitCenterMode.DnaLike, .7f, out _));
                world.SetTime(new TimeData(0, 2.7f)); system.Update(world.Unmanaged);
                EnemyProjectileDnaMotion.Evaluate(new float2(0, 1), .7f, 1f, 2.7f, 3f, out var offset, out _, out _);
                Near(em.GetComponentData<LocalTransform>(large).Position.xy, new float2(2, 3) + offset,
                    "A large frame step crosses several boundaries without resetting positions.");
                foreach (var mode in new[] { EnemyProjectileOrbitCenterMode.SourcePosition, EnemyProjectileOrbitCenterMode.FixedWorldPoint })
                {
                    var movement = CreateMovement(authoring, mode, .2f, out var spawn);
                    float2 expectedCenter = mode == EnemyProjectileOrbitCenterMode.SourcePosition
                        ? new float2(1, 1) : new float2(-4, 5);
                    Near(movement.OrbitCenter, expectedCenter, "Legacy center is preserved.");
                    Near(math.distance(new float2(spawn.x, spawn.y), expectedCenter), 2f, "Legacy radius positioning is preserved.");
                    var e = Projectile(em, spawn, movement);
                    world.SetTime(new TimeData(0, .1f)); system.Update(world.Unmanaged);
                    float expectedRadius = 2f + .5f * .1f;
                    float expectedAngle = movement.OrbitAngleRadians - math.radians(90f) * (3f / movement.ReferenceSpeed) * .1f;
                    Near(em.GetComponentData<LocalTransform>(e).Position.xy,
                        expectedCenter + new float2(math.cos(expectedAngle), math.sin(expectedAngle)) * expectedRadius,
                        "Legacy Orbit angular and radial movement is unchanged.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(helper); }
        }
    }
    private static EnemyProjectileMovement CreateMovement(EnemyProjectileAuthoring authoring,
        EnemyProjectileOrbitCenterMode mode, float angle, out Vector3 spawn)
    {
        var request = new EnemyProjectileSpawnRequest(null, new Vector3(2, 3, 0),
            new Vector3(-math.sin(angle), math.cos(angle), 0), 1, 3,
            EnemyProjectileSpeedBehavior.None, 0, null, EnemyProjectileSizeBehavior.Default, 0, 1, null, 1,
            new Vector3(1, 1, 0), EnemyProjectileMovementPattern.Orbit, 90, 0, mode, new Vector2(-4, 5), 2, .5f,
            false, 10, Vector3.up, 1);
        var type = typeof(EnemyProjectileEcsSpawner).GetNestedType("PreparedProjectileSpawn", BindingFlags.NonPublic);
        var prepared = Activator.CreateInstance(type);
        type.GetField("Request").SetValue(prepared, request);
        type.GetField("Configuration").SetValue(prepared, authoring);
        type.GetField("EffectiveSpeed").SetValue(prepared, 3f);
        type.GetField("Direction").SetValue(prepared, new float2(request.Direction.x, request.Direction.y));
        object[] args = { prepared, Vector3.zero };
        var movement = (EnemyProjectileMovement)typeof(EnemyProjectileEcsSpawner)
            .GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        spawn = (Vector3)args[1]; return movement;
    }
    private static Entity Projectile(EntityManager em, Vector3 position, EnemyProjectileMovement movement)
    {
        var e = em.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectileVelocity), typeof(EnemyProjectileMovement),
            typeof(EnemyProjectilePreviousPosition), typeof(EnemyProjectileResolution));
        em.SetComponentData(e, LocalTransform.FromPosition(position));
        em.SetComponentData(e, movement);
        em.SetComponentData(e, new EnemyProjectileVelocity { Value = movement.ForwardDirection * 3f });
        return e;
    }
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static void Near(float a, float b, string message) => Check(math.abs(a - b) < .001f, message);
    private static void Near(float2 a, float2 b, string message) => Check(math.distance(a, b) < .003f, message);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
}
