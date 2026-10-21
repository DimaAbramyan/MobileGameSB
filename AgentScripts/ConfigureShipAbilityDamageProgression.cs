using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ConfigureShipAbilityDamageProgression
{
    private const string ConfigFolder =
        "Assets/Prefub/Data/Ship/AbilityDamageProgression";
    private const float DefaultBonusPercent = 10f;

    public static object Execute()
    {
        EnsureFolder(ConfigFolder);

        var configuredHulls = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData"))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ShipData shipData = AssetDatabase.LoadAssetAtPath<ShipData>(assetPath);
            ParentShip hull = shipData?.ShipMetaConfig?.SourceHullPrefab;
            if (shipData == null || !HasDamagingAbility(hull))
                continue;

            ShipAbilityDamageProgressionConfig config =
                shipData.AbilityDamageProgressionConfig;
            if (config == null)
            {
                string configPath = AssetDatabase.GenerateUniqueAssetPath(
                    ConfigFolder + "/" + SanitizeFileName(shipData.name)
                    + "AbilityDamageProgression.asset");
                config = ScriptableObject.CreateInstance<
                    ShipAbilityDamageProgressionConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                Undo.RecordObject(shipData, "Assign ship ability damage progression");
                shipData.SetAbilityDamageProgressionConfig(config);
            }

            config.SetUniformBonusPercent(DefaultBonusPercent);
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(shipData);
            configuredHulls.Add(shipData.name + " -> "
                + AssetDatabase.GetAssetPath(config));
        }

        AssetDatabase.SaveAssets();
        return new
        {
            configured = configuredHulls.ToArray(),
            skipped = new[] { "Heavy", "Huskar", "Ghost" },
            bonusPerLevel = DefaultBonusPercent
        };
    }

    private static bool HasDamagingAbility(ParentShip hull)
    {
        return hull != null
            && (hull.ActiveAbility is PrismBeamActiveAbility
                || hull.ActiveAbility is DictatorTurretActiveAbility
                || hull.ActiveAbility is CreateBladeAbility
                || hull.ActiveAbility is BlackHoleActive
                || hull.PassiveAbility is ArkanoidPassiveAbility);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value;
    }
}
