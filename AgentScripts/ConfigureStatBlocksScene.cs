using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureStatBlocksScene
{
    const string Craft = "Canvas/ScreenRoot/CraftMenu";
    static Transform Find(string path)
    {
        var parts = path.Split('/');
        var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(r => r.name == parts[0]).transform;
        return parts.Length == 1 ? root : root.Find(path.Substring(parts[0].Length + 1))
            ?? throw new Exception("Missing scene object: " + path);
    }
    static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); var property = so.FindProperty(field);
        if (property == null) throw new Exception("Missing field: " + field);
        property.objectReferenceValue = value; so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(target); EditorUtility.SetDirty(target);
    }
    static object Panel(string stage, string infoName, string panelName)
    {
        var stageObject = Find(Craft + "/" + stage);
        var info = stageObject.Find("HandlerTop/" + infoName);
        var middle = (RectTransform)info.Find("Middle");
        var panel = (RectTransform)middle.Find(panelName);
        var view = panel.GetComponent<StatBlocksView>();
        if (view == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject) != StatBlocksPreviewWindow.PanelPath)
            throw new Exception("Expected existing StatBlocksPanel prefab instance: " + panelName);
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(StatBlocksPreviewWindow.PanelPath).GetComponent<StatBlocksView>();
        var templateData = new SerializedObject(template);
        Reference(view, "cellPrefab", templateData.FindProperty("cellPrefab").objectReferenceValue);
        Reference(view, "rowPrefab", templateData.FindProperty("rowPrefab").objectReferenceValue);
        Reference(view, "baseStatsRoot", panel.Find("Base Stats"));
        Reference(view, "secondaryStatsRoot", panel.Find("Secondary Stats"));
        Reference(view, "meta", null); Reference(view, "runtimeWeapon", null); Reference(view, "runtimeShip", null);

        Undo.RecordObject(panel, "Fit stat panel to info area");
        panel.anchorMin = new Vector2(0f, 1f); panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(.5f, 1f); panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = Vector2.zero; panel.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel);

        var mask = middle.GetComponent<RectMask2D>();
        if (mask == null) mask = Undo.AddComponent<RectMask2D>(middle.gameObject);
        var scroll = middle.GetComponent<ScrollRect>();
        if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(middle.gameObject);
        Undo.RecordObject(scroll, "Configure stat scrolling");
        scroll.content = panel; scroll.viewport = middle;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.verticalNormalizedPosition = 1f;
        EditorUtility.SetDirty(scroll);
        if (stage == "SelectBody")
        {
            var controller = info.GetComponent<HullSelectionInfoController>();
            Reference(controller, "statBlocksView", view);
            Reference(stageObject.GetComponent<HullSelectionListController>(), "infoView", controller);
        }
        else
        {
            var controller = info.GetComponent<WeaponSelectionInfoController>();
            Reference(controller, "statBlocksView", view);
            Reference(stageObject.GetComponent<WeaponSelectionListController>(), "infoView", controller);
        }
        return new { panel = Craft + "/" + stage + "/HandlerTop/" + infoName + "/Middle/" + panelName,
            width = panel.rect.width, scrolling = true, existingPrefab = true };
    }
    static object Prefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var owner = root.GetComponent<CraftUIButton>();
            var button = root.GetComponentInChildren<Button>(true);
            if (owner == null || button == null) throw new Exception("Invalid content button prefab: " + path);
            Reference(owner, "button", button);
            if (button.gameObject != owner.gameObject)
            {
                var relay = button.GetComponent<CraftUIButtonNavigationRelay>();
                if (relay == null) relay = button.gameObject.AddComponent<CraftUIButtonNavigationRelay>();
                Reference(relay, "owner", owner);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return new { prefab = path, selectable = button.name, keyboardRelay = true };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/NewMainMenu.unity") throw new Exception("Expected open NewMainMenu scene.");
        Undo.SetCurrentGroupName("Connect crafting stat panels");
        var panels = new[] { Panel("SelectBody", "InfoBody", "StatBlocksPanelForHull"), Panel("SelectWeapon", "InfoWeapon", "StatBlocksPanelForWeapon") };
        var prefabs = new[] { Prefab("Assets/Prefub/UI/Common/Buttons/Craft.prefab"), Prefab("Assets/Prefub/UI/Common/Buttons/Weapon.prefab") };
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
        return new { scene = scene.path, saved = true, panels, prefabs };
    }
}
