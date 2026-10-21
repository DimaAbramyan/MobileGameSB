using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigureAllHullCircleHitboxes
{
    private static readonly string[] Folders = { "Assets/Prefub/Ships", "Assets/GameContent/Hulls/Gameplay" };
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Arkanoid.prefab");
        var templateCollider = template.GetComponent<CircleCollider2D>();
        var templateVisual = template.GetComponent<PlayerHitboxIndicator>();
        if (templateCollider == null || templateVisual == null || templateVisual.VisualRadius <= templateCollider.radius)
            throw new Exception("Arkanoid circle and larger visual are required.");
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponent<ParentShip>() == null) continue;
            var colliders = prefab.GetComponents<Collider2D>();
            if (colliders.Length != 1 || !(colliders[0] is BoxCollider2D || colliders[0] is CircleCollider2D))
                throw new Exception("Expected one box or circle on hull root: " + path);
            paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);
        var changed = new List<string>(); int preservedChildren = 0;
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var oldCollider = root.GetComponent<Collider2D>();
                var children = new Dictionary<Collider2D, string>();
                foreach (var child in root.GetComponentsInChildren<Collider2D>(true))
                    if (child.gameObject != root) children.Add(child, EditorJsonUtility.ToJson(child));
                var circle = oldCollider as CircleCollider2D;
                var visual = root.GetComponent<PlayerHitboxIndicator>();
                var ship = root.GetComponent<ParentShip>();
                bool needsChange = circle == null || circle.radius != templateCollider.radius
                    || circle.offset != templateCollider.offset || visual == null
                    || visual.VisualRadius != templateVisual.VisualRadius || ship.DamageHitboxCollider != circle;
                if (!needsChange) continue;
                if (circle == null)
                {
                    circle = Undo.AddComponent<CircleCollider2D>(root);
                    var source = new SerializedObject(oldCollider);
                    var destination = new SerializedObject(circle);
                    var property = source.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.depth != 0 || property.name == "m_GameObject"
                            || property.name.StartsWith("m_Prefab") || property.name == "m_CorrespondingSourceObject"
                            || property.name == "m_ObjectHideFlags") continue;
                        var target = destination.FindProperty(property.propertyPath);
                        if (target != null && target.propertyType == property.propertyType)
                            destination.CopyFromSerializedProperty(property);
                    }
                    destination.ApplyModifiedProperties();
                    // Replace references to the root hitbox on components, not child physics shapes.
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null || component == oldCollider || component == circle) continue;
                        var so = new SerializedObject(component);
                        var field = so.GetIterator(); bool dirty = false;
                        while (field.NextVisible(true))
                        {
                            if (field.propertyType != SerializedPropertyType.ObjectReference
                                || field.objectReferenceValue != oldCollider) continue;
                            Undo.RecordObject(component, "Replace hull hitbox reference");
                            field.objectReferenceValue = circle; dirty = true;
                        }
                        if (dirty) so.ApplyModifiedProperties();
                    }
                    Undo.DestroyObjectImmediate(oldCollider);
                }
                Undo.RecordObject(circle, "Round hull damage hitbox");
                circle.radius = templateCollider.radius; circle.offset = templateCollider.offset;
                if (visual == null) visual = Undo.AddComponent<PlayerHitboxIndicator>(root);
                Undo.RecordObject(visual, "Larger visual hull hitbox");
                var visualSo = new SerializedObject(visual);
                visualSo.FindProperty("visualRadius").floatValue = templateVisual.VisualRadius;
                visualSo.ApplyModifiedProperties();
                Undo.RecordObject(ship, "Bind hull damage hitbox");
                var shipSo = new SerializedObject(ship);
                shipSo.FindProperty("damageHitboxCollider").objectReferenceValue = circle;
                shipSo.ApplyModifiedProperties();
                foreach (var child in children)
                {
                    if (EditorJsonUtility.ToJson(child.Key) != child.Value)
                        throw new Exception("Child collider changed: " + path + "/" + child.Key.name);
                    preservedChildren++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new Exception("Save failed: " + path);
                changed.Add(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var circle = prefab.GetComponent<CircleCollider2D>();
            var visual = prefab.GetComponent<PlayerHitboxIndicator>();
            if (prefab.GetComponents<Collider2D>().Length != 1 || circle == null || visual == null
                || circle.radius != templateCollider.radius || circle.offset != templateCollider.offset
                || visual.VisualRadius != templateVisual.VisualRadius
                || prefab.GetComponent<ParentShip>().DamageHitboxCollider != circle)
                throw new Exception("Saved hull hitbox mismatch: " + path);
        }
        return new { hulls = paths.Count, changed, preservedChildren,
            physicalRadius = templateCollider.radius, visualRadius = templateVisual.VisualRadius };
    }
}
