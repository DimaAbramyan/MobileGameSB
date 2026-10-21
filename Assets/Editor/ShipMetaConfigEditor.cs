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
                EditorGUILayout.LabelField("Active Regional Speed Field", EditorStyles.miniBoldLabel);
                DrawField(contract, "activeDuration", "ActiveDuration");
                DrawField(contract, "activeCooldown", "ActiveCooldown", "Uses the standard ActiveAbility recovery timer. This value is authoritative for BlackHole.");
                DrawField(contract, "frontSpeedMultiplier", "FrontSpeedMultiplier", "Above the ship, including the dividing line. Multiplies the passive effect when enabled.");
                DrawField(contract, "rearSpeedMultiplier", "RearSpeedMultiplier", "Below the ship. Direction remains unchanged.");
                DrawField(contract, "rewriteAttackSpeed", "RewriteAttackSpeed", "Also changes enemy attack timing according to enemy position.");
                DrawField(contract, "enablePassiveAbility", "EnablePassiveAbility", "Keep the original passive field active during the ability.");
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Passive Projectile Speed Field", EditorStyles.miniBoldLabel);
                DrawField(contract, "projectileSlowRadius", "Effect Radius",
                    "World-space radius around the ship. Influence smoothly reaches zero at its boundary.");
                DrawField(contract, "frontFullEffectAngle", "Front Full Effect Angle",
                    "Full cone width in degrees, centred on local Forward (+Y). Angular influence is strongest inside this cone.");
                DrawField(contract, "frontTransitionAngle", "Front Transition Angle",
                    "Smooth fade width in degrees on EACH side of the front cone. Half Full Angle + Transition is limited to 90 degrees.");
                DrawField(contract, "backFullEffectAngle", "Back Full Effect Angle",
                    "Full cone width in degrees, centred on local Back (-Y). Angular influence is strongest inside this cone.");
                DrawField(contract, "backTransitionAngle", "Back Transition Angle",
                    "Smooth fade width in degrees on EACH side of the back cone. Half Full Angle + Transition is limited to 90 degrees.");
                DrawField(
                    contract,
                    "minimumProjectileSpeedMultiplier",
                    "Min Speed Multiplier", "Maximum slowing on the front axis near the ship. Does not overwrite base projectile speed.");
                DrawField(contract, "maximumProjectileSpeedMultiplier", "Max Speed Multiplier",
                    "Maximum acceleration on the rear axis near the ship. 1 disables acceleration.");
                DrawField(contract, "distanceFalloff", "Distance Falloff",
                    "Exponent of the smooth distance envelope. 1 = SmoothStep; higher values concentrate the field closer to the ship.");
                DrawFieldShapeWarning(contract);
                break;
            case BladeShipMetaContract:
                DrawField(contract, "projectileDestructionPolicy", "Projectile Destruction Policy", "Fragile Only: destroy only Fragile. Normal And Fragile: destroy both types.");
                DrawField(contract, "recordingDuration", "Recording Duration");
                DrawField(contract, "sampleInterval", "Path Sample Interval");
                DrawField(contract, "width", "Blade Width");
                DrawField(contract, "speed", "Blade Speed");
                DrawField(contract, "damage", "Damage");
                DrawField(contract, "damageType", "Damage Type");
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
                DrawField(contract, "extraHealthAmount", "Extra Health Amount",
                    "Grants additional health to the living team ship with the lowest current hull health.");
                break;
            case ArkanoidShipMetaContract:
                DrawField(contract, "projectileDestructionPolicy", "Projectile Destruction Policy", "Fragile Only: destroy only Fragile. Normal And Fragile: destroy both types.");
                DrawField(contract, "paddleFollowSpeed", "Paddle Follow Speed");
                DrawField(contract, "paddleAttractionRadius", "Paddle Attraction Radius",
                    "Ball attraction toward its own paddle starts only after a top screen bounce. Independent of the pickup magnet.");
                DrawField(contract, "paddleAttractionForce", "Paddle Attraction Force",
                    "Steering strength toward the paddle without changing ball speed. 0 disables attraction.");
                DrawField(contract, "ballSpeed", "Ball Speed");
                DrawField(contract, "paddleBounceSpeedMultiplier", "Paddle Bounce Speed Multiplier",
                    "Multiplier of base Ball Speed after each paddle bounce. 1 disables the boost. Bounces restart the boost without stacking.");
                DrawField(contract, "paddleBounceRecoveryDuration", "Paddle Bounce Recovery Duration (s)");
                var bounceCurve = contract.FindPropertyRelative("paddleBounceRecoveryCurve");
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField(new GUIContent("Paddle Bounce Recovery Curve",
                    "X: normalized recovery time (0–1). Y: remaining boost (1 = initial boost, 0 = base speed). Endpoints are enforced at runtime."),
                    bounceCurve.animationCurveValue, Color.green, new Rect(0f, 0f, 1f, 1f));
                if (EditorGUI.EndChangeCheck()) bounceCurve.animationCurveValue = curve;
                DrawField(contract, "ballDamage", "Ball Damage");
                DrawField(contract, "ballDamageType", "Ball Damage Type");
                DrawField(contract, "stasisDuration", "Stasis Duration");
                DrawField(contract, "stasisRadiusMultiplier", "Stasis Radius Multiplier");
                DrawField(contract, "stasisDamagePerSecond", "Stasis Damage per Second");
                DrawField(contract, "stasisDamageType", "Stasis Damage Type");
                DrawField(contract, "stasisTickInterval", "Stasis Tick Interval");
                break;
            case OrbitShipMetaContract orbit:
                DrawField(contract, "projectileDestructionPolicy", "Projectile Destruction Policy", "Fragile Only: destroy only Fragile. Normal And Fragile: destroy both types.");
                DrawField(contract, "objectCount", "Object Count", "Independent satellites with open gaps between them.");
                DrawField(contract, "objectRadius", "Base Object Radius", "World-space physical hitbox radius without ability charge. Existing Object Radius values are preserved.");
                DrawField(contract, "maximumChargedObjectRadius", "Maximum Object Radius", "World-space visual and physical radius at full charge. Objects grow and shrink together with the orbit; gaps remain between them.");
                DrawField(contract, "baseOrbitRadius", "Base Orbit Radius", "Distance from the ship centre to each satellite in the normal state.");
                DrawField(contract, "maximumOrbitRadius", "Maximum Orbit Radius");
                DrawField(contract, "baseAngularSpeed", "Base Rotation Speed (deg/s)");
                DrawField(contract, "maximumAngularSpeed", "Maximum Rotation Speed (deg/s)");
                DrawField(contract, "clockwise", "Clockwise", "Enabled: clockwise. Disabled: counterclockwise. Applies to normal orbit, expansion and collapse.");
                DrawField(contract, "chargeUpDuration", "Charge Up Duration (s)", "Time after pressing the ability to reach maximum rotation speed, orbit radius and object size. Lower values mean faster charge. Independent of Maximum Hold Time and recharge.");
                DrawField(contract, "collapseSpeed", "Collapse Speed", "Return speed in intensity units per second after release. Satellites continue intercepting throughout.");
                DrawField(contract, "contactDamage", "Enemy Contact Damage");
                DrawField(contract, "damageType", "Damage Type");
                DrawField(contract, "damageInterval", "Damage Interval (s)", "Shared per enemy across all satellites, preventing multiple hits in the same interval.");
                DrawField(contract, "cooldownReductionPerProjectile", "Cooldown Reduction per Projectile (s)",
                    "Advances the cooldown and Toggle reserve recharge by this many seconds per intercepted enemy projectile, including while held. 0 disables the bonus.");
                EditorGUILayout.HelpBox("Charge Up Duration controls satellite acceleration. Ability reserve and recovery are configured separately in Ability Recovery.", MessageType.Info);
                if (orbit.IsObjectRadiusLimited)
                    EditorGUILayout.HelpBox($"Object radius is limited to {orbit.ObjectRadius:0.###} at rest and {orbit.MaximumChargedObjectRadius:0.###} at full charge to retain dangerous gaps between satellites.", MessageType.Warning);
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
            "Kinetick Damage Bonus (%)");
    }

    private static void DrawPrismFields(SerializedProperty contract)
    {
        EditorGUILayout.LabelField("Active Energy Wave", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox("Cooldown uses the standard Ability Recovery contract. Forward is captured from the ship at activation.", MessageType.Info);
        DrawField(contract, "wavePrefab", "WavePrefab");
        if (contract.FindPropertyRelative("wavePrefab").objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign a visual WavePrefab before using this ability.", MessageType.Warning);
        DrawField(contract, "spawnOffset", "SpawnOffset");
        DrawField(contract, "waveWidth", "WaveWidth");
        DrawField(contract, "waveThickness", "WaveThickness");
        DrawField(contract, "waveSpeed", "WaveSpeed");
        DrawField(contract, "maxDistance", "MaxDistance");
        DrawField(contract, "plasmaDamage", "Damage");
        DrawField(contract, "damageType", "DamageType");
        DrawField(contract, "destroyProjectiles", "DestroyProjectiles");
        if (contract.FindPropertyRelative("destroyProjectiles").boolValue)
            DrawField(contract, "projectileDestructionPolicy", "Projectile Destruction Policy");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "Passive Damage Bonus",
            EditorStyles.miniBoldLabel);
        DrawField(
            contract,
            "plasmaDamageBonusPercent",
            "Plazma Damage Bonus (%)");
    }

    private static void DrawAbilityRecoveryFields(
        SerializedProperty contract,
        AbilityRecoveryShipMetaContract recovery)
    {
        EditorGUILayout.LabelField("Recovery Mode", recovery.AbilityMode.ToString());
        if (contract.serializedObject.targetObject is ShipMetaConfig config
            && config.SourceHullPrefab != null && config.SourceHullPrefab.ActiveAbility is BlackHoleActive
            && recovery.AbilityMode == UltimateAbilityMode.Active)
        {
            EditorGUILayout.HelpBox("BlackHole uses ActiveCooldown from its Active Regional Speed Field contract.", MessageType.Info);
            return;
        }
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

        DrawField(contract, "toggleMaximumTime", "Maximum Hold Time",
            "Reserve for the existing held ability system. At a cost of 1 per second, this is the maximum hold duration in seconds.");
        DrawField(contract, "toggleTimeCostPerSecond", "Active Time Cost per Second");
        EditorGUILayout.LabelField("Full Reserve Hold Duration (s)",
            (recovery.Settings.ToggleMaximumTime / recovery.Settings.ToggleTimeCostPerSecond).ToString("0.##"));
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

    private static void DrawFieldShapeWarning(SerializedProperty contract)
    {
        float front = contract.FindPropertyRelative("frontFullEffectAngle").floatValue * 0.5f;
        float back = contract.FindPropertyRelative("backFullEffectAngle").floatValue * 0.5f;
        if (front + contract.FindPropertyRelative("frontTransitionAngle").floatValue >= 90f
            || back + contract.FindPropertyRelative("backTransitionAngle").floatValue >= 90f)
        {
            EditorGUILayout.HelpBox("This lobe reaches the side axis. Transition is capped at 90 degrees; reduce its angles to keep a wide neutral side region.", MessageType.Warning);
        }
    }

    private static void DrawField(
        SerializedProperty parent,
        string propertyName,
        string label,
        string tooltip = null)
    {
        SerializedProperty property = parent?.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
    }
}
