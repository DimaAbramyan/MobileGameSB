using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyUnitRegeneration
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var checks = new List<string>();
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefub/Ships/Arkanoid.prefab"));
        var black = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab"));
        try
        {
            var ship = clone.GetComponent<ParentShip>();
            ship.Awake();
            var hp = clone.GetComponent<HealthRegeneration>() ?? clone.AddComponent<HealthRegeneration>();
            var shield = clone.GetComponent<ShieldRegeneration>() ?? clone.AddComponent<ShieldRegeneration>();
            Invoke(hp, "Awake"); Invoke(shield, "Awake");
            ship.IsVisible = false;
            ship.SetHealthPoints(2); ship.SetShieldPoints(1);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 2 && ship.CurrentShieldPoints == 1,
                "First recovery waits a full cooldown", checks);
            SetElapsed(hp, 59); SetElapsed(shield, 29);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 2 && ship.CurrentShieldPoints == 1,
                "No recovery before 60/30 seconds", checks);
            SetElapsed(hp, 60); SetElapsed(shield, 30);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 3 && ship.CurrentShieldPoints == 2,
                "Exactly one unit at each cooldown boundary", checks);
            Check(ship.HealthRegenerationCooldownProgress == 0 && ship.ShieldRegenerationCooldownProgress == 0,
                "Both progress indicators restart after a recovery", checks);
            for (int i = 0; i < 20; i++) { Invoke(hp, "Update"); Invoke(shield, "Update"); }
            Check(ship.CurrentHealthPoints == 3 && ship.CurrentShieldPoints == 2,
                "Repeated frames cannot continue healing", checks);
            SetElapsed(hp, 59); SetElapsed(shield, 29);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 3 && ship.CurrentShieldPoints == 2,
                "Second step waits another full cooldown", checks);
            SetElapsed(hp, 60); SetElapsed(shield, 30);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 4 && ship.CurrentShieldPoints == 3,
                "Second recovery is also one unit", checks);

            ship.IsVisible = true;
            SetElapsed(hp, 60); SetElapsed(shield, 30);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 4 && ship.CurrentShieldPoints == 4,
                "Existing active-ship shield/hidden-ship hull rules are preserved", checks);
            ship.IsVisible = false;
            int healthFull = 0, shieldFull = 0;
            ship.OnHealthFullyRegenerated += () => healthFull++;
            ship.OnShieldFullyRegenerated += () => shieldFull++;
            SetElapsed(hp, 60); Invoke(hp, "Update"); SetElapsed(shield, 30); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 5 && ship.CurrentShieldPoints == 5
                && healthFull == 1 && shieldFull == 1, "Full recovery events fire once", checks);
            SetElapsed(hp, 120); SetElapsed(shield, 120);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 5 && ship.CurrentShieldPoints == 5
                && healthFull == 1 && shieldFull == 1, "Full bars neither over-heal nor repeat events", checks);

            ship.SetHealthPoints(2); ship.SetShieldPoints(2);
            SetElapsed(hp, 55); SetElapsed(shield, 25);
            Check(ship.TryTakeDamage(999), "Incoming hit accepted", checks);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 2 && ship.CurrentShieldPoints == 1
                && ship.HealthRegenerationCooldownProgress == 0 && ship.ShieldRegenerationCooldownProgress == 0,
                "Damage restarts both recovery timers", checks);
            SetElapsed(hp, 120); SetElapsed(shield, 120);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 3 && ship.CurrentShieldPoints == 2,
                "Late update performs one step and starts a new cooldown", checks);
            ship.SetHealthPoints(4.5f); ship.SetShieldPoints(4.5f);
            SetElapsed(hp, 60); SetElapsed(shield, 30);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(ship.CurrentHealthPoints == 5 && ship.CurrentShieldPoints == 5,
                "Last partial deficit clamps to the maximum", checks);

            var blackShip = black.GetComponent<ParentShip>(); blackShip.Awake();
            // Test a large pool on this isolated clone without relying on designer test stats.
            blackShip.AddMaxHealthPoints(100000f - blackShip.MaximumHealthPoints);
            var blackHp = black.GetComponent<HealthRegeneration>() ?? black.AddComponent<HealthRegeneration>();
            Invoke(blackHp, "Awake"); blackShip.IsVisible = false;
            blackShip.SetHealthPoints(99990); SetElapsed(blackHp, blackShip.ShipData.HealthRegenCooldown); Invoke(blackHp, "Update");
            Check(blackShip.CurrentHealthPoints == 99991,
                "100000-HP BlackHole still restores only one unit", checks);
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); UnityEngine.Object.DestroyImmediate(black); }
        return new { passed = checks.Count, checks };
    }
    private static void SetElapsed(object value, float seconds) => value.GetType().GetField("lastDamageTime", Private).SetValue(value, Time.time - seconds);
    private static void Invoke(object value, string method) => value.GetType().GetMethod(method, Private).Invoke(value, null);
    private static void Check(bool value, string message, List<string> checks)
    { if (!value) throw new Exception(message); checks.Add(message); }
}
