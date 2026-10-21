using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigureHullMagnets
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Arkanoid.prefab")
            .GetComponentInChildren<Magnite>(true);
        var settings = new SerializedObject(template);
        var fields = new[] { "targetOffset", "pickupSnapDistance", "affectedLayers", "magnetRadius", "attractionRadius", "forceAmount" };
        var changed = new List<string>(); int hulls = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefub/Ships", "Assets/GameContent/Hulls/Gameplay" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<ParentShip>() == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ship = root.GetComponent<ParentShip>();
                var hitbox = ship.DamageHitboxCollider;
                string hitboxBefore = EditorJsonUtility.ToJson(hitbox);
                var magnets = root.GetComponentsInChildren<Magnite>(true);
                if (magnets.Length > 1) throw new Exception("Multiple magnets: " + path);
                Magnite magnet = magnets.Length == 1 ? magnets[0] : null;
                if (magnet != null && magnet.gameObject == root)
                    throw new Exception("Root magnet requires explicit migration: " + path);
                bool dirty = false;
                if (magnet == null)
                {
                    var child = new GameObject("Magnet");
                    Undo.RegisterCreatedObjectUndo(child, "Create separate hull magnet");
                    child.transform.SetParent(root.transform, false);
                    Undo.AddComponent<CircleCollider2D>(child).isTrigger = true;
                    magnet = Undo.AddComponent<Magnite>(child);
                    dirty = true;
                }
                var before = EditorJsonUtility.ToJson(magnet);
                Undo.RecordObject(magnet, "Match Arkanoid magnet settings");
                var destination = new SerializedObject(magnet);
                foreach (string field in fields)
                    destination.CopyFromSerializedProperty(settings.FindProperty(field));
                destination.ApplyModifiedProperties();
                dirty |= before != EditorJsonUtility.ToJson(magnet);
                if (magnet.gameObject.layer != template.gameObject.layer)
                {
                    Undo.RecordObject(magnet.gameObject, "Set magnet layer");
                    magnet.gameObject.layer = template.gameObject.layer; dirty = true;
                }
                var circle = magnet.GetComponent<CircleCollider2D>();
                if (circle == null) { circle = Undo.AddComponent<CircleCollider2D>(magnet.gameObject); dirty = true; }
                if (!circle.isTrigger || !Mathf.Approximately(circle.radius, template.MagnetRadius)
                    || circle.offset != (Vector2)settings.FindProperty("targetOffset").vector2Value)
                {
                    Undo.RecordObject(circle, "Configure pickup trigger");
                    circle.isTrigger = true; circle.radius = template.MagnetRadius;
                    circle.offset = settings.FindProperty("targetOffset").vector2Value; dirty = true;
                }
                var owner = new SerializedObject(ship);
                if (owner.FindProperty("buffMagnet").objectReferenceValue != magnet)
                {
                    Undo.RecordObject(ship, "Bind hull magnet");
                    owner.FindProperty("buffMagnet").objectReferenceValue = magnet;
                    owner.ApplyModifiedProperties(); dirty = true;
                }
                if (EditorJsonUtility.ToJson(hitbox) != hitboxBefore)
                    throw new Exception("Damage hitbox changed: " + path);
                if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, path); changed.Add(path); }
                hulls++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return new { hulls, changed, absorptionRadius = template.MagnetRadius, attractionRadius = template.AttractionRadius };
    }
}
