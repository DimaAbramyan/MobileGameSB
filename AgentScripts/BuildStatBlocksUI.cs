using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BuildStatBlocksUI
{
    const string Folder = "Assets/Prefub/UI/Stats";
    static RectTransform Node(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }
    static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
    { using var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static VerticalLayoutGroup Vertical(GameObject go, float spacing)
    {
        var group = go.AddComponent<VerticalLayoutGroup>(); group.spacing = spacing;
        group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = true; group.childForceExpandHeight = false; return group;
    }
    public static object Build()
    {
        if (AssetDatabase.IsValidFolder(Folder)) throw new Exception("Stats folder exists; refusing to overwrite authored assets.");
        AssetDatabase.CreateFolder("Assets/Prefub/UI", "Stats");
        if (TMP_Settings.defaultFontAsset == null) throw new Exception("Project TMP default font is missing");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var box = Node("StatBox"); SceneManager.MoveGameObjectToScene(box.gameObject, scene); box.sizeDelta = new Vector2(280, 100);
            var bg = box.gameObject.AddComponent<Image>(); bg.color = new Color(.085f, .12f, .18f); bg.raycastTarget = false;
            var layout = Vertical(box.gameObject, 6); layout.padding = new RectOffset(16, 16, 12, 12);
            var element = box.gameObject.AddComponent<LayoutElement>(); element.minHeight = 100; element.minWidth = 0;
            element.preferredWidth = 0; element.flexibleWidth = 1;
            var label = Node("Label", box).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.fontSize = 18; label.color = new Color(.68f, .75f, .84f); label.text = "Stat"; label.raycastTarget = false;
            var value = Node("Value", box).gameObject.AddComponent<TextMeshProUGUI>();
            value.font = TMP_Settings.defaultFontAsset; value.fontSize = 30; value.fontStyle = FontStyles.Bold;
            value.color = new Color(.48f, .9f, 1); value.text = "—"; value.raycastTarget = false;
            var component = box.gameObject.AddComponent<StatBox>();
            Reference(component, "label", label); Reference(component, "value", value); Reference(component, "contentLayout", layout);
            var boxAsset = PrefabUtility.SaveAsPrefabAsset(box.gameObject, Folder + "/StatBox.prefab");
            var row = Node("StatRow"); SceneManager.MoveGameObjectToScene(row.gameObject, scene); row.sizeDelta = new Vector2(900, 100);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 10;
            horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 100;
            var rowAsset = PrefabUtility.SaveAsPrefabAsset(row.gameObject, Folder + "/StatRow.prefab");
            var panel = Node("StatBlocksPanel"); SceneManager.MoveGameObjectToScene(panel.gameObject, scene);
            panel.pivot = new Vector2(.5f, 1); panel.sizeDelta = new Vector2(940, 0);
            var group = Vertical(panel.gameObject, 20); group.padding = new RectOffset(12, 12, 12, 12);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var primary = Node("Base Stats", panel); Vertical(primary.gameObject, 10);
            var secondary = Node("Secondary Stats", panel); Vertical(secondary.gameObject, 10);
            var view = panel.gameObject.AddComponent<StatBlocksView>();
            Reference(view, "cellPrefab", boxAsset.GetComponent<StatBox>()); Reference(view, "rowPrefab", rowAsset.GetComponent<RectTransform>());
            Reference(view, "baseStatsRoot", primary); Reference(view, "secondaryStatsRoot", secondary);
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject, Folder + "/StatBlocksPanel.prefab");
            AssetDatabase.SaveAssets(); return new { created = true, folder = Folder, prefabs = 3 };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    public static object FixPadding()
    {
        var root = PrefabUtility.LoadPrefabContents(Folder + "/StatBox.prefab");
        try
        {
            root.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 12, 12);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/StatBox.prefab");
            return new { saved = true };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
