using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WeaponProjectileMigrationUtility
{
    private const string ProjectileDataFolder =
        "Assets/Prefub/Data/ProjectileData";
    private const string MetaDataFolder =
        "Assets/Prefub/Data/WeaponData/Meta";

    private readonly struct MigrationEntry
    {
        public MigrationEntry(
            string weaponName,
            string weaponDataPath,
            string metaPath,
            string projectilePath,
            string projectilePrefabPath,
            bool isBeam,
            string weaponPrefabPath = null)
        {
            WeaponName = weaponName;
            WeaponDataPath = weaponDataPath;
            MetaPath = metaPath;
            ProjectilePath = projectilePath;
            ProjectilePrefabPath = projectilePrefabPath;
            IsBeam = isBeam;
            WeaponPrefabPath = weaponPrefabPath;
        }

        public string WeaponName { get; }
        public string WeaponDataPath { get; }
        public string MetaPath { get; }
        public string ProjectilePath { get; }
        public string ProjectilePrefabPath { get; }
        public bool IsBeam { get; }
        public string WeaponPrefabPath { get; }
    }

    public static string MigrateRequestedWeapons()
    {
        MigrationEntry[] entries =
        {
            new(
                "MiniGun",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/MiniGun.asset",
                "Assets/Prefub/Data/WeaponData/Meta/MiniGunMeta.asset",
                ProjectileDataFolder + "/MiniGunProjectile.asset",
                "Assets/Prefub/Projectiles/Bullet.prefab",
                false),
            new(
                "FlameThrower",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/FlameThrower.asset",
                MetaDataFolder + "/FlameThrowerMeta.asset",
                ProjectileDataFolder + "/FlameThrowerProjectile.asset",
                "Assets/Prefub/Projectiles/Fire.prefab",
                false),
            new(
                "Laser",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/Laser.asset",
                "Assets/Prefub/Data/WeaponData/Meta/LaserMeta.asset",
                ProjectileDataFolder + "/LaserBeam.asset",
                null,
                true),
            new(
                "GammaRay",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/GammaRay.asset",
                "Assets/Prefub/Data/WeaponData/Meta/GammaRayMeta.asset",
                ProjectileDataFolder + "/GammaRayProjectile.asset",
                "Assets/Prefub/Projectiles/Gamma.prefab",
                false),
            new(
                "PlasmaGun",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/PlasmaGun.asset",
                "Assets/Prefub/Data/WeaponData/Meta/PlasmaGunMeta.asset",
                ProjectileDataFolder + "/PlasmaGunProjectile.asset",
                "Assets/Prefub/Projectiles/Bullet.prefab",
                false)
        };

        EnsureFolder(ProjectileDataFolder);
        EnsureFolder(MetaDataFolder);

        List<string> migrated = new();
        for (int index = 0; index < entries.Length; index++)
        {
            Migrate(entries[index]);
            migrated.Add(entries[index].WeaponName);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Migrated: " + string.Join(", ", migrated);
    }

    public static string MigrateRemainingGameplayWeapons()
    {
        MigrationEntry[] entries =
        {
            new(
                "BallLightning",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/BallLightning.asset",
                MetaDataFolder + "/BallLightningMeta.asset",
                ProjectileDataFolder + "/BallLightningProjectileData.asset",
                "Assets/Prefub/Projectiles/BallLightningProjectile.prefab",
                false),
            new(
                "Circular",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/Circular.asset",
                MetaDataFolder + "/CircularMeta.asset",
                ProjectileDataFolder + "/CircularProjectileData.asset",
                "Assets/Prefub/Projectiles/CircularProjectile.prefab",
                false),
            new(
                "QBeam",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/QBeam.asset",
                MetaDataFolder + "/QBeamMeta.asset",
                ProjectileDataFolder + "/QBeamBeam.asset",
                null,
                true,
                "Assets/Prefub/Weapons/QBeamGun.prefab"),
            new(
                "RocketLauncher",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/RocketLauncher.asset",
                MetaDataFolder + "/RocketLauncherMeta.asset",
                ProjectileDataFolder + "/RocketProjectileData.asset",
                "Assets/Prefub/Projectiles/Rocket.prefab",
                false),
            new(
                "ThermalLaser",
                "Assets/Prefub/Data/WeaponData/GamePlayProgression/ThermalLaser.asset",
                MetaDataFolder + "/ThermalLaserMeta.asset",
                ProjectileDataFolder + "/ThermalLaserBeam.asset",
                null,
                true,
                "Assets/Prefub/Weapons/ThermalLaserGun.prefab")
        };

        EnsureFolder(ProjectileDataFolder);
        EnsureFolder(MetaDataFolder);

        List<string> migrated = new();
        for (int index = 0; index < entries.Length; index++)
        {
            Migrate(entries[index]);
            migrated.Add(entries[index].WeaponName);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Migrated: " + string.Join(", ", migrated);
    }

    private static void Migrate(MigrationEntry entry)
    {
        WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(
            entry.WeaponDataPath);
        if (weaponData == null)
            throw new InvalidOperationException(
                $"WeaponData was not found: {entry.WeaponDataPath}");

        SerializedObject weaponSerialized = new(weaponData);
        WeaponMetaConfig metaConfig = GetOrCreateMetaConfig(
            entry,
            weaponSerialized);
        ProjectileData projectileData = GetOrCreateProjectileData(entry);

        Undo.RecordObject(weaponData, $"Migrate {entry.WeaponName} projectile");
        Undo.RecordObject(metaConfig, $"Migrate {entry.WeaponName} meta");
        Undo.RecordObject(
            projectileData,
            $"Migrate {entry.WeaponName} projectile data");

        ConfigureMetaConfig(metaConfig, weaponSerialized);
        ConfigureProjectileData(entry, projectileData, weaponSerialized, metaConfig);

        weaponSerialized.Update();
        weaponSerialized.FindProperty("weaponMetaConfig").objectReferenceValue =
            metaConfig;
        weaponSerialized.ApplyModifiedPropertiesWithoutUndo();

        EnsureProjectileSlot(metaConfig, projectileData);
        weaponData.SynchronizeProjectileLevelBonuses();
        MoveInBattleProjectileBonuses(
            weaponData,
            projectileData,
            entry.IsBeam);
        MoveBallLightningProjectileBonuses(weaponData, projectileData);

        EditorUtility.SetDirty(weaponData);
        EditorUtility.SetDirty(metaConfig);
        EditorUtility.SetDirty(projectileData);
    }

    private static WeaponMetaConfig GetOrCreateMetaConfig(
        MigrationEntry entry,
        SerializedObject weaponSerialized)
    {
        WeaponMetaConfig configuredMeta = weaponSerialized
            .FindProperty("weaponMetaConfig")
            .objectReferenceValue as WeaponMetaConfig;
        if (configuredMeta != null)
            return configuredMeta;

        WeaponMetaConfig existingMeta = AssetDatabase.LoadAssetAtPath<
            WeaponMetaConfig>(entry.MetaPath);
        if (existingMeta != null)
            return existingMeta;

        WeaponMetaConfig createdMeta = ScriptableObject.CreateInstance<
            WeaponMetaConfig>();
        AssetDatabase.CreateAsset(createdMeta, entry.MetaPath);
        return createdMeta;
    }

    private static ProjectileData GetOrCreateProjectileData(MigrationEntry entry)
    {
        ProjectileData existing = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            entry.ProjectilePath);
        if (existing != null)
            return existing;

        ProjectileData created = ScriptableObject.CreateInstance<ProjectileData>();
        AssetDatabase.CreateAsset(created, entry.ProjectilePath);
        return created;
    }

    private static void ConfigureMetaConfig(
        WeaponMetaConfig metaConfig,
        SerializedObject weaponSerialized)
    {
        SerializedObject metaSerialized = new(metaConfig);
        metaSerialized.Update();
        metaSerialized.FindProperty("energyCost").intValue = weaponSerialized
            .FindProperty("energyCost")
            .intValue;

        SerializedProperty levels = metaSerialized.FindProperty("levels");
        if (levels.arraySize == 0)
            levels.arraySize = 1;

        SerializedProperty basicStats = levels
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("basicStats");
        if (basicStats == null)
        {
            metaSerialized.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        bool hasAssignedMeta = weaponSerialized.FindProperty("weaponMetaConfig")
            .objectReferenceValue != null;
        if (!hasAssignedMeta || !HasConfiguredMetaStats(basicStats))
        {
            SerializedProperty manualBaseStats = weaponSerialized.FindProperty(
                "manualBaseStats");
            CopyFloat(manualBaseStats, "reloadTime", basicStats, "reloadTime");
            CopyFloat(manualBaseStats, "angle", basicStats, "angle");
            CopyFloat(manualBaseStats, "damage", basicStats, "damage");
            CopyFloat(manualBaseStats, "range", basicStats, "range");
            CopyFloat(
                manualBaseStats,
                "speed",
                basicStats,
                "projectileSpeed");
        }

        metaSerialized.ApplyModifiedPropertiesWithoutUndo();
        ConfigureLegacyBurstFire(metaConfig, weaponSerialized);
    }

    private static bool HasConfiguredMetaStats(SerializedProperty basicStats)
    {
        SerializedProperty reloadTime = basicStats.FindPropertyRelative(
            "reloadTime");
        return reloadTime != null && reloadTime.floatValue > 0f;
    }

    private static void ConfigureProjectileData(
        MigrationEntry entry,
        ProjectileData projectileData,
        SerializedObject weaponSerialized,
        WeaponMetaConfig metaConfig)
    {
        SerializedObject metaSerialized = new(metaConfig);
        SerializedProperty basicStats = metaSerialized.FindProperty("levels")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("basicStats");

        SerializedObject projectileSerialized = new(projectileData);
        projectileSerialized.Update();
        BallLightningData ballLightningData = weaponSerialized.targetObject
            as BallLightningData;
        projectileSerialized.FindProperty("deliveryType").enumValueIndex =
            entry.IsBeam
                ? (int)ProjectileDeliveryType.Beam
                : (int)ProjectileDeliveryType.Projectile;
        if (ballLightningData != null)
        {
            projectileSerialized.FindProperty("damage").floatValue =
                ballLightningData.GetDirectDamage(0);
        }
        else
        {
            CopyFloat(basicStats, "damage", projectileSerialized, "damage");
        }
        projectileSerialized.FindProperty("damageType").enumValueIndex =
            weaponSerialized.FindProperty("damageType").enumValueIndex;

        if (!entry.IsBeam)
        {
            Projectile prefab = AssetDatabase.LoadAssetAtPath<Projectile>(
                entry.ProjectilePrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Projectile prefab was not found: {entry.ProjectilePrefabPath}");
            }

            projectileSerialized.FindProperty("projectilePrefab")
                .objectReferenceValue = prefab;
            if (ballLightningData != null)
            {
                projectileSerialized.FindProperty("range").floatValue =
                    ballLightningData.MaxTravelDistance;
                projectileSerialized.FindProperty("speed").floatValue =
                    ballLightningData.ProjectileSpeed;
            }
            else
            {
                CopyFloat(basicStats, "range", projectileSerialized, "range");
                CopyFloat(
                    basicStats,
                    "projectileSpeed",
                    projectileSerialized,
                    "speed");
            }
        }

        SerializedProperty contracts = projectileSerialized.FindProperty(
            "contracts");
        contracts.arraySize = 0;
        AddMappedContracts(entry, contracts, weaponSerialized, metaConfig);
        projectileSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddMappedContracts(
        MigrationEntry entry,
        SerializedProperty contracts,
        SerializedObject weaponSerialized,
        WeaponMetaConfig metaConfig)
    {
        if (entry.IsBeam)
        {
            SerializedProperty beam = AddContract<ProjectileBeamContract>(
                contracts);
            float beamWidth = GetBeamWidth(entry.WeaponPrefabPath);
            if (beamWidth > 0f)
                beam.FindPropertyRelative("width").floatValue = beamWidth;
            beam.FindPropertyRelative("damageTickInterval").floatValue =
                GetLegacyContinuousDamageInterval(
                    metaConfig,
                    weaponSerialized.FindProperty("continuousDamageInterval")
                        .floatValue);
            return;
        }

        if (entry.WeaponName == "Circular")
        {
            SerializedProperty circularChain =
                AddContract<ProjectileCircularChainContract>(contracts);
            SerializedProperty manualBaseStats = weaponSerialized.FindProperty(
                "manualBaseStats");
            circularChain.FindPropertyRelative("damagePerHit").floatValue =
                manualBaseStats.FindPropertyRelative("damage").floatValue;
            circularChain.FindPropertyRelative("hitInterval").floatValue =
                weaponSerialized.FindProperty("continuousDamageInterval")
                    .floatValue;

            AddLegacyLifetimeContract(contracts, weaponSerialized);
            return;
        }

        ProjectileFlightMode flightMode = (ProjectileFlightMode)weaponSerialized
            .FindProperty("flightMode")
            .enumValueIndex;
        if (flightMode == ProjectileFlightMode.Homing)
        {
            SerializedProperty homing = AddContract<ProjectileHomingContract>(
                contracts);
            homing.FindPropertyRelative("rotationSpeed").floatValue =
                weaponSerialized.FindProperty("homingRotationSpeed").floatValue;
        }

        if (weaponSerialized.FindProperty("growDuringFlight").boolValue)
        {
            SerializedProperty flight = AddContract<ProjectileFlightContract>(
                contracts);
            flight.FindPropertyRelative("growDuringFlight").boolValue = true;
            flight.FindPropertyRelative("scaleGrowthPerSecond").vector2Value =
                weaponSerialized.FindProperty("scaleGrowthPerSecond").vector2Value;
        }

        SerializedProperty contact = AddContract<ProjectileContactContract>(
            contracts);
        contact.FindPropertyRelative("contactMode").enumValueIndex =
            weaponSerialized.FindProperty("contactMode").enumValueIndex;

        ProjectileContactMode contactMode = (ProjectileContactMode)weaponSerialized
            .FindProperty("contactMode")
            .enumValueIndex;
        if (contactMode == ProjectileContactMode.PierceContinuous)
        {
            SerializedProperty continuous = AddContract<
                ProjectileContinuousDamageContract>(contracts);
            continuous.FindPropertyRelative("damageTickInterval").floatValue =
                weaponSerialized.FindProperty("continuousDamageInterval")
                    .floatValue;
        }

        if (contactMode == ProjectileContactMode.ExplodeAndSpawn)
        {
            SerializedProperty explosion = AddContract<ProjectileExplosionContract>(
                contracts);
            explosion.FindPropertyRelative("explosionPrefab").objectReferenceValue =
                weaponSerialized.FindProperty("explosionPrefab")
                    .objectReferenceValue;

            SerializedProperty sources = AddContract<
                ProjectileDamageSourcesContract>(contracts);
            SerializedProperty sourceList = sources.FindPropertyRelative("sources");
            sourceList.arraySize = 1;
            SerializedProperty source = sourceList.GetArrayElementAtIndex(0);
            source.FindPropertyRelative("id").stringValue = "explosion";
            source.FindPropertyRelative("trigger").enumValueIndex =
                (int)ProjectileDamageTrigger.Explosion;
            source.FindPropertyRelative("damage").floatValue = weaponSerialized
                .FindProperty("explosionDamage")
                .floatValue;
            source.FindPropertyRelative("damageType").enumValueIndex =
                weaponSerialized.FindProperty("damageType").enumValueIndex;
        }

        AddLegacyLifetimeContract(contracts, weaponSerialized);

        MigrateLegacyMetaContracts(metaConfig, contracts);
    }

    private static void AddLegacyLifetimeContract(
        SerializedProperty contracts,
        SerializedObject weaponSerialized)
    {
        SerializedProperty lifetime = AddContract<ProjectileLifetimeContract>(
            contracts);
        lifetime.FindPropertyRelative("lifetime").floatValue = weaponSerialized
            .FindProperty("projectileLifetime")
            .floatValue;
        lifetime.FindPropertyRelative("disableColliderAfterFirstPhysicsStep")
            .boolValue = weaponSerialized
                .FindProperty("disableColliderAfterFirstPhysicsStep")
                .boolValue;
        lifetime.FindPropertyRelative("fadeBeforeDespawn").boolValue =
            weaponSerialized.FindProperty("fadeDuringLifetime").boolValue;
        lifetime.FindPropertyRelative("fadeDuration").floatValue = weaponSerialized
            .FindProperty("fadeDuration")
            .floatValue;
    }

    private static void ConfigureLegacyBurstFire(
        WeaponMetaConfig metaConfig,
        SerializedObject weaponSerialized)
    {
        SerializedProperty manualBaseStats = weaponSerialized.FindProperty(
            "manualBaseStats");
        if (manualBaseStats == null)
            return;

        int volleys = Mathf.Max(
            1,
            manualBaseStats.FindPropertyRelative("volleysPerActivation").intValue);
        int projectiles = Mathf.Max(
            1,
            manualBaseStats.FindPropertyRelative("projectilesPerVolley").intValue);
        float delay = Mathf.Max(
            0f,
            manualBaseStats.FindPropertyRelative("delayBetweenVolleys").floatValue);
        float spread = Mathf.Max(
            0f,
            manualBaseStats.FindPropertyRelative("spreadAngle").floatValue);
        if (volleys == 1 && projectiles == 1 && delay <= 0f && spread <= 0f)
            return;

        metaConfig.TryAddContractToAllLevels(
            typeof(BurstFireWeaponMetaContract));

        SerializedObject metaSerialized = new(metaConfig);
        SerializedProperty levels = metaSerialized.FindProperty("levels");
        for (int levelIndex = 0; levelIndex < levels.arraySize; levelIndex++)
        {
            SerializedProperty contracts = levels
                .GetArrayElementAtIndex(levelIndex)
                .FindPropertyRelative("contracts");
            for (int contractIndex = 0;
                 contractIndex < contracts.arraySize;
                 contractIndex++)
            {
                SerializedProperty contract = contracts.GetArrayElementAtIndex(
                    contractIndex);
                if (contract.managedReferenceValue
                    is not BurstFireWeaponMetaContract)
                {
                    continue;
                }

                contract.FindPropertyRelative("volleysPerActivation").intValue =
                    volleys;
                contract.FindPropertyRelative("projectilesPerVolley").intValue =
                    projectiles;
                contract.FindPropertyRelative("delayBetweenVolleys").floatValue =
                    delay;
                contract.FindPropertyRelative("spreadAngle").floatValue = spread;
            }
        }

        metaSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float GetLegacyContinuousDamageInterval(
        WeaponMetaConfig metaConfig,
        float fallback)
    {
        if (metaConfig != null
            && metaConfig.GetLevel(0).TryGetContract(
                out ContinuousDamageWeaponMetaContract continuousDamage))
        {
            return continuousDamage.DamageTickInterval;
        }

        return fallback;
    }

    private static void MigrateLegacyMetaContracts(
        WeaponMetaConfig metaConfig,
        SerializedProperty projectileContracts)
    {
        if (metaConfig == null)
            return;

        WeaponMetaLevel level = metaConfig.GetLevel(0);
        if (level.TryGetContract(out HomingWeaponMetaContract homing)
            && !ContainsContract<ProjectileHomingContract>(projectileContracts))
        {
            SerializedProperty contract = AddContract<ProjectileHomingContract>(
                projectileContracts);
            contract.FindPropertyRelative("rotationSpeed").floatValue =
                homing.RotationSpeed;
        }

        if (level.TryGetContract(out TargetingWeaponMetaContract targeting)
            && !ContainsContract<ProjectileTargetingContract>(projectileContracts))
        {
            SerializedProperty contract = AddContract<ProjectileTargetingContract>(
                projectileContracts);
            contract.FindPropertyRelative("maxTargets").intValue =
                targeting.MaxTargets;
            contract.FindPropertyRelative("searchRadius").floatValue =
                targeting.TargetSearchRadius;
        }

        if (level.TryGetContract(
                out ContinuousDamageWeaponMetaContract continuousDamage)
            && !ContainsContract<ProjectileContinuousDamageContract>(
                projectileContracts))
        {
            SerializedProperty contract = AddContract<
                ProjectileContinuousDamageContract>(projectileContracts);
            contract.FindPropertyRelative("damageTickInterval").floatValue =
                continuousDamage.DamageTickInterval;
        }

        metaConfig.TryRemoveContractFromAllLevels(
            typeof(HomingWeaponMetaContract));
        metaConfig.TryRemoveContractFromAllLevels(
            typeof(TargetingWeaponMetaContract));
        metaConfig.TryRemoveContractFromAllLevels(
            typeof(ContinuousDamageWeaponMetaContract));
    }

    private static bool ContainsContract<TContract>(
        SerializedProperty contracts)
        where TContract : ProjectileDataContract
    {
        for (int index = 0; index < contracts.arraySize; index++)
        {
            if (contracts.GetArrayElementAtIndex(index).managedReferenceValue
                is TContract)
            {
                return true;
            }
        }

        return false;
    }

    private static SerializedProperty AddContract<TContract>(
        SerializedProperty contracts)
        where TContract : ProjectileDataContract, new()
    {
        int index = contracts.arraySize;
        contracts.arraySize++;
        SerializedProperty contract = contracts.GetArrayElementAtIndex(index);
        contract.managedReferenceValue = new TContract();
        return contract;
    }

    private static void EnsureProjectileSlot(
        WeaponMetaConfig metaConfig,
        ProjectileData projectileData)
    {
        for (int index = 0; index < metaConfig.ProjectileSlots.Count; index++)
        {
            if (metaConfig.ProjectileSlots[index]?.Projectile == projectileData)
                return;
        }

        metaConfig.AddProjectileSlot();
        SerializedObject metaSerialized = new(metaConfig);
        SerializedProperty slots = metaSerialized.FindProperty("projectileSlots");
        slots.GetArrayElementAtIndex(slots.arraySize - 1)
            .FindPropertyRelative("projectile")
            .objectReferenceValue = projectileData;
        metaSerialized.ApplyModifiedPropertiesWithoutUndo();
        metaConfig.SynchronizeProjectileSlots();
    }

    private static void MoveInBattleProjectileBonuses(
        WeaponData weaponData,
        ProjectileData projectileData,
        bool isBeam)
    {
        if (!weaponData.TryGetPrimaryProjectileData(
                out WeaponProjectileSlot slot,
                out _))
        {
            throw new InvalidOperationException(
                $"Projectile slot was not created for {weaponData.name}.");
        }

        SerializedObject serialized = new(weaponData);
        serialized.Update();
        SerializedProperty levels = serialized.FindProperty("levelBonuses");
        for (int levelIndex = 0; levelIndex < levels.arraySize; levelIndex++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(levelIndex);
            SerializedProperty projectileBonus = FindProjectileBonus(
                level.FindPropertyRelative("projectileBonuses"),
                slot.Id);
            if (projectileBonus == null)
                continue;

            CopyFloat(level, "damagePercent", projectileBonus, "damagePercent");
            level.FindPropertyRelative("damagePercent").floatValue = 0f;

            if (isBeam)
                continue;

            CopyFloat(level, "rangePercent", projectileBonus, "rangePercent");
            CopyFloat(level, "speedPercent", projectileBonus, "speedPercent");
            level.FindPropertyRelative("rangePercent").floatValue = 0f;
            level.FindPropertyRelative("speedPercent").floatValue = 0f;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MoveBallLightningProjectileBonuses(
        WeaponData weaponData,
        ProjectileData projectileData)
    {
        if (weaponData is not BallLightningData ballLightning
            || !weaponData.TryGetPrimaryProjectileData(out WeaponProjectileSlot slot,
                out _))
        {
            return;
        }

        float baseDamage = projectileData.Damage;
        if (baseDamage <= 0f)
            return;

        SerializedObject serialized = new(weaponData);
        serialized.Update();
        SerializedProperty levels = serialized.FindProperty("levelBonuses");
        float previousTotal = 0f;
        for (int levelIndex = 0; levelIndex < levels.arraySize; levelIndex++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(levelIndex);
            SerializedProperty projectileBonus = FindProjectileBonus(
                level.FindPropertyRelative("projectileBonuses"),
                slot.Id);
            if (projectileBonus == null)
                continue;

            float currentDamage = ballLightning.GetDirectDamage(levelIndex);
            float total = Mathf.Max(
                0f,
                (currentDamage / baseDamage - 1f) * 100f);
            projectileBonus.FindPropertyRelative("damagePercent").floatValue =
                total - previousTotal;
            previousTotal = total;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty FindProjectileBonus(
        SerializedProperty bonuses,
        string slotId)
    {
        if (bonuses == null)
            return null;

        for (int index = 0; index < bonuses.arraySize; index++)
        {
            SerializedProperty bonus = bonuses.GetArrayElementAtIndex(index);
            if (bonus.FindPropertyRelative("projectileSlotId").stringValue == slotId)
                return bonus;
        }

        return null;
    }

    private static void CopyFloat(
        SerializedProperty source,
        string sourceName,
        SerializedProperty destination,
        string destinationName)
    {
        SerializedProperty sourceValue = source?.FindPropertyRelative(sourceName);
        SerializedProperty destinationValue = destination?.FindPropertyRelative(
            destinationName);
        if (sourceValue != null && destinationValue != null)
            destinationValue.floatValue = sourceValue.floatValue;
    }

    private static void CopyFloat(
        SerializedObject source,
        string sourceName,
        SerializedObject destination,
        string destinationName)
    {
        SerializedProperty sourceValue = source.FindProperty(sourceName);
        SerializedProperty destinationValue = destination.FindProperty(destinationName);
        if (sourceValue != null && destinationValue != null)
            destinationValue.floatValue = sourceValue.floatValue;
    }

    private static void CopyFloat(
        SerializedProperty source,
        string sourceName,
        SerializedObject destination,
        string destinationName)
    {
        SerializedProperty sourceValue = source?.FindPropertyRelative(sourceName);
        SerializedProperty destinationValue = destination.FindProperty(destinationName);
        if (sourceValue != null && destinationValue != null)
            destinationValue.floatValue = sourceValue.floatValue;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string name = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static float GetBeamWidth(string weaponPrefabPath)
    {
        if (string.IsNullOrWhiteSpace(weaponPrefabPath))
            return 0f;

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(
            weaponPrefabPath);
        try
        {
            ContinuousBeamWeapon beamWeapon = prefabContents
                .GetComponentInChildren<ContinuousBeamWeapon>(true);
            if (beamWeapon == null)
                return 0f;

            SerializedProperty width = new SerializedObject(beamWeapon)
                .FindProperty("beamWidth");
            return width != null ? width.floatValue : 0f;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }
}
