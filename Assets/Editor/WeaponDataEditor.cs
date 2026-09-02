using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponData), true)]
[CanEditMultipleObjects]
public sealed class WeaponDataEditor : Editor
{
    private SerializedProperty reloadTimeByLevel;
    private SerializedProperty angleByLevel;
    private SerializedProperty damageByLevel;
    private SerializedProperty rangeByLevel;
    private SerializedProperty speedByLevel;
    private SerializedProperty levelConfigs;

    private SerializedProperty startLevel;
    private SerializedProperty maxLevel;
    private SerializedProperty energyCost;
    private SerializedProperty damageType;

    private SerializedProperty flightMode;
    private SerializedProperty contactMode;
    private SerializedProperty homingRotationSpeed;
    private SerializedProperty growDuringFlight;
    private SerializedProperty scaleGrowthPerSecond;
    private SerializedProperty projectileLifetime;
    private SerializedProperty disableColliderAfterFirstPhysicsStep;
    private SerializedProperty fadeDuringLifetime;
    private SerializedProperty fadeDuration;
    private SerializedProperty explosionPrefab;
    private SerializedProperty explosionDamage;
    private SerializedProperty continuousDamageInterval;

    private SerializedProperty audioClipDefault;
    private SerializedProperty audioClipProjectileShot;

    private SerializedProperty thermalLevels;
    private SerializedProperty beamBlockingLayers;
    private SerializedProperty thermalExplosionRadius;
    private SerializedProperty thermalExplosionDamage;
    private SerializedProperty transferredHeatPercent;
    private SerializedProperty coolingDelay;
    private SerializedProperty coolingPercentPerSecond;
    private SerializedProperty thermalExplosionPrefab;

    private SerializedProperty qBeamLevels;
    private SerializedProperty qBeamChargeDecayDelay;
    private SerializedProperty qBeamChargeDecayPerSecond;

    private SerializedProperty ballLightningLevels;
    private SerializedProperty ballLightningProjectileSpeed;
    private SerializedProperty ballLightningBallsPerShot;
    private SerializedProperty ballLightningSpreadAngle;
    private SerializedProperty ballLightningAreaRadius;
    private SerializedProperty ballLightningAreaDamageLayers;

    private bool showLegacyStats = true;

