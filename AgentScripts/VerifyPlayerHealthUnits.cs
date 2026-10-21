using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

// Runs production damage/regen code and projectile spawning in isolation.
public static class VerifyPlayerHealthUnits
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var checks = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData"))
        {
            var ship = AssetDatabase.LoadAssetAtPath<ShipData>(AssetDatabase.GUIDToAssetPath(guid));
            var stats = ship.GetMetaRuntimeStats(0);
            Check(stats.MaximumHealthPoints == Mathf.Round(stats.MaximumHealthPoints)
                && stats.MaximumShieldPoints == Mathf.Round(stats.MaximumShieldPoints)
                && ship.HealthRegenCooldown == 60 && ship.ShieldRegenCooldown == 30,
                "Effective stats/cooldowns: " + ship.name, checks);
            if (ship.name == "BlackHole")
                Check(stats.MaximumHealthPoints == 100000, "BlackHole test HP preserved", checks);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:LevelConfig"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(AssetDatabase.GUIDToAssetPath(guid));
            Check(level.EnemyDamageMultiplier == 1, "No damage scaling: " + level.name, checks);
        }

        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefub/Ships/Arkanoid.prefab"));
        var world = new World("Health units verification");
        EnemyProjectileEcsSpawner spawner = null;
        try
        {
            var ship = clone.GetComponent<ParentShip>();
            ship.Awake();
            var health = clone.GetComponent<HealthRegeneration>() ?? clone.AddComponent<HealthRegeneration>();
            var shield = clone.GetComponent<ShieldRegeneration>() ?? clone.AddComponent<ShieldRegeneration>();
            Invoke(health, "Awake");
            Invoke(shield, "Awake");
            ship.SetShieldPoints(2);
            float hp = ship.CurrentHealthPoints;
            float seenDamage = 0;
            ship.OnDamageTaken += value => seenDamage = value;
            Check(ship.TryTakeDamage(999) && ship.CurrentShieldPoints == 1
                && ship.CurrentHealthPoints == hp && seenDamage == 1,
                "Legacy large damage consumes one shield unit", checks);
            Check(!ship.TryTakeDamage(999) && ship.CurrentShieldPoints == 1,
                "Shared hit invulnerability remains active", checks);
            ClearInvulnerability(ship);
            ship.TakeDamage(50);
            Check(ship.CurrentShieldPoints == 0 && ship.CurrentHealthPoints == hp,
                "Generic TakeDamage consumes the last shield unit", checks);
            ClearInvulnerability(ship);
            Check(ship.TryTakeDamage(0.2f) && ship.CurrentHealthPoints == hp - 1,
                "Unshielded hit consumes exactly one HP", checks);
            ClearInvulnerability(ship);
            Check(!ship.TryTakeDamage(0) && !ship.TryTakeDamage(-5)
                && ship.CurrentHealthPoints == hp - 1, "Non-damaging calls remain ignored", checks);
            Func<float, float> block = value => 0;
            ship.OnDamagePipeline += block;
            Check(!ship.TryTakeDamage(500) && ship.CurrentHealthPoints == hp - 1,
                "Defensive pipeline can still absorb a hit", checks);
            ship.OnDamagePipeline -= block;

            Check(!CanRegenerate(health) && !CanRegenerate(shield),
                "A hit restarts both regeneration cooldowns", checks);
            Set(health, "lastDamageTime", Time.time - 59);
            Set(shield, "lastDamageTime", Time.time - 29);
            Check(!CanRegenerate(health) && !CanRegenerate(shield),
                "Regeneration remains locked before 60/30 seconds", checks);
            Set(health, "lastDamageTime", Time.time - 60);
            Set(shield, "lastDamageTime", Time.time - 30);
            Check(CanRegenerate(health) && CanRegenerate(shield),
                "Regeneration unlocks at 60/30 seconds", checks);

            spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world);
            foreach (string file in new[] { "Bullet", "HomingEnemyProjectile", "BurstAtPointEnemyProjectile" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefub/Enemy Projectiles/" + file + ".prefab").GetComponent<EnemyBullet>();
                foreach (float multiplier in new[] { 0.1f, 1f, 100f })
                    Check(spawner.TrySpawn(prefab, Vector3.zero, Vector3.down, multiplier),
                        "Spawn " + file + " at old multiplier " + multiplier, checks);
            }
            using (var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileDamage)))
            using (var damages = query.ToComponentDataArray<EnemyProjectileDamage>(Unity.Collections.Allocator.Temp))
            {
                Check(damages.Length == 9, "All ECS projectile variants spawned", checks);
                for (int i = 0; i < damages.Length; i++)
                    Check(damages[i].Value == 1, "ECS projectile unit damage " + i, checks);
            }
            var modifier = clone.AddComponent<WaveEnemyDifficultyModifier>();
            Set(modifier, "damageMultiplier", 100f);
            modifier.ConfigureLevelMultipliers(AssetDatabase.LoadAssetAtPath<LevelConfig>(
                "Assets/Config/Levels/Level_1_Extreme.asset"));
            Check(modifier.TotalDamageMultiplier == 1, "Old wave damage multiplier is ignored", checks);
        }
        finally
        {
            // Spawner destroys entities before freeing their render resources.
            spawner?.Dispose();
            world.Dispose();
            UnityEngine.Object.DestroyImmediate(clone);
        }
        return new { passed = checks.Count, checks };
    }
    private static void ClearInvulnerability(ParentShip ship) => Set(ship, "damageInvulnerableUntil", float.NegativeInfinity);
    private static bool CanRegenerate(object value) => (bool)value.GetType().GetProperty("CanRegenerate", Private).GetValue(value);
    private static void Invoke(object value, string method) => value.GetType().GetMethod(method, Private).Invoke(value, null);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Private).SetValue(value, data);
    private static void Check(bool condition, string message, List<string> checks)
    {
        if (!condition) throw new Exception(message);
        checks.Add(message);
    }
}
