using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyCatalogVisualHitboxes
{
    private static int checks;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        checks = 0;
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/GameContent/Catalogs/WeaponCatalog.asset");
        var reordered = ScriptableObject.CreateInstance<HullCatalog>();
        var rows = new List<string>();
        try
        {
            var serialized = new SerializedObject(reordered);
            var list = serialized.FindProperty("hulls");
            list.arraySize = catalog.Hulls.Count;
            for (int index = 0; index < catalog.Hulls.Count; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = catalog.Hulls[catalog.Hulls.Count - 1 - index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var service = new ContentCatalogService(reordered, weapons);
            foreach (var hull in catalog.Hulls)
            {
                Require(hull.Data.name != "Huskar", "Huskar remains in catalogue.");
                Require(service.TryGetHull(hull.Id, hull.Data.shipId, out var resolved) && resolved == hull,
                    "Reordering changes blueprint-to-gameplay mapping: " + hull.name);
                var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(hull.GameplayPrefab));
                try
                {
                    var ship = root.GetComponent<ParentShip>();
                    var collider = root.GetComponent<CircleCollider2D>();
                    var indicator = root.GetComponent<PlayerHitboxIndicator>();
                    Require(collider != null && collider.enabled && Mathf.Approximately(collider.radius, 0.05f), "Damage radius incorrect: " + hull.name);
                    Require(indicator != null && Mathf.Approximately(indicator.VisualRadius, 0.075f), "Visual radius incorrect: " + hull.name);
                    int colliderCount = root.GetComponentsInChildren<Collider2D>(true).Length;
                    string colliderSnapshot = EditorJsonUtility.ToJson(collider);
                    ship.IsVisible = false;
                    indicator.Initialize(ship, collider);
                    var visual = root.transform.Find("Visual Hitbox").GetComponent<LineRenderer>();
                    Require(visual != null && !visual.enabled && visual.loop, "Hidden ship shows visual hitbox: " + hull.name);
                    Require(root.GetComponentsInChildren<Collider2D>(true).Length == colliderCount, "Visual creates physics hitboxes: " + hull.name);
                    var center = indicator.transform.InverseTransformPoint(collider.transform.TransformPoint(collider.offset));
                    for (int index = 0; index < visual.positionCount; index++)
                        Require(Mathf.Abs(Vector3.Distance(center, visual.GetPosition(index)) - 0.075f) < 0.00001f, "Visual uses damage radius: " + hull.name);
                    Require(EditorJsonUtility.ToJson(collider) == colliderSnapshot, "Visual changes real collider: " + hull.name);
                    ship.IsVisible = true;
                    Tick(indicator);
                    Require(visual.enabled, "Selected ship marker hidden: " + hull.name);
                    collider.enabled = false;
                    Tick(indicator);
                    Require(!visual.enabled, "Intangible ship marker shown: " + hull.name);
                    collider.enabled = true;
                    Tick(indicator);
                    Require(visual.enabled, "Marker not restored: " + hull.name);
                    ship.IsVisible = false;
                    Tick(indicator);
                    Require(!visual.enabled, "Previous ship marker remains on switch: " + hull.name);
                    ship.IsVisible = true;
                    var visualSettings = new SerializedObject(indicator);
                    visualSettings.FindProperty("visualRadius").floatValue = 0.12f;
                    visualSettings.ApplyModifiedPropertiesWithoutUndo();
                    indicator.Initialize(ship, collider);
                    Require(Mathf.Approximately(visual.GetPosition(0).magnitude, 0.12f) && Mathf.Approximately(collider.radius, 0.05f), "Visual radius cannot change independently: " + hull.name);
                    Require(root.GetComponentsInChildren<LineRenderer>(true).Length == 1, "Reinitialization duplicates markers: " + hull.name);
                    rows.Add(hull.DisplayName);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(reordered); }
        return new { passed = checks, hulls = rows };
    }

    private static void Tick(PlayerHitboxIndicator indicator) => typeof(PlayerHitboxIndicator).GetMethod("LateUpdate", PrivateInstance).Invoke(indicator, null);
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
}