    private void OnEnable()
    {
        reloadTimeByLevel = serializedObject.FindProperty("reloadTimeByLevel");
        angleByLevel = serializedObject.FindProperty("angleByLevel");
        damageByLevel = serializedObject.FindProperty("damageByLevel");
        rangeByLevel = serializedObject.FindProperty("rangeByLevel");
        speedByLevel = serializedObject.FindProperty("speedByLevel");
        levelConfigs = serializedObject.FindProperty("levelConfigs");

        startLevel = serializedObject.FindProperty("startLevel");
        maxLevel = serializedObject.FindProperty("maxLevel");
        energyCost = serializedObject.FindProperty("energyCost");
        damageType = serializedObject.FindProperty("damageType");

        flightMode = serializedObject.FindProperty("flightMode");
        contactMode = serializedObject.FindProperty("contactMode");
        homingRotationSpeed = serializedObject.FindProperty("homingRotationSpeed");
        growDuringFlight = serializedObject.FindProperty("growDuringFlight");
        scaleGrowthPerSecond = serializedObject.FindProperty("scaleGrowthPerSecond");
        projectileLifetime = serializedObject.FindProperty("projectileLifetime");
        disableColliderAfterFirstPhysicsStep =
            serializedObject.FindProperty(
                "disableColliderAfterFirstPhysicsStep");
        fadeDuringLifetime =
            serializedObject.FindProperty("fadeDuringLifetime");
        fadeDuration = serializedObject.FindProperty("fadeDuration");
        explosionPrefab = serializedObject.FindProperty("explosionPrefab");
        explosionDamage = serializedObject.FindProperty("explosionDamage");
        continuousDamageInterval = serializedObject.FindProperty("continuousDamageInterval");

        audioClipDefault = serializedObject.FindProperty("audioClipDefault");
        audioClipProjectileShot = serializedObject.FindProperty("audioClipProjectileShot");

        thermalLevels = serializedObject.FindProperty("thermalLevels");
        beamBlockingLayers = serializedObject.FindProperty("beamBlockingLayers");
        thermalExplosionRadius =
            serializedObject.FindProperty("overheatExplosionRadius");
        thermalExplosionDamage =
            serializedObject.FindProperty("overheatExplosionDamage");
        transferredHeatPercent =
            serializedObject.FindProperty("transferredHeatPercent");
        coolingDelay = serializedObject.FindProperty("coolingDelay");
        coolingPercentPerSecond =
            serializedObject.FindProperty("coolingPercentPerSecond");
        thermalExplosionPrefab =
            serializedObject.FindProperty("overheatExplosionPrefab");

        qBeamLevels = serializedObject.FindProperty("qBeamLevels");
        qBeamChargeDecayDelay =
            serializedObject.FindProperty("chargeDecayDelay");
        qBeamChargeDecayPerSecond =
            serializedObject.FindProperty("chargeDecayPerSecond");

        ballLightningLevels =
            serializedObject.FindProperty("ballLightningLevels");
        ballLightningProjectileSpeed =
            serializedObject.FindProperty("projectileSpeed");
        ballLightningBallsPerShot =
            serializedObject.FindProperty("ballsPerShot");
        ballLightningSpreadAngle =
            serializedObject.FindProperty("ballSpreadAngle");
        ballLightningAreaRadius =
            serializedObject.FindProperty("areaRadius");
        ballLightningAreaDamageLayers =
            serializedObject.FindProperty("areaDamageLayers");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawLevelConfigs();
        if (target is not BallLightningData)
            DrawLegacyStats();
        DrawLevels();
        DrawBuild();

        if (target is ThermalLaserData)
        {
            DrawThermalLaserSettings();
        }
        else if (target is QBeamData)
        {
            DrawQBeamSettings();
        }
        else if (target is BallLightningData)
        {
            DrawBallLightningSettings();
            DrawLifetime();
        }
        else
        {
            DrawBehaviors();
            DrawLifetime();
        }

        DrawAudio();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLevelConfigs()
    {
        bool isBallLightning = target is BallLightningData;
        EditorGUILayout.LabelField("Level Configurations", EditorStyles.boldLabel);

        if (levelConfigs.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "This weapon still uses the legacy per-stat lists below. "
                + "Create configurations to edit complete level sections.",
                MessageType.Info);

            if (targets.Length == 1
                && ((WeaponData)target).HasLegacyLevelStats
                && GUILayout.Button("Create Configurations From Legacy Stats"))
            {
                CreateConfigurationsFromLegacy();
                GUIUtility.ExitGUI();
            }
        }
        else
        {
            bool usesContinuousContact = UsesContinuousContact();
            for (int levelIndex = 0;
                 levelIndex < levelConfigs.arraySize;
                 levelIndex++)
            {
                DrawLevelConfig(
                    levelIndex,
                    levelConfigs.GetArrayElementAtIndex(levelIndex),
                    isBallLightning,
                    usesContinuousContact,
                    continuousDamageInterval.floatValue);
            }
        }

        if (target is ThermalLaserData)
            DrawThermalLaserLevels();
        else if (target is QBeamData)
            DrawQBeamLevels();
        else if (target is BallLightningData)
            DrawBallLightningLevels();

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox(
                "Creating and copying level configurations is available for one "
                + "WeaponData asset at a time.",
                MessageType.Info);
        }
        else if (GUILayout.Button(new GUIContent(
                     "Add Level (Copy Previous)",
                     "Adds a level section by copying the previous one. "
                     + "For legacy data, existing levels are migrated first.")))
        {
            AddLevelConfiguration();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space();
    }

