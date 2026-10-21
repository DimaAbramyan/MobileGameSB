using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public static class VerifyOrbitLiveRecovery
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Cleanup()
    {
        int removed = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Orbit temporary live verification") continue;
            var passive = root.GetComponentInChildren<OrbitPassiveAbility>(true);
            foreach (var satellite in (OrbitSatellite[])Get(passive, "satellites"))
                if (satellite != null) UnityEngine.Object.Destroy(satellite.gameObject);
            Set(passive, "satellites", null);
            UnityEngine.Object.Destroy(root);
            removed++;
        }
        return new { removed };
    }
    public static object Run()
    {
        if (!Application.isPlaying) throw new Exception("Requires Play Mode.");
        OrbitPassiveAbility passive = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<OrbitPassiveAbility>(true))
                if (candidate.GetComponent<ParentShip>().IsVisible) passive = candidate;
        GameObject fixture = null;
        if (passive == null)
        {
            fixture = new GameObject("Orbit temporary live verification");
            fixture.SetActive(false);
            var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab"), fixture.transform);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            ship.Awake();
            var ability = ship.GetComponent<OrbitExpansionActiveAbility>();
            ability.GetType().GetMethod("Awake", Private).Invoke(ability, null);
            passive.Init(ship);
            ship.ShowShip();
        }
        var active = passive.GetComponent<ParentShip>().ActiveAbility;
        var settings = (OrbitShipMetaContract)Get(passive, "settings");
        var satellite = ((OrbitSatellite[])Get(passive, "satellites"))[0];
        float cooldown = (float)Get(active, "cooldownTimer");
        float reserve = (float)Get(active, "toggleTimeRemaining");
        float elapsed = (float)Get(active, "toggleRechargeElapsed");
        GameObject shotRoot = null;
        try
        {
            Set(active, "cooldownTimer", 0f);
            float before = active.ToggleMaximumTime * 0.5f;
            Set(active, "toggleTimeRemaining", before);
            Set(active, "toggleRechargeElapsed", active.ExportMetaRecoverySettings().ToggleRechargeStartTime);
            shotRoot = new GameObject("Orbit recovery live probe projectile");
            var shot = shotRoot.AddComponent<EnemyBullet>();
            bool first = satellite.TryDestroyEnemyProjectile(shot);
            float after = active.ToggleTimeRemaining;
            bool duplicate = satellite.TryDestroyEnemyProjectile(shot);
            float expected = settings.CooldownReductionPerProjectile * active.ToggleMaximumTime
                / active.ExportMetaRecoverySettings().ToggleRechargeDuration;
            if (!first || duplicate || Mathf.Abs(after - before - expected) > 0.0001f
                || active.ToggleTimeRemaining != after)
                throw new Exception("Live interception reward mismatch: " + (after - before));
            return new { before, after, reserveAdded = after - before,
                recoverySeconds = settings.CooldownReductionPerProjectile, cooldownBefore = 0f,
                duplicateReward = false, temporaryPrefabInstance = fixture != null };
        }
        finally
        {
            Set(active, "cooldownTimer", cooldown);
            Set(active, "toggleTimeRemaining", reserve);
            Set(active, "toggleRechargeElapsed", elapsed);
            if (shotRoot != null) UnityEngine.Object.Destroy(shotRoot);
            if (fixture != null)
            {
                foreach (var satelliteObject in (OrbitSatellite[])Get(passive, "satellites"))
                    if (satelliteObject != null) UnityEngine.Object.Destroy(satelliteObject.gameObject);
                Set(passive, "satellites", null);
                UnityEngine.Object.Destroy(fixture);
            }
        }
    }
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Private);
            if (field != null) return field;
        }
        throw new Exception("Missing field: " + name);
    }
    private static object Get(object target, string field) => Field(target, field).GetValue(target);
    private static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);
}
