using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DirectedWaveAttackBehaviour))]
public sealed class DirectedWaveAttackBehaviourEditor : Editor
{
    private const string DefaultPresetFolder = "Assets/Config/PatternAttacks";

    private SerializedProperty attackSettings;
    private SerializedProperty attackPreset;
    private SerializedProperty overridePresetAttacksPerSecond;
    private SerializedProperty presetAttacksPerSecond;
    private SerializedProperty overrideBaseProjectileSpeed;
    private SerializedProperty baseProjectileSpeed;
    private SerializedProperty attackPatterns;
    private SerializedProperty entranceAttackSettings;
    private SerializedProperty allowAutonomousAttackDuringEntrance;
    private SerializedProperty useSelectedEnemySlots;
    private SerializedProperty selectedEnemySlots;
    private int selectedLocalOverrideSlot = -1;

    private void OnEnable()
    {
        attackSettings = serializedObject.FindProperty("attackSettings");
        attackPreset = serializedObject.FindProperty("attackPreset");
        overridePresetAttacksPerSecond = serializedObject.FindProperty(
            "overridePresetAttacksPerSecond");
        presetAttacksPerSecond = serializedObject.FindProperty(
            "presetAttacksPerSecond");
        overrideBaseProjectileSpeed = serializedObject.FindProperty(
            "overrideBaseProjectileSpeed");
        baseProjectileSpeed = serializedObject.FindProperty(
            "baseProjectileSpeed");
        attackPatterns = serializedObject.FindProperty("attackPatterns");
        entranceAttackSettings = serializedObject.FindProperty(
            "entranceAttackSettings");
        allowAutonomousAttackDuringEntrance = serializedObject.FindProperty(
            "allowAutonomousAttackDuringEntrance");
        useSelectedEnemySlots = serializedObject.FindProperty(
            "useSelectedEnemySlots");
        selectedEnemySlots = serializedObject.FindProperty("selectedEnemySlots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (attackPatterns == null || attackPatterns.arraySize == 0)
        {
            EditorGUILayout.LabelField(
                "Post-Formation Attack",
                EditorStyles.boldLabel);
            DrawPostFormationAttackSettings(
                attackSettings,
                attackPreset,
                overridePresetAttacksPerSecond,
                presetAttacksPerSecond,
                overrideBaseProjectileSpeed,
                baseProjectileSpeed,
                -1);
            EditorGUILayout.Space(6f);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "The legacy Post-Formation Attack is inactive while Attack Patterns are configured.",
                MessageType.None);
        }

        DrawEntranceAttacks();
        EditorGUILayout.Space(6f);

        EditorGUILayout.PropertyField(allowAutonomousAttackDuringEntrance);
        EditorGUILayout.Space(6f);

        if (attackPatterns == null || attackPatterns.arraySize == 0)
        {
            EditorGUILayout.LabelField("Eligible Enemies", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                useSelectedEnemySlots,
                new GUIContent("Use Selected Enemy Slots"));
            if (useSelectedEnemySlots.boolValue)
                DrawEnemySlotSelection(selectedEnemySlots, "Controlled Enemy Slots");
        }

        EditorGUILayout.Space(6f);
        DrawAttackPatterns();
        EditorGUILayout.Space(6f);
        DrawPerEnemyAttackOverride();

        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    private void DrawPostFormationAttackSettings(
        SerializedProperty settings,
        SerializedProperty preset,
        SerializedProperty overridePresetRate,
        SerializedProperty presetRate,
        SerializedProperty overrideProjectileSpeed,
        SerializedProperty projectileSpeed,
        int patternIndex)
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox(
                "Post-formation attack settings could not be serialized.",
                MessageType.Error);
            return;
        }

        SerializedProperty selectedSlots = patternIndex >= 0
            ? attackPatterns.GetArrayElementAtIndex(patternIndex)
                .FindPropertyRelative("selectedEnemySlots")
            : useSelectedEnemySlots.boolValue ? selectedEnemySlots : null;

        if (DrawAttackPresetSettings(
                preset,
                overridePresetRate,
                presetRate,
                patternIndex))
        {
            DrawAimModeWarnings(((DirectedWaveAttackPreset)preset.objectReferenceValue).AttackSettings,
                selectedSlots);
            DrawBaseProjectileSpeedOverride(
                overrideProjectileSpeed,
                projectileSpeed,
                selectedSlots);
            return;
        }

        SerializedProperty useStartDelay = settings.FindPropertyRelative(
            "useAttackStartDelay");
        SerializedProperty fireMode = settings.FindPropertyRelative(
            "fireMode");
        SerializedProperty movementMode = settings.FindPropertyRelative(
            "movementMode");