    private static void DrawLevelConfig(
        int levelIndex,
        SerializedProperty levelConfig,
        bool isBallLightning,
        bool usesContinuousContact,
        float continuousDamageInterval)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        levelConfig.isExpanded = EditorGUILayout.Foldout(
            levelConfig.isExpanded,
            $"Level {levelIndex}",
            true);

        if (levelConfig.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Base Stats", EditorStyles.miniBoldLabel);
            DrawLevelField(levelConfig, "reloadTime", "Reload Time");

            if (!isBallLightning)
            {
                DrawLevelField(levelConfig, "damage", "Damage");
                DrawDpsHint(
                    levelConfig,
                    usesContinuousContact,
                    continuousDamageInterval);
                DrawLevelField(levelConfig, "range", "Range");
                DrawLevelField(levelConfig, "speed", "Speed");
                DrawLevelField(levelConfig, "angle", "Angle");

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Fire", EditorStyles.miniBoldLabel);
                DrawLevelField(levelConfig, "volleysPerActivation", "Volleys Per Activation");
                DrawLevelField(levelConfig, "projectilesPerVolley", "Projectiles Per Volley");
                DrawLevelField(levelConfig, "delayBetweenVolleys", "Delay Between Volleys");
                DrawLevelField(levelConfig, "spreadAngle", "Spread Angle");

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Targeting", EditorStyles.miniBoldLabel);
                DrawLevelField(levelConfig, "maxTargets", "Maximum Targets");
                DrawLevelField(levelConfig, "targetSearchRadius", "Target Search Radius (0 = Unlimited)");
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawDpsHint(
        SerializedProperty levelConfig,
        bool usesContinuousContact,
        float continuousDamageInterval)
    {
        SerializedProperty damage = levelConfig.FindPropertyRelative("damage");
        SerializedProperty reloadTime =
            levelConfig.FindPropertyRelative("reloadTime");
        if (damage == null || reloadTime == null)
            return;

        float dpsInterval = usesContinuousContact
            ? continuousDamageInterval
            : reloadTime.floatValue;
        string dpsFormula = usesContinuousContact
            ? "Damage / Continuous Damage Interval"
            : "Damage / Reload Time";
        if (dpsInterval <= 0f)
        {
            EditorGUILayout.HelpBox(
                $"DPS: — ({dpsFormula} must be greater than zero)",
                MessageType.Info);
        }
        else
        {
            float dps = damage.floatValue / dpsInterval;
            EditorGUILayout.HelpBox(
                $"DPS: {dps:0.##} ({dpsFormula})",
                MessageType.Info);
        }

        string shotsPerSecond = reloadTime.floatValue <= 0f
            ? "— (Reload Time must be greater than zero)"
            : (1f / reloadTime.floatValue).ToString("0.##");
        EditorGUILayout.LabelField("Shots Per Second", shotsPerSecond);
    }

    private static void DrawLevelField(
        SerializedProperty levelConfig,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            levelConfig.FindPropertyRelative(propertyName);
        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void CreateConfigurationsFromLegacy()
    {
        serializedObject.ApplyModifiedProperties();

        WeaponData weaponData = target as WeaponData;
        if (weaponData == null)
            return;

        Undo.RecordObject(
            weaponData,
            "Create weapon level configurations from legacy stats");
        weaponData.TryCreateLevelConfigsFromLegacy();
        if (weaponData is ThermalLaserData thermalLaserData)
            thermalLaserData.SynchronizeThermalLevels();
        if (weaponData is QBeamData qBeamData)
            qBeamData.SynchronizeQBeamLevels();
        if (weaponData is BallLightningData ballLightningData)
            ballLightningData.SynchronizeBallLightningLevels();
        EditorUtility.SetDirty(weaponData);
        serializedObject.Update();
    }

    private void AddLevelConfiguration()
    {
        serializedObject.ApplyModifiedProperties();

        WeaponData weaponData = target as WeaponData;
        if (weaponData == null)
            return;

        Undo.RecordObject(weaponData, "Add weapon level configuration");
        weaponData.AddLevelConfigCopyingPrevious();
        if (weaponData is ThermalLaserData thermalLaserData)
            thermalLaserData.SynchronizeThermalLevels();
        if (weaponData is QBeamData qBeamData)
            qBeamData.SynchronizeQBeamLevels();
        if (weaponData is BallLightningData ballLightningData)
            ballLightningData.SynchronizeBallLightningLevels();
        EditorUtility.SetDirty(weaponData);
        serializedObject.Update();
    }

    private void DrawLegacyStats()
    {
        showLegacyStats = EditorGUILayout.Foldout(
            showLegacyStats,
            "Legacy Stats Per Level",
            true);

        if (!showLegacyStats)
        {
            EditorGUILayout.Space();
            return;
        }

        EditorGUILayout.PropertyField(reloadTimeByLevel);
        EditorGUILayout.PropertyField(angleByLevel);
        EditorGUILayout.PropertyField(damageByLevel);
        EditorGUILayout.PropertyField(rangeByLevel);
        EditorGUILayout.PropertyField(speedByLevel);
        DrawLegacyDpsHints();
        EditorGUILayout.Space();
    }

    private void DrawLegacyDpsHints()
    {
        int levelCount = Mathf.Max(
            damageByLevel.arraySize,
            reloadTimeByLevel.arraySize);
        if (levelCount == 0)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("DPS Per Level", EditorStyles.miniBoldLabel);

        for (int levelIndex = 0; levelIndex < levelCount; levelIndex++)
        {
            float damage = GetLegacyStatValue(
                damageByLevel,
                levelIndex,
                1f);
            float reloadTime = GetLegacyStatValue(
                reloadTimeByLevel,
                levelIndex,
                1f);
            float dpsInterval = UsesContinuousContact()
                ? continuousDamageInterval.floatValue
                : reloadTime;
            string dpsText = dpsInterval <= 0f
                ? "—"
                : (damage / dpsInterval).ToString("0.##");
            string shotsPerSecond = reloadTime <= 0f
                ? "—"
                : (1f / reloadTime).ToString("0.##");

            EditorGUILayout.LabelField($"Level {levelIndex} DPS", dpsText);
            EditorGUILayout.LabelField(
                $"Level {levelIndex} Shots Per Second",
                shotsPerSecond);
        }
    }

    private static float GetLegacyStatValue(
        SerializedProperty values,
        int levelIndex,
        float fallback)
    {
        if (values == null || values.arraySize == 0)
            return fallback;

        int valueIndex = Mathf.Min(levelIndex, values.arraySize - 1);
        return values.GetArrayElementAtIndex(valueIndex).floatValue;
    }

    private void DrawLevels()
    {
        EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startLevel);
        EditorGUILayout.PropertyField(maxLevel);
        EditorGUILayout.Space();
    }

    private void DrawBuild()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            energyCost,
            new GUIContent("Energy Cost"));
        EditorGUILayout.PropertyField(
            damageType,
            new GUIContent("Damage Type"));
        EditorGUILayout.Space();
    }

