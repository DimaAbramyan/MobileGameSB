using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FixBlackHoleRuntime
{
    public static object ApplyMagnet()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Arkanoid.prefab")
            .GetComponentInChildren<Magnite>(true);
        var referenceData = new SerializedObject(reference);
        var changed = new List<string>();
        foreach (var path in new[] { "Assets/Prefub/Ships/BlackHole.prefab",
            "Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var magnet = root.GetComponentInChildren<Magnite>(true);
                if (magnet == null) throw new Exception("Magnet missing: " + path);
                Undo.RecordObject(magnet, "Match Black Hole magnet to Arkanoid");
                var data = new SerializedObject(magnet);
                foreach (var field in new[] { "targetOffset", "magnetRadius", "attractionRadius" })
                    data.CopyFromSerializedProperty(referenceData.FindProperty(field));
                data.ApplyModifiedProperties();
                var collider = magnet.GetComponent<CircleCollider2D>();
                if (collider == null) throw new Exception("Magnet collider missing: " + path);
                Undo.RecordObject(collider, "Match Black Hole magnet collider");
                collider.radius = reference.MagnetRadius;
                collider.offset = referenceData.FindProperty("targetOffset").vector2Value;
                EditorUtility.SetDirty(magnet);
                EditorUtility.SetDirty(collider);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new Exception("Failed to save: " + path);
                changed.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return new { changed, absorptionRadius = reference.MagnetRadius,
            attractionRadius = reference.AttractionRadius,
            offset = referenceData.FindProperty("targetOffset").vector2Value.ToString() };
    }

    public static object Audit()
    {
        var paths = new HashSet<string> { "Assets/Prefub/Ships/Arkanoid.prefab" };
        foreach (var guid in AssetDatabase.FindAssets("BlackHole t:Prefab"))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        var rows = new List<object>();
        foreach (var path in paths)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var magnets = new List<object>();
            foreach (var magnet in root.GetComponentsInChildren<Magnite>(true))
                magnets.Add(new { name = magnet.name, data = EditorJsonUtility.ToJson(magnet),
                    scale = magnet.transform.lossyScale.ToString(), localScale = magnet.transform.localScale.ToString(),
                    position = magnet.transform.localPosition.ToString(),
                    collider = magnet.GetComponent<CircleCollider2D>() != null
                        ? EditorJsonUtility.ToJson(magnet.GetComponent<CircleCollider2D>()) : null });
            var colliders = new List<object>();
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                colliders.Add(new { name = collider.name, type = collider.GetType().Name,
                    trigger = collider.isTrigger, bounds = collider.bounds.ToString() });
            var ship = root.GetComponent<ParentShip>();
            rows.Add(new { path, parentPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                rootScale = root.transform.localScale.ToString(), magnets, colliders,
                passive = ship != null && ship.PassiveAbility != null ? ship.PassiveAbility.GetType().Name : null,
                shipData = ship != null ? AssetDatabase.GetAssetPath(ship.ShipData) : null });
        }
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        var hulls = new List<object>();
        foreach (var hull in catalog.Hulls)
            if (hull != null && hull.GameplayPrefab != null && hull.GameplayPrefab.GetComponent<BlackHolePassive>() != null)
                hulls.Add(new { name = hull.DisplayName, runtime = AssetDatabase.GetAssetPath(hull.GameplayPrefab) });
        return new { rows, hulls };
    }
}
