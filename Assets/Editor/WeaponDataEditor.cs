using System;
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
    private SerializedProperty baseStatsConfig;
    private SerializedProperty manualBaseStats;
    private SerializedProperty levelBonuses;
    private SerializedProperty weaponMetaConfig;

    private SerializedProperty startLevel;
    private SerializedProperty maxLevel;
    private SerializedProperty energyCost;
    private SerializedProperty battleOffset;
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
    private SerializedProperty manualBaseThermalStats;
    private SerializedProperty thermalLevelBonuses;
    private SerializedProperty beamBlockingLayers;
    private SerializedProperty thermalExplosionRadius;
    private SerializedProperty thermalExplosionDamage;
    private SerializedProperty transferredHeatPercent;
    private SerializedProperty coolingDelay;
    private SerializedProperty coolingPercentPerSecond;
    private SerializedProperty thermalExplosionPrefab;

    private SerializedProperty qBeamLevels;
    private SerializedProperty manualBaseQBeamStats;
    private SerializedProperty qBeamLevelBonuses;
    private SerializedProperty qBeamChargeDecayDelay;
    private SerializedProperty qBeamChargeDecayPerSecond;

    private SerializedProperty ballLightningLevels;
    private SerializedProperty manualBaseBallLightningStats;
    private SerializedProperty ballLightningLevelBonuses;
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
        baseStatsConfig = serializedObject.FindProperty("baseStatsConfig");
        manualBaseStats = serializedObject.FindProperty("manualBaseStats");
        levelBonuses = serializedObject.FindProperty("levelBonuses");
        weaponMetaConfig = serializedObject.FindProperty("weaponMetaConfig");

        startLevel = serializedObject.FindProperty("startLevel");
        maxLevel = serializedObject.FindProperty("maxLevel");
        energyCost = serializedObject.FindProperty("energyCost");
        battleOffset = serializedObject.FindProperty("battleOffset");
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
        manualBaseThermalStats =
            serializedObject.FindProperty("manualBaseThermalStats");
        thermalLevelBonuses =
            serializedObject.FindProperty("thermalLevelBonuses");
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
        manualBaseQBeamStats =
            serializedObject.FindProperty("manualBaseQBeamStats");
        qBeamLevelBonuses =
            serializedObject.FindProperty("qBeamLevelBonuses");
        qBeamChargeDecayDelay =
            serializedObject.FindProperty("chargeDecayDelay");
        qBeamChargeDecayPerSecond =
            serializedObject.FindProperty("chargeDecayPerSecond");

        ballLightningLevels =
            serializedObject.FindProperty("ballLightningLevels");
        manualBaseBallLightningStats =
            serializedObject.FindProperty("manualBaseBallLightningStats");
        ballLightningLevelBonuses =
            serializedObject.FindProperty("ballLightningLevelBonuses");
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

        DrawMetaProgression();
        DrawLevelConfigs();
        if (!((WeaponData)target).UsesPercentageLevelProgression
            && target is not BallLightningData)
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
        else if (!((WeaponData)target).UsesProjectileData)
        {
            DrawBehaviors();
            DrawLifetime();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Damage type, projectile behaviour and lifetime are configured in Projectile Data."
                + " This WeaponData only contains in-battle bonuses and placement.",
                MessageType.Info);
        }

        DrawAudio();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLevelConfigs()
    {
        WeaponData weaponData = (WeaponData)target;
        EditorGUILayout.LabelField(
            "In-Battle Level Bonuses",
            EditorStyles.boldLabel);
        if (weaponMetaConfig.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Weapon Meta Config to use its Meta Level 0 as this "
                + "weapon's base stats. Until then, hidden legacy values are "
                + "used only to keep existing weapons working.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                weaponData.UsesProjectileData
                    ? "Reload time and angle are read from Weapon Meta Config. "
                        + "Each Projectile Data supplies its own damage, range and speed."
                    : "Base stats are read from Meta Level 0 of Weapon Meta Config. "
                        + "Add Projectile slots there to use the new projectile configuration.",
                MessageType.Info);
        }

        if (!weaponData.UsesPercentageLevelProgression)
        {
            EditorGUILayout.HelpBox(
                "This asset still uses absolute level values. Migrate it once "
                + "to preserve its current values as base + percentage bonuses.",
                MessageType.Warning);

            if (targets.Length == 1
                && GUILayout.Button("Migrate Existing Levels To Percent Bonuses"))
            {
                MigrateExistingLevels();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space();
            return;
        }

        for (int levelIndex = 0;
             levelIndex < levelBonuses.arraySize;
             levelIndex++)
        {
            DrawLevelBonus(
                weaponData,
                levelIndex,
                levelBonuses.GetArrayElementAtIndex(levelIndex));
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
                "Adding a level is available for one WeaponData asset at a time.",
                MessageType.Info);
        }
        else if (GUILayout.Button(new GUIContent(
                     "Add Level (Copy Previous Bonuses)",
                     "Copies the previous level's percentage bonuses, so they "
                     + "continue to accumulate from the base values.")))
        {
            AddLevelConfiguration();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space();
    }

    private void DrawLevelBonus(
        WeaponData weaponData,
        int levelIndex,
        SerializedProperty levelBonus)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                levelBonus.isExpanded = EditorGUILayout.Foldout(
                    levelBonus.isExpanded,
                    $"Level {levelIndex + 1}",
                    true);

                using (new EditorGUI.DisabledScope(
                           targets.Length != 1 || levelBonuses.arraySize <= 1))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                    {
                        RemoveLevelConfiguration(levelIndex);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (!levelBonus.isExpanded)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "Bonus added by this level (%)",
                EditorStyles.miniBoldLabel);
            DrawBonusField(levelBonus, "fireRatePercent", "Fire Rate");
            DrawBonusField(levelBonus, "anglePercent", "Angle");

            if (weaponData.UsesProjectileData)
            {
                DrawProjectileBonusFields(weaponData, levelBonus);
            }
            else
            {
                DrawBonusField(levelBonus, "damagePercent", "Damage");
                DrawBonusField(levelBonus, "rangePercent", "Range");
                DrawBonusField(levelBonus, "speedPercent", "Speed");
            }

            bool usesFireBonuses = HasMetaContract<BurstFireWeaponMetaContract>(
                weaponData);
            bool usesTargetingBonuses = HasMetaContract<
                TargetingWeaponMetaContract>(weaponData);
            if (usesFireBonuses)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "Fire Bonuses",
                    EditorStyles.miniBoldLabel);
                DrawBonusField(
                    levelBonus,
                    "volleysPerActivationPercent",
                    "Volleys Per Activation");
                DrawBonusField(
                    levelBonus,
                    "projectilesPerVolleyPercent",
                    "Projectiles Per Volley");
                DrawBonusField(
                    levelBonus,
                    "delayBetweenVolleysRatePercent",
                    "Delay Between Volleys Rate");
                DrawBonusField(
                    levelBonus,
                    "spreadAnglePercent",
                    "Spread Angle");
            }

            if (usesTargetingBonuses)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "Targeting",
                    EditorStyles.miniBoldLabel);
                DrawBonusField(levelBonus, "maxTargetsPercent", "Maximum Targets");
                DrawBonusField(
                    levelBonus,
                    "targetSearchRadiusPercent",
                    "Target Search Radius");
            }

            if (weaponData.UsesProjectileData)
            {
                DrawProjectileCumulativeBonusSummary(
                    weaponData,
                    levelIndex,
                    usesFireBonuses);
            }
            else
            {
                DrawCumulativeBonusSummary(
                    levelIndex,
                    usesFireBonuses,
                    usesTargetingBonuses);
            }
            DrawDpsHint(weaponData, levelIndex);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawDpsHint(WeaponData weaponData, int levelIndex)
    {
        WeaponRuntimeStats stats = weaponData.GetRuntimeStats(levelIndex);
        float damage = stats.Damage;
        bool usesContinuousDamage = UsesContinuousContact();
        float continuousInterval = continuousDamageInterval.floatValue;
        if (weaponData.TryGetPrimaryProjectileRuntimeStats(
                levelIndex,
                out ProjectileData projectileData,
                out ProjectileRuntimeStats projectileStats))
        {
            damage = projectileStats.Damage;
            if (projectileData.TryGetContract(
                    out ProjectileContinuousDamageContract continuousDamage))
            {
                usesContinuousDamage = true;
                continuousInterval = continuousDamage.DamageTickInterval;
            }
            else if (projectileData.TryGetContract(
                         out ProjectileCircularChainContract circularChain))
            {
                damage = circularChain.DamagePerHit;
                usesContinuousDamage = true;
                continuousInterval = circularChain.HitInterval;
            }
            else if (projectileData.TryGetContract(
                         out ProjectileBeamContract beam))
            {
                usesContinuousDamage = true;
                continuousInterval = beam.DamageTickInterval;
            }
        }

        float damagePerActivation = damage
            * stats.VolleysPerActivation
            * stats.ProjectilesPerVolley;
        float dpsInterval = usesContinuousDamage
            ? continuousInterval
            : stats.ReloadTime;
        string formula = usesContinuousDamage
            ? "Damage / Continuous Damage Interval"
            : "Damage per activation / Reload Time";
        string dps = dpsInterval <= 0f
            ? "—"
            : (damagePerActivation / dpsInterval).ToString("0.##");
        string shotsPerSecond = stats.ReloadTime <= 0f
            ? "—"
            : (1f / stats.ReloadTime).ToString("0.##");

        EditorGUILayout.HelpBox(
            $"Effective DPS: {dps} ({formula})",
            MessageType.Info);
        EditorGUILayout.LabelField("Shots Per Second", shotsPerSecond);
    }

    private static void DrawProjectileBonusFields(
        WeaponData weaponData,
        SerializedProperty levelBonus)
    {
        WeaponMetaConfig metaConfig = weaponData.WeaponMetaConfig;
        if (metaConfig == null || !metaConfig.HasProjectileSlots)
        {
            EditorGUILayout.HelpBox(
                "Add at least one Projectile Data slot to Weapon Meta Config.",
                MessageType.Warning);
            return;
        }

        SerializedProperty projectileBonuses =
            levelBonus.FindPropertyRelative("projectileBonuses");
        if (projectileBonuses == null)
            return;

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(
            "Projectile Bonuses",
            EditorStyles.miniBoldLabel);

        for (int slotIndex = 0;
             slotIndex < metaConfig.ProjectileSlots.Count;
             slotIndex++)
        {
            WeaponProjectileSlot slot = metaConfig.ProjectileSlots[slotIndex];
            if (slot == null || string.IsNullOrWhiteSpace(slot.Id))
                continue;

            SerializedProperty projectileBonus = FindOrCreateProjectileBonus(
                projectileBonuses,
                slot.Id);
            if (projectileBonus == null)
                continue;

            string title = slot.Projectile != null
                ? slot.Projectile.name
                : $"Projectile {slotIndex + 1} (missing)";
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            DrawBonusField(projectileBonus, "damagePercent", "Damage");

            if (slot.Projectile == null
                || slot.Projectile.DeliveryType == ProjectileDeliveryType.Projectile)
            {
                DrawBonusField(projectileBonus, "rangePercent", "Range");
                DrawBonusField(projectileBonus, "speedPercent", "Speed");
            }
        }
    }

    private static SerializedProperty FindOrCreateProjectileBonus(
        SerializedProperty projectileBonuses,
        string projectileSlotId)
    {
        for (int index = 0; index < projectileBonuses.arraySize; index++)
        {
            SerializedProperty candidate =
                projectileBonuses.GetArrayElementAtIndex(index);
            SerializedProperty id = candidate.FindPropertyRelative(
                "projectileSlotId");
            if (id != null && id.stringValue == projectileSlotId)
                return candidate;
        }

        int newIndex = projectileBonuses.arraySize;
        projectileBonuses.arraySize++;
        SerializedProperty created = projectileBonuses.GetArrayElementAtIndex(
            newIndex);
        SerializedProperty createdId = created.FindPropertyRelative(
            "projectileSlotId");
        if (createdId == null)
            return null;

        createdId.stringValue = projectileSlotId;
        return created;
    }

    private static void DrawBonusField(
        SerializedProperty levelBonus,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            levelBonus.FindPropertyRelative(propertyName);
        EditorGUILayout.PropertyField(
            property,
            new GUIContent($"{label} + (%)"));
    }

    private void DrawCumulativeBonusSummary(
        int levelIndex,
        bool usesFireBonuses,
        bool usesTargetingBonuses)
    {
        float fireRate = GetCumulativeBonus("fireRatePercent", levelIndex);
        float damage = GetCumulativeBonus("damagePercent", levelIndex);
        float range = GetCumulativeBonus("rangePercent", levelIndex);
        float speed = GetCumulativeBonus("speedPercent", levelIndex);
        float angle = GetCumulativeBonus("anglePercent", levelIndex);
        float volleys = GetCumulativeBonus(
            "volleysPerActivationPercent",
            levelIndex);
        float projectiles = GetCumulativeBonus(
            "projectilesPerVolleyPercent",
            levelIndex);
        float delayRate = GetCumulativeBonus(
            "delayBetweenVolleysRatePercent",
            levelIndex);
        float spread = GetCumulativeBonus("spreadAnglePercent", levelIndex);
        float targets = GetCumulativeBonus("maxTargetsPercent", levelIndex);
        float searchRadius = GetCumulativeBonus(
            "targetSearchRadiusPercent",
            levelIndex);
        string summary =
            $"Cumulative from base: Fire Rate {fireRate:+0.##;-0.##;0}% | "
            + $"Damage {damage:+0.##;-0.##;0}% | "
            + $"Range {range:+0.##;-0.##;0}% | "
            + $"Speed {speed:+0.##;-0.##;0}% | "
            + $"Angle {angle:+0.##;-0.##;0}%";

        if (usesFireBonuses)
        {
            summary += $"\nVolleys {volleys:+0.##;-0.##;0}% | "
                + $"Projectiles {projectiles:+0.##;-0.##;0}% | "
                + $"Volley Rate {delayRate:+0.##;-0.##;0}% | "
                + $"Spread {spread:+0.##;-0.##;0}%";
        }

        if (usesTargetingBonuses)
        {
            summary += $"\nTargets {targets:+0.##;-0.##;0}% | "
                + $"Search Radius {searchRadius:+0.##;-0.##;0}%";
        }

        EditorGUILayout.HelpBox(summary, MessageType.None);
    }

    private void DrawProjectileCumulativeBonusSummary(
        WeaponData weaponData,
        int levelIndex,
        bool usesFireBonuses)
    {
        float fireRate = GetCumulativeBonus("fireRatePercent", levelIndex);
        float angle = GetCumulativeBonus("anglePercent", levelIndex);
        string summary = $"Cumulative from base: Fire Rate "
            + $"{fireRate:+0.##;-0.##;0}% | "
            + $"Angle {angle:+0.##;-0.##;0}%";

        WeaponMetaConfig metaConfig = weaponData.WeaponMetaConfig;
        if (metaConfig != null)
        {
            for (int index = 0; index < metaConfig.ProjectileSlots.Count; index++)
            {
                WeaponProjectileSlot slot = metaConfig.ProjectileSlots[index];
                if (slot == null || string.IsNullOrWhiteSpace(slot.Id))
                    continue;

                string name = slot.Projectile != null
                    ? slot.Projectile.name
                    : $"Projectile {index + 1}";
                float damage = GetCumulativeProjectileBonus(
                    slot.Id,
                    "damagePercent",
                    levelIndex);
                float range = GetCumulativeProjectileBonus(
                    slot.Id,
                    "rangePercent",
                    levelIndex);
                float speed = GetCumulativeProjectileBonus(
                    slot.Id,
                    "speedPercent",
                    levelIndex);
                summary += $"\n{name}: Damage {damage:+0.##;-0.##;0}%";
                if (slot.Projectile == null
                    || slot.Projectile.DeliveryType
                        == ProjectileDeliveryType.Projectile)
                {
                    summary += $" | Range {range:+0.##;-0.##;0}%"
                        + $" | Speed {speed:+0.##;-0.##;0}%";
                }
            }
        }

        if (usesFireBonuses)
        {
            float volleys = GetCumulativeBonus(
                "volleysPerActivationPercent",
                levelIndex);
            float projectiles = GetCumulativeBonus(
                "projectilesPerVolleyPercent",
                levelIndex);
            float delayRate = GetCumulativeBonus(
                "delayBetweenVolleysRatePercent",
                levelIndex);
            float spread = GetCumulativeBonus(
                "spreadAnglePercent",
                levelIndex);
            summary += $"\nVolleys {volleys:+0.##;-0.##;0}% | "
                + $"Projectiles {projectiles:+0.##;-0.##;0}% | "
                + $"Volley Rate {delayRate:+0.##;-0.##;0}% | "
                + $"Spread {spread:+0.##;-0.##;0}%";
        }

        EditorGUILayout.HelpBox(summary, MessageType.None);
    }

    private float GetCumulativeProjectileBonus(
        string projectileSlotId,
        string propertyName,
        int levelIndex)
    {
        float total = 0f;
        int lastIndex = Mathf.Min(levelIndex, levelBonuses.arraySize - 1);
        for (int index = 0; index <= lastIndex; index++)
        {
            SerializedProperty projectileBonuses = levelBonuses
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("projectileBonuses");
            if (projectileBonuses == null)
                continue;

            for (int projectileIndex = 0;
                 projectileIndex < projectileBonuses.arraySize;
                 projectileIndex++)
            {
                SerializedProperty projectileBonus = projectileBonuses
                    .GetArrayElementAtIndex(projectileIndex);
                SerializedProperty id = projectileBonus.FindPropertyRelative(
                    "projectileSlotId");
                if (id == null || id.stringValue != projectileSlotId)
                    continue;

                total += projectileBonus.FindPropertyRelative(propertyName)
                    ?.floatValue ?? 0f;
                break;
            }
        }

        return total;
    }

    private static bool HasMetaContract<TContract>(WeaponData weaponData)
        where TContract : WeaponMetaContract
    {
        return weaponData != null
            && weaponData.WeaponMetaConfig != null
            && weaponData.WeaponMetaConfig.HasContract<TContract>();
    }

    private float GetCumulativeBonus(string propertyName, int levelIndex)
    {
        float total = 0f;
        int lastIndex = Mathf.Min(levelIndex, levelBonuses.arraySize - 1);
        for (int index = 0; index <= lastIndex; index++)
        {
            SerializedProperty property = levelBonuses
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative(propertyName);
            total += property?.floatValue ?? 0f;
        }

        return total;
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

    private void MigrateExistingLevels()
    {
        serializedObject.ApplyModifiedProperties();

        WeaponData weaponData = target as WeaponData;
        if (weaponData == null)
            return;

        Undo.RecordObject(weaponData, "Migrate weapon levels to percent bonuses");
        if (!weaponData.TryMigrateLegacyLevelsToPercentage())
            return;

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

    private void RemoveLevelConfiguration(int levelIndex)
    {
        serializedObject.ApplyModifiedProperties();

        WeaponData weaponData = target as WeaponData;
        if (weaponData == null)
            return;

        Undo.RecordObject(weaponData, "Delete weapon level configuration");
        if (!weaponData.TryRemoveLevelConfig(levelIndex))
            return;

        SynchronizeSpecialLevels(weaponData);
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
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(maxLevel);
        if (EditorGUI.EndChangeCheck()
            && targets.Length == 1
            && target is WeaponData weaponData
            && weaponData.UsesPercentageLevelProgression)
        {
            Undo.RecordObject(weaponData, "Resize weapon levels");
            serializedObject.ApplyModifiedProperties();
            weaponData.SetMaxLevel(maxLevel.intValue);
            SynchronizeSpecialLevels(weaponData);
            EditorUtility.SetDirty(weaponData);
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space();
    }

    private static void SynchronizeSpecialLevels(WeaponData weaponData)
    {
        if (weaponData is ThermalLaserData thermalLaserData)
            thermalLaserData.SynchronizeThermalLevels();
        else if (weaponData is QBeamData qBeamData)
            qBeamData.SynchronizeQBeamLevels();
        else if (weaponData is BallLightningData ballLightningData)
            ballLightningData.SynchronizeBallLightningLevels();
    }

    private void DrawMetaProgression()
    {
        EditorGUILayout.LabelField("Meta Progression", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            weaponMetaConfig,
            new GUIContent(
                "Weapon Meta Config",
                "Persistent source for weapon build cost and projectile slots. "
                + "Meta Level 0 supplies reload time and angle."));
        EditorGUILayout.Space();
    }

    private void DrawBuild()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
        bool usesProjectileData = ((WeaponData)target).UsesProjectileData;
        if (usesProjectileData)
        {
            EditorGUILayout.LabelField(
                "Energy Cost",
                ((WeaponData)target).EnergyCost.ToString());
            EditorGUILayout.HelpBox(
                "Energy Cost is configured in Weapon Meta Config.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.PropertyField(
                energyCost,
                new GUIContent("Energy Cost"));
        }
        EditorGUILayout.PropertyField(
            battleOffset,
            new GUIContent(
                "Battle Offset",
                "Local offset from the shared ship weapon mount during battle."));
        if (!usesProjectileData)
        {
            EditorGUILayout.PropertyField(
                damageType,
                new GUIContent("Damage Type"));
        }
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
        if (thermalLevelBonuses == null)
            return;

        EditorGUILayout.LabelField(
            "Thermal Laser Base and Bonuses",
            EditorStyles.boldLabel);

        if (baseStatsConfig.objectReferenceValue is not ThermalLaserData)
            EditorGUILayout.PropertyField(
                manualBaseThermalStats,
                new GUIContent("Manual Thermal Base Stats"),
                true);

        if (thermalLevelBonuses.arraySize != levelBonuses.arraySize)
            DrawSynchronizeButton("Synchronize Thermal Bonuses", () =>
                ((ThermalLaserData)target).SynchronizeThermalLevels());

        for (int levelIndex = 0;
             levelIndex < thermalLevelBonuses.arraySize;
             levelIndex++)
        {
            SerializedProperty level =
                thermalLevelBonuses.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex + 1} Thermal Bonus",
                    true);
                if (level.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        level.FindPropertyRelative("heatPerHitPercentBonus"),
                        new GUIContent("Heat Per Hit + (%)"));
                    DrawSpecialCumulativeSummary(
                        thermalLevelBonuses,
                        "heatPerHitPercentBonus",
                        levelIndex,
                        "Heat Per Hit");
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
        if (ballLightningLevelBonuses == null)
            return;

        EditorGUILayout.LabelField(
            "Ball Lightning Base and Bonuses",
            EditorStyles.boldLabel);

        if (baseStatsConfig.objectReferenceValue is not BallLightningData)
            EditorGUILayout.PropertyField(
                manualBaseBallLightningStats,
                new GUIContent("Manual Ball Lightning Base Stats"),
                true);

        if (ballLightningLevelBonuses.arraySize != levelBonuses.arraySize)
            DrawSynchronizeButton("Synchronize Ball Lightning Bonuses", () =>
                ((BallLightningData)target).SynchronizeBallLightningLevels());

        for (int levelIndex = 0;
             levelIndex < ballLightningLevelBonuses.arraySize;
             levelIndex++)
        {
            SerializedProperty level =
                ballLightningLevelBonuses.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex + 1} Ball Lightning Bonus",
                    true);
                if (!level.isExpanded)
                    continue;

                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("directDamagePercent"),
                    new GUIContent("Direct Damage + (%)"));
                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("areaDamagePercent"),
                    new GUIContent("Area Damage + (%)"));
                EditorGUILayout.PropertyField(
                    level.FindPropertyRelative("areaTickRatePercent"),
                    new GUIContent("Area Tick Rate + (%)"));
                DrawSpecialCumulativeSummary(
                    ballLightningLevelBonuses,
                    "directDamagePercent",
                    levelIndex,
                    "Direct Damage");
                DrawSpecialCumulativeSummary(
                    ballLightningLevelBonuses,
                    "areaDamagePercent",
                    levelIndex,
                    "Area Damage");
                DrawSpecialCumulativeSummary(
                    ballLightningLevelBonuses,
                    "areaTickRatePercent",
                    levelIndex,
                    "Area Tick Rate");
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
        if (qBeamLevelBonuses == null)
            return;

        EditorGUILayout.LabelField(
            "Q-Beam Base and Bonuses",
            EditorStyles.boldLabel);

        if (baseStatsConfig.objectReferenceValue is not QBeamData)
            EditorGUILayout.PropertyField(
                manualBaseQBeamStats,
                new GUIContent("Manual Q-Beam Base Stats"),
                true);

        if (qBeamLevelBonuses.arraySize != levelBonuses.arraySize)
            DrawSynchronizeButton("Synchronize Q-Beam Bonuses", () =>
                ((QBeamData)target).SynchronizeQBeamLevels());

        for (int levelIndex = 0;
             levelIndex < qBeamLevelBonuses.arraySize;
             levelIndex++)
        {
            SerializedProperty level =
                qBeamLevelBonuses.GetArrayElementAtIndex(levelIndex);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                level.isExpanded = EditorGUILayout.Foldout(
                    level.isExpanded,
                    $"Level {levelIndex + 1} Q-Beam Bonus",
                    true);
                if (level.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        level.FindPropertyRelative("chargePerHitPercentBonus"),
                        new GUIContent("Charge Per Hit + (%)"));
                    DrawSpecialCumulativeSummary(
                        qBeamLevelBonuses,
                        "chargePerHitPercentBonus",
                        levelIndex,
                        "Charge Per Hit");
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

    private void DrawSynchronizeButton(
        string buttonText,
        System.Action synchronize)
    {
        EditorGUILayout.HelpBox(
            "Special weapon bonuses must match the common level count.",
            MessageType.Warning);

        if (targets.Length != 1 || !GUILayout.Button(buttonText))
            return;

        Undo.RecordObject(target, buttonText);
        synchronize();
        EditorUtility.SetDirty(target);
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }

    private static void DrawSpecialCumulativeSummary(
        SerializedProperty bonuses,
        string propertyName,
        int levelIndex,
        string label)
    {
        float total = 0f;
        int lastIndex = Mathf.Min(levelIndex, bonuses.arraySize - 1);
        for (int index = 0; index <= lastIndex; index++)
        {
            SerializedProperty property = bonuses
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative(propertyName);
            total += property?.floatValue ?? 0f;
        }

        EditorGUILayout.LabelField(
            $"Cumulative {label}",
            $"{total:+0.##;-0.##;0}%");
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
