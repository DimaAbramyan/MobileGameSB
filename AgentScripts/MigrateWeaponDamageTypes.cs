using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MigrateWeaponDamageTypes
{
    public static object Run(bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Requires ready Edit Mode.");
        var changes = new List<string>();
        foreach (string candidate in File.ReadAllLines("AgentScripts/DamageTypeCandidatePaths.txt"))
        {
            string path = candidate.Replace('\\', '/');
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(asset is WeaponData || asset is ProjectileData
                || asset is EnemyPeriodicDamageDebuffConfig || asset is ShipMetaConfig))
                throw new InvalidOperationException("Unexpected migration target: " + path);
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Enum
                    || !(property.name == "damageType" || property.name == "turretDamageType"))
                    continue;
                int mapped = property.intValue switch
                {
                    0 => 0, 1 => 1, 2 => 2, 3 => 1, 4 => 4, 5 => 0, 6 => 1,
                    _ => throw new InvalidOperationException("Unknown damage value in " + path)
                };
                if (mapped == property.intValue) continue;
                changes.Add(path + " :: " + property.propertyPath + " " + property.intValue + " -> " + mapped);
                if (!apply) continue;
                if (!changed) Undo.RecordObject(asset, "Migrate weapon damage types");
                property.intValue = mapped;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }
        MergeBonus("Assets/Prefub/Data/ShipMeta/DictatorMeta.asset",
            "kineticDamageBonusPercent", "explosionDamageBonusPercent", apply, changes);
        MergeBonus("Assets/Prefub/Data/ShipMeta/PrismMeta.asset",
            "plasmaDamageBonusPercent", "electricDamageBonusPercent", apply, changes);
        if (apply) AssetDatabase.SaveAssets();
        return new { applied = apply, changedFields = changes.Count, changes };
    }

    private static void MergeBonus(string path, string primary, string legacy, bool apply, List<string> changes)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>(path);
        var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.name != primary) continue;
            string sibling = property.propertyPath.Substring(0,
                property.propertyPath.Length - primary.Length) + legacy;
            var old = serialized.FindProperty(sibling);
            if (old == null || old.floatValue == 0f) continue;
            changes.Add(path + " :: merge " + legacy + " into " + primary);
            if (!apply) continue;
            Undo.RecordObject(asset, "Merge damage passive bonuses");
            property.floatValue = Mathf.Max(property.floatValue, old.floatValue);
            old.floatValue = 0f;
        }
        if (apply && serialized.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
    }

    public static object RestoreChemical(bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Requires ready Edit Mode.");
        string[] paths = {
            "Assets/Prefub/Data/EnemyDebuffs/AcidSprayerPeriodicDamageDebuff.asset",
            "Assets/Prefub/Data/ProjectileData/AcidSprayerProjectile.asset",
            "Assets/Prefub/Data/ProjectileData/FlameThrowerProjectile.asset",
            "Assets/Prefub/Data/ProjectileData/FreezeThrowerProjectile.asset",
            "Assets/Prefub/Data/WeaponData/GamePlayProgression/AcidSprayer.asset",
            "Assets/Prefub/Data/WeaponData/GamePlayProgression/FlameThrower.asset",
            "Assets/Prefub/Data/WeaponData/GamePlayProgression/FreezeThrower.asset"
        };
        var changes = new List<string>();
        foreach (string path in paths)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty("damageType");
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("Missing damageType: " + path);
            if (property.intValue == (int)EnemyDamageType.Chemical) continue;
            if (property.intValue != (int)EnemyDamageType.Default)
                throw new InvalidOperationException("Damage type changed externally: " + path);
            changes.Add(path);
            if (!apply) continue;
            Undo.RecordObject(asset, "Restore chemical damage");
            property.intValue = (int)EnemyDamageType.Chemical;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
        if (apply) AssetDatabase.SaveAssets();
        return new { applied = apply, changedAssets = changes.Count, changes };
    }
}
