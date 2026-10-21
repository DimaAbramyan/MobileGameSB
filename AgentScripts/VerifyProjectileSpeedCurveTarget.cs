using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public sealed class SpeedCurveTargetVerificationHost : ScriptableObject
{
    public EnemyShootingSettings shooting = new EnemyShootingSettings();
}

public static class VerifyProjectileSpeedCurveTarget
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object InspectPrefabDefaults()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Waves" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var behaviour in prefab.GetComponentsInChildren<DirectedWaveAttackBehaviour>(true))
            {
                using var data = new SerializedObject(behaviour);
                var field = data.GetIterator();
                int count = 0, both = 0;
                while (field.Next(true))
                {
                    if (field.name != "projectileSpeedCurveTarget") continue;
                    count++;
                    if (field.intValue == (int)EnemyProjectileSpeedCurveTarget.Both) both++;
                }
                if (count > 0) return new { path, curveTargets = count, both, passed = both == count };
            }
        }
        throw new Exception("No authored attack pattern found.");
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0;
        VerifySettings();
        var go = new GameObject("Speed curve target verification"); go.SetActive(false);
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var authoring = go.AddComponent<EnemyProjectileAuthoring>();
            using (var world = new World("Speed curve target verification", WorldFlags.Editor))
            using (var spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world))
            {
                var speed = world.GetOrCreateSystem<ProjectileCustomSpeedSystem>();
                var movement = world.GetOrCreateSystem<ProjectileMovementSystem>();
                foreach (EnemyProjectileSpeedCurveTarget target in Enum.GetValues(typeof(EnemyProjectileSpeedCurveTarget)))
                    foreach (float multiplier in new[] { 0f, .25f, 2f })
                        foreach (float radial in new[] { -1f, 1f })
                            foreach (var center in new[] { EnemyProjectileOrbitCenterMode.SourcePosition, EnemyProjectileOrbitCenterMode.FixedWorldPoint })
                                VerifyMotion(world, spawner, authoring, speed, movement, target, multiplier, radial, center);
                VerifyMotion(world, spawner, authoring, speed, movement, EnemyProjectileSpeedCurveTarget.Both, 2f, 1f,
                    EnemyProjectileOrbitCenterMode.SourcePosition, false);
                foreach (var pattern in new[] { EnemyProjectileMovementPattern.Default, EnemyProjectileMovementPattern.AngularTurn,
                    EnemyProjectileMovementPattern.Lateral })
                {
                    var e = Spawn(world.EntityManager, spawner, authoring, EnemyProjectileSpeedCurveTarget.Radial, 2f, 0f,
                        EnemyProjectileOrbitCenterMode.SourcePosition, true, pattern);
                    Check(world.EntityManager.GetComponentData<EnemyProjectileCustomSpeed>(e).CurveTarget
                        == EnemyProjectileSpeedCurveTarget.BaseSpeed, "Non-Orbit keeps ordinary base-speed curve");
                    world.EntityManager.DestroyEntity(e);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        return new { passed = checks, modes = 3, oldDefaultBoth = true, runtimeAndPreviewShareEcs = true };
    }

    private static void VerifySettings()
    {
        Check(new EnemyShootingSettings().SpeedCurveTarget == EnemyProjectileSpeedCurveTarget.Both, "New default Both");
        var old = JsonUtility.FromJson<EnemyShootingSettings>("{\"projectileMovementPattern\":3}");
        Check(old.SpeedCurveTarget == EnemyProjectileSpeedCurveTarget.Both, "Missing serialized field defaults to Both");
        var host = ScriptableObject.CreateInstance<SpeedCurveTargetVerificationHost>();
        try
        {
            foreach (EnemyProjectileSpeedCurveTarget target in Enum.GetValues(typeof(EnemyProjectileSpeedCurveTarget)))
            {
                var source = host.shooting;
                Set(source, "projectileSpeedCurveTarget", target);
                Set(source, "projectileSpeedBehavior", EnemyProjectileSpeedBehavior.Customized);
                Set(source, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
                var copy = new EnemyShootingSettings(); copy.CopyFrom(source);
                Check(copy.SpeedCurveTarget == target, "Preset/local copy retains target");
                Check(JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(source)).SpeedCurveTarget == target,
                    "Serialization retains target");
                var child = new EnemyChildAttackSettings(); child.Pattern.Shooting.CopyFrom(source);
                source.SetChildAttack(child); copy.CopyFrom(source);
                Check(copy.ChildAttack.Pattern.Shooting.SpeedCurveTarget == target, "Nested pattern copy retains target");
                var requests = new List<EnemyProjectileSpawnRequest>();
                EnemyAttackProjectileBuilder.AppendEmitter(requests, null, source, Vector3.zero, Vector3.zero, Vector3.up,
                    1f, 5f, 0f, default);
                Check(requests.Count > 0 && requests[0].SpeedCurveTarget == target, "Shared enemy/child builder transfers target");
                source.SetChildAttack(null);
            }
            using (var serialized = new SerializedObject(host))
            {
                var property = serialized.FindProperty("shooting");
                Check(ShowsTarget(property), "Orbit exposes target in Inspector");
                property.FindPropertyRelative("projectileMovementPattern").intValue = 0;
                Check(!ShowsTarget(property), "Straight movement hides irrelevant target");
                property.FindPropertyRelative("layoutRotationEnabled").boolValue = true;
                Check(ShowsTarget(property), "Rotation exposes target in Inspector");
                property.FindPropertyRelative("projectileSpeedBehavior").intValue = 0;
                Check(!ShowsTarget(property), "No curve hides target");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static bool ShowsTarget(SerializedProperty property)
    {
        var fields = (IEnumerable)typeof(EnemyShootingSettingsDrawer).GetMethod("ProjectileFields",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { property });
        foreach (string field in fields) if (field == "projectileSpeedCurveTarget") return true;
        return false;
    }

    private static void VerifyMotion(World world, EnemyProjectileEcsSpawner spawner, EnemyProjectileAuthoring authoring,
        SystemHandle speed, SystemHandle movement, EnemyProjectileSpeedCurveTarget target, float multiplier, float radial,
        EnemyProjectileOrbitCenterMode center, bool reached = true)
    {
        var em = world.EntityManager;
        var entity = Spawn(em, spawner, authoring, target, multiplier, radial, center, reached);
        var initial = em.GetComponentData<EnemyProjectileMovement>(entity);
        Near(em.GetComponentData<EnemyProjectileCustomSpeed>(entity).SpeedMultiplier, 1f, "Before trigger multiplier is one");
        float baseMultiplier = !reached || target == EnemyProjectileSpeedCurveTarget.Radial ? 1f : multiplier;
        float radialMultiplier = !reached || target == EnemyProjectileSpeedCurveTarget.BaseSpeed ? 1f : multiplier;
        for (int tick = 1; tick <= (reached ? 3 : 1); tick++)
        {
            world.SetTime(new TimeData(tick * .1, .1f));
            speed.Update(world.Unmanaged); movement.Update(world.Unmanaged);
            var data = em.GetComponentData<EnemyProjectileMovement>(entity);
            Near(data.OrbitRadius, 2f + radial * radialMultiplier * tick * .1f, "Selected radial scaling without accumulation");
            Near(data.OrbitAngleRadians, initial.OrbitAngleRadians - math.radians(90f) * (5f / initial.ReferenceSpeed)
                * baseMultiplier * tick * .1f, "Selected base scaling without accumulation");
            Near(math.length(em.GetComponentData<EnemyProjectileVelocity>(entity).Value), 5f * baseMultiplier,
                "Selected base velocity including zero multiplier");
            Check(math.all(math.isfinite(em.GetComponentData<LocalTransform>(entity).Position)), "Finite orbit at zero speed");
        }
        em.DestroyEntity(entity);
    }

    private static Entity Spawn(EntityManager em, EnemyProjectileEcsSpawner spawner, EnemyProjectileAuthoring authoring,
        EnemyProjectileSpeedCurveTarget target, float multiplier, float radial, EnemyProjectileOrbitCenterMode center,
        bool reached, EnemyProjectileMovementPattern pattern = EnemyProjectileMovementPattern.Orbit)
    {
        var e = em.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectileStaticData), typeof(EnemyProjectileVelocity),
            typeof(EnemyProjectileMovement), typeof(EnemyProjectilePreviousPosition), typeof(EnemyProjectileResolution),
            typeof(EnemyProjectileDamage), typeof(EnemyProjectileRemainingLifetime), typeof(EnemyProjectileCustomSpeed));
        em.AddBuffer<EnemyProjectileSpeedCurveSample>(e);
        var request = new EnemyProjectileSpawnRequest(null, new Vector3(2f, 0f), Vector3.up, 1f, 5f,
            EnemyProjectileSpeedBehavior.Customized, reached ? -100f : 100f, AnimationCurve.Constant(0f, 10f, multiplier),
            EnemyProjectileSizeBehavior.Default, 0f, 1f, null, 1f, Vector3.zero, pattern, 90f, 0f,
            center, Vector2.zero, 2f, radial, false, 100f, speedCurveTarget: target);
        var type = typeof(EnemyProjectileEcsSpawner).GetNestedType("PreparedProjectileSpawn", BindingFlags.NonPublic);
        object prepared = Activator.CreateInstance(type);
        type.GetField("Request").SetValue(prepared, request);
        type.GetField("Configuration").SetValue(prepared, authoring);
        type.GetField("EffectiveSpeed").SetValue(prepared, 5f);
        type.GetField("Direction").SetValue(prepared, new float2(0f, 1f));
        typeof(EnemyProjectileEcsSpawner).GetMethod("ConfigureSpawnedEntity", Private).Invoke(spawner, new[] { (object)e, prepared });
        Check(em.GetComponentData<EnemyProjectileCustomSpeed>(e).CurveTarget
            == (pattern == EnemyProjectileMovementPattern.Orbit ? target : EnemyProjectileSpeedCurveTarget.BaseSpeed),
            "Spawner transfers effective curve target");
        return e;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Near(float actual, float expected, string message) => Check(math.abs(actual - expected) < .002f,
        message + ": " + actual + " != " + expected);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
}