        EditorGUILayout.LabelField("Attack Start", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            useStartDelay,
            new GUIContent("Delay Attack Start"));
        if (useStartDelay.boolValue)
        {
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("attackStartDelay"),
                new GUIContent("Attack Start Delay"));
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Attack", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            fireMode,
            new GUIContent("Fire Mode", "Default runs the configured Attack Pattern without aiming at the player. None disables wave-controlled shooting."));
        if ((DirectedWaveAttackFireMode)fireMode.enumValueIndex == DirectedWaveAttackFireMode.Aimed)
            DrawAimMode(settings.FindPropertyRelative("aimMode"));
        if ((DirectedWaveAttackFireMode)fireMode.enumValueIndex
            == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
        {
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("forwardFireHalfAngle"),
                new GUIContent(
                    "Forward Fire Angle Offset",
                    "The player must be within this angle on either side of the enemy's forward direction. Default: 15 degrees."));
        }
        EditorGUILayout.PropertyField(
            movementMode,
            new GUIContent("Movement Mode"));

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Scheduling", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            settings.FindPropertyRelative("attacksPerEnemyPerCycle"),
            new GUIContent("Attacks Per Enemy Per Cycle"));
        EditorGUILayout.PropertyField(
            settings.FindPropertyRelative("attacksPerSecond"),
            new GUIContent("Attacks Per Second"));

        SerializedProperty waitForPreviousAttack = settings.FindPropertyRelative(
            "waitForPreviousAttack");
        EditorGUILayout.PropertyField(
            waitForPreviousAttack,
            new GUIContent("Wait For Previous Attack"));
        if (waitForPreviousAttack.boolValue)
        {
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("delayAfterAttack"),
                new GUIContent("Delay After Attack"));
        }

        if ((DirectedWaveAttackFireMode)fireMode.enumValueIndex
            != DirectedWaveAttackFireMode.None)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Attack Pattern", EditorStyles.miniBoldLabel);
            SerializedProperty burstSettingsSource = settings.FindPropertyRelative(
                "burstSettingsSource");
            EditorGUILayout.PropertyField(
                burstSettingsSource,
                new GUIContent("Attack Settings Source"));
            if ((DirectedWaveBurstSettingsSource)burstSettingsSource.enumValueIndex
                == DirectedWaveBurstSettingsSource.WaveOverride)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("waveBurstSettings"),
                    new GUIContent("Wave Attack Pattern"),
                    true);
            }
            else
            {
                DrawEnemyAttackPatternOverrides(settings);
                DrawEnemyAttackDurationSummary(
                    selectedSlots);
            }

            DrawBaseProjectileSpeedOverride(
                overrideProjectileSpeed,
                projectileSpeed,
                selectedSlots);
            DrawAimModeWarnings((DirectedWaveAttackSettings)settings.boxedValue, selectedSlots);
        }

        DirectedWaveAttackMovementMode selectedMovementMode =
            (DirectedWaveAttackMovementMode)movementMode.enumValueIndex;
        if (selectedMovementMode == DirectedWaveAttackMovementMode.None)
        {
            return;
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "Dive Preparation",
            EditorStyles.miniBoldLabel);
        SerializedProperty useDivePreparation = settings.FindPropertyRelative(
            "useDivePreparation");
        EditorGUILayout.PropertyField(
            useDivePreparation,
            new GUIContent("Use Preparation"));
        if (useDivePreparation.boolValue)
        {
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("divePreparationDistance"),
                new GUIContent("Preparation Distance"));
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("divePreparationDuration"),
                new GUIContent("Preparation Duration"));
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("divePreparationSpeedCurve"),
                new GUIContent("Preparation Speed Curve"));
        }

        EditorGUILayout.Space(3f);
        if (selectedMovementMode
            == DirectedWaveAttackMovementMode.FlyThroughDive)
        {
            EditorGUILayout.LabelField(
                "Fly Through Dive",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("flyThroughApproachSpeed"),
                new GUIContent("Approach Speed"));
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("flyThroughExitPadding"),
                new GUIContent("Exit Padding"));
            SerializedProperty returnMode = settings.FindPropertyRelative(
                "flyThroughReturnMode");
            EditorGUILayout.PropertyField(
                returnMode,
                new GUIContent("Return Mode"));
            if ((DirectedWaveFlyThroughReturnMode)returnMode.enumValueIndex
                == DirectedWaveFlyThroughReturnMode.TeleportPosition)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative(
                        "flyThroughReturnTeleportPosition"),
                    new GUIContent("Return Teleport Position"));
            }
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("flyThroughDiveCooldown"),
                new GUIContent("Dive Cooldown"));
            EditorGUILayout.PropertyField(
                settings.FindPropertyRelative("diveSpeedCurve"),
                new GUIContent("Speed Curve"));
            EditorGUILayout.HelpBox(
                "Entrance Path teleports the enemy to the start of its entrance route and replays that route using Return Speed Multiplier. Dive Cooldown begins after the full sequence.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.LabelField("Dive", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("minDiveDepth"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("maxDiveDepth"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("minDiveSpeed"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("maxDiveSpeed"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("diveSpeedCurve"));

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Dive Target", EditorStyles.miniBoldLabel);
            SerializedProperty diveTargetMode = settings.FindPropertyRelative(
                "diveTargetMode");
            EditorGUILayout.PropertyField(diveTargetMode);
            if ((DirectedWaveDiveTargetMode)diveTargetMode.enumValueIndex
                == DirectedWaveDiveTargetMode.StopAtPlayerRadius)
            {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("playerStandoffRadius"));
            }
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Dive Scheduling", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            settings.FindPropertyRelative("diveSchedulingMode"));

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Return", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            settings.FindPropertyRelative("returnSpeedMultiplier"));
        EditorGUILayout.PropertyField(
            settings.FindPropertyRelative("returnSpeedCurve"));
    }

    private bool DrawAttackPresetSettings(
        SerializedProperty presetProperty,
        SerializedProperty overridePresetRate,
        SerializedProperty presetRate,
        int patternIndex)
    {
        EditorGUILayout.LabelField("Attack Preset", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            presetProperty,
            new GUIContent("Preset"));

        if (presetProperty.objectReferenceValue == null)
        {
            if (GUILayout.Button("Export Current Settings As Preset"))
                ExportSettingsAsPreset(patternIndex);

            EditorGUILayout.Space(3f);
            return false;
        }

        DirectedWaveAttackPreset preset =
            presetProperty.objectReferenceValue as DirectedWaveAttackPreset;
        EditorGUILayout.HelpBox(
            "This behaviour uses the preset for all attack settings. Enable the override below to change only its attack rate for this wave.",
            MessageType.None);

        bool wasOverridden = overridePresetRate.boolValue;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            overridePresetRate,
            new GUIContent("Override Attacks Per Second"));
        if (EditorGUI.EndChangeCheck()
            && !wasOverridden
            && overridePresetRate.boolValue
            && preset != null)
        {
            presetRate.floatValue =
                preset.AttackSettings.AttacksPerSecond;
        }

        if (overridePresetRate.boolValue)
        {
            EditorGUILayout.PropertyField(
                presetRate,
                new GUIContent("Attacks Per Second"));
        }

        if (GUILayout.Button("Use Preset As Local Settings"))
        {
            ApplyModifiedPropertiesAndUsePresetAsLocalSettings(patternIndex);
            return false;
        }

        if (GUILayout.Button("Export Resolved Settings As Preset"))
            ExportSettingsAsPreset(patternIndex);

        EditorGUILayout.Space(3f);
        return true;
    }

    private void ApplyModifiedPropertiesAndUsePresetAsLocalSettings(int patternIndex)
    {
        serializedObject.ApplyModifiedProperties();
        DirectedWaveAttackBehaviour behaviour =
            (DirectedWaveAttackBehaviour)target;
        Undo.RecordObject(behaviour, "Use Attack Preset As Local Settings");
        if (patternIndex < 0)
            behaviour.UsePresetAsLocalSettings();
        else
            behaviour.UseAttackPatternPresetAsLocalSettings(patternIndex);
        EditorUtility.SetDirty(behaviour);
        serializedObject.Update();
    }

    private void ExportSettingsAsPreset(int patternIndex)
    {
        serializedObject.ApplyModifiedProperties();
        EnsureProjectFolder(DefaultPresetFolder);

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Export Directed Wave Attack Preset",
            "DirectedWaveAttackPreset",
            "asset",
            "Choose where to save the reusable attack preset.",
            DefaultPresetFolder);
        if (string.IsNullOrEmpty(assetPath))
            return;

        DirectedWaveAttackPreset preset =
            CreateInstance<DirectedWaveAttackPreset>();
        DirectedWaveAttackBehaviour behaviour =
            (DirectedWaveAttackBehaviour)target;
        if (patternIndex < 0)
            behaviour.CopyResolvedAttackSettingsTo(preset);
        else
            behaviour.CopyResolvedAttackPatternSettingsTo(patternIndex, preset);
        AssetDatabase.CreateAsset(preset, assetPath);
        Undo.RegisterCreatedObjectUndo(
            preset,
            "Export Directed Wave Attack Preset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
        serializedObject.Update();
    }

    private void DrawAttackPatterns()
    {
        if (attackPatterns == null)
        {
            EditorGUILayout.HelpBox(
                "Attack pattern list could not be serialized.",
                MessageType.Error);
            return;
        }

        if (attackPatterns.arraySize > 0)
        {
            EditorGUILayout.LabelField(
                "Post-Formation Attack Patterns",
                EditorStyles.boldLabel);
        }

        int removeIndex = -1;
        for (int i = 0; i < attackPatterns.arraySize; i++)
        {
            SerializedProperty pattern = attackPatterns.GetArrayElementAtIndex(i);
            SerializedProperty selectedSlots = pattern.FindPropertyRelative(
                "selectedEnemySlots");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"Attack Pattern {i + 1}",
                EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            DrawAttackPatternSlotSelection(
                selectedSlots,
                "Enemies Using This Pattern",
                i);
            EditorGUILayout.Space(4f);
            DrawPostFormationAttackSettings(
                pattern.FindPropertyRelative("attackSettings"),
                pattern.FindPropertyRelative("attackPreset"),
                pattern.FindPropertyRelative("overridePresetAttacksPerSecond"),
                pattern.FindPropertyRelative("presetAttacksPerSecond"),
                pattern.FindPropertyRelative("overrideBaseProjectileSpeed"),
                pattern.FindPropertyRelative("baseProjectileSpeed"),
                i);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        if (removeIndex >= 0)
        {
            ApplyModifiedPropertiesAndRemoveAttackPattern(removeIndex);
            return;
        }

        if (GUILayout.Button("Add Attack Pattern"))
            ApplyModifiedPropertiesAndAddAttackPattern();
    }

    private void DrawPerEnemyAttackOverride()
    {
        DirectedEnemySubWave wave = GetWave();
        if (wave == null)
            return;

        int slotCount = wave.GetConfiguredEnemySlotCount();
        EditorGUILayout.LabelField(
            "Per-Enemy Attack Override",
            EditorStyles.boldLabel);
        if (slotCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Create formation slots before configuring a local attack override.",
                MessageType.Info);
            return;
        }

        selectedLocalOverrideSlot = Mathf.Clamp(
            selectedLocalOverrideSlot < 0 ? 0 : selectedLocalOverrideSlot,
            0,
            slotCount - 1);
        string[] labels = new string[slotCount];
        for (int i = 0; i < slotCount; i++)
            labels[i] = $"Slot {i}: {GetEnemyName(wave, i)}";
        selectedLocalOverrideSlot = EditorGUILayout.Popup(
            "Selected Enemy",
            selectedLocalOverrideSlot,
            labels);
        EditorGUILayout.HelpBox(
            "You can also click a slot marker in Scene View. A local override changes only Fire Mode and projectile/attack-pattern settings. Attack cadence, movement and dive settings remain shared.",
            MessageType.None);

        Transform host = wave.GetConfiguredEnemyOverrideHostForSlot(
            selectedLocalOverrideSlot);
        if (host == null)
        {
            EditorGUILayout.HelpBox(
                "Per-enemy attack overrides require Transform Points (or a frozen Transform Point formation), because settings are stored on the selected slot.",
                MessageType.Info);
            return;
        }

        DirectedWaveEnemyOverride enemyOverride =
            host.GetComponent<DirectedWaveEnemyOverride>();
        if (enemyOverride == null)
        {
            if (GUILayout.Button("Add Local Attack Override"))
            {
                Undo.AddComponent<DirectedWaveEnemyOverride>(host.gameObject);
                SceneView.RepaintAll();
            }

            return;
        }

        DrawLocalAttackOverride(wave, enemyOverride);
    }

    private void DrawLocalAttackOverride(
        DirectedEnemySubWave wave,
        DirectedWaveEnemyOverride enemyOverride)
    {
        var overrideObject = new SerializedObject(enemyOverride);
        overrideObject.Update();
        SerializedProperty enabled = overrideObject.FindProperty(
            "overrideAttackPattern");
        bool wasEnabled = enabled.boolValue;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            enabled,
            new GUIContent(
                "Override Attack Pattern",
                "Copies the resolved Directed Wave Attack Behaviour values when first enabled."));
        bool changed = EditorGUI.EndChangeCheck();
        if (overrideObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(enemyOverride);
            SceneView.RepaintAll();
        }

        if (changed && !wasEnabled && enabled.boolValue)
        {
            InitializeLocalAttackOverride(
                wave,
                enemyOverride,
                selectedLocalOverrideSlot);
            overrideObject.Update();
        }

        if (!enabled.boolValue)
            return;

        if (overrideObject.FindProperty("useLegacyFullAttackSettings").boolValue)
        {
            EditorGUILayout.HelpBox(
                "This migrated override keeps its former local cadence to preserve the wave. Reset From Resolved Wave Attack switches it to the shared cadence and keeps only local shooting settings.",
                MessageType.Info);
        }

        SerializedProperty inheritFireMode = overrideObject.FindProperty(
            "inheritWaveFireMode");
        if (inheritFireMode.boolValue)
        {
            EditorGUILayout.PropertyField(
                inheritFireMode,
                new GUIContent("Use Shared Fire Mode"));
            var sharedSettings = new DirectedWaveAttackSettings();
            ((DirectedWaveAttackBehaviour)target).TryCopyResolvedAttackSettingsTo(
                selectedLocalOverrideSlot, false, sharedSettings);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Fire Mode", sharedSettings.FireMode);
                if (sharedSettings.FireMode == DirectedWaveAttackFireMode.Aimed)
                    EditorGUILayout.EnumPopup("Aim Mode", sharedSettings.AimMode);
                if (sharedSettings.FireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
                {
                    EditorGUILayout.FloatField(
                        "Forward Fire Angle Offset", sharedSettings.ForwardFireHalfAngle);
                }
            }
        }

        if (!inheritFireMode.boolValue)
        {
            SerializedProperty fireMode = overrideObject.FindProperty("fireMode");
            EditorGUILayout.PropertyField(
                fireMode,
                new GUIContent("Fire Mode", "Default runs this slot's configured Attack Pattern without aiming at the player. None disables wave-controlled shooting."));
            if ((DirectedWaveAttackFireMode)fireMode.enumValueIndex == DirectedWaveAttackFireMode.Aimed)
                DrawAimMode(overrideObject.FindProperty("aimMode"));
            if ((DirectedWaveAttackFireMode)fireMode.enumValueIndex
                == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
            {
                EditorGUILayout.PropertyField(
                    overrideObject.FindProperty("forwardFireHalfAngle"),
                    new GUIContent("Forward Fire Angle Offset"));
            }
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Attack Pattern", EditorStyles.miniBoldLabel);
        DrawLocalShootingPatterns(overrideObject.FindProperty("attackPattern"));
        DrawLocalAimModeWarning(wave, enemyOverride, overrideObject);
        EditorGUILayout.PropertyField(
            overrideObject.FindProperty("overrideProjectileBaseSpeed"),
            new GUIContent(
                "Override Base Projectile Speed",
                "Uses this value only for this enemy slot. The projectile prefab is unchanged."));
        if (overrideObject.FindProperty("overrideProjectileBaseSpeed").boolValue)
        {
            EditorGUILayout.PropertyField(
                overrideObject.FindProperty("projectileBaseSpeed"),
                new GUIContent("Base Projectile Speed"));
        }

        if (GUILayout.Button("Reset From Resolved Wave Attack"))
        {
            overrideObject.ApplyModifiedProperties();
            InitializeLocalAttackOverride(wave, enemyOverride, selectedLocalOverrideSlot);
            overrideObject.Update();
        }

        if (overrideObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(enemyOverride);
            SceneView.RepaintAll();
        }
    }

    private static void DrawAimMode(SerializedProperty aimMode)
    {
        EditorGUILayout.PropertyField(aimMode, new GUIContent("Aim Mode"));
        EditorGUILayout.HelpBox(
            "Aim On Start locks direction at the first shot until the attack ends. "
            + "Aim On Burst refreshes direction at the first shot of each burst and keeps it for the remaining shots. "
            + "Aim Continuous tracks the player and aims again for every shot, including shots within a burst.",
            MessageType.None);
    }

    private static void DrawBurstWarning(DirectedWaveAttackAimMode mode,
        EnemyBurstAttackSettings profile, string context)
    {
        if (DirectedWaveAttackAimState.RequiresBurstWarning(mode, profile))
            EditorGUILayout.HelpBox(context + ": Aim On Burst is selected, but Burst Attack is disabled "
                + "in one or more shooting patterns. These patterns keep their aim for the entire attack. "
                + "Enable Burst Attack or select Aim On Start.", MessageType.Warning);
    }

    private void DrawAimModeWarnings(DirectedWaveAttackSettings settings, SerializedProperty selectedSlots)
    {
        if (settings.FireMode != DirectedWaveAttackFireMode.Aimed
            || settings.AimMode != DirectedWaveAttackAimMode.AimOnBurst)
            return;
        if (!settings.UsesEnemyBurstSettings)
        {
            DrawBurstWarning(settings.AimMode, settings.WaveBurstSettings, "Wave Attack Pattern");
            return;
        }
        DirectedEnemySubWave wave = GetWave();
        if (wave == null) return;
        HashSet<int> slots = selectedSlots != null
            ? GetSelectedSlots(selectedSlots, wave.GetConfiguredEnemySlotCount()) : null;
        for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
        {
            if (slots != null && !slots.Contains(i)) continue;
            Enemy prefab = wave.GetConfiguredEnemyPrefabForSlot(i);
            if (prefab == null) continue;
            DrawBurstWarning(settings.AimMode, GetBurstAttackExecutor(prefab)?.BurstAttackSettings, $"Slot {i}");
        }
    }

    private void DrawLocalAimModeWarning(DirectedEnemySubWave wave,
        DirectedWaveEnemyOverride enemyOverride, SerializedObject overrideObject)
    {
        var resolved = new DirectedWaveAttackSettings();
        ((DirectedWaveAttackBehaviour)target).TryCopyResolvedAttackSettingsTo(
            selectedLocalOverrideSlot, false, resolved);
        bool inherits = overrideObject.FindProperty("inheritWaveFireMode").boolValue;
        DirectedWaveAttackFireMode mode = inherits ? resolved.FireMode
            : (DirectedWaveAttackFireMode)overrideObject.FindProperty("fireMode").enumValueIndex;
        if (mode != DirectedWaveAttackFireMode.Aimed) return;
        DirectedWaveAttackAimMode aim = inherits ? resolved.AimMode
            : (DirectedWaveAttackAimMode)overrideObject.FindProperty("aimMode").enumValueIndex;
        var profile = new EnemyBurstAttackSettings();
        if (!enemyOverride.TryCopyLegacyFullAttackSettings(profile))
        {
            Enemy prefab = wave.GetConfiguredEnemyPrefabForSlot(selectedLocalOverrideSlot);
            profile.CopyFrom(resolved.UsesEnemyBurstSettings && prefab != null
                ? GetBurstAttackExecutor(prefab)?.BurstAttackSettings : resolved.WaveBurstSettings);
        }
        // Local shooting does not change the cadence, but can change the pattern count.
        profile.ApplyShootingOverrideFrom((EnemyBurstAttackSettings)overrideObject.FindProperty("attackPattern").boxedValue);
        DrawBurstWarning(aim, profile, $"Slot {selectedLocalOverrideSlot}");
    }

    private void InitializeLocalAttackOverride(
        DirectedEnemySubWave wave,
        DirectedWaveEnemyOverride enemyOverride,
        int slotIndex)
    {
        serializedObject.ApplyModifiedProperties();
        var resolved = new DirectedWaveAttackSettings();
        bool hasResolvedSettings = ((DirectedWaveAttackBehaviour)target)
            .TryCopyResolvedAttackSettingsTo(slotIndex, false, resolved);
        EnemyBurstAttackSettings source = null;
        if (hasResolvedSettings && !resolved.UsesEnemyBurstSettings)
            source = resolved.WaveBurstSettings;
        else
        {
            Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(slotIndex);
            IEnemyBurstAttackExecutor executor = enemyPrefab != null
                ? GetBurstAttackExecutor(enemyPrefab)
                : null;
            source = executor != null ? executor.BurstAttackSettings : null;
        }

        Undo.RecordObject(
            enemyOverride,
            "Initialize Directed Wave Local Attack Override");
        enemyOverride.InitializeAttackPatternOverride(
            hasResolvedSettings ? resolved : null,
            source);
        EditorUtility.SetDirty(enemyOverride);
        if (PrefabUtility.IsPartOfPrefabInstance(enemyOverride))
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemyOverride);
        if (!hasResolvedSettings)
        {
            EditorGUILayout.HelpBox(
                "This slot is not controlled by a post-formation attack pattern. The local profile was initialized from its enemy prefab and will take effect after the slot is assigned to an attack pattern.",
                MessageType.Warning);
        }

        SceneView.RepaintAll();
    }

    private static void DrawLocalShootingPatterns(
        SerializedProperty attackPattern)
    {
        if (attackPattern == null)
            return;

        SerializedProperty mode = attackPattern.FindPropertyRelative(
            "attackPatterns");
        SerializedProperty patterns = attackPattern.FindPropertyRelative("patterns");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(mode, new GUIContent("Attack Patterns"));
        if (EditorGUI.EndChangeCheck()
            && (EnemyAttackPatternsMode)mode.enumValueIndex
                == EnemyAttackPatternsMode.Multiple
            && patterns.arraySize == 0)
        {
            EnemyBurstAttackSettingsDrawer.InitializeMultiple(attackPattern);
        }

        DrawLocalFacingSettings(attackPattern);
        if ((EnemyAttackPatternsMode)mode.enumValueIndex
            != EnemyAttackPatternsMode.Multiple)
        {
            EditorGUILayout.PropertyField(
                attackPattern.FindPropertyRelative("shooting"),
                new GUIContent("Shooting"),
                true);
            return;
        }

        for (int i = 0; i < patterns.arraySize; i++)
        {
            SerializedProperty entry = patterns.GetArrayElementAtIndex(i);
            entry.isExpanded = EditorGUILayout.Foldout(
                entry.isExpanded,
                $"Pattern {i + 1}",
                true);
            if (!entry.isExpanded)
                continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                entry.FindPropertyRelative("shooting"),
                new GUIContent("Shooting"),
                true);
            bool changedPatterns = false;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate"))
            {
                EnemyBurstAttackSettingsDrawer.Duplicate(patterns, i);
                changedPatterns = true;
            }

            using (new EditorGUI.DisabledScope(patterns.arraySize <= 1))
            {
                if (!changedPatterns && GUILayout.Button("Remove"))
                {
                    patterns.DeleteArrayElementAtIndex(i);
                    changedPatterns = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            if (changedPatterns)
                break;
        }

        if (GUILayout.Button("Add Pattern"))
        {
            int index = patterns.arraySize;
            patterns.arraySize++;
            patterns.GetArrayElementAtIndex(index).boxedValue =
                new EnemyAttackPatternSettings();
            patterns.GetArrayElementAtIndex(index).isExpanded = true;
        }
    }

    private static void DrawLocalFacingSettings(SerializedProperty attackPattern)
    {
        SerializedProperty facingMode = attackPattern.FindPropertyRelative(
            "facingMode");
        EditorGUILayout.PropertyField(facingMode, new GUIContent("Facing Mode"));
        if ((EnemyFacingMode)facingMode.enumValueIndex
            != EnemyFacingMode.FollowPattern)
        {
            return;
        }

        SerializedProperty mode = attackPattern.FindPropertyRelative(
            "attackPatterns");
        SerializedProperty patterns = attackPattern.FindPropertyRelative("patterns");
        if ((EnemyAttackPatternsMode)mode.enumValueIndex
            == EnemyAttackPatternsMode.Multiple
            && patterns.arraySize > 0)
        {
            string[] labels = new string[patterns.arraySize];
            SerializedProperty selectedId = attackPattern.FindPropertyRelative(
                "facingPatternId");
            int selectedIndex = 0;
            for (int i = 0; i < patterns.arraySize; i++)
            {
                SerializedProperty entry = patterns.GetArrayElementAtIndex(i);
                SerializedProperty shooting = entry.FindPropertyRelative("shooting");
                SerializedProperty shootingType = shooting.FindPropertyRelative(
                    "shootingType");
                labels[i] = $"Pattern {i + 1} ({shootingType.enumDisplayNames[shootingType.enumValueIndex]})";
                if (selectedId.stringValue == entry.FindPropertyRelative("patternId").stringValue)
                    selectedIndex = i;
            }

            int chosen = EditorGUILayout.Popup(
                "Facing Pattern",
                selectedIndex,
                labels);
            selectedId.stringValue = patterns.GetArrayElementAtIndex(chosen)
                .FindPropertyRelative("patternId").stringValue;
        }

        EditorGUILayout.PropertyField(
            attackPattern.FindPropertyRelative("facingSpeed"),
            new GUIContent("Facing Speed"));
        EditorGUILayout.PropertyField(
            attackPattern.FindPropertyRelative("facingAngleOffset"),
            new GUIContent("Facing Angle Offset"));
    }

    private void ApplyModifiedPropertiesAndAddAttackPattern()
    {
        serializedObject.ApplyModifiedProperties();
        DirectedWaveAttackBehaviour behaviour =
            (DirectedWaveAttackBehaviour)target;
        Undo.RecordObject(behaviour, "Add Directed Wave Attack Pattern");
        behaviour.AddAttackPattern();
        EditorUtility.SetDirty(behaviour);
        serializedObject.Update();
    }

    private void ApplyModifiedPropertiesAndRemoveAttackPattern(int patternIndex)
    {
        serializedObject.ApplyModifiedProperties();
        DirectedWaveAttackBehaviour behaviour =
            (DirectedWaveAttackBehaviour)target;
        Undo.RecordObject(behaviour, "Remove Directed Wave Attack Pattern");
        behaviour.RemoveAttackPattern(patternIndex);
        EditorUtility.SetDirty(behaviour);
        serializedObject.Update();
    }

    private void DrawAttackPatternSlotSelection(
        SerializedProperty selectedSlots,
        string label,
        int patternIndex)
    {
        DirectedEnemySubWave wave = GetWave();
        if (wave == null)
        {
            EditorGUILayout.HelpBox(
                "DirectedEnemySubWave is required to select attacking enemies.",
                MessageType.Error);
            return;
        }

        int slotCount = wave.GetConfiguredEnemySlotCount();
        if (slotCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "Create formation slots before selecting attacking enemies.",
                MessageType.Info);
            return;
        }

        HashSet<int> selectedSlotsSet = GetSelectedSlots(selectedSlots, slotCount);
        HashSet<int> occupiedSlots = GetAttackPatternOccupiedSlots(
            patternIndex,
            slotCount);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < slotCount; i++)
        {
            Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(i);
            string enemyName = enemyPrefab != null ? enemyPrefab.name : "Missing Enemy";
            bool occupiedByOtherPattern = occupiedSlots.Contains(i);
            EditorGUI.BeginDisabledGroup(occupiedByOtherPattern);
            bool selected = EditorGUILayout.ToggleLeft(
                $"Slot {i} - {enemyName}"
                + (occupiedByOtherPattern ? " (assigned to another pattern)" : string.Empty),
                selectedSlotsSet.Contains(i));
            EditorGUI.EndDisabledGroup();

            if (occupiedByOtherPattern)
                continue;

            if (selected)
                selectedSlotsSet.Add(i);
            else
                selectedSlotsSet.Remove(i);
        }

        if (EditorGUI.EndChangeCheck())
            WriteSelectedSlots(selectedSlots, selectedSlotsSet);
    }

    private HashSet<int> GetAttackPatternOccupiedSlots(
        int exceptPatternIndex,
        int slotCount)
    {
        HashSet<int> result = new();
        if (attackPatterns == null)
            return result;

        for (int i = 0; i < attackPatterns.arraySize; i++)
        {
            if (i == exceptPatternIndex)
                continue;

            SerializedProperty selectedSlots = attackPatterns
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("selectedEnemySlots");
            result.UnionWith(GetSelectedSlots(selectedSlots, slotCount));
        }

        return result;
    }

    private static void EnsureProjectFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string currentFolder = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string nextFolder = currentFolder + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(nextFolder))
                AssetDatabase.CreateFolder(currentFolder, segments[i]);

            currentFolder = nextFolder;
        }
    }

    private static void DrawEnemyAttackPatternOverrides(SerializedProperty settings)
    {
        SerializedProperty overrideCooldown = settings.FindPropertyRelative(
            "overrideEnemyAttackCooldown");
        SerializedProperty cooldown = settings.FindPropertyRelative(
            "enemyAttackCooldown");
        if (overrideCooldown == null || cooldown == null)
            return;

        EditorGUILayout.PropertyField(
            overrideCooldown,
            new GUIContent("Override Enemy Attack Cooldown"));
        if (overrideCooldown.boolValue)
        {
            EditorGUILayout.PropertyField(
                cooldown,
                new GUIContent("Attack Cooldown"));
        }
    }

    private void DrawBaseProjectileSpeedOverride(
        SerializedProperty overrideProjectileSpeed,
        SerializedProperty projectileSpeed,
        SerializedProperty selectedSlots)
    {
        if (overrideProjectileSpeed == null || projectileSpeed == null)
            return;

        bool wasEnabled = overrideProjectileSpeed.boolValue;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            overrideProjectileSpeed,
            new GUIContent(
                "Override Base Projectile Speed",
                "Creates a runtime copy of the attack pattern. The enemy and projectile prefabs are not changed."));
        bool changed = EditorGUI.EndChangeCheck();

        if (changed && !wasEnabled && overrideProjectileSpeed.boolValue)
        {
            if (TryGetBaseProjectileSpeed(selectedSlots, out float baseSpeed))
                projectileSpeed.floatValue = baseSpeed;
            else
            {
                EditorGUILayout.HelpBox(
                    "Base speed could not be read from the selected enemy projectile. Enter a value manually.",
                    MessageType.Warning);
            }
        }

        if (overrideProjectileSpeed.boolValue)
        {
            EditorGUILayout.PropertyField(
                projectileSpeed,
                new GUIContent("Base Projectile Speed"));
        }
    }

    private bool TryGetBaseProjectileSpeed(
        SerializedProperty selectedSlots,
        out float baseSpeed)
    {
        baseSpeed = 0f;
        DirectedEnemySubWave wave = GetWave();
        if (wave == null)
            return false;

        int slotCount = wave.GetConfiguredEnemySlotCount();
        HashSet<int> selectedSlotSet = selectedSlots != null
            ? GetSelectedSlots(selectedSlots, slotCount)
            : null;
        HashSet<Enemy> processedEnemies = new();
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            if (selectedSlotSet != null && !selectedSlotSet.Contains(slotIndex))
                continue;

            Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(slotIndex);
            if (enemyPrefab == null || !processedEnemies.Add(enemyPrefab))
                continue;

            SimpleEnemyAttackExecutor executor = enemyPrefab.GetComponent<
                SimpleEnemyAttackExecutor>();
            if (executor != null && executor.TryGetProjectileBaseSpeed(
                    executor.BurstAttackSettings,
                    out baseSpeed))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawEnemyAttackDurationSummary(SerializedProperty selectedSlots)
    {
        DirectedWaveAttackBehaviour behaviour =
            target as DirectedWaveAttackBehaviour;
        DirectedEnemySubWave wave = behaviour != null
            ? behaviour.GetComponent<DirectedEnemySubWave>()
            : null;
        if (wave == null)
            return;

        HashSet<Enemy> processedEnemies = new();
        HashSet<int> selectedSlotSet = selectedSlots != null
            ? GetSelectedSlots(
                selectedSlots,
                wave.GetConfiguredEnemySlotCount())
            : null;
        int slotCount = wave.GetConfiguredEnemySlotCount();
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            if (selectedSlotSet != null && !selectedSlotSet.Contains(slotIndex))
                continue;

            Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(slotIndex);
            if (enemyPrefab == null || !processedEnemies.Add(enemyPrefab))
                continue;

            IEnemyBurstAttackExecutor executor =
                GetBurstAttackExecutor(enemyPrefab);
            if (executor == null || executor.BurstAttackSettings == null)
            {
                EditorGUILayout.HelpBox(
                    $"{enemyPrefab.name}: no EnemyBurstAttackSettings were found.",
                    MessageType.Warning);
                continue;
            }

            EnemyBurstAttackSettings settings = executor.BurstAttackSettings;
            EditorGUILayout.HelpBox(
                $"{enemyPrefab.name}: fires for {settings.AttackDuration:0.###} s "
                + $"per attack ({settings.ShotEventsPerAttack} shot events).",
                MessageType.Info);
        }
    }

    private static IEnemyBurstAttackExecutor GetBurstAttackExecutor(
        Enemy enemyPrefab)
    {
        MonoBehaviour[] components = enemyPrefab.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is IEnemyBurstAttackExecutor executor)
                return executor;
        }

        return null;
    }

    private void DrawEntranceAttacks()
    {
        EditorGUILayout.LabelField("Entrance Attacks", EditorStyles.boldLabel);
        if (entranceAttackSettings == null)
        {
            EditorGUILayout.HelpBox(
                "Entrance attack settings could not be serialized.",
                MessageType.Error);
            return;
        }

        SerializedProperty isEnabled = entranceAttackSettings.FindPropertyRelative(
            "isEnabled");
        EditorGUILayout.PropertyField(
            isEnabled,
            new GUIContent("Enable Entrance Attacks"));
        if (!isEnabled.boolValue)
            return;

        DirectedEnemySubWave wave = GetWave();
        if (wave == null)
        {
            EditorGUILayout.HelpBox(
                "DirectedEnemySubWave is required for entrance attacks.",
                MessageType.Error);
            return;
        }

        if (!wave.UsesCheckpointEntrancePath)
        {
            EditorGUILayout.HelpBox(
                "Entrance attacks use checkpoint routes. Switch Entrance Mode to Checkpoints to run these rules.",
                MessageType.Warning);
        }

        int checkpointCount = wave.GetConfiguredEntranceCheckpointCount();
        if (checkpointCount < 2)
        {
            EditorGUILayout.HelpBox(
                "Create at least two entrance checkpoints before adding attack rules.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Entrance rules fire without changing the route. Fire Mode and Attack Pattern are shared with Post-Formation Attack above. "
            + "Each rule entry is one firing action; the Area Attack setting still creates a projectile spread.",
            MessageType.None);

        DrawContinuousRouteAttack(
            entranceAttackSettings.FindPropertyRelative("continuousAttackRule"),
            checkpointCount);
        EditorGUILayout.Space(4f);
        DrawAtCheckpointRules(
            entranceAttackSettings.FindPropertyRelative("atCheckpointRules"),
            checkpointCount);
        EditorGUILayout.Space(4f);
        DrawAcrossCheckpointRules(
            entranceAttackSettings.FindPropertyRelative("acrossCheckpointRules"),
            checkpointCount);
    }

    private void DrawContinuousRouteAttack(
        SerializedProperty rule,
        int checkpointCount)
    {
        EditorGUILayout.LabelField(
            "Continuous Route Attack",
            EditorStyles.boldLabel);
        if (rule == null)
        {
            EditorGUILayout.HelpBox(
                "Continuous route attack settings could not be serialized.",
                MessageType.Error);
            return;
        }

        SerializedProperty isEnabled = rule.FindPropertyRelative("isEnabled");
        EditorGUILayout.PropertyField(
            isEnabled,
            new GUIContent("Enable Continuous Route Attack"));
        if (!isEnabled.boolValue)
            return;

        SerializedProperty startMode = rule.FindPropertyRelative("startMode");
        EditorGUILayout.PropertyField(
            startMode,
            new GUIContent("Start Attack At"));
        if ((DirectedWaveContinuousEntranceAttackStartMode)startMode.enumValueIndex
            == DirectedWaveContinuousEntranceAttackStartMode.Checkpoint)
        {
            DrawCheckpointIndex(
                rule.FindPropertyRelative("checkpointIndex"),
                "Start Checkpoint",
                checkpointCount);
        }

        SerializedProperty movementMode = attackSettings.FindPropertyRelative(
            "movementMode");
        if ((DirectedWaveAttackMovementMode)movementMode.enumValueIndex
            != DirectedWaveAttackMovementMode.None)
        {
            EditorGUILayout.HelpBox(
                "During Dive, the entrance route keeps advancing in the background. "
                + "The enemy returns to its current route position after the attack.",
                MessageType.Info);
        }
    }

    private void DrawAtCheckpointRules(
        SerializedProperty rules,
        int checkpointCount)
    {
        EditorGUILayout.LabelField("At Checkpoint", EditorStyles.boldLabel);
        int removeIndex = -1;
        for (int i = 0; i < rules.arraySize; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Rule {i + 1}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("isEnabled"));
            DrawCheckpointIndex(
                rule.FindPropertyRelative("checkpointIndex"),
                "Checkpoint",
                checkpointCount);
            EditorGUILayout.PropertyField(
                rule.FindPropertyRelative("shotCount"),
                new GUIContent("Shots Per Enemy"));
            DrawEnemySlotSelection(
                rule.FindPropertyRelative("selectedEnemySlots"),
                rule.FindPropertyRelative("useSelectedEnemySlots"),
                "Attacking Slots");

            if (GUILayout.Button("Remove Rule"))
                removeIndex = i;

            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            rules.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("Add At Checkpoint Rule"))
        {
            int index = rules.arraySize;
            rules.InsertArrayElementAtIndex(index);
            InitializeAtCheckpointRule(rules.GetArrayElementAtIndex(index));
        }
    }

    private void DrawAcrossCheckpointRules(
        SerializedProperty rules,
        int checkpointCount)
    {
        EditorGUILayout.LabelField("Across Checkpoints", EditorStyles.boldLabel);
        int removeIndex = -1;
        for (int i = 0; i < rules.arraySize; i++)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Rule {i + 1}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("isEnabled"));
            DrawCheckpointIndex(
                rule.FindPropertyRelative("startCheckpointIndex"),
                "Start Checkpoint",
                checkpointCount);
            DrawCheckpointIndex(
                rule.FindPropertyRelative("endCheckpointIndex"),
                "End Checkpoint",
                checkpointCount);
            if (rule.FindPropertyRelative("startCheckpointIndex").intValue
                == rule.FindPropertyRelative("endCheckpointIndex").intValue)
            {
                EditorGUILayout.HelpBox(
                    "Start and End Checkpoint must be different.",
                    MessageType.Warning);
            }
            EditorGUILayout.PropertyField(
                rule.FindPropertyRelative("attackCountMode"),
                new GUIContent("Attack Count Applies To"));
            EditorGUILayout.PropertyField(
                rule.FindPropertyRelative("attackCount"),
                new GUIContent("Attack Count"));

            SerializedProperty attackCountMode = rule.FindPropertyRelative(
                "attackCountMode");
            if ((DirectedWaveEntranceAttackCountMode)attackCountMode.enumValueIndex
                == DirectedWaveEntranceAttackCountMode.TotalForGroup)
            {
                EditorGUILayout.PropertyField(
                    rule.FindPropertyRelative("attackOrder"),
                    new GUIContent("Group Order"));
            }

            DrawEnemySlotSelection(
                rule.FindPropertyRelative("selectedEnemySlots"),
                rule.FindPropertyRelative("useSelectedEnemySlots"),
                "Attacking Slots");

            if (GUILayout.Button("Remove Rule"))
                removeIndex = i;

            EditorGUILayout.EndVertical();
        }

        if (removeIndex >= 0)
            rules.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("Add Across Checkpoints Rule"))
        {
            int index = rules.arraySize;
            rules.InsertArrayElementAtIndex(index);
            InitializeAcrossCheckpointRule(
                rules.GetArrayElementAtIndex(index),
                checkpointCount);
        }
    }

    private void DrawEnemySlotSelection(
        SerializedProperty selectedSlots,
        SerializedProperty useSlots,
        string label)
    {
        EditorGUILayout.PropertyField(
            useSlots,
            new GUIContent("Use Selected Enemy Slots"));
        if (!useSlots.boolValue)
            return;

        DrawEnemySlotSelection(selectedSlots, label);
    }

    private void DrawEnemySlotSelection(
        SerializedProperty selectedSlots,
        string label)
    {
        DirectedEnemySubWave wave = GetWave();
        if (wave == null)
        {
            EditorGUILayout.HelpBox(
                "DirectedEnemySubWave is required to select attacking enemies.",
                MessageType.Error);
            return;
        }

        int slotCount = wave.GetConfiguredEnemySlotCount();
        if (slotCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "Create formation slots before selecting attacking enemies.",
                MessageType.Info);
            return;
        }

        HashSet<int> selectedSlotsSet = GetSelectedSlots(selectedSlots, slotCount);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < slotCount; i++)
        {
            Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(i);
            string enemyName = enemyPrefab != null ? enemyPrefab.name : "Missing Enemy";
            bool selected = EditorGUILayout.ToggleLeft(
                $"Slot {i} - {enemyName}",
                selectedSlotsSet.Contains(i));
            if (selected)
                selectedSlotsSet.Add(i);
            else
                selectedSlotsSet.Remove(i);
        }

        if (EditorGUI.EndChangeCheck())
            WriteSelectedSlots(selectedSlots, selectedSlotsSet);
    }

    private static void DrawCheckpointIndex(
        SerializedProperty property,
        string label,
        int checkpointCount)
    {
        int maxIndex = Mathf.Max(0, checkpointCount - 1);
        property.intValue = EditorGUILayout.IntSlider(
            label,
            Mathf.Clamp(property.intValue, 0, maxIndex),
            0,
            maxIndex);
    }

    private static void InitializeAtCheckpointRule(SerializedProperty rule)
    {
        rule.FindPropertyRelative("isEnabled").boolValue = true;
        rule.FindPropertyRelative("checkpointIndex").intValue = 0;
        rule.FindPropertyRelative("useSelectedEnemySlots").boolValue = false;
        rule.FindPropertyRelative("selectedEnemySlots").arraySize = 0;
        rule.FindPropertyRelative("shotCount").intValue = 1;
    }

    private static void InitializeAcrossCheckpointRule(
        SerializedProperty rule,
        int checkpointCount)
    {
        rule.FindPropertyRelative("isEnabled").boolValue = true;
        rule.FindPropertyRelative("startCheckpointIndex").intValue = 0;
        rule.FindPropertyRelative("endCheckpointIndex").intValue =
            Mathf.Min(1, Mathf.Max(0, checkpointCount - 1));
        rule.FindPropertyRelative("useSelectedEnemySlots").boolValue = false;
        rule.FindPropertyRelative("selectedEnemySlots").arraySize = 0;
        rule.FindPropertyRelative("attackCountMode").enumValueIndex =
            (int)DirectedWaveEntranceAttackCountMode.PerEnemy;
        rule.FindPropertyRelative("attackCount").intValue = 1;
        rule.FindPropertyRelative("attackOrder").enumValueIndex =
            (int)DirectedWaveEntranceAttackOrder.Sequential;
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        DirectedWaveAttackBehaviour behaviour =
            (DirectedWaveAttackBehaviour)target;
        DirectedEnemySubWave wave = behaviour.GetComponent<DirectedEnemySubWave>();
        if (wave == null)
            return;

        DrawLocalAttackOverrideSlotHandles(wave);
        DrawPostAttackSlotMarkers(wave);
        DrawEntranceAttackMarkers(wave);
    }

    private void DrawLocalAttackOverrideSlotHandles(DirectedEnemySubWave wave)
    {
        int slotCount = wave.GetConfiguredEnemySlotCount();
        if (slotCount <= 0)
            return;

        Color previousColor = Handles.color;
        for (int i = 0; i < slotCount; i++)
        {
            Vector3 position = wave.GetConfiguredFormationSlotPosition(i);
            bool selected = i == selectedLocalOverrideSlot;
            DirectedWaveEnemyOverride enemyOverride =
                wave.GetConfiguredEnemyOverrideForSlot(i);
            bool hasOverride = enemyOverride != null
                && enemyOverride.HasAttackPatternOverride;
            Color color = selected
                ? new Color(1f, 0.62f, 0.1f, 1f)
                : hasOverride
                    ? new Color(0.8f, 0.35f, 1f, 0.95f)
                    : new Color(0.55f, 0.8f, 1f, 0.75f);
            float size = Mathf.Max(
                0.08f,
                HandleUtility.GetHandleSize(position) * 0.075f);
            Handles.color = color;
            if (Handles.Button(
                    position,
                    Quaternion.identity,
                    size,
                    size,
                    Handles.CircleHandleCap))
            {
                selectedLocalOverrideSlot = i;
                Repaint();
                SceneView.RepaintAll();
            }

            if (selected)
            {
                Handles.Label(
                    position + Vector3.up * size * 1.8f,
                    $"Attack override: slot {i}",
                    CreateMarkerLabelStyle(color));
            }
        }

        Handles.color = previousColor;
    }

    private void DrawPostAttackSlotMarkers(DirectedEnemySubWave wave)
    {
        if (attackPatterns != null && attackPatterns.arraySize > 0)
        {
            Color[] patternColors =
            {
                new Color(0.2f, 1f, 0.35f, 0.9f),
                new Color(0.25f, 0.7f, 1f, 0.9f),
                new Color(1f, 0.4f, 0.8f, 0.9f),
                new Color(1f, 0.75f, 0.2f, 0.9f)
            };
            Color patternPreviousColor = Handles.color;
            int slotCount = wave.GetConfiguredEnemySlotCount();
            for (int i = 0; i < attackPatterns.arraySize; i++)
            {
                HashSet<int> selectedSlots = GetSelectedSlots(
                    attackPatterns.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("selectedEnemySlots"),
                    slotCount);
                Handles.color = patternColors[i % patternColors.Length];
                foreach (int slotIndex in selectedSlots)
                {
                    Vector3 position = wave.GetConfiguredFormationSlotPosition(slotIndex);
                    Handles.DrawSolidDisc(position, Vector3.forward, 0.16f);
                    Handles.DrawWireDisc(position, Vector3.forward, 0.28f);
                    Handles.Label(
                        position + Vector3.up * 0.32f,
                        $"ATTACK {i + 1} {slotIndex}: {GetEnemyName(wave, slotIndex)}",
                        CreateMarkerLabelStyle(Handles.color));
                }
            }

            Handles.color = patternPreviousColor;
            return;
        }

        if (!useSelectedEnemySlots.boolValue)
            return;

        HashSet<int> selectedSlotsSet = GetSelectedSlots(
            selectedEnemySlots,
            wave.GetConfiguredEnemySlotCount());
        if (selectedSlotsSet.Count == 0)
            return;

        Color previousColor = Handles.color;
        Handles.color = new Color(0.2f, 1f, 0.35f, 0.9f);
        foreach (int slotIndex in selectedSlotsSet)
        {
            Vector3 position = wave.GetConfiguredFormationSlotPosition(slotIndex);
            Handles.DrawSolidDisc(position, Vector3.forward, 0.16f);
            Handles.DrawWireDisc(position, Vector3.forward, 0.28f);
            Handles.Label(
                position + Vector3.up * 0.32f,
                $"POST ATTACK {slotIndex}: {GetEnemyName(wave, slotIndex)}",
                CreateMarkerLabelStyle(Handles.color));
        }

        Handles.color = previousColor;
    }

    private void DrawEntranceAttackMarkers(DirectedEnemySubWave wave)
    {
        if (entranceAttackSettings == null
            || !entranceAttackSettings.FindPropertyRelative("isEnabled").boolValue
            || !wave.UsesCheckpointEntrancePath)
        {
            return;
        }

        int checkpointCount = wave.GetConfiguredEntranceCheckpointCount();
        if (checkpointCount <= 0)
            return;

        SerializedProperty checkpointRules = entranceAttackSettings.FindPropertyRelative(
            "atCheckpointRules");
        SerializedProperty acrossRules = entranceAttackSettings.FindPropertyRelative(
            "acrossCheckpointRules");
        Color previousColor = Handles.color;

        for (int i = 0; i < checkpointRules.arraySize; i++)
        {
            SerializedProperty rule = checkpointRules.GetArrayElementAtIndex(i);
            if (!rule.FindPropertyRelative("isEnabled").boolValue)
                continue;

            int checkpointIndex = Mathf.Clamp(
                rule.FindPropertyRelative("checkpointIndex").intValue,
                0,
                checkpointCount - 1);
            Vector3 position = wave.GetConfiguredEntranceCheckpointPosition(
                checkpointIndex);
            Handles.color = new Color(1f, 0.8f, 0.15f, 0.95f);
            Handles.DrawSolidDisc(position, Vector3.forward, 0.12f);
            Handles.DrawWireDisc(position, Vector3.forward, 0.22f);
            int shotCount = Mathf.Max(
                1,
                rule.FindPropertyRelative("shotCount").intValue);
            Handles.Label(
                position + Vector3.up * 0.24f,
                $"FIRE x{shotCount}",
                CreateMarkerLabelStyle(Handles.color));
            DrawRuleSlotMarkers(wave, rule, new Color(1f, 0.8f, 0.15f, 0.9f));
        }

        for (int i = 0; i < acrossRules.arraySize; i++)
        {
            SerializedProperty rule = acrossRules.GetArrayElementAtIndex(i);
            if (!rule.FindPropertyRelative("isEnabled").boolValue)
                continue;

            int startCheckpointIndex = Mathf.Clamp(
                rule.FindPropertyRelative("startCheckpointIndex").intValue,
                0,
                checkpointCount - 1);
            int endCheckpointIndex = Mathf.Clamp(
                rule.FindPropertyRelative("endCheckpointIndex").intValue,
                0,
                checkpointCount - 1);
            Vector3 startPosition = wave.GetConfiguredEntranceCheckpointPosition(
                startCheckpointIndex);
            Vector3 endPosition = wave.GetConfiguredEntranceCheckpointPosition(
                endCheckpointIndex);
            Handles.color = new Color(1f, 0.4f, 0.15f, 0.95f);
            Handles.DrawWireDisc(startPosition, Vector3.forward, 0.19f);
            Handles.DrawWireDisc(endPosition, Vector3.forward, 0.19f);
            Handles.DrawDottedLine(startPosition, endPosition, 4f);
            int attackCount = Mathf.Max(
                1,
                rule.FindPropertyRelative("attackCount").intValue);
            Handles.Label(
                startPosition + Vector3.up * 0.24f,
                $"FIRE x{attackCount} -> {endCheckpointIndex}",
                CreateMarkerLabelStyle(Handles.color));
            DrawRuleSlotMarkers(wave, rule, new Color(1f, 0.4f, 0.15f, 0.9f));
        }

        Handles.color = previousColor;
    }

    private static void DrawRuleSlotMarkers(
        DirectedEnemySubWave wave,
        SerializedProperty rule,
        Color color)
    {
        SerializedProperty useSlots = rule.FindPropertyRelative(
            "useSelectedEnemySlots");
        if (useSlots == null || !useSlots.boolValue)
            return;

        HashSet<int> slots = GetSelectedSlots(
            rule.FindPropertyRelative("selectedEnemySlots"),
            wave.GetConfiguredEnemySlotCount());
        Color previousColor = Handles.color;
        Handles.color = color;
        foreach (int slotIndex in slots)
        {
            Vector3 position = wave.GetConfiguredFormationSlotPosition(slotIndex);
            Handles.DrawWireDisc(position, Vector3.forward, 0.22f);
            Handles.Label(
                position + Vector3.down * 0.3f,
                $"ENTRY {slotIndex}",
                CreateMarkerLabelStyle(color));
        }

        Handles.color = previousColor;
    }

    private static GUIStyle CreateMarkerLabelStyle(Color color)
    {
        return new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = color }
        };
    }

    private static string GetEnemyName(
        DirectedEnemySubWave wave,
        int slotIndex)
    {
        Enemy enemyPrefab = wave.GetConfiguredEnemyPrefabForSlot(slotIndex);
        return enemyPrefab != null ? enemyPrefab.name : "Missing Enemy";
    }

    private static HashSet<int> GetSelectedSlots(
        SerializedProperty slots,
        int slotCount)
    {
        HashSet<int> result = new();
        if (slots == null)
            return result;

        for (int i = 0; i < slots.arraySize; i++)
        {
            int slotIndex = slots.GetArrayElementAtIndex(i).intValue;
            if (slotIndex >= 0 && slotIndex < slotCount)
                result.Add(slotIndex);
        }

        return result;
    }

    private static void WriteSelectedSlots(
        SerializedProperty slots,
        HashSet<int> selectedSlotsSet)
    {
        List<int> orderedSlots = new(selectedSlotsSet);
        orderedSlots.Sort();
        slots.arraySize = orderedSlots.Count;
        for (int i = 0; i < orderedSlots.Count; i++)
            slots.GetArrayElementAtIndex(i).intValue = orderedSlots[i];
    }

    private DirectedEnemySubWave GetWave()
    {
        return ((DirectedWaveAttackBehaviour)target)
            .GetComponent<DirectedEnemySubWave>();
    }
}
