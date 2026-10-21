using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ClearRuntimeWeaponSprites
{
    public static object Inspect()
    {
        RequireIdle();
        var found = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var weapon = root.GetComponent<Weapon>();
            if (weapon == null) continue;
            var sprites = new List<object>();
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                sprites.Add(new { node = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform),
                    sprite = renderer.sprite != null ? renderer.sprite.name : null });
            found.Add(new { path, sprites });
        }
        return new { count = found.Count, prefabs = found };
    }
    public static object Apply()
    {
        RequireIdle();
        var changed = new List<string>();
        int inspected = 0, cleared = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<Weapon>() == null) continue;
            inspected++;
            // Runtime weapon visuals are on the weapon root; children may be
            // live beam/effect visuals and are deliberately handled separately.
            var source = asset.GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                Undo.RecordObject(renderer, "Remove runtime weapon sprite");
                renderer.sprite = null;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path);
                cleared++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return new { inspected, cleared, changed };
    }
    private static void RequireIdle()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
    }
}
