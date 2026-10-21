using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AuditCatalogHullPlatforms
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var catalogs = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:HullCatalog"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>(path);
            var rows = new List<object>();
            foreach (HullContentDefinition hull in catalog.Hulls)
            {
                if (hull == null) { rows.Add(new { error = "Null hull entry" }); continue; }
                GameObject prefab = hull.GameplayPrefab;
                HullLoadoutDefinition loadout = prefab != null ? prefab.GetComponent<HullLoadoutDefinition>() : null;
                var mounts = new List<object>();
                if (loadout != null)
                    foreach (HullWeaponPlatform platform in loadout.WeaponPlatforms)
                        mounts.Add(new { id = platform.SlotId, mount = Hierarchy(platform.WeaponMount, prefab.transform),
                            position = platform.WeaponMount != null ? platform.WeaponMount.localPosition.ToString("F3") : null,
                            parent = platform.WeaponMount != null && platform.WeaponMount.parent != null ? Hierarchy(platform.WeaponMount.parent, prefab.transform) : null,
                            tier = platform.GetMaxWeaponTier(0) });
                var hierarchy = new List<object>();
                if (prefab != null)
                    foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                        hierarchy.Add(new { path = Hierarchy(t, prefab.transform), position = t.localPosition.ToString("F3"),
                            components = Components(t.gameObject) });
                rows.Add(new { name = hull.DisplayName, id = hull.Id, hull = AssetDatabase.GetAssetPath(hull),
                    data = AssetDatabase.GetAssetPath(hull.Data), expected = hull.Data != null ? hull.Data.maximumWeaponCount : -1,
                    energy = hull.Data != null ? hull.Data.MetaMaximumEnergy : -1,
                    prefab = AssetDatabase.GetAssetPath(prefab), platforms = loadout != null ? loadout.WeaponPlatforms.Count : -1,
                    dataMatches = loadout != null && loadout.ShipData == hull.Data,
                    valid = loadout != null && loadout.IsConfigurationValid(out _), mounts, hierarchy,
                    preview = Preview(hull.Prefab) });
            }
            catalogs.Add(new { path, rows });
        }
        var references = new List<object>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component); var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is HullCatalog catalog)
                            references.Add(new { scene = scene.path, component = component.GetType().Name,
                                field = it.propertyPath, catalog = AssetDatabase.GetAssetPath(catalog) });
                }
        }
        return new { catalogs, references };
    }
    private static object Preview(GameObject prefab)
    {
        if (prefab == null) return null;
        var loadout = prefab.GetComponent<HullLoadoutDefinition>();
        var anchors = new List<object>();
        foreach (CraftWeaponSlotAnchor anchor in prefab.GetComponentsInChildren<CraftWeaponSlotAnchor>(true))
            anchors.Add(new { id = anchor.SlotId, path = Hierarchy(anchor.transform, prefab.transform),
                mount = Hierarchy(anchor.WeaponMount, prefab.transform),
                position = anchor.transform.localPosition.ToString("F3"),
                scale = anchor.transform.localScale.ToString("F3"), size = anchor.ButtonSize.ToString("F3"),
                components = Components(anchor.gameObject) });
        var layout = new List<object>();
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
            layout.Add(new { path = Hierarchy(t, prefab.transform), position = t.localPosition.ToString("F3"),
                scale = t.localScale.ToString("F3"), components = Components(t.gameObject) });
        var mounts = new List<object>();
        if (loadout != null)
            foreach (var platform in loadout.WeaponPlatforms)
                mounts.Add(new { id = platform.SlotId, mount = Hierarchy(platform.WeaponMount, prefab.transform),
                    offset = platform.PreviewButtonOffset.ToString("F3"), size = platform.PreviewButtonSize.ToString("F3"),
                    icon = platform.PreviewWeaponIconScale.ToString("F3") });
        var renderer = prefab.GetComponent<SpriteRenderer>();
        return new { path = AssetDatabase.GetAssetPath(prefab), anchors, layout, mounts,
            data = loadout != null ? AssetDatabase.GetAssetPath(loadout.ShipData) : null,
            valid = loadout != null && loadout.IsConfigurationValid(out _),
            sprite = renderer != null && renderer.sprite != null ? renderer.sprite.bounds.ToString() : null,
            platforms = loadout != null ? loadout.WeaponPlatforms.Count : -1 };
    }

    private static string Hierarchy(Transform t, Transform root)
    {
        if (t == null) return null;
        string path = t.name;
        while (t != root && t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
    private static string[] Components(GameObject gameObject)
    {
        var components = gameObject.GetComponents<Component>(); var names = new string[components.Length];
        for (int i = 0; i < components.Length; i++) names[i] = components[i] != null ? components[i].GetType().Name : "MissingScript";
        return names;
    }
}
