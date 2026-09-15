using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponMetaConfig))]
public sealed class WeaponMetaConfigEditor : Editor
{
    private SerializedProperty maxLevels;
    private SerializedProperty levels;
    private SerializedProperty energyCost;
    private SerializedProperty projectileSlots;

    private void OnEnable()
    {
        maxLevels = serializedObject.FindProperty("maxLevels");
        levels = serializedObject.FindProperty("levels");
        energyCost = serializedObject.FindProperty("energyCost");
        projectileSlots = serializedObject.FindProperty("projectileSlots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Weapon fields are configured here. Projectile sections below edit the "
            + "linked Projectile Data directly, including its contract fields.",
            MessageType.Info);

        DrawBuildAndProjectiles();
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

            DrawDpsInfo(levelIndex);
            EditorGUI.indentLevel--;
        }
    }

    private static void DrawBasicStats(SerializedProperty basicStats)
    {
        EditorGUILayout.LabelField("Basic Stats", EditorStyles.miniBoldLabel);
        DrawField(basicStats, "reloadTime", "Reload Time");
        DrawField(basicStats, "angle", "Angle");
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
            WeaponMetaContract value =
                contract.managedReferenceValue as WeaponMetaContract;
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

                switch (value)
                {
                    case BurstFireWeaponMetaContract:
                        DrawField(
                            contract,
                            "volleysPerActivation",
                            "Volleys Per Activation");
                        DrawField(
                            contract,
                            "projectilesPerVolley",
                            "Projectiles Per Volley");
                        DrawField(
                            contract,
                            "delayBetweenVolleys",
                            "Delay Between Volleys");
                        DrawField(contract, "spreadAngle", "Spread Angle");
                        break;
                }
            }
        }
    }

    private void DrawAddContractButton()
    {
        if (!GUILayout.Button("Add Contract"))
            return;

        GenericMenu menu = new();
        List<Type> contractTypes = new()
        {
            typeof(BurstFireWeaponMetaContract)
        };
        contractTypes.Sort((first, second) => string.Compare(
            GetDisplayName(first),
            GetDisplayName(second),
            StringComparison.Ordinal));

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        for (int index = 0; index < contractTypes.Count; index++)
        {
            Type contractType = contractTypes[index];
            if (contractType.IsAbstract)
                continue;

            string displayName = GetDisplayName(contractType);
            if (config.Levels[0].ContainsContract(contractType))
            {
                menu.AddDisabledItem(new GUIContent(displayName));
                continue;
            }

            menu.AddItem(
                new GUIContent(displayName),
                false,
                () => AddContract(contractType));
        }

        menu.ShowAsContext();
    }

    private void AddContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Add weapon meta contract");
        if (config.TryAddContractToAllLevels(contractType))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private void RemoveContract(Type contractType)
    {
        serializedObject.ApplyModifiedProperties();

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Remove weapon meta contract");
        if (config.TryRemoveContractFromAllLevels(contractType))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private void AddMetaLevel()
    {
        serializedObject.ApplyModifiedProperties();

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Add weapon meta level");
        config.AddLevelCopyingPrevious();
        EditorUtility.SetDirty(config);
        serializedObject.Update();
    }

    private void DrawBuildAndProjectiles()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(energyCost, new GUIContent("Energy Cost"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Projectiles", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Each slot points to a Projectile Data. WeaponData creates separate in-battle damage/range/speed bonuses for every slot.",
            MessageType.None);

        for (int slotIndex = 0;
             slotIndex < projectileSlots.arraySize;
             slotIndex++)
        {
            SerializedProperty slot = projectileSlots.GetArrayElementAtIndex(slotIndex);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(
                    slot.FindPropertyRelative("projectile"),
                    new GUIContent($"Projectile {slotIndex + 1}"));
                if (GUILayout.Button("Remove", GUILayout.Width(65f)))
                {
                    RemoveProjectileSlot(slotIndex);
                    GUIUtility.ExitGUI();
                }
            }

            SerializedProperty spawnedByAnotherProjectile =
                slot.FindPropertyRelative("spawnedByAnotherProjectile");
            EditorGUILayout.PropertyField(
                spawnedByAnotherProjectile,
                new GUIContent("Spawned By Another Projectile"));
            if (spawnedByAnotherProjectile.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "This Projectile Data is available for upgrades, but is launched "
                    + "only by a Spawn Secondary Projectile contract.",
                    MessageType.None);
            }

            DrawLinkedProjectileData(slotIndex, slot);
        }

        if (GUILayout.Button("Add Projectile"))
        {
            AddProjectileSlot();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space();
    }

    private static void DrawLinkedProjectileData(
        int slotIndex,
        SerializedProperty slot)
    {
        ProjectileData projectileData = slot
            ?.FindPropertyRelative("projectile")
            ?.objectReferenceValue as ProjectileData;
        if (projectileData == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Projectile Data asset to edit its fields here.",
                MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string title = $"Projectile {slotIndex + 1}: {projectileData.name}";
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These are the source fields of the linked Projectile Data asset. "
                + "Changes are shared by every weapon that references it.",
                MessageType.None);

            SerializedObject projectileSerialized = new(projectileData);
            projectileSerialized.Update();

            SerializedProperty deliveryType = projectileSerialized.FindProperty(
                "deliveryType");
            SerializedProperty damage = projectileSerialized.FindProperty("damage");
            SerializedProperty damageType = projectileSerialized.FindProperty(
                "damageType");
            SerializedProperty projectilePrefab = projectileSerialized.FindProperty(
                "projectilePrefab");
            SerializedProperty range = projectileSerialized.FindProperty("range");
            SerializedProperty speed = projectileSerialized.FindProperty("speed");
            SerializedProperty contracts = projectileSerialized.FindProperty(
                "contracts");

            EditorGUILayout.LabelField("Delivery", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(deliveryType, new GUIContent("Type"));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                "Direct Damage",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(damage);
            EditorGUILayout.PropertyField(damageType, new GUIContent("Damage Type"));

            bool isPhysicalProjectile = deliveryType.enumValueIndex
                == (int)ProjectileDeliveryType.Projectile;
            if (isPhysicalProjectile)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "Physical Projectile",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    projectilePrefab,
                    new GUIContent("Prefab"));
                EditorGUILayout.PropertyField(range);
                EditorGUILayout.PropertyField(speed);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Beam range is intentionally not configured. Its width, damage "
                    + "interval and effects are defined by the Beam contract.",
                    MessageType.Info);
            }

            DrawProjectileContracts(
                projectileData,
                projectileSerialized,
                contracts,
                isPhysicalProjectile);

            if (projectileSerialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(projectileData);
        }
    }

    private static void DrawProjectileContracts(
        ProjectileData projectileData,
        SerializedObject projectileSerialized,
        SerializedProperty contracts,
        bool isPhysicalProjectile)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Projectile Contracts", EditorStyles.miniBoldLabel);

        for (int index = 0; index < contracts.arraySize; index++)
        {
            SerializedProperty contract = contracts.GetArrayElementAtIndex(index);
            ProjectileDataContract value =
                contract.managedReferenceValue as ProjectileDataContract;
            if (value == null)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(value.DisplayName, EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(65f)))
                    {
                        RemoveProjectileContract(
                            projectileData,
                            projectileSerialized,
                            value.GetType());
                        GUIUtility.ExitGUI();
                    }
                }

                DrawProjectileContractFields(contract, value);
            }
        }

        if (GUILayout.Button("Add Projectile Contract"))
        {
            ShowAddProjectileContractMenu(
                projectileData,
                projectileSerialized,
                isPhysicalProjectile);
        }
    }

    private static void DrawProjectileContractFields(
        SerializedProperty contract,
        ProjectileDataContract value)
    {
        switch (value)
        {
            case ProjectileFlightContract:
                DrawField(contract, "growDuringFlight", "Grow During Flight");
                if (contract.FindPropertyRelative("growDuringFlight").boolValue)
                {
                    DrawField(
                        contract,
                        "scaleGrowthPerSecond",
                        "Scale Growth Per Second");
                }
                break;
            case ProjectileHomingContract:
                DrawField(contract, "rotationSpeed", "Rotation Speed");
                break;
            case ProjectileTargetingContract:
                DrawField(contract, "maxTargets", "Maximum Targets");
                DrawField(contract, "searchRadius", "Search Radius (0 = Unlimited)");
                break;
            case ProjectileContactContract:
                DrawField(contract, "contactMode", "Contact Mode");
                break;
            case ProjectileContinuousDamageContract:
                DrawField(contract, "damageTickInterval", "Damage Tick Interval");
                break;
            case ProjectileCircularChainContract:
                DrawField(contract, "hitsPerTarget", "Hits Per Target");
                DrawField(contract, "damagePerHit", "Damage Per Hit");
                DrawField(contract, "hitInterval", "Hit Interval");
                DrawField(contract, "maximumTargets", "Maximum Targets");
                DrawField(contract, "searchConeAngle", "Search Cone Angle");
                DrawField(contract, "searchRange", "Search Range");
                DrawField(contract, "randomEscapeAngle", "Random Escape Angle");
                break;
            case ProjectileLifetimeContract:
                DrawField(contract, "lifetime", "Lifetime");
                DrawField(
                    contract,
                    "disableColliderAfterFirstPhysicsStep",
                    "Collider Active For One Physics Step");
                DrawField(contract, "fadeBeforeDespawn", "Fade Before Despawn");
                if (contract.FindPropertyRelative("fadeBeforeDespawn").boolValue)
                    DrawField(contract, "fadeDuration", "Fade Duration");
                break;
            case ProjectileExplosionContract:
                DrawField(contract, "explosionPrefab", "Explosion Visual");
                EditorGUILayout.HelpBox(
                    "Configure the Explosion damage and damage type in Damage Sources.",
                    MessageType.None);
                break;
            case ProjectileDamageSourcesContract:
                EditorGUILayout.PropertyField(
                    contract.FindPropertyRelative("sources"),
                    new GUIContent("Sources"),
                    true);
                break;
            case ProjectileBeamContract:
                DrawField(contract, "width", "Width");
                DrawField(contract, "damageTickInterval", "Damage Tick Interval");
                EditorGUILayout.PropertyField(
                    contract.FindPropertyRelative("impactEffects"),
                    new GUIContent("Impact Effects"),
                    true);
                break;
        }
    }

    private static void ShowAddProjectileContractMenu(
        ProjectileData projectileData,
        SerializedObject projectileSerialized,
        bool isPhysicalProjectile)
    {
        GenericMenu menu = new();
        List<Type> contractTypes = new(
            TypeCache.GetTypesDerivedFrom<ProjectileDataContract>());
        contractTypes.Sort((first, second) => string.Compare(
            GetProjectileContractDisplayName(first),
            GetProjectileContractDisplayName(second),
            StringComparison.Ordinal));

        for (int index = 0; index < contractTypes.Count; index++)
        {
            Type contractType = contractTypes[index];
            if (contractType.IsAbstract)
                continue;

            string displayName = GetProjectileContractDisplayName(contractType);
            if (!IsProjectileContractAvailable(
                    contractType,
                    isPhysicalProjectile)
                || projectileData.ContainsContract(contractType))
            {
                menu.AddDisabledItem(new GUIContent(displayName));
                continue;
            }

            menu.AddItem(
                new GUIContent(displayName),
                false,
                () => AddProjectileContract(
                    projectileData,
                    projectileSerialized,
                    contractType));
        }

        menu.ShowAsContext();
    }

    private static bool IsProjectileContractAvailable(
        Type contractType,
        bool isPhysicalProjectile)
    {
        if (contractType == typeof(ProjectileDamageSourcesContract)
            || contractType == typeof(ProjectileTargetingContract))
        {
            return true;
        }

        return isPhysicalProjectile
            ? contractType != typeof(ProjectileBeamContract)
            : contractType == typeof(ProjectileBeamContract);
    }

    private static void AddProjectileContract(
        ProjectileData projectileData,
        SerializedObject projectileSerialized,
        Type contractType)
    {
        projectileSerialized.ApplyModifiedProperties();
        Undo.RecordObject(projectileData, "Add projectile contract from weapon meta");
        if (projectileData.TryAddContract(contractType))
            EditorUtility.SetDirty(projectileData);
        projectileSerialized.Update();
    }

    private static void RemoveProjectileContract(
        ProjectileData projectileData,
        SerializedObject projectileSerialized,
        Type contractType)
    {
        projectileSerialized.ApplyModifiedProperties();
        Undo.RecordObject(
            projectileData,
            "Remove projectile contract from weapon meta");
        if (projectileData.TryRemoveContract(contractType))
            EditorUtility.SetDirty(projectileData);
        projectileSerialized.Update();
    }

    private static string GetProjectileContractDisplayName(Type contractType)
    {
        if (Activator.CreateInstance(contractType) is ProjectileDataContract contract)
            return contract.DisplayName;

        return ObjectNames.NicifyVariableName(contractType.Name);
    }

    private void AddProjectileSlot()
    {
        serializedObject.ApplyModifiedProperties();
        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Add weapon projectile slot");
        config.AddProjectileSlot();
        EditorUtility.SetDirty(config);
        serializedObject.Update();
    }

    private void RemoveProjectileSlot(int slotIndex)
    {
        serializedObject.ApplyModifiedProperties();
        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Remove weapon projectile slot");
        if (config.TryRemoveProjectileSlot(slotIndex))
            EditorUtility.SetDirty(config);
        serializedObject.Update();
    }

    private void DrawMaxLevels()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            maxLevels,
            new GUIContent("Max Levels", "Includes Meta Level 0."));
        if (!EditorGUI.EndChangeCheck())
            return;

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Resize weapon meta levels");
        serializedObject.ApplyModifiedProperties();
        config.SetMaxLevels(maxLevels.intValue);
        EditorUtility.SetDirty(config);
        serializedObject.Update();
        GUIUtility.ExitGUI();
    }

    private void RemoveMetaLevel(int levelIndex)
    {
        serializedObject.ApplyModifiedProperties();

        WeaponMetaConfig config = (WeaponMetaConfig)target;
        Undo.RecordObject(config, "Delete weapon meta level");
        if (config.TryRemoveLevel(levelIndex))
            EditorUtility.SetDirty(config);

        serializedObject.Update();
    }

    private void DrawDpsInfo(int levelIndex)
    {
        WeaponMetaDpsInfo dpsInfo =
            ((WeaponMetaConfig)target).GetDpsInfo(levelIndex);
        string formula = dpsInfo.UsesContinuousDamage
            ? "Damage / Damage Tick Interval"
            : "Damage × Volleys × Projectiles / Reload Time";
        string dps = dpsInfo.Dps <= 0f
            ? "—"
            : dpsInfo.Dps.ToString("0.##");
        string shotsPerSecond = dpsInfo.ShotsPerSecond <= 0f
            ? "—"
            : dpsInfo.ShotsPerSecond.ToString("0.##");

        EditorGUILayout.HelpBox(
            $"DPS: {dps} ({formula})",
            MessageType.Info);
        EditorGUILayout.LabelField("Shots Per Second", shotsPerSecond);
    }

    private static string GetDisplayName(Type contractType)
    {
        if (Activator.CreateInstance(contractType) is WeaponMetaContract contract)
            return contract.DisplayName;

        return ObjectNames.NicifyVariableName(contractType.Name);
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
