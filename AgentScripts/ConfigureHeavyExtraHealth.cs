using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class ConfigureHeavyExtraHealth
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        var changed = new List<string>();
        foreach (var hull in catalog.Hulls)
        {
            if (hull == null || hull.GameplayPrefab == null) continue;
            string path = AssetDatabase.GetAssetPath(hull.GameplayPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<ExtraHealthPassive>() == null)
                {
                    Undo.AddComponent<ExtraHealthPassive>(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add(path);
                }
                if (root.GetComponent<ShieldCreateAbility>() != null)
                {
                    var meta = hull.Data.ShipMetaConfig;
                    Undo.RecordObject(meta, "Heavy additional health settings");
                    var metaSo = new SerializedObject(meta);
                    var levels = metaSo.FindProperty("levels");
                    for (int i = 0; i < levels.arraySize; i++)
                    {
                        var contracts = levels.GetArrayElementAtIndex(i).FindPropertyRelative("contracts");
                        for (int j = 0; j < contracts.arraySize; j++)
                        {
                            var amount = contracts.GetArrayElementAtIndex(j).FindPropertyRelative("extraHealthAmount");
                            if (amount != null) amount.floatValue = 1f;
                        }
                    }
                    metaSo.ApplyModifiedProperties(); EditorUtility.SetDirty(meta);
                    Undo.RecordObject(hull, "Heavy ability description");
                    var contentSo = new SerializedObject(hull);
                    contentSo.FindProperty("activeAbilityDescription").stringValue =
                        "Adds 1 Extra Health to the living team ship with the lowest current hull health.";
                    contentSo.ApplyModifiedProperties(); EditorUtility.SetDirty(hull);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        int rows = 0;
        foreach (var ui in UnityEngine.Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ui.gameObject.scene.path != "Assets/Scenes/Fighting/Fighting.unity") continue;
            Undo.RecordObject(ui, "Bind team ExtraHealthBar");
            var so = new SerializedObject(ui);
            foreach (string field in new[] { "playerShip1Hud", "playerShip2Hud" })
            {
                var row = so.FindProperty(field);
                var root = row.FindPropertyRelative("hudRoot").objectReferenceValue as RectTransform;
                if (root == null) throw new Exception("Missing HUD root: " + field);
                var extra = root.Find("ExtraHealthBar");
                if (extra == null || extra.GetComponent<StatBar>() == null)
                    throw new Exception("Missing ExtraHealthBar: " + field);
                row.FindPropertyRelative("extraHealthBar").objectReferenceValue = extra.GetComponent<StatBar>();
                row.FindPropertyRelative("extraHealthValueText").objectReferenceValue = extra.GetComponentInChildren<TMP_Text>(true);
                rows++;
            }
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            EditorSceneManager.SaveScene(ui.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        if (rows != 2) throw new Exception("Both battle HUD rows must be configured.");
        return new { prefabs = changed, hudRows = rows, amount = 1f };
    }
}
