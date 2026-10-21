using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// An explicit before/after plan makes applying the asset migration idempotent.
// Only listed fields are changed; all asset writes go through Unity APIs.
public static class MigratePlayerHealthUnits
{
    private const string PlanPath = "AgentScripts/PlayerHealthUnitsMigrationPlan.json";
    [Serializable] public sealed class Change
    {
        public string asset;
        public string field;
        public float before;
        public float after;
    }
    [Serializable] public sealed class Plan { public List<Change> changes = new List<Change>(); }

    public static object Prepare()
    {
        RequireIdle();
        if (File.Exists(PlanPath) && File.ReadAllText(PlanPath).Trim() != "{}")
            throw new Exception("Migration plan already exists; inspect it instead of recalculating unit values.");
        var plan = new Plan();
        int ships = 0, metas = 0, levels = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<ShipData>(path);
            var serialized = new SerializedObject(data);
            Scale(plan, path, serialized, "maximumHealthPoints");
            Scale(plan, path, serialized, "maximumShieldPoints");
            Add(plan, path, serialized, "healthRegenCooldown", 60f);
            Add(plan, path, serialized, "shieldRegenCooldown", 30f);
            ships++;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ShipMetaConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<ShipMetaConfig>(path));
            SerializedProperty list = serialized.FindProperty("levels");
            for (int i = 0; i < list.arraySize; i++)
            {
                string prefix = "levels.Array.data[" + i + "].basicStats.";
                Scale(plan, path, serialized, prefix + "maximumHealthPoints");
                Scale(plan, path, serialized, prefix + "maximumShieldPoints");
            }
            metas++;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:LevelConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<LevelConfig>(path));
            Add(plan, path, serialized, "enemyDamageMultiplier", 1f);
            levels++;
        }
        File.WriteAllText(PlanPath, Newtonsoft.Json.JsonConvert.SerializeObject(plan, Newtonsoft.Json.Formatting.Indented));
        return new { ships, metas, levels, fields = plan.changes.Count, plan = PlanPath };
    }

    public static object Apply()
    {
        RequireIdle();
        Plan plan = Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(File.ReadAllText(PlanPath));
        var groups = new Dictionary<string, List<Change>>();
        foreach (Change change in plan.changes)
        {
            if (!groups.TryGetValue(change.asset, out var list))
            {
                list = new List<Change>();
                groups.Add(change.asset, list);
            }
            list.Add(change);
        }
        // Validate every target before writing any asset.
        foreach (var group in groups)
        {
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(group.Key));
            foreach (Change change in group.Value)
            {
                float value = serialized.FindProperty(change.field).floatValue;
                if (!Mathf.Approximately(value, change.before) && !Mathf.Approximately(value, change.after))
                    throw new Exception("Asset changed since planning: " + change.asset + " / " + change.field);
            }
        }
        int changed = 0;
        foreach (var group in groups)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(group.Key);
            Undo.RecordObject(asset, "Migrate player health to hit units");
            var serialized = new SerializedObject(asset);
            foreach (Change change in group.Value)
                serialized.FindProperty(change.field).floatValue = change.after;
            serialized.ApplyModifiedProperties();
            // Also persist values that OnValidate already updated during reload.
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            changed++;
        }
        return new { savedAssets = changed, fields = plan.changes.Count };
    }

    public static object Verify()
    {
        RequireIdle();
        Plan plan = Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(File.ReadAllText(PlanPath));
        foreach (Change change in plan.changes)
        {
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(change.asset));
            if (!Mathf.Approximately(serialized.FindProperty(change.field).floatValue, change.after))
                throw new Exception("Migration mismatch: " + change.asset + " / " + change.field);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData"))
        {
            var ship = AssetDatabase.LoadAssetAtPath<ShipData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ship.HealthRegenCooldown != 60f || ship.ShieldRegenCooldown != 30f)
                throw new Exception("Incorrect effective cooldown: " + ship.name);
            if (ship.MetaMaximumHealthPoints != Mathf.Round(ship.MetaMaximumHealthPoints)
                || ship.MetaMaximumShieldPoints != Mathf.Round(ship.MetaMaximumShieldPoints))
                throw new Exception("Non-integral effective hit points: " + ship.name);
        }
        return new { verifiedFields = plan.changes.Count };
    }

    private static void Scale(Plan plan, string path, SerializedObject serialized, string field)
    {
        float value = serialized.FindProperty(field).floatValue;
        float converted = value <= 0f ? 0f : Mathf.Max(1f, Mathf.Round(value / 10f));
        Add(plan, path, serialized, field, converted);
    }
    private static void Add(Plan plan, string path, SerializedObject serialized, string field, float value)
    {
        plan.changes.Add(new Change { asset = path, field = field,
            before = serialized.FindProperty(field).floatValue, after = value });
    }
    private static void RequireIdle()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
    }
}
