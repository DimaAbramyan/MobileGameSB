using System;
using UnityEditor;
using UnityEngine;

public static class ConfigurePrismEnergyWave
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        const string folder = "Assets/GameContent/Abilities";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/GameContent", "Abilities");
        const string materialPath = folder + "/PrismEnergyWave.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default")) { name = "Prism Energy Wave" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        const string prefabPath = folder + "/PrismEnergyWave.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var visual = new GameObject("Prism Energy Wave Visual");
            try
            {
                var line = visual.AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.useWorldSpace = false;
                line.alignment = LineAlignment.TransformZ;
                line.positionCount = 2; line.SetPosition(0, Vector3.left * .5f); line.SetPosition(1, Vector3.right * .5f);
                line.startWidth = line.endWidth = 1f;
                line.startColor = line.endColor = new Color(.1f, .85f, 1f, .9f);
                line.sortingOrder = 30;
                prefab = PrefabUtility.SaveAsPrefabAsset(visual, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(visual); }
        }
        var meta = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>("Assets/Prefub/Data/ShipMeta/PrismMeta.asset");
        Undo.RecordObject(meta, "Configure Prism Energy Wave");
        var so = new SerializedObject(meta);
        var levels = so.FindProperty("levels");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var contracts = levels.GetArrayElementAtIndex(i).FindPropertyRelative("contracts");
            for (int j = 0; j < contracts.arraySize; j++)
            {
                var contract = contracts.GetArrayElementAtIndex(j);
                var wave = contract.FindPropertyRelative("wavePrefab");
                if (wave != null) wave.objectReferenceValue = prefab;
            }
        }
        so.ApplyModifiedProperties(); EditorUtility.SetDirty(meta);
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefub/Ships/Prism.prefab");
        try
        {
            var ability = root.GetComponent<PrismBeamActiveAbility>();
            Undo.RecordObject(ability, "Default Prism wave visual");
            var abilitySo = new SerializedObject(ability);
            abilitySo.FindProperty("defaultWavePrefab").objectReferenceValue = prefab;
            abilitySo.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefub/Ships/Prism.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        foreach (var hull in catalog.Hulls)
        {
            if (hull?.GameplayPrefab == null || hull.GameplayPrefab.GetComponent<PrismBeamActiveAbility>() == null) continue;
            Undo.RecordObject(hull, "Prism energy wave description");
            var hullSo = new SerializedObject(hull);
            hullSo.FindProperty("activeAbilityDescription").stringValue =
                "Launches a piercing energy wave forward. Each enemy is hit once. Projectile destruction can be enabled in PrismMeta.";
            hullSo.ApplyModifiedProperties(); EditorUtility.SetDirty(hull);
        }
        AssetDatabase.SaveAssets();
        return new { prefabPath, meta = AssetDatabase.GetAssetPath(meta), cooldown = 12f };
    }
}
