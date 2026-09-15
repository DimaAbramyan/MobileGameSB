using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipMetaConfig))]
public sealed class ShipMetaConfigEditor : Editor
{
    private SerializedProperty sourceShipData;
    private SerializedProperty sourceHullPrefab;
    private SerializedProperty maxLevels;
    private SerializedProperty levels;

    private void OnEnable()
    {
        sourceShipData = serializedObject.FindProperty("sourceShipData");
        sourceHullPrefab = serializedObject.FindProperty("sourceHullPrefab");
        maxLevels = serializedObject.FindProperty("maxLevels");
        levels = serializedObject.FindProperty("levels");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Meta Level 0 is the hull's initial state. Each following level "
            + "is created as an exact copy of the previous one. Contracts are "
            + "added and removed across every meta level together.",
            MessageType.Info);

        DrawSources();
        DrawMaxLevels();

        for (int levelIndex = 0;
             levelIndex < levels.arraySize;
             levelIndex++)
        {
            DrawLevel(levelIndex, levels.GetArrayElementAtIndex(levelIndex));
        }

        if (GUILayout.Button("Add Meta Level (Copy Previous)"))
        {
            AddMetaLevel();
            GUIUtility.ExitGUI();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSources()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sourceShipData, new GUIContent("Ship Data"));
        EditorGUILayout.PropertyField(
            sourceHullPrefab,
            new GUIContent("Hull Prefab", "Determines the contracts available for this hull."));

        if (sourceHullPrefab.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the hull prefab to choose ability contracts.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
    }