    private void DrawBehaviors()
    {
        EditorGUILayout.LabelField("Behaviours", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(flightMode, new GUIContent("Flight Mode"));
        if (IsSelected(flightMode, ProjectileFlightMode.Homing))
            EditorGUILayout.PropertyField(homingRotationSpeed);

        EditorGUILayout.PropertyField(
            growDuringFlight,
            new GUIContent("Grow During Flight"));

        if (growDuringFlight.hasMultipleDifferentValues
            || growDuringFlight.boolValue)
        {
            EditorGUILayout.PropertyField(
                scaleGrowthPerSecond,
                new GUIContent("Scale Growth Per Second"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            contactMode,
            new GUIContent("Contact Mode"));

        if (IsSelected(
            contactMode,
            ProjectileContactMode.ExplodeAndSpawn))
        {
            EditorGUILayout.PropertyField(explosionPrefab);
            EditorGUILayout.PropertyField(explosionDamage);
        }

        if (IsSelected(
            contactMode,
            ProjectileContactMode.PierceContinuous))
        {
            EditorGUILayout.PropertyField(continuousDamageInterval);
            EditorGUILayout.HelpBox(
                "Set the interval to 0.02 to deal damage on every physics update.",
                MessageType.Info);
        }

        EditorGUILayout.Space();
    }

    private void DrawLifetime()
    {
        EditorGUILayout.LabelField(
            "Projectile Lifetime",
            EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            projectileLifetime,
            new GUIContent("Lifetime"));
        EditorGUILayout.PropertyField(
            disableColliderAfterFirstPhysicsStep,
            new GUIContent("Collider Active For One Physics Step"));

        if (disableColliderAfterFirstPhysicsStep.hasMultipleDifferentValues
            || disableColliderAfterFirstPhysicsStep.boolValue)
        {
            EditorGUILayout.HelpBox(
                "The collider stays enabled for one physics simulation and is disabled before the next one.",
                MessageType.Info);
        }

        EditorGUILayout.PropertyField(
            fadeDuringLifetime,
            new GUIContent("Fade Before Despawn"));

        if (fadeDuringLifetime.hasMultipleDifferentValues
            || fadeDuringLifetime.boolValue)
        {
            EditorGUILayout.PropertyField(
                fadeDuration,
                new GUIContent("Fade Duration"));
        }

        EditorGUILayout.Space();
    }

    private void DrawAudio()
    {
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(audioClipDefault);
        EditorGUILayout.PropertyField(audioClipProjectileShot);
    }

    private void DrawThermalLaserLevels()
    {
        if (thermalLevels == null)
            return;

        EditorGUILayout.LabelField(
            "Thermal Laser Levels",
            EditorStyles.boldLabel);

        int expectedCount = levelConfigs != null && levelConfigs.arraySize > 0
            ? levelConfigs.arraySize
            : Mathf.Max(1, ((ThermalLaserData)target).LevelCount);
        if (thermalLevels.arraySize != expectedCount)
        {
            EditorGUILayout.HelpBox(
                "Thermal level settings must match the weapon level count.",
                MessageType.Warning);

            if (targets.Length == 1
                && GUILayout.Button("Synchronize Thermal Levels"))
            {
                ThermalLaserData data = (ThermalLaserData)target;
                Undo.RecordObject(data, "Synchronize thermal laser levels");
                data.SynchronizeThermalLevels();
                EditorUtility.SetDirty(data);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        for (int levelIndex = 0;
             levelIndex < thermalLevels.arraySize;
             levelIndex++)
        {
            SerializedProperty level = thermalLevels.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex}",
                    true);
                if (level.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        level.FindPropertyRelative("heatPerHitPercent"),
                        new GUIContent("Heat Per Hit (%)"));
                }
            }
        }

        EditorGUILayout.Space();
    }

    private void DrawThermalLaserSettings()
    {
        EditorGUILayout.LabelField(
            "Thermal Laser",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            beamBlockingLayers,
            new GUIContent("Beam Blocking Layers"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "Overheat Explosion",
            EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            thermalExplosionRadius,
            new GUIContent("Radius"));
        EditorGUILayout.PropertyField(
            thermalExplosionDamage,
            new GUIContent("Damage"));
        EditorGUILayout.PropertyField(
            transferredHeatPercent,
            new GUIContent("Transferred Heat (%)"));
        EditorGUILayout.PropertyField(
            coolingDelay,
            new GUIContent("Cooling Delay"));
        EditorGUILayout.PropertyField(
            coolingPercentPerSecond,
            new GUIContent("Cooling Per Second (%)"));
        EditorGUILayout.PropertyField(
            thermalExplosionPrefab,
            new GUIContent("Explosion Visual"));
        EditorGUILayout.Space();
    }

    private void DrawBallLightningLevels()
    {
        if (ballLightningLevels == null)
            return;

        EditorGUILayout.LabelField(
            "Ball Lightning Levels",
            EditorStyles.boldLabel);

        int expectedCount = levelConfigs != null && levelConfigs.arraySize > 0
            ? levelConfigs.arraySize
            : Mathf.Max(1, ((BallLightningData)target).LevelCount);
        if (ballLightningLevels.arraySize != expectedCount)
        {
            EditorGUILayout.HelpBox(
                "Ball lightning settings must match the weapon level count.",
                MessageType.Warning);

            if (targets.Length == 1
                && GUILayout.Button("Synchronize Ball Lightning Levels"))
            {
                BallLightningData data = (BallLightningData)target;
                Undo.RecordObject(data, "Synchronize ball lightning levels");
                data.SynchronizeBallLightningLevels();
                EditorUtility.SetDirty(data);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        for (int levelIndex = 0;
             levelIndex < ballLightningLevels.arraySize;
             levelIndex++)
        {
            SerializedProperty level =
                ballLightningLevels.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex}",
                    true);
                if (!level.isExpanded)
                    continue;

                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("directDamage"),
                    new GUIContent("Direct Damage Per Physics Step"));
                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("areaDamage"),
                    new GUIContent("Area Damage Per Pulse"));
                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("areaTickInterval"),
                    new GUIContent("Final Area Tick Interval"));
            }
        }

        EditorGUILayout.Space();
    }

    private void DrawBallLightningSettings()
    {
        EditorGUILayout.LabelField(
            "Ball Lightning",
            EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            ballLightningProjectileSpeed,
            new GUIContent("Projectile Speed"));
        EditorGUILayout.PropertyField(
            ballLightningBallsPerShot,
            new GUIContent("Balls Per Shot"));
        EditorGUILayout.PropertyField(
            ballLightningSpreadAngle,
            new GUIContent("Ball Spread Angle"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Area Damage", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            ballLightningAreaRadius,
            new GUIContent("Radius"));
        EditorGUILayout.PropertyField(
            ballLightningAreaDamageLayers,
            new GUIContent("Damage Layers"));
        EditorGUILayout.Space();
    }

    private void DrawQBeamLevels()
    {
        if (qBeamLevels == null)
            return;

        EditorGUILayout.LabelField("Q-Beam Levels", EditorStyles.boldLabel);

        int expectedCount = levelConfigs != null && levelConfigs.arraySize > 0
            ? levelConfigs.arraySize
            : Mathf.Max(1, ((QBeamData)target).LevelCount);
        if (qBeamLevels.arraySize != expectedCount)
        {
            EditorGUILayout.HelpBox(
                "Q-Beam level settings must match the weapon level count.",
                MessageType.Warning);

            if (targets.Length == 1
                && GUILayout.Button("Synchronize Q-Beam Levels"))
            {
                QBeamData data = (QBeamData)target;
                Undo.RecordObject(data, "Synchronize Q-Beam levels");
                data.SynchronizeQBeamLevels();
                EditorUtility.SetDirty(data);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        for (int levelIndex = 0;
             levelIndex < qBeamLevels.arraySize;
             levelIndex++)
        {
            SerializedProperty level = qBeamLevels.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex}",
                    true);
                if (level.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        level.FindPropertyRelative("chargePerHit"),
                        new GUIContent("Charge Per Hit"));
                }
            }
        }

        EditorGUILayout.Space();
    }

    private void DrawQBeamSettings()
    {
        EditorGUILayout.LabelField("Q-Beam", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            beamBlockingLayers,
            new GUIContent("Beam Blocking Layers"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Charge Decay", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            qBeamChargeDecayDelay,
            new GUIContent("Decay Delay"));
        EditorGUILayout.PropertyField(
            qBeamChargeDecayPerSecond,
            new GUIContent("Charge Decay Per Second"));
        EditorGUILayout.Space();
    }

    private static bool IsSelected<TEnum>(SerializedProperty property, TEnum value)
        where TEnum : System.Enum
    {
        return property.hasMultipleDifferentValues
            || property.intValue == System.Convert.ToInt32(value);
    }

    private bool UsesContinuousContact()
    {
        return contactMode != null
            && !contactMode.hasMultipleDifferentValues
            && (ProjectileContactMode)contactMode.enumValueIndex
                == ProjectileContactMode.PierceContinuous;
    }

}

