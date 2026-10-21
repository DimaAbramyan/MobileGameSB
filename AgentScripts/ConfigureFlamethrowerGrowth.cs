using System;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class ConfigureFlamethrowerGrowth
{
    private const string FlamePath = "Assets/Prefub/Data/ProjectileData/FlameThrowerProjectile.asset";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Apply()
    {
        RequireIdle();
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(FlamePath);
        var so = new SerializedObject(data);
        var contracts = so.FindProperty("contracts");
        for (int i = 0; i < contracts.arraySize; i++)
        {
            var contract = contracts.GetArrayElementAtIndex(i);
            if (!(contract.managedReferenceValue is ProjectileFlightContract)) continue;
            Undo.RecordObject(data, "Flamethrower growth damage falloff");
            contract.FindPropertyRelative("growDuringFlight").boolValue = true;
            contract.FindPropertyRelative("reduceDamageWithGrowth").boolValue = true;
            contract.FindPropertyRelative("damageGrowthFalloffPower").floatValue = 1f;
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
            var config = data.CreateRuntimeConfig();
            return new { grow = config.growDuringFlight, growth = config.scaleGrowthPerSecond,
                damageFalloffPower = config.damageGrowthFalloffPower };
        }
        throw new Exception("Flamethrower Flight contract not found.");
    }
    public static object Verify()
    {
        RequireIdle();
        int checks = 0;
        var flame = AssetDatabase.LoadAssetAtPath<ProjectileData>(FlamePath);
        var gamma = AssetDatabase.LoadAssetAtPath<ProjectileData>("Assets/Prefub/Data/ProjectileData/GammaRayProjectile.asset");
        var config = flame.CreateRuntimeConfig();
        Check(config.growDuringFlight && config.damageGrowthFalloffPower == 1, "Flame config enabled", ref checks);
        Check(gamma.CreateRuntimeConfig().damageGrowthFalloffPower == 0, "Gamma damage remains unchanged", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(2, 2), new float2(1), 1) == 0.5f,
            "Double size halves damage", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(4, 1), new float2(1), 1) == 0.25f,
            "Single-axis growth also reduces damage", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(0.5f), new float2(1), 1) == 1,
            "Shrinking never boosts damage", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(2), new float2(1), 2) == 0.25f,
            "Configurable falloff power works", ref checks);
        Check(math.abs(ProjectileGrowthDamage.EvaluateMultiplier(new float2(4), new float2(1), 0.5f) - 0.5f) < 0.0001f,
            "Half coefficient uses square-root size falloff", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(4), new float2(1), 2) == 0.0625f,
            "Double coefficient uses squared size falloff", ref checks);
        Check(ProjectileGrowthDamage.EvaluateMultiplier(new float2(4), new float2(1), 0) == 1,
            "Zero coefficient disables damage falloff", ref checks);
        var clone = UnityEngine.Object.Instantiate(flame.ProjectilePrefab);
        var world = new World("Flamethrower growth verification");
        var spawner = new PlayerProjectileEcsSpawner();
        Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            clone.Awake();
            var parameters = new ProjectileParams { damage = 8, speed = 5, maxLength = 3, direction = Vector3.up };
            clone.Init(parameters, config, null);
            Check(clone.GetDamage() == 8, "MonoBehaviour starts with full damage", ref checks);
            Vector3 initial = clone.transform.localScale;
            clone.transform.localScale = initial * 2;
            Check(clone.GetDamage() == 4 && clone.damage == 4, "MB contact/property see reduced damage", ref checks);
            clone.SetDamage(12);
            Check(clone.GetDamage() == 6, "Runtime damage modifier is evaluated once", ref checks);
            clone.ResetState(); clone.Init(parameters, config, null);
            Check(clone.GetDamage() == 8 && clone.transform.localScale == initial, "Pool reuse resets growth and damage", ref checks);
            Check(spawner.TrySpawn(flame, Vector3.zero, parameters, null), "Real ECS flame spawned", ref checks);
            var em = world.EntityManager;
            Entity flameEntity;
            using (var query = em.CreateEntityQuery(typeof(PlayerProjectileDamage))) flameEntity = query.GetSingletonEntity();
            Check(em.GetComponentData<PlayerProjectileScaleState>(flameEntity).InitialDamage == 8,
                "ECS caches per-shot base damage", ref checks);
            Check(spawner.TrySpawn(gamma, Vector3.right, parameters, null), "Real ECS Gamma spawned", ref checks);
            Entity gammaEntity = Entity.Null;
            using (var query = em.CreateEntityQuery(typeof(PlayerProjectileDamage)))
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                for (int i = 0; i < entities.Length; i++) if (entities[i] != flameEntity) gammaEntity = entities[i];
            var movement = world.GetOrCreateSystem<PlayerProjectileMovementSystem>();
            for (int i = 1; i <= 10; i++)
            {
                world.SetTime(new TimeData(i * 0.1, 0.1f)); movement.Update(world.Unmanaged);
                var scale = em.GetComponentData<PlayerProjectileScaleState>(flameEntity);
                float ratio = math.max(1, math.cmax(math.abs(scale.RootScale) / math.abs(scale.InitialRootScale)));
                Check(math.abs(em.GetComponentData<PlayerProjectileDamage>(flameEntity).Value - 8 / ratio) < 0.0001f,
                    "ECS damage follows current size without compounding", ref checks);
                Check(em.GetComponentData<PlayerProjectileDamage>(gammaEntity).Value == 8,
                    "Gamma growth preserves its damage", ref checks);
                clone.transform.localScale = new Vector3(scale.RootScale.x, scale.RootScale.y, initial.z);
                Check(math.abs(clone.GetDamage() - em.GetComponentData<PlayerProjectileDamage>(flameEntity).Value) < 0.0001f,
                    "ECS and MB damage agree", ref checks);
            }
        }
        finally { spawner.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(clone.gameObject); }
        return new { passed = checks };
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Check(bool value, string message, ref int count) { if (!value) throw new Exception(message); count++; }
    private static void RequireIdle()
    { if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Editor must be idle."); }
}
