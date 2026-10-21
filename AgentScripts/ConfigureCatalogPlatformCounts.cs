using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigureCatalogPlatformCounts
{
    public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>
    {
        { "Blade", 2 }, { "Black Hole", 2 }, { "Prism", 2 }, { "Arkanoid", 2 }, { "Ghost", 2 },
        { "Dictator", 3 }, { "Orbit", 3 }, { "Shield", 3 }
    };

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        if (catalog == null || catalog.Hulls.Count != Counts.Count) throw new Exception("Unexpected catalog coverage.");
        var changed = new List<string>(); var visited = new HashSet<string>();
        foreach (var hull in catalog.Hulls)
        {
            if (!Counts.TryGetValue(hull.DisplayName, out int count)) throw new Exception("Unmapped hull: " + hull.DisplayName);
            if (hull.Data == null) throw new Exception("Missing ShipData: " + hull.DisplayName);
            if (hull.Data.maximumWeaponCount != count)
            {
                Undo.RecordObject(hull.Data, "Set physical platform count");
                hull.Data.maximumWeaponCount = count;
                EditorUtility.SetDirty(hull.Data); AssetDatabase.SaveAssetIfDirty(hull.Data);
            }
            foreach (var asset in new[] { hull.GameplayPrefab, hull.Prefab })
            {
                if (asset == null) throw new Exception("Missing hull prefab: " + hull.DisplayName);
                string path = AssetDatabase.GetAssetPath(asset);
                if (!visited.Add(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var definition = root.GetComponent<HullLoadoutDefinition>();
                    if (definition == null || definition.ShipData != hull.Data) throw new Exception("Hull/data mismatch: " + path);
                    int oldCount = definition.WeaponPlatforms.Count;
                    if (oldCount < count) throw new Exception("Not enough existing platforms: " + path);
                    if (oldCount == count) continue;
                    string colliderBefore = root.GetComponent<Collider2D>() != null
                        ? EditorJsonUtility.ToJson(root.GetComponent<Collider2D>()) : null;
                    var oldPlatforms = definition.WeaponPlatforms;
                    var keptIds = new HashSet<string>();
                    var removedMounts = new HashSet<Transform>();
                    for (int i = 0; i < oldCount; i++)
                    {
                        var platform = oldPlatforms[i];
                        // A variant can retain a stale removed array entry after its base was resized.
                        if (i >= count)
                        {
                            if (platform != null && platform.WeaponMount != null && platform.WeaponMount != root.transform)
                                removedMounts.Add(platform.WeaponMount);
                            continue;
                        }
                        if (platform == null || platform.WeaponMount == null || platform.WeaponMount == root.transform)
                            throw new Exception("Unsafe platform reference: " + path);
                        keptIds.Add(platform.SlotId);
                    }
                    // Keep stable SlotIds. Use Blade's existing symmetric side mounts for its two slots.
                    if (hull.DisplayName == "Blade" && count == 2 && oldCount == 3)
                    {
                        Vector3 left = oldPlatforms[1].WeaponMount.localPosition;
                        Vector3 right = oldPlatforms[2].WeaponMount.localPosition;
                        Undo.RecordObject(oldPlatforms[0].WeaponMount, "Position Blade platform");
                        Undo.RecordObject(oldPlatforms[1].WeaponMount, "Position Blade platform");
                        oldPlatforms[0].WeaponMount.localPosition = left;
                        oldPlatforms[1].WeaponMount.localPosition = right;
                    }
                    else if (count == 3)
                    {
                        var mount = oldPlatforms[2].WeaponMount;
                        Undo.RecordObject(mount, "Center third physical platform");
                        Vector3 position = mount.localPosition; position.x = 0f; mount.localPosition = position;
                    }
                    var serialized = new SerializedObject(definition);
                    Undo.RecordObject(definition, "Resize physical platform list");
                    serialized.FindProperty("weaponPlatforms").arraySize = count;
                    serialized.ApplyModifiedProperties();
                    foreach (var anchor in root.GetComponentsInChildren<CraftWeaponSlotAnchor>(true))
                        if (!keptIds.Contains(anchor.SlotId)) Undo.DestroyObjectImmediate(anchor);
                    foreach (var mount in removedMounts)
                    {
                        if (mount.GetComponentInChildren<Weapon>(true) != null)
                            throw new Exception("Removed platform contains authored weapon: " + path);
                        Undo.DestroyObjectImmediate(mount.gameObject);
                    }
                    if (!definition.IsConfigurationValid(out string error)) throw new Exception(path + ": " + error);
                    if (colliderBefore != null && colliderBefore != EditorJsonUtility.ToJson(root.GetComponent<Collider2D>()))
                        throw new Exception("Hitbox changed: " + path);
                    PrefabUtility.SaveAsPrefabAsset(root, path); changed.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        return new { hulls = catalog.Hulls.Count, prefabs = visited.Count, changed };
    }
}
