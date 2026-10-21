using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public static class VerifyDebuffDamageRouting
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Requires ready Edit Mode.");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var periodicConfig = ScriptableObject.CreateInstance<EnemyPeriodicDamageDebuffConfig>();
        var heatConfig = ScriptableObject.CreateInstance<EnemyHeatDebuffConfig>();
        var manager = new DealDamageManager();
        var container = new DiContainer();
        container.Bind<DealDamageManager>().FromInstance(manager);
        var lazy = new LazyInject<DealDamageManager>(container, new InjectContext(container, typeof(DealDamageManager)));
        var periodic = new EnemyPeriodicDamageSystem(null, lazy);
        var heat = new EnemyHeatSystem(null, lazy, null);
        var temperature = new EnemyTemperatureController(null, heat, lazy, container);
        float originalScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            var root = new GameObject("Debuff routing fixture");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var enemy = root.AddComponent<Enemy>();
            var shield = root.AddComponent<EnemyShieldModifier>();
            Set(enemy, "DoHaveBuff", false);
            Set(enemy, "_maxHealth", 1000f);
            Set(enemy, "shieldPoints", 1000f);
            Set(shield, "showShieldVisual", false);
            Set(shield, "enemy", enemy);
            enemy.Awake();
            Check(!periodicConfig.DamageShieldFirst && !heatConfig.DamageShieldFirst, "Default configs damage hull");

            foreach (EnemyDamageType type in Enum.GetValues(typeof(EnemyDamageType)))
            {
                var profile = EnemyDamageProfiles.Get(type);
                Reset(enemy, shield, 1000f);
                manager.DealDebuffDamage(enemy, null, 100f, type);
                Near(shield.CurrentShieldPoints, 1000f, "Default debuff keeps shield");
                Near(enemy._currentHealth, 1000f - 100f * profile.HullMultiplier, "Default debuff damages hull");
                Reset(enemy, shield, 1000f);
                manager.DealDebuffDamage(enemy, null, 100f, type, true);
                Near(shield.CurrentShieldPoints, 1000f - 100f * profile.ShieldMultiplier, "Shield-first respects multiplier");
                Near(enemy._currentHealth, 1000f, "Shield-first protects hull, including Chemical");
                Reset(enemy, shield, 50f);
                manager.DealDebuffDamage(enemy, null, 100f, type, true);
                Near(shield.CurrentShieldPoints, 0f, "Shield depleted first");
                Near(enemy._currentHealth, 1000f - (100f - 50f / profile.ShieldMultiplier) * profile.HullMultiplier,
                    "Only remaining damage reaches hull");
            }
            Reset(enemy, shield, 1000f);
            manager.DealDamage(enemy, null, 100f, EnemyDamageType.Chemical);
            Near(shield.CurrentShieldPoints, 1000f, "Ordinary Chemical still bypasses shield");
            Near(enemy._currentHealth, 900f, "Ordinary Chemical damages hull");

            foreach (bool shieldFirst in new[] { false, true })
            {
                Reset(enemy, shield, 1000f);
                Set(periodicConfig, "bypassesEnemyShield", !shieldFirst);
                periodic.Apply(enemy, periodicConfig, null);
                SetDueTick(periodic, enemy, "NextTickTime");
                periodic.Tick();
                Near(shield.CurrentShieldPoints, shieldFirst ? 995f : 1000f, "Real Chemical periodic tick shield routing");
                Near(enemy._currentHealth, shieldFirst ? 1000f : 995f, "Real Chemical periodic tick hull routing");
                periodic.Dispose();

                Reset(enemy, shield, 1000f);
                Set(heatConfig, "damageShieldFirst", shieldFirst);
                Set(heatConfig, "coolingDelay", 1000f);
                temperature.ApplyHeat(enemy, heatConfig, 100f, null);
                SetDueTick(temperature, enemy, "NextBurnDamageTime");
                Check(Time.deltaTime > 0f, "Editor frame allows burn tick");
                temperature.Tick();
                Near(shield.CurrentShieldPoints, shieldFirst ? 995f : 1000f, "Real burning tick shield routing");
                Near(enemy._currentHealth, shieldFirst ? 1000f : 995f, "Real burning tick hull routing");
                Check(heatConfig.CreateProfile(null).DamageShieldFirst == shieldFirst, "Thermal explosion profile routing");
                temperature.Dispose();
                heat.Dispose();
            }
            var saved = AssetDatabase.LoadAssetAtPath<EnemyPeriodicDamageDebuffConfig>(
                "Assets/Prefub/Data/EnemyDebuffs/AcidSprayerPeriodicDamageDebuff.asset");
            Check(saved != null && !saved.DamageShieldFirst, "Saved acid debuff defaults to hull");
            var inspector = Editor.CreateEditor(saved);
            Check(inspector.GetType().Name == "EnemyPeriodicDamageDebuffConfigEditor", "Positive checkbox Inspector selected");
            UnityEngine.Object.DestroyImmediate(inspector);
            return new { passed = checks, periodicTicks = true, burningTicks = true, chemicalProjectilePreserved = true };
        }
        finally
        {
            periodic.Dispose(); temperature.Dispose(); heat.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(periodicConfig);
            UnityEngine.Object.DestroyImmediate(heatConfig);
            Time.timeScale = originalScale;
        }
    }

    private static void Reset(Enemy enemy, EnemyShieldModifier shield, float points)
    { enemy._currentHealth = 1000f; shield.SetShieldPoints(points); }
    private static void SetDueTick(object system, Enemy enemy, string field)
    {
        var states = (IDictionary)system.GetType().GetField("states", Private).GetValue(system);
        object state = states[enemy];
        state.GetType().GetField(field).SetValue(state, Time.time - 0.01f);
        states[enemy] = state;
    }
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Near(float actual, float expected, string message)
        => Check(Mathf.Abs(actual - expected) < 0.001f, message + ": " + actual);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
}