[CustomPropertyDrawer(typeof(MovementCommandData))]
public sealed class MovementCommandDataDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty type = property.FindPropertyRelative("type");
        Rect line = NextLine(ref position);
        EditorGUI.PropertyField(line, type, GetCommandLabel(property, type));

        EditorGUI.indentLevel++;
        switch ((MovementCommandType)type.enumValueIndex)
        {
            case MovementCommandType.SpawnAt:
                Draw(ref position, property, "position", "World Position");
                break;

            case MovementCommandType.MoveLocal:
                DrawMoveFields(ref position, property, "Local Offset");
                break;

            case MovementCommandType.MoveWorld:
                DrawMoveFields(ref position, property, "World Position");
                break;

            case MovementCommandType.RotateBy:
                Draw(ref position, property, "degrees", "Degrees");
                Draw(ref position, property, "duration", "Duration");
                Draw(ref position, property, "ease", "Ease");
                break;

            case MovementCommandType.Repeat:
                Draw(ref position, property, "fromAction", "From Action");
                Draw(ref position, property, "toAction", "To Action");
                Draw(ref position, property, "infinite", "Infinite");
                if (!property.FindPropertyRelative("infinite").boolValue)
                    Draw(ref position, property, "repeatCount", "Additional Repeats");
                break;

            case MovementCommandType.Wait:
                Draw(ref position, property, "waitDuration", "Duration");
                break;

            case MovementCommandType.DeactivateChildrenFor:
                Draw(ref position, property, "deactivateDuration", "Duration");
                break;
        }
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        int lineCount = 1;
        MovementCommandType type =
            (MovementCommandType)property.FindPropertyRelative("type").enumValueIndex;

        switch (type)
        {
            case MovementCommandType.SpawnAt:
            case MovementCommandType.Wait:
            case MovementCommandType.DeactivateChildrenFor:
                lineCount += 1;
                break;

            case MovementCommandType.MoveLocal:
            case MovementCommandType.MoveWorld:
            case MovementCommandType.RotateBy:
                lineCount += 3;
                break;

            case MovementCommandType.Repeat:
                lineCount += property.FindPropertyRelative("infinite").boolValue ? 3 : 4;
                break;
        }

        return lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * Gap;
    }

    private static void DrawMoveFields(
        ref Rect position,
        SerializedProperty property,
        string positionLabel)
    {
        Draw(ref position, property, "position", positionLabel);
        Draw(ref position, property, "duration", "Duration");
        Draw(ref position, property, "ease", "Ease");
    }

    private static void Draw(
        ref Rect position,
        SerializedProperty property,
        string propertyName,
        string label)
    {
        Rect line = NextLine(ref position);
        EditorGUI.PropertyField(
            line,
            property.FindPropertyRelative(propertyName),
            new GUIContent(label));
    }

    private static Rect NextLine(ref Rect position)
    {
        Rect line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);

        position.y += EditorGUIUtility.singleLineHeight + Gap;
        return line;
    }

    private static GUIContent GetCommandLabel(
        SerializedProperty property,
        SerializedProperty type)
    {
        int actionNumber = GetArrayIndex(property.propertyPath) + 1;
        string commandName = type.enumDisplayNames[type.enumValueIndex];
        return new GUIContent($"Action {actionNumber}: {commandName}");
    }

    private static int GetArrayIndex(string propertyPath)
    {
        int marker = propertyPath.LastIndexOf("data[");
        if (marker < 0)
            return 0;

        int start = marker + 5;
        int end = propertyPath.IndexOf(']', start);
        if (end < 0)
            return 0;

        return int.TryParse(propertyPath.Substring(start, end - start), out int index)
            ? index
            : 0;
    }
}
