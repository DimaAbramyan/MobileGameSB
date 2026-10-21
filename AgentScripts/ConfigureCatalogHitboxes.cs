using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ConfigureCatalogHitboxes
{
    private const string CatalogPath = "Assets/GameContent/Catalogs/HullCatalog.asset";
    public static object Audit() => Run(false);
    public static object Apply() => Run(true);

    private static object Run(bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>(CatalogPath);
        var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/GameContent/Catalogs/WeaponCatalog.asset");
        var service = new ContentCatalogService(catalog, weapons);
        var ids = new HashSet<string>();
        var shipIds = new HashSet<int>();
        var paths = new HashSet<string>();
        // Validate all mappings before writing any prefab; never renumber persistent IDs by catalogue index.
        foreach (var hull in catalog.Hulls)
        {
            Require(hull != null && hull.Data != null && hull.GameplayPrefab != null && hull.Prefab != null, "Missing hull reference.");
            Require(ids.Add(hull.Id) && shipIds.Add(hull.Data.shipId), "Duplicate hull identity: " + hull.name);
            Require(hull.GameplayPrefab.GetComponent<ParentShip>().ShipData == hull.Data, "Gameplay ShipData mismatch: " + hull.name);
            Require(service.TryGetHull(hull.Id, hull.Data.shipId, out var resolved) && resolved == hull, "Saved blueprint resolves a different hull: " + hull.name);
            var preview = hull.Prefab.GetComponent<HullLoadoutDefinition>();
            var gameplay = hull.GameplayPrefab.GetComponent<HullLoadoutDefinition>();
            Require(preview != null && gameplay != null && preview.ShipData == hull.Data && gameplay.ShipData == hull.Data, "Loadout ShipData mismatch: " + hull.name);
            Require(preview.IsConfigurationValid(out _) && gameplay.IsConfigurationValid(out _), "Invalid hull platforms: " + hull.name);
            Require(preview.WeaponPlatforms.Count == gameplay.WeaponPlatforms.Count, "Slot count mismatch: " + hull.name);
            foreach (var platform in preview.WeaponPlatforms)
                Require(gameplay.TryGetWeaponPlatform(platform.SlotId, out _), "Slot ID mismatch: " + hull.name);
            foreach (var prefab in new[] { hull.Prefab, hull.GameplayPrefab })
            {
                var body = prefab.GetComponentInChildren<BodyData>(true);
                if (body != null)
                    Require(body.VisualConfig != null && body.VisualConfig.ShipData == hull.Data && body.ShipId == hull.Data.shipId, "Visual blueprint mismatch: " + hull.name);
            }
            var meta = hull.Data.ShipMetaConfig;
            Require(meta != null && meta.SourceShipData == hull.Data && meta.SourceHullPrefab != null && meta.SourceHullPrefab.ShipData == hull.Data, "Meta mismatch: " + hull.name);
            if (!hull.IsAvailableForCrafting) continue;
            string path = AssetDatabase.GetAssetPath(hull.GameplayPrefab);
            Require(paths.Add(path), "Duplicate gameplay prefab: " + path);
            var circle = hull.GameplayPrefab.GetComponent<CircleCollider2D>();
            Require(circle != null && hull.GameplayPrefab.GetComponents<Collider2D>().Length == 1, "Expected one root circle: " + path);
            Require(hull.GameplayPrefab.GetComponent<ParentShip>().DamageHitboxCollider == circle, "Damage collider reference mismatch: " + path);
            Require(hull.GameplayPrefab.transform.localScale == Vector3.one, "Unexpected root scale: " + path);
        }

        var rows = new List<object>();
        int changed = 0;
        for (int index = 0; index < catalog.Hulls.Count; index++)
        {
            var hull = catalog.Hulls[index];
            if (!hull.IsAvailableForCrafting) continue;
            string path = AssetDatabase.GetAssetPath(hull.GameplayPrefab);
            if (apply)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var circle = root.GetComponent<CircleCollider2D>();
                    var colliderBefore = EditorJsonUtility.ToJson(circle);
                    var children = new Dictionary<Collider2D, string>();
                    foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                        if (collider != circle) children.Add(collider, EditorJsonUtility.ToJson(collider));
                    bool enabled = circle.enabled;
                    bool trigger = circle.isTrigger;
                    Vector2 offset = circle.offset;
                    Undo.RecordObject(circle, "Configure player damage hitbox");
                    circle.radius = 0.05f;
                    var indicator = root.GetComponent<PlayerHitboxIndicator>();
                    bool added = indicator == null;
                    if (added) indicator = Undo.AddComponent<PlayerHitboxIndicator>(root);
                    Undo.RecordObject(indicator, "Configure player visual hitbox");
                    var serialized = new SerializedObject(indicator);
                    serialized.FindProperty("visualRadius").floatValue = 0.075f;
                    serialized.ApplyModifiedProperties();
                    Require(circle.enabled == enabled && circle.isTrigger == trigger && circle.offset == offset, "Collider state changed: " + path);
                    foreach (var child in children)
                        Require(EditorJsonUtility.ToJson(child.Key) == child.Value, "Child collider changed: " + path);
                    EditorUtility.SetDirty(circle);
                    EditorUtility.SetDirty(indicator);
                    if (added || colliderBefore != EditorJsonUtility.ToJson(circle)) changed++;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    Require(saved, "Prefab save failed: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var damage = prefab.GetComponent<CircleCollider2D>();
            var visual = prefab.GetComponent<PlayerHitboxIndicator>();
            if (apply) Require(Mathf.Approximately(damage.radius, 0.05f) && visual != null && Mathf.Approximately(visual.VisualRadius, 0.075f), "Saved hitbox verification failed: " + path);
            rows.Add(new { index, hull = hull.DisplayName, contentId = hull.Id, shipId = hull.Data.shipId, path, damageRadius = damage.radius, visualRadius = visual != null ? visual.VisualRadius : -1f });
        }
        return new { hulls = rows.Count, changed, rows };
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
