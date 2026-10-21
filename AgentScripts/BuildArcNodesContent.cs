using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildArcNodesContent
{
    private const string ProjectilePath =
        "Assets/Prefub/Data/ProjectileData/ArcNodesProjectile.asset";
    private const string MetaPath =
        "Assets/Prefub/Data/WeaponData/Meta/ArcNodesMeta.asset";
    private const string WeaponDataPath =
        "Assets/Prefub/Data/WeaponData/GamePlayProgression/ArcNodes.asset";
    private const string ProjectilePrefabPath =
        "Assets/Prefub/Projectiles/ArcNodesProjectile.prefab";
    private const string WeaponPrefabPath =
        "Assets/Prefub/Weapons/ArcNodes.prefab";
    private const string ContentPath =
        "Assets/GameContent/Weapons/Data/ArcNodesContent.asset";
    private const string CatalogPath =
        "Assets/GameContent/Catalogs/WeaponCatalog.asset";

    private const string ProjectilePrefabSourcePath =
        "Assets/Prefub/Projectiles/CircularProjectile.prefab";
    private const string WeaponPrefabSourcePath =
        "Assets/Prefub/Weapons/Circular.prefab";
    private const string IconSourcePath =
        "Assets/GameContent/Weapons/Data/BallLightningContent.asset";

    public static object Execute()
    {
        EnsureFolders();

        Projectile projectilePrefab = CreateOrLoadProjectilePrefab();
        ProjectileData projectileData = CreateOrLoadProjectileData(projectilePrefab);
        WeaponMetaConfig metaConfig = CreateOrLoadMetaConfig(projectileData);
        WeaponData weaponData = CreateOrLoadWeaponData(metaConfig);
        GameObject weaponPrefab = CreateOrLoadWeaponPrefab(
            projectilePrefab,
            weaponData);
        WeaponContentDefinition content = CreateOrLoadContent(weaponPrefab, weaponData);
        RegisterInCatalog(content);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new
        {
            projectile = AssetDatabase.GetAssetPath(projectileData),
            meta = AssetDatabase.GetAssetPath(metaConfig),
            battleProgression = AssetDatabase.GetAssetPath(weaponData),
            projectilePrefab = AssetDatabase.GetAssetPath(projectilePrefab),
            weaponPrefab = AssetDatabase.GetAssetPath(weaponPrefab),
            content = AssetDatabase.GetAssetPath(content),
            registered = IsRegistered(content),
            arcDamage = 18f,
            pulseInterval = 0.5f,
            nodeLifetime = 3f,
            battleLevels = weaponData.LevelCount
        };
    }

    private static Projectile CreateOrLoadProjectilePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ProjectilePrefabPath);
        if (prefab == null)
        {
            CopyAsset(ProjectilePrefabSourcePath, ProjectilePrefabPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        }

        if (prefab == null)
            throw new InvalidOperationException("Arc Nodes projectile prefab was not created.");

        GameObject contents = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
        try
        {
            contents.name = "Arc Nodes Projectile";
            PrefabUtility.SaveAsPrefabAsset(contents, ProjectilePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        Projectile projectile = prefab != null
            ? prefab.GetComponent<Projectile>()
            : null;
        if (projectile == null)
            throw new InvalidOperationException(
                "Arc Nodes projectile prefab requires a Projectile component.");

        return projectile;
    }

    private static ProjectileData CreateOrLoadProjectileData(Projectile projectilePrefab)
    {
        ProjectileData projectileData = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            ProjectilePath);
        if (projectileData == null)
        {
            projectileData = ScriptableObject.CreateInstance<ProjectileData>();
            AssetDatabase.CreateAsset(projectileData, ProjectilePath);
        }

        projectileData.TryRemoveContract(typeof(ProjectileArcNodesContract));
        projectileData.TryRemoveContract(typeof(ProjectileLifetimeContract));
        projectileData.TryAddContract(typeof(ProjectileArcNodesContract));
        projectileData.TryAddContract(typeof(ProjectileLifetimeContract));

        SerializedObject serialized = new(projectileData);
        serialized.FindProperty("deliveryType").enumValueIndex =
            (int)ProjectileDeliveryType.Projectile;
        serialized.FindProperty("simulationBackend").enumValueIndex =
            (int)ProjectileSimulationBackend.Entities;
        serialized.FindProperty("damage").floatValue = 18f;
        serialized.FindProperty("damageType").enumValueIndex =
            (int)EnemyDamageType.Plazma;
        serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
        serialized.FindProperty("range").floatValue = 8f;
        serialized.FindProperty("speed").floatValue = 4f;

        SerializedProperty contracts = serialized.FindProperty("contracts");
        ConfigureArcNodesContract(contracts);
        ConfigureLifetimeContract(contracts);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(projectileData);
        return projectileData;
    }

    private static void ConfigureArcNodesContract(SerializedProperty contracts)
    {
        SerializedProperty contract = FindManagedReference(
            contracts,
            nameof(ProjectileArcNodesContract));
        if (contract == null)
            throw new InvalidOperationException("Arc Nodes contract was not added.");

        contract.FindPropertyRelative("damagePerArc").floatValue = 18f;
        contract.FindPropertyRelative("connectionRange").floatValue = 4f;
          contract.FindPropertyRelative("pulseInterval").floatValue = 0.2f;
        contract.FindPropertyRelative("maximumConnections").intValue = 2;
        contract.FindPropertyRelative("arcHitRadius").floatValue = 0.1f;
          contract.FindPropertyRelative("visualDuration").floatValue = 0.3f;
          contract.FindPropertyRelative("visualWidth").floatValue = 0.12f;
          contract.FindPropertyRelative("visualSegments").intValue = 7;
          contract.FindPropertyRelative("visualJitter").floatValue = 0.12f;
        contract.FindPropertyRelative("visualColor").colorValue =
            new Color(0.2f, 0.85f, 1f, 1f);
    }

    private static void ConfigureLifetimeContract(SerializedProperty contracts)
    {
        SerializedProperty contract = FindManagedReference(
            contracts,
            nameof(ProjectileLifetimeContract));
        if (contract == null)
            throw new InvalidOperationException("Arc Nodes lifetime contract was not added.");

        contract.FindPropertyRelative("lifetime").floatValue = 3f;
        contract.FindPropertyRelative("disableColliderAfterFirstPhysicsStep").boolValue = false;
        contract.FindPropertyRelative("fadeBeforeDespawn").boolValue = false;
        contract.FindPropertyRelative("fadeDuration").floatValue = 0.35f;
    }

    private static WeaponMetaConfig CreateOrLoadMetaConfig(ProjectileData projectileData)
    {
        WeaponMetaConfig meta = AssetDatabase.LoadAssetAtPath<WeaponMetaConfig>(MetaPath);
        if (meta == null)
        {
            meta = ScriptableObject.CreateInstance<WeaponMetaConfig>();
            AssetDatabase.CreateAsset(meta, MetaPath);
        }

        SerializedObject serialized = new(meta);
        SerializedProperty slots = serialized.FindProperty("projectileSlots");
        slots.arraySize = 1;
        SerializedProperty slot = slots.GetArrayElementAtIndex(0);
        slot.FindPropertyRelative("id").stringValue = "arc-nodes-primary";
        slot.FindPropertyRelative("projectile").objectReferenceValue = projectileData;
        slot.FindPropertyRelative("spawnedByAnotherProjectile").boolValue = false;
        serialized.FindProperty("energyCost").intValue = 3;
        serialized.FindProperty("maxLevels").intValue = 1;

        SerializedProperty levels = serialized.FindProperty("levels");
        levels.arraySize = 1;
        SerializedProperty basicStats = levels.GetArrayElementAtIndex(0)
            .FindPropertyRelative("basicStats");
        basicStats.FindPropertyRelative("reloadTime").floatValue = 0.75f;
        basicStats.FindPropertyRelative("damage").floatValue = 18f;
        basicStats.FindPropertyRelative("range").floatValue = 8f;
        basicStats.FindPropertyRelative("projectileSpeed").floatValue = 4f;
        basicStats.FindPropertyRelative("angle").floatValue = 0f;
          basicStats.FindPropertyRelative("initialDirectionAngle").floatValue = 0f;
          SerializedProperty contracts = levels.GetArrayElementAtIndex(0)
              .FindPropertyRelative("contracts");
          contracts.arraySize = 1;
          SerializedProperty fanFire = contracts.GetArrayElementAtIndex(0);
          fanFire.managedReferenceValue = new FanFireWeaponMetaContract();
          fanFire.FindPropertyRelative("fanFireDisabled").boolValue = false;
          fanFire.FindPropertyRelative("usePartialFan").boolValue = true;
          fanFire.FindPropertyRelative("cyclePartialFanGroups").boolValue = true;
          SerializedProperty groups = fanFire.FindPropertyRelative("partialFanGroups");
          int[] projectileCounts = { 1, 2, 2, 2, 2, 2 };
          float[] angleOffsets = { 0f, 15f, 30f, 45f, 30f, 15f };
          groups.arraySize = projectileCounts.Length;
          for (int groupIndex = 0; groupIndex < groups.arraySize; groupIndex++)
          {
              SerializedProperty group = groups.GetArrayElementAtIndex(groupIndex);
              group.FindPropertyRelative("projectileCount").intValue =
                  projectileCounts[groupIndex];
              group.FindPropertyRelative("angleOffset").floatValue =
                  angleOffsets[groupIndex];
          }
          serialized.ApplyModifiedPropertiesWithoutUndo();

        meta.SynchronizeProjectileSlots();
        EditorUtility.SetDirty(meta);
        return meta;
    }

    private static WeaponData CreateOrLoadWeaponData(WeaponMetaConfig meta)
    {
        WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(
            WeaponDataPath);
        if (weaponData == null)
        {
            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(weaponData, WeaponDataPath);
        }

        SerializedObject serialized = new(weaponData);
        serialized.FindProperty("weaponMetaConfig").objectReferenceValue = meta;
        serialized.FindProperty("baseStatsConfig").objectReferenceValue = null;
        serialized.FindProperty("startLevel").intValue = 1;
        serialized.FindProperty("maxLevel").intValue = 11;
        serialized.FindProperty("energyCost").intValue = 3;
        serialized.FindProperty("battleOffset").vector3Value = Vector3.zero;
        serialized.FindProperty("damageType").enumValueIndex =
            (int)EnemyDamageType.Plazma;
        serialized.FindProperty("flightMode").enumValueIndex =
            (int)ProjectileFlightMode.Straight;
        serialized.FindProperty("contactMode").enumValueIndex =
            (int)ProjectileContactMode.Ignore;
        serialized.FindProperty("projectileLifetime").floatValue = 3f;
        serialized.FindProperty("fadeDuringLifetime").boolValue = true;
        serialized.FindProperty("fadeDuration").floatValue = 0.35f;
        serialized.FindProperty("levelBonuses").arraySize = 11;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        weaponData.SynchronizeProjectileLevelBonuses();

        serialized.Update();
        SerializedProperty bonuses = serialized.FindProperty("levelBonuses");
        for (int levelIndex = 0; levelIndex < bonuses.arraySize; levelIndex++)
        {
            SerializedProperty level = bonuses.GetArrayElementAtIndex(levelIndex);
            level.FindPropertyRelative("fireRatePercent").floatValue = 0f;
            level.FindPropertyRelative("anglePercent").floatValue = 0f;
            level.FindPropertyRelative("damagePercent").floatValue = 0f;
            level.FindPropertyRelative("rangePercent").floatValue = 0f;
            level.FindPropertyRelative("speedPercent").floatValue = 0f;

            SerializedProperty projectileBonuses =
                level.FindPropertyRelative("projectileBonuses");
            for (int index = 0; index < projectileBonuses.arraySize; index++)
            {
                SerializedProperty projectileBonus =
                    projectileBonuses.GetArrayElementAtIndex(index);
                projectileBonus.FindPropertyRelative("damagePercent").floatValue =
                    levelIndex == 0 ? 0f : 10f;
                projectileBonus.FindPropertyRelative("rangePercent").floatValue = 0f;
                projectileBonus.FindPropertyRelative("speedPercent").floatValue = 0f;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weaponData);
        return weaponData;
    }

    private static GameObject CreateOrLoadWeaponPrefab(
        Projectile projectilePrefab,
        WeaponData weaponData)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath) == null)
            CopyAsset(WeaponPrefabSourcePath, WeaponPrefabPath);

        GameObject contents = PrefabUtility.LoadPrefabContents(WeaponPrefabPath);
        try
        {
            contents.name = "Arc Nodes";
            Weapon weapon = contents.GetComponent<Weapon>();
            if (weapon == null)
                throw new InvalidOperationException("Arc Nodes weapon prefab requires Weapon.");

            SerializedObject serialized = new(weapon);
            serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serialized.FindProperty("weaponData").objectReferenceValue = weaponData;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, WeaponPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
    }

    private static WeaponContentDefinition CreateOrLoadContent(
        GameObject weaponPrefab,
        WeaponData weaponData)
    {
        WeaponContentDefinition content =
            AssetDatabase.LoadAssetAtPath<WeaponContentDefinition>(ContentPath);
        if (content == null)
        {
            content = ScriptableObject.CreateInstance<WeaponContentDefinition>();
            AssetDatabase.CreateAsset(content, ContentPath);
        }

        Sprite icon = GetSourceIcon();
        SerializedObject serialized = new(content);
        serialized.FindProperty("id").stringValue = "arc-nodes";
        serialized.FindProperty("displayName").stringValue = "Arc Nodes";
        serialized.FindProperty("passiveAbilityDescription").stringValue =
            "Deploys harmless electric nodes. Nearby nodes periodically connect "
            + "with arcs that damage enemies crossing the links.";
        serialized.FindProperty("activeAbilityDescription").stringValue = string.Empty;
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.FindProperty("prefab").objectReferenceValue = weaponPrefab;
        serialized.FindProperty("rarity").enumValueIndex =
            (int)ContentRarity.Legendary;
        serialized.FindProperty("purchasePrice").intValue = 0;
        serialized.FindProperty("purchaseCoresPrice").intValue = 0;
        serialized.FindProperty("isStarterUnlocked").boolValue = true;
        serialized.FindProperty("maxUpgradeLevel").intValue = 0;
        serialized.FindProperty("upgradePrices").arraySize = 0;
        serialized.FindProperty("data").objectReferenceValue = weaponData;
        serialized.FindProperty("requiredPlatformTier").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(content);
        return content;
    }

    private static void RegisterInCatalog(WeaponContentDefinition content)
    {
        WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
        if (catalog == null)
            throw new InvalidOperationException("WeaponCatalog asset is missing.");

        SerializedObject serialized = new(catalog);
        SerializedProperty weapons = serialized.FindProperty("weapons");
        for (int index = 0; index < weapons.arraySize; index++)
        {
            if (weapons.GetArrayElementAtIndex(index).objectReferenceValue == content)
                return;
        }

        weapons.InsertArrayElementAtIndex(weapons.arraySize);
        weapons.GetArrayElementAtIndex(weapons.arraySize - 1).objectReferenceValue = content;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static bool IsRegistered(WeaponContentDefinition content)
    {
        WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
        if (catalog == null)
            return false;

        foreach (WeaponContentDefinition entry in catalog.Weapons)
        {
            if (entry == content)
                return true;
        }

        return false;
    }

    private static Sprite GetSourceIcon()
    {
        WeaponContentDefinition source =
            AssetDatabase.LoadAssetAtPath<WeaponContentDefinition>(IconSourcePath);
        return source != null ? source.Icon : null;
    }

    private static SerializedProperty FindManagedReference(
        SerializedProperty collection,
        string typeName)
    {
        if (collection == null)
            return null;

        for (int index = 0; index < collection.arraySize; index++)
        {
            SerializedProperty element = collection.GetArrayElementAtIndex(index);
            if (element.managedReferenceFullTypename.EndsWith(typeName, StringComparison.Ordinal))
                return element;
        }

        return null;
    }

    private static void CopyAsset(string sourcePath, string destinationPath)
    {
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            throw new InvalidOperationException(
                "Unable to copy asset from '" + sourcePath + "' to '"
                + destinationPath + "'.");
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder(Path.GetDirectoryName(ProjectilePath));
        EnsureFolder(Path.GetDirectoryName(MetaPath));
        EnsureFolder(Path.GetDirectoryName(WeaponDataPath));
        EnsureFolder(Path.GetDirectoryName(ProjectilePrefabPath));
        EnsureFolder(Path.GetDirectoryName(WeaponPrefabPath));
        EnsureFolder(Path.GetDirectoryName(ContentPath));
    }

    private static void EnsureFolder(string folderPath)
    {
        string normalized = folderPath?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized)
            || AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        string folder = Path.GetFileName(normalized);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