    private void DrawMaxLevels()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            maxLevels,
            new GUIContent("Max Levels", "Includes Meta Level 0."));
        if (!EditorGUI.EndChangeCheck())
            return;

        ShipMetaConfig config = (ShipMetaConfig)target;
        Undo.RecordObject(config, "Resize ship meta levels");
        serializedObject.ApplyModifiedProperties();
        config.SetMaxLevels(maxLevels.intValue);
        EditorUtility.SetDirty(config);
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }

    private void DrawLevel(int levelIndex, SerializedProperty level)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Meta Level {levelIndex}",
                    true);

                using (new EditorGUI.DisabledScope(levelIndex == 0))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                    {
                        RemoveMetaLevel(levelIndex);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (!level.isExpanded)
                return;

            EditorGUI.indentLevel++;
            DrawBasicStats(level.FindPropertyRelative("basicStats"));
            DrawContracts(
                levelIndex,
                level.FindPropertyRelative("contracts"));

            if (levelIndex == 0)
                DrawAddContractButton();

            EditorGUI.indentLevel--;
        }
    }

    private static void DrawBasicStats(SerializedProperty basicStats)
    {
        EditorGUILayout.LabelField("Basic Stats", EditorStyles.miniBoldLabel);
        DrawField(basicStats, "maximumHealthPoints", "Maximum Hull Health");
        DrawField(basicStats, "maximumShieldPoints", "Maximum Shield Health");
        DrawField(
            basicStats,
            "healthRegenRatePercent",
            "Hull Regeneration (% / sec)");
        DrawField(
            basicStats,
            "shieldRegenRatePercent",
            "Shield Regeneration (% / sec)");
        DrawField(basicStats, "speed", "Speed");
        DrawField(basicStats, "maximumEnergy", "Maximum Energy");
    }

    private void DrawContracts(int levelIndex, SerializedProperty contracts)
    {
        if (contracts == null || contracts.arraySize == 0)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Contracts", EditorStyles.miniBoldLabel);
        for (int contractIndex = 0;
             contractIndex < contracts.arraySize;
             contractIndex++)
        {
            SerializedProperty contract =
                contracts.GetArrayElementAtIndex(contractIndex);
            ShipMetaContract value =
                contract.managedReferenceValue as ShipMetaContract;
            if (value == null)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        value.DisplayName,
                        EditorStyles.boldLabel);
                    if (levelIndex == 0
                        && GUILayout.Button("Remove", GUILayout.Width(65f)))
                    {
                        RemoveContract(value.GetType());
                        GUIUtility.ExitGUI();
                    }
                }

                DrawContractFields(contract, value);
            }
        }
    }

    private static void DrawContractFields(
        SerializedProperty contract,
        ShipMetaContract value)
    {
        switch (value)
        {
            case AbilityRecoveryShipMetaContract recovery:
                DrawAbilityRecoveryFields(contract, recovery);
                break;
            case BlackHoleShipMetaContract:
                DrawField(contract, "duration", "Duration");
                DrawField(contract, "damage", "Damage per Tick");
                DrawField(contract, "projectileSlowRadius", "Projectile Slow Radius");
                DrawField(
                    contract,
                    "minimumProjectileSpeedMultiplier",
                    "Minimum Projectile Speed Multiplier");
                break;
            case BladeShipMetaContract:
                DrawField(contract, "recordingDuration", "Recording Duration");
                DrawField(contract, "sampleInterval", "Path Sample Interval");
                DrawField(contract, "width", "Blade Width");
                DrawField(contract, "speed", "Blade Speed");
                DrawField(contract, "damage", "Damage");
                DrawField(contract, "lifetime", "Lifetime");
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "Speed Passive",
                    EditorStyles.miniBoldLabel);
                DrawField(contract, "passiveSpeedDivisor", "Speed Divisor");
                DrawField(
                    contract,
                    "passiveSpeedMultiplier",
                    "Movement Speed Multiplier");
                DrawField(
                    contract,
                    "maximumReloadMultiplier",
                    "Maximum Reload Multiplier");
                DrawField(
                    contract,
                    "minimumReloadMultiplier",
                    "Minimum Reload Multiplier");
                break;
            case HeavyShieldShipMetaContract:
                DrawField(contract, "shieldHealthMultiplier", "Shield Health Multiplier");
                break;
            case ArkanoidShipMetaContract:
                DrawField(contract, "paddleFollowSpeed", "Paddle Follow Speed");
                DrawField(contract, "ballSpeed", "Ball Speed");
                DrawField(contract, "ballDamage", "Ball Damage");
                DrawField(contract, "stasisDuration", "Stasis Duration");
                DrawField(contract, "stasisRadiusMultiplier", "Stasis Radius Multiplier");
                DrawField(contract, "stasisDamagePerSecond", "Stasis Damage per Second");
                DrawField(contract, "stasisTickInterval", "Stasis Tick Interval");
                break;
            case DictatorShipMetaContract:
                DrawDictatorFields(contract);
                break;
            case PrismShipMetaContract:
                DrawPrismFields(contract);
                break;
            case PhantomShipMetaContract:
                DrawField(contract, "phaseDuration", "Phase Duration");
                DrawField(contract, "purgeRadius", "Projectile Purge Radius");
                break;
            case HuskarShipMetaContract:
                DrawField(contract, "healFromDamagePercent", "Heal from Damage Multiplier");
                DrawField(contract, "healDuration", "Heal Duration");
                DrawField(contract, "missingHealthFireRateBonus", "Max Missing-Health Fire Rate Bonus");
                break;
        }
    }

    private static void DrawDictatorFields(SerializedProperty contract)
    {
        EditorGUILayout.LabelField("Turrets", EditorStyles.miniBoldLabel);
        DrawField(contract, "turretLifetime", "Turret Lifetime");
        DrawField(contract, "turretProjectilePrefab", "Projectile Prefab");
        DrawField(contract, "turretDamageType", "Damage Type");
        DrawField(contract, "turretProjectileDamage", "Projectile Damage");
        DrawField(contract, "turretReloadTime", "Reload Time");
        DrawField(contract, "turretProjectileSpeed", "Projectile Speed");
        DrawField(contract, "turretTargetingRange", "Targeting Range");

        SerializedProperty spawnGroups =
            contract.FindPropertyRelative("turretSpawnGroups");
        if (spawnGroups != null)
        {
            EditorGUILayout.PropertyField(
                spawnGroups,
                new GUIContent(
                    "Turret Spawn Groups",
                    "Each group has its own local position and turret count."),
                true);
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "Passive Damage Bonus",
            EditorStyles.miniBoldLabel);
        DrawField(
            contract,
            "kineticDamageBonusPercent",
            "Kinetic Damage Bonus (%)");
        DrawField(
            contract,
            "explosionDamageBonusPercent",
            "Explosion Damage Bonus (%)");
    }

    private static void DrawPrismFields(SerializedProperty contract)
    {
        EditorGUILayout.LabelField("Active Beam", EditorStyles.miniBoldLabel);
        DrawField(contract, "beamDamage", "Beam Damage");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "Passive Damage Bonus",
            EditorStyles.miniBoldLabel);
        DrawField(
            contract,
            "beamDamageBonusPercent",
            "Beam Damage Bonus (%)");
        DrawField(
            contract,
            "energyDamageBonusPercent",
            "Energy Damage Bonus (%)");
    }

    private static void DrawAbilityRecoveryFields(
        SerializedProperty contract,
        AbilityRecoveryShipMetaContract recovery)
    {
        EditorGUILayout.LabelField("Recovery Mode", recovery.AbilityMode.ToString());
        DrawField(contract, "cooldown", recovery.AbilityMode == UltimateAbilityMode.Charges
            ? "Recharge Time per Charge"
            : "Cooldown");

        if (recovery.AbilityMode == UltimateAbilityMode.Charges)
        {
            DrawField(contract, "maxCharges", "Maximum Charges");
            return;
        }

        if (recovery.AbilityMode != UltimateAbilityMode.Toggle)
            return;

        DrawField(contract, "toggleMaximumTime", "Maximum Active Time");
        DrawField(contract, "toggleTimeCostPerSecond", "Active Time Cost per Second");
        DrawField(contract, "toggleRechargeStartTime", "Recharge Delay");
        DrawField(contract, "toggleRechargeDuration", "Full Recharge Duration");
    }

    private void DrawAddContractButton()
    {
        if (!GUILayout.Button("Add Contract"))
            return;

        ShipMetaConfig config = (ShipMetaConfig)target;
        List<Type> types = config.GetSupportedContractTypes();
        if (types.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No contracts are available until a compatible hull prefab is assigned.",
                MessageType.Warning);
            return;
        }

        GenericMenu menu = new();
        for (int index = 0; index < types.Count; index++)
        {
            Type type = types[index];
            string label = GetDisplayName(type);
            if (config.GetLevel(0).ContainsContract(type))
            {
                menu.AddDisabledItem(new GUIContent(label));
                continue;
            }

            menu.AddItem(
                new GUIContent(label),
                false,
                () => AddContract(type));
        }

        menu.ShowAsContext();
    }

    private static string GetDisplayName(Type contractType)
    {
        if (Activator.CreateInstance(contractType) is ShipMetaContract contract)
            return contract.DisplayName;

        return ObjectNames.NicifyVariableName(contractType.Name);
    }

    private void AddContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();

        ShipMetaConfig config = (ShipMetaConfig)target;
        Undo.RecordObject(config, "Add ship meta contract");
        if (config.TryAddContractToAllLevels(contractType))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private void RemoveContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();

        ShipMetaConfig config = (ShipMetaConfig)target;
        Undo.RecordObject(config, "Remove ship meta contract");
        if (config.TryRemoveContractFromAllLevels(contractType))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private void AddMetaLevel()
    {
        serializedObject.ApplyModifiedProperties();

        ShipMetaConfig config = (ShipMetaConfig)target;
        Undo.RecordObject(config, "Add ship meta level");
        config.AddLevelCopyingPrevious();
        EditorUtility.SetDirty(config);
        serializedObject.Update();
    }

    private void RemoveMetaLevel(int levelIndex)
    {
        serializedObject.ApplyModifiedProperties();

        ShipMetaConfig config = (ShipMetaConfig)target;
        Undo.RecordObject(config, "Delete ship meta level");
        if (config.TryRemoveLevel(levelIndex))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private static void DrawField(
        SerializedProperty parent,
        string propertyName,
        string label)
    {
        SerializedProperty property = parent?.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
