using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyDamageTypeMigration
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    public static object Run()
    {
        checks = 0;
        Check(Enum.GetNames(typeof(EnemyDamageType)).Length == 4, "Exactly four damage types");
        var types = new[] { EnemyDamageType.Plazma, EnemyDamageType.Default, EnemyDamageType.Kinetick, EnemyDamageType.Chemical };
        var shield = new[] { 1.35f, 1f, 0.65f, 1f };
        var hull = new[] { 0.65f, 1f, 1.35f, 1f };
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Damage matrix fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            var enemy = root.AddComponent<Enemy>();
            var modifier = root.AddComponent<EnemyShieldModifier>();
            Set(modifier, "showShieldVisual", false);
            Set(modifier, "enemy", enemy);
            Set(enemy, "shieldModifier", modifier);
            Set(enemy, "_maxHealth", 1000f);
            Set(enemy, "shieldPoints", 1000f);
            for (int i = 0; i < types.Length; i++)
            {
                var profile = EnemyDamageProfiles.Get(types[i]);
                Near(profile.ShieldMultiplier, shield[i], "Shield profile");
                Near(profile.HullMultiplier, hull[i], "Hull profile");
                bool chemical = types[i] == EnemyDamageType.Chemical;
                Near(profile.ShieldBypassFraction, chemical ? 1f : 0f, "Chemical bypass only");
                enemy._currentHealth = 1000f;
                modifier.SetShieldPoints(1000f);
                enemy.TakeDamageWithType(100f, types[i]);
                Near(modifier.CurrentShieldPoints, chemical ? 1000f : 1000f - 100f * shield[i], "Real shield damage");
                Near(enemy._currentHealth, chemical ? 900f : 1000f, "Chemical hits hull through full shield");
                enemy._currentHealth = 1000f;
                modifier.SetShieldPoints(0f);
                enemy.TakeDamageWithType(100f, types[i]);
                Near(enemy._currentHealth, 1000f - 100f * hull[i], "Real hull damage");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }

        foreach (string candidate in File.ReadAllLines("AgentScripts/DamageTypeCandidatePaths.txt"))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(candidate.Replace('\\', '/'));
            var iterator = new SerializedObject(asset).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.Enum
                    && (iterator.name == "damageType" || iterator.name == "turretDamageType"))
                    Check(Enum.IsDefined(typeof(EnemyDamageType), iterator.intValue), candidate + " :: " + iterator.propertyPath);
            }
        }
        var weapons = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            Check(Enum.IsDefined(typeof(EnemyDamageType), weapon.DamageType), "Effective weapon type: " + path);
            weapons.Add(new { name = weapon.name, type = weapon.DamageType.ToString(), path });
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefub/Weapons" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Weapon prefab script references");
        }
        return new { passed = checks, weapons };
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Near(float actual, float expected, string label)
        => Check(Mathf.Abs(actual - expected) < 0.001f, label + ": " + actual);
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks++; }
}
