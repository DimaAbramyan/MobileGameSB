using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class InspectStatBlocksScene
{
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    static object Refs(MonoBehaviour component)
    {
        var refs = new System.Collections.Generic.Dictionary<string, string>();
        var so = new SerializedObject(component); var p = so.GetIterator();
        while (p.NextVisible(true)) if (p.propertyType == SerializedPropertyType.ObjectReference)
        {
            var value = p.objectReferenceValue;
            refs[p.propertyPath] = value is Component c ? Path(c.transform) : value is GameObject go ? Path(go.transform) : value != null ? AssetDatabase.GetAssetPath(value) : "NULL";
        }
        return refs;
    }
    public static object Run()
    {
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        return new {
            scene = scene.path,
            buttonPrefabs = all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c is WeaponSelectionListController || c is HullSelectionListController)
                .Select(c => { var prefab = (CraftUIButton)new SerializedObject(c).FindProperty("craftButtonPrefab").objectReferenceValue;
                    var b = (UnityEngine.UI.Button)new SerializedObject(prefab).FindProperty("button").objectReferenceValue;
                    return new { prefab = AssetDatabase.GetAssetPath(prefab), root = Path(prefab.transform), button = b != null ? Path(b.transform) : "NULL" }; }).ToArray(),
            panels = all.Where(t => t.name.Contains("StatBlocksPanel")).Select(t => new {
                path = Path(t), active = t.gameObject.activeSelf, size = (t as RectTransform)?.rect.size.ToString(),
                components = t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray(),
                children = t.Cast<Transform>().Select(c => c.name).ToArray(), prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)
                , ancestry = new[] {t, t.parent, t.parent.parent}.Select(p => new { path = Path(p), rect = ((RectTransform)p).rect.ToString(),
                    anchorMin=((RectTransform)p).anchorMin.ToString(), anchorMax=((RectTransform)p).anchorMax.ToString(), size=((RectTransform)p).sizeDelta.ToString(),
                    position=((RectTransform)p).anchoredPosition.ToString(), components = p.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray() }).ToArray()
            }).ToArray(),
            controls = all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c is WeaponSelectionInfoController || c is HullSelectionInfoController
                || c is WeaponSelectionListController || c is HullSelectionListController || c is HangarCatalogController || c is HullPreviewController || c is CraftCreationFlowController)
                .Select(c => new { path = Path(c.transform), type = c.GetType().Name, references = Refs(c) }).ToArray()
        };
    }
}
