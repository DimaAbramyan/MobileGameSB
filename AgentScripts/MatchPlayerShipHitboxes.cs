using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Only the eight catalogue gameplay prefabs. Child Magnet/ability colliders are excluded.
public static class MatchPlayerShipHitboxes
{
    private const string CatalogPath = "Assets/GameContent/Catalogs/HullCatalog.asset";
    private static readonly Vector2 TargetSize = new Vector2(0.1f, 0.1f);

    public static object Audit() => Run(false);
    public static object Apply() => Run(true);

    private static object Run(bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>(CatalogPath);
        if (catalog == null || catalog.Hulls.Count != 8)
            throw new Exception("Expected eight player hulls in the catalogue.");
        var paths = new HashSet<string>();
        // Preflight every target before modifying any asset.
        foreach (var hull in catalog.Hulls)
        {
            if (hull == null || hull.GameplayPrefab == null)
                throw new Exception("Catalogue hull or gameplay prefab is missing.");
            string path = AssetDatabase.GetAssetPath(hull.GameplayPrefab);
            if (!paths.Add(path)) throw new Exception("Duplicate gameplay prefab: " + path);
            var ship = hull.GameplayPrefab.GetComponent<ParentShip>();
            var box = hull.GameplayPrefab.GetComponent<BoxCollider2D>();
            if (ship == null || box == null || ship.DamageHitboxCollider != box)
                throw new Exception("Expected a root BoxCollider2D damage hitbox: " + path);
        }

        var rows = new List<object>();
        int changed = 0;
        foreach (var hull in catalog.Hulls)
        {
            string path = AssetDatabase.GetAssetPath(hull.GameplayPrefab);
            var prefabBox = hull.GameplayPrefab.GetComponent<BoxCollider2D>();
            Vector2 beforeSize = prefabBox.size;
            Vector2 beforeOffset = prefabBox.offset;
            if (apply && beforeSize != TargetSize)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var box = root.GetComponent<BoxCollider2D>();
                    var childColliders = root.GetComponentsInChildren<Collider2D>(true);
                    var childSnapshots = new Dictionary<Collider2D, string>();
                    foreach (var child in childColliders)
                        if (child != box) childSnapshots.Add(child, EditorJsonUtility.ToJson(child));
                    Undo.RecordObject(box, "Set player ship hitbox to 0.1 x 0.1");
                    box.size = TargetSize;
                    if (box.offset != beforeOffset) throw new Exception("Offset changed unexpectedly: " + path);
                    foreach (var child in childSnapshots)
                        if (EditorJsonUtility.ToJson(child.Key) != child.Value)
                            throw new Exception("Child collider changed unexpectedly: " + path);
                    EditorUtility.SetDirty(box);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved) throw new Exception("Could not save " + path);
                    changed++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var after = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<BoxCollider2D>();
            if (apply && (after.size != TargetSize || after.offset != beforeOffset))
                throw new Exception("Saved collider verification failed: " + path);
            rows.Add(new { name = hull.DisplayName, path,
                beforeSize = beforeSize.ToString("F3"), size = after.size.ToString("F3"),
                offset = after.offset.ToString("F3"), offsetPreserved = after.offset == beforeOffset,
                rootScale = hull.GameplayPrefab.transform.localScale.ToString("F3") });
        }
        return new { playerShips = rows.Count, changed, rows };
    }
}
