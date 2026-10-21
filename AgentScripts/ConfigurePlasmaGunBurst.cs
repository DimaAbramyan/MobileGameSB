using UnityEditor;
using UnityEngine;

public static class ConfigurePlasmaGunBurst
{
    private const string WeaponDataPath =
        "Assets/Prefub/Data/WeaponData/GamePlayProgression/PlasmaGun.asset";

    public static object Execute()
    {
        WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(
            WeaponDataPath);
        if (weaponData == null || weaponData.WeaponMetaConfig == null)
            return new { error = "PlasmaGun WeaponData or WeaponMetaConfig is missing." };

        WeaponMetaConfig metaConfig = weaponData.WeaponMetaConfig;
        Undo.RecordObject(metaConfig, "Configure PlasmaGun burst fire");
        metaConfig.TryAddContractToAllLevels(
            typeof(BurstFireWeaponMetaContract));

        var serialized = new SerializedObject(metaConfig);
        SerializedProperty levels = serialized.FindProperty("levels");
        int configuredLevels = 0;
        for (int levelIndex = 0; levelIndex < levels.arraySize; levelIndex++)
        {
            SerializedProperty contracts = levels.GetArrayElementAtIndex(levelIndex)
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

                contract.FindPropertyRelative("volleysPerActivation").intValue = 3;
                contract.FindPropertyRelative("projectilesPerVolley").intValue = 1;
                contract.FindPropertyRelative("delayBetweenVolleys").floatValue = 0.2f;
                configuredLevels++;
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(metaConfig);
        AssetDatabase.SaveAssets();

        WeaponMetaRuntimeStats levelZero = metaConfig.GetRuntimeStats(0);
        return new
        {
            configuredLevels,
            volleys = levelZero.WeaponStats.VolleysPerActivation,
            projectilesPerVolley = levelZero.WeaponStats.ProjectilesPerVolley,
            delay = levelZero.WeaponStats.DelayBetweenVolleys
        };
    }
}
