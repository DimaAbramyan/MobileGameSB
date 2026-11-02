using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyStatBlocksUI
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static StatDisplayRow Row(int count)
    {
        var r = new StatDisplayRow();
        for (int i = 0; i < count; i++) r.cells.Add(new StatDisplayCell { label = "Label " + i, value = "123", labelLocalizationKey = "stats.test." + i });
        return r;
    }
    public static object Run()
    {
        checks = 0;
        var weaponAsset = AssetDatabase.LoadAssetAtPath<WeaponMetaConfig>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:WeaponMetaConfig")[0]));
        var shipAsset = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ShipMetaConfig")[0]));
        string originalWeapon = EditorJsonUtility.ToJson(weaponAsset), originalShip = EditorJsonUtility.ToJson(shipAsset);
        var weapon = UnityEngine.Object.Instantiate(weaponAsset); var ship = UnityEngine.Object.Instantiate(shipAsset);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var meta in new ScriptableObject[] { weapon, ship })
            {
                var config = ((IStatDisplaySource)meta).StatDisplay;
                config.baseStats.rows.Clear(); config.secondaryStats.rows.Clear();
                config.baseStats.rows.Add(Row(1)); config.baseStats.rows.Add(Row(2)); config.baseStats.rows.Add(Row(3));
                config.baseStats.rows.Add(Row(5)); config.baseStats.rows.Add(Row(0));
                config.secondaryStats.rows.Add(Row(2));
                using (var so = new SerializedObject(meta))
                {
                    var rows = so.FindProperty("statDisplay.baseStats.rows");
                    rows.MoveArrayElement(0, 2);
                    var cells = rows.GetArrayElementAtIndex(0).FindPropertyRelative("cells");
                    cells.GetArrayElementAtIndex(0).FindPropertyRelative("label").stringValue = "Moved";
                    cells.MoveArrayElement(0, 1); so.ApplyModifiedPropertiesWithoutUndo();
                }
                Check(config.baseStats.rows[0].cells[1].label == "Moved", "Reorder cells keeps their values");
                var clone = ScriptableObject.CreateInstance(meta.GetType());
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(meta), clone);
                    var restored = ((IStatDisplaySource)clone).StatDisplay;
                    Check(restored.baseStats.rows.Count == 5 && restored.baseStats.rows[0].cells.Count == 2, "Meta serialization and row reorder");
                    Check(restored.baseStats.rows[0].cells[1].labelLocalizationKey == "stats.test.0", "Localization metadata round trip");
                    Check(restored.secondaryStats.rows.Count == 1, "Independent secondary section survives serialization");
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StatBlocksPreviewWindow.PanelPath), scene);
                try
                {
                    var rect = panel.GetComponent<RectTransform>(); var view = panel.GetComponent<StatBlocksView>();
                    view.Show(meta); LayoutRebuilder.ForceRebuildLayoutImmediate(rect); LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    var primary = panel.transform.Find("Base Stats"); var secondary = panel.transform.Find("Secondary Stats");
                    Check(primary.childCount == 4 && secondary.childCount == 1, "Empty row skipped, sections independent");
                    int[] sizes = { 2, 3, 1, 5 };
                    for (int r = 0; r < primary.childCount; r++)
                    {
                        var row = primary.GetChild(r); Check(row.childCount == sizes[r], "Independent cell count per row");
                        float first = ((RectTransform)row.GetChild(0)).rect.width;
                        Check(first > 0, "Cells receive layout width");
                        for (int c = 1; c < row.childCount; c++) Check(Mathf.Abs(((RectTransform)row.GetChild(c)).rect.width - first) < 1, "Equal widths within one row");
                    }
                    var texts = primary.GetChild(0).GetChild(0).GetComponentsInChildren<TMP_Text>();
                    Check(texts[1].fontSize > texts[0].fontSize, "Value emphasized");
                    view.Rebuild(); Check(primary.childCount == 4, "Rebuild replaces generated rows without duplication");
                    config.baseStats.rows.Clear(); view.Rebuild();
                    Check(!primary.gameObject.activeSelf && secondary.gameObject.activeSelf, "Empty section hidden independently");
                    config.secondaryStats.rows.Clear(); view.Rebuild(); Check(panel.GetComponentsInChildren<StatBox>(true).Length == 0, "Empty Meta safe");
                }
                finally { UnityEngine.Object.DestroyImmediate(panel); }
            }
            Check(EditorJsonUtility.ToJson(weaponAsset) == originalWeapon && EditorJsonUtility.ToJson(shipAsset) == originalShip, "Existing Meta assets unchanged");
            weapon.StatDisplay.baseStats.rows.Add(new StatDisplayRow { cells = new List<StatDisplayCell> { new() { label = "Damage", value = "125" }, new() { label = "Range", value = "18 m" } } });
            weapon.StatDisplay.baseStats.rows.Add(new StatDisplayRow { cells = new List<StatDisplayCell> { new() { label = "Rate of fire", value = "4 / s" }, new() { label = "Damage type", value = "Thermal" }, new() { label = "Projectile speed", value = "22 m / s" } } });
            weapon.StatDisplay.secondaryStats.rows.Add(new StatDisplayRow { cells = new List<StatDisplayCell> { new() { label = "Burn damage", value = "30 / s" } } });
            StatBlocksPreviewWindow.Open(weapon);
            var window = EditorWindow.GetWindow<StatBlocksPreviewWindow>();
            // Render the exact prefab preview and keep the real Meta assets untouched.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var camera = (Camera)typeof(StatBlocksPreviewWindow).GetField("previewCamera", flags).GetValue(window);
            var texture = (RenderTexture)typeof(StatBlocksPreviewWindow).GetField("texture", flags).GetValue(window);
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            File.WriteAllBytes("AgentArtifacts/stat-blocks-preview.png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); window.Close();
            return new { passed = true, checks, categories = 2, rowSizes = "1, 2, 3, 5 and empty", preview = "AgentArtifacts/stat-blocks-preview.png" };
        }
        finally { UnityEngine.Object.DestroyImmediate(weapon); UnityEngine.Object.DestroyImmediate(ship); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
