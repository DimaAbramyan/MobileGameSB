using System;
using System.Collections;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyFlamethrowerDistanceCurve
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string FlamePath = "Assets/Prefub/Data/ProjectileData/FlameThrowerProjectile.asset";

    public static object SaveCurve()
    {
        RequireIdle();
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(FlamePath);
        var so = new SerializedObject(data);
        var contracts = so.FindProperty("contracts");
        for (int i = 0; i < contracts.arraySize; i++)
        {
            var contract = contracts.GetArrayElementAtIndex(i);
            if (!(contract.managedReferenceValue is ProjectileFlightContract)) continue;
            var curve = contract.FindPropertyRelative("damageMultiplierByDistance");
            var current = curve.animationCurveValue;
            Undo.RecordObject(data, "Flamethrower distance damage curve");
            curve.animationCurveValue = current != null && current.length > 0
                ? current : AnimationCurve.Linear(0f, 1f, 1f, 0f);
            // Persist only the newly introduced curve; keep all existing weapon values.
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return new { path = FlamePath, start = curve.animationCurveValue.Evaluate(0f),
                end = curve.animationCurveValue.Evaluate(1f) };
        }
        throw new Exception("Flight contract missing");
    }

    public static object Run()
    {
        RequireIdle();
        int checks = 0;
        var flame = AssetDatabase.LoadAssetAtPath<ProjectileData>(FlamePath);
        var gamma = AssetDatabase.LoadAssetAtPath<ProjectileData>("Assets/Prefub/Data/ProjectileData/GammaRayProjectile.asset");
        var data = UnityEngine.Object.Instantiate(flame);
        var flight = FindFlight(data);
        var curveField = typeof(ProjectileFlightContract).GetField("damageMultiplierByDistance", Private);
        var curve = AnimationCurve.Linear(0f, 2f, 1f, 0.25f);
        curveField.SetValue(flight, curve);
        var config = data.CreateRuntimeConfig();
        Check(config.damageMultiplierByDistance == curve, "Runtime shares authored curve", ref checks);
        Check(gamma.CreateRuntimeConfig().damageMultiplierByDistance == null, "Gamma damage unaffected", ref checks);
        var clone = UnityEngine.Object.Instantiate(data.ProjectilePrefab);
        var world = new World("Distance curve verification");
        var spawner = new PlayerProjectileEcsSpawner();
        Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            clone.Awake();
            var parameters = new ProjectileParams { damage = 8f, speed = 2f, maxLength = 2f, direction = Vector3.up };
            clone.Init(parameters, config, null);
            Vector3 start = clone.transform.position;
            Vector3 initialScale = clone.transform.localScale;
            Check(Mathf.Abs(clone.GetDamage() - 16f) < 0.0001f, "Curve Y above one is supported", ref checks);
            clone.transform.localScale *= 5f;
            Check(Mathf.Abs(clone.GetDamage() - 16f) < 0.0001f, "Size does not drive damage anymore", ref checks);
            clone.transform.localScale = initialScale;
            Check(spawner.TrySpawn(data, Vector3.zero, parameters, null), "Actual ECS flame spawn", ref checks);
            var em = world.EntityManager;
            Entity entity;
            using (var query = em.CreateEntityQuery(typeof(PlayerProjectileDistanceDamageCurve)))
                entity = query.GetSingletonEntity();
            var movement = world.GetOrCreateSystem<PlayerProjectileMovementSystem>();
            var sharedCurve = em.GetComponentData<PlayerProjectileDistanceDamageCurve>(entity);
            Check(spawner.TrySpawn(data, Vector3.right, parameters, null), "Second ECS spawn", ref checks);
            using (var query = em.CreateEntityQuery(typeof(PlayerProjectileDistanceDamageCurve)))
            using (var curves = query.ToComponentDataArray<PlayerProjectileDistanceDamageCurve>(Unity.Collections.Allocator.Temp))
                Check(curves[0].Data == curves[1].Data, "Curve native data is shared between all shots", ref checks);
            for (int i = 1; i <= 12; i++)
            {
                world.SetTime(new TimeData(i * 0.1, 0.1f));
                movement.Update(world.Unmanaged);
                float progress = Mathf.Clamp01(i * 0.1f);
                float expected = curve.Evaluate(progress);
                var damage = em.GetComponentData<PlayerProjectileDamage>(entity);
                Check(Mathf.Abs(damage.Value - 8f * expected) < 0.0001f, "ECS normalized distance damage", ref checks);
                Check(Mathf.Abs(1f + damage.HeatMultiplierDelta - expected) < 0.0001f,
                    "ECS heat uses same distance multiplier", ref checks);
                clone.transform.position = start + Vector3.up * i * 0.2f;
                Check(Mathf.Abs(clone.GetDamage() - damage.Value) < 0.0001f, "MB and ECS curve agree", ref checks);
            }
            clone.ResetState(); clone.Init(parameters, config, null);
            Check(Mathf.Abs(clone.GetDamage() - 16f) < 0.0001f, "Pool reuse resets distance", ref checks);
            // Accumulated path: two opposite one-unit segments still mean full range traveled.
            Set(clone, "traveledDistance", 1f); Set(clone, "lastTravelPosition", start + Vector3.up);
            clone.transform.position = start;
            Check(Mathf.Abs(clone.GetDamage() - 2f) < 0.0001f, "Curved/reversed path uses cumulative distance", ref checks);
            curveField.SetValue(flight, AnimationCurve.Constant(0f, 1f, -2f));
            clone.ResetState(); clone.Init(parameters, data.CreateRuntimeConfig(), null);
            Check(clone.GetDamage() == 0f, "Negative curve Y clamps to zero", ref checks);
            Check(sharedCurve.Evaluate(-1f) == 2f && sharedCurve.Evaluate(2f) == 0.25f,
                "ECS endpoints clamp normalized X", ref checks);
            VerifyHeat(ref checks);
        }
        finally
        {
            spawner.Dispose(); // Entity references destroyed before shared curve storage.
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(clone.gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
        return new { passed = checks };
    }

    private static void VerifyHeat(ref int checks)
    {
        var root = new GameObject("Distance heat verification");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        Set(enemy, "DoHaveBuff", false); Set(enemy, "_maxHealth", 10000f);
        enemy.Awake();
        var heatConfig = ScriptableObject.CreateInstance<EnemyHeatDebuffConfig>();
        Set(heatConfig, "coolingDelay", 1000f);
        var manager = new DealDamageManager();
        var container = new Zenject.DiContainer();
        container.Bind<DealDamageManager>().FromInstance(manager);
        var lazyDamage = new Zenject.LazyInject<DealDamageManager>(container,
            new Zenject.InjectContext(container, typeof(DealDamageManager)));
        var heat = new EnemyHeatSystem(null, null, null);
        var temperature = new EnemyTemperatureController(null, heat, lazyDamage, container);
        var debuffs = new EnemyDebuffController(null, temperature, null, null);
        Set(manager, "enemyDebuffController", debuffs);
        try
        {
            var applications = new[] { new EnemyDebuffApplication(heatConfig, 20f) };
            float hp = enemy._currentHealth;
            manager.DealDamage(enemy, null, 4f, EnemyDamageType.Default, true,
                Vector3.zero, applications, 0.25f);
            Check(Mathf.Abs(heat.GetHeatPercent(enemy) - 5f) < 0.0001f,
                "Actual damage bridge scales temperature amount by curve", ref checks);
            manager.DealDamage(enemy, null, 4f, EnemyDamageType.Default, true,
                Vector3.zero, applications, 2f);
            Check(Mathf.Abs(heat.GetHeatPercent(enemy) - 45f) < 0.0001f,
                "Temperature amplification above one works", ref checks);
            temperature.ApplyHeat(enemy, heatConfig, 100f, null);
            var states = (IDictionary)typeof(EnemyTemperatureController).GetField("states", Private).GetValue(temperature);
            object state = states[enemy];
            Type stateType = state.GetType();
            float nextTick = (float)stateType.GetField("NextBurnDamageTime").GetValue(state);
            Check(Mathf.Abs(nextTick - (Time.time + heatConfig.BurningDamageInterval)) < 0.0001f,
                "Ignition waits for first interval", ref checks);
            float scheduled = Time.time + 0.123f;
            stateType.GetField("NextBurnDamageTime").SetValue(state, scheduled);
            states[enemy] = state;
            temperature.ApplyHeat(enemy, heatConfig, 1f, null);
            state = states[enemy];
            Check((float)stateType.GetField("NextBurnDamageTime").GetValue(state) == scheduled,
                "Refresh preserves scheduled tick instead of immediate damage/reset", ref checks);
            Check((float)stateType.GetField("BurningEndTime").GetValue(state) >= Time.time + heatConfig.BurningDuration - 0.001f,
                "Refresh extends burning duration", ref checks);
            Check(Time.deltaTime > 0f, "Editor frame has positive delta for actual burning tick", ref checks);
            float beforeBurn = enemy._currentHealth;
            temperature.Tick();
            Check(enemy._currentHealth == beforeBurn, "Refresh causes no immediate burn damage", ref checks);
            state = states[enemy];
            stateType.GetField("NextBurnDamageTime").SetValue(state, Time.time - 0.01f);
            states[enemy] = state;
            temperature.Tick();
            Check(Mathf.Abs(enemy._currentHealth - (beforeBurn - heatConfig.BurningDamagePerTick)) < 0.0001f,
                "Due burn tick damages exactly once", ref checks);
            temperature.ApplyHeat(enemy, heatConfig, 1f, null);
            float afterBurn = enemy._currentHealth;
            temperature.Tick();
            Check(enemy._currentHealth == afterBurn, "Refresh after tick cannot bypass interval", ref checks);
            Check(enemy._currentHealth < hp && !enemy.isDead, "Real hits damage living enemy", ref checks);
        }
        finally
        {
            temperature.Dispose(); debuffs.Dispose(); heat.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(heatConfig);
        }
    }

    private static ProjectileFlightContract FindFlight(ProjectileData data)
    {
        if (data.TryGetContract<ProjectileFlightContract>(out var flight)) return flight;
        throw new Exception("Flight missing");
    }
    private static void Set(object target, string field, object value)
    { target.GetType().GetField(field, Private).SetValue(target, value); }
    private static void Check(bool value, string message, ref int checks)
    { if (!value) throw new Exception(message); checks++; }
    private static void RequireIdle()
    { if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Editor must be idle"); }
}
