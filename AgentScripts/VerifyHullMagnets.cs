using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class HullMagnetPickupProbe : CollectiblePickup
{
    public ParentShip ExpectedOwner;
    protected override bool TryApplyCollection(ParentShip ship) => ship == ExpectedOwner;
    protected override bool CountsAsBonus => false;
    protected override void RemoveFromWorld() { }
}

public static class VerifyHullMagnets
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0; int hulls = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefub/Ships", "Assets/GameContent/Hulls/Gameplay" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<ParentShip>() == null) continue;
            var clone = UnityEngine.Object.Instantiate(asset);
            var pickupObject = new GameObject("Magnet pickup verification");
            try
            {
                clone.transform.position = new Vector3(10000f + hulls * 10f, 10000f);
                var ship = clone.GetComponent<ParentShip>();
                ship.Awake(); ship.IsVisible = true;
                var magnets = clone.GetComponentsInChildren<Magnite>(true);
                Check(magnets.Length == 1 && magnets[0].gameObject != clone, "Separate magnet: " + path);
                var magnet = magnets[0]; Invoke(magnet, "Awake");
                var hitbox = ship.DamageHitboxCollider as CircleCollider2D;
                Check(hitbox != null && Mathf.Approximately(hitbox.radius, .05f) && hitbox.offset == Vector2.zero,
                    "Awake preserves physical hitbox: " + path);
                var zone = magnet.GetComponent<CircleCollider2D>();
                Check(zone != null && zone.isTrigger && zone.enabled && zone != hitbox, "Pickup trigger: " + path);
                Check(magnet.gameObject.layer == LayerMask.NameToLayer("Magnet"), "Magnet layer: " + path);
                Check(Mathf.Approximately(zone.radius, .2f) && Mathf.Approximately(magnet.AttractionRadius, 2f)
                    && zone.offset == new Vector2(0f, .15f), "Arkanoid settings: " + path);
                Check(clone.GetComponent<PlayerHitboxIndicator>().VisualRadius == .075f, "Visual preserved: " + path);
                Check(new SerializedObject(ship).FindProperty("buffMagnet").objectReferenceValue == magnet,
                    "Explicit owner reference: " + path);
                var pickup = pickupObject.AddComponent<HullMagnetPickupProbe>(); pickup.ExpectedOwner = ship;
                var pickupCollider = pickupObject.AddComponent<CircleCollider2D>();
                pickupCollider.isTrigger = true; pickupCollider.radius = .01f;
                pickupObject.transform.position = (Vector3)(magnet.GetMagnetCenter() + Vector2.right * .9f);
                Physics2D.SyncTransforms();
                float before = Vector2.Distance(pickup.transform.position, magnet.GetMagnetCenter());
                Invoke(magnet, "FixedUpdate");
                Check(pickup.IsMagneticallyAttracted && !pickup.IsCollected
                    && Vector2.Distance(pickup.transform.position, magnet.GetMagnetCenter()) < before,
                    "Attract pickup beyond absorption radius: " + path);
                for (int i = 0; i < 10 && !pickup.IsCollected; i++)
                { Physics2D.SyncTransforms(); Invoke(magnet, "FixedUpdate"); }
                Check(pickup.IsCollected, "Collect attracted pickup: " + path);
                ship.MultiplyMagnetRadiusForSeconds(2f, 5f);
                Check(Mathf.Approximately(zone.radius, .4f) && Mathf.Approximately(hitbox.radius, .05f),
                    "Magnet bonus does not resize hitbox: " + path);
                hulls++;
            }
            finally { UnityEngine.Object.DestroyImmediate(pickupObject); UnityEngine.Object.DestroyImmediate(clone); }
        }
        var fallback = new GameObject("Unconfigured hull verification");
        var legacy = new GameObject("Legacy root magnet verification");
        try
        {
            var hitbox = fallback.AddComponent<CircleCollider2D>(); hitbox.radius = .05f;
            var ship = fallback.AddComponent<ParentShip>(); Invoke(ship, "EnsureBuffMagnet");
            var magnet = fallback.GetComponentInChildren<Magnite>(true); Invoke(magnet, "Awake");
            Check(magnet.gameObject != fallback && magnet.GetComponent<CircleCollider2D>().isTrigger,
                "Runtime fallback creates child sensor");
            Check(Mathf.Approximately(hitbox.radius, .05f), "Runtime fallback preserves hitbox");
            var legacyHitbox = legacy.AddComponent<CircleCollider2D>(); legacyHitbox.radius = .05f;
            legacy.AddComponent<ParentShip>(); var legacyMagnet = legacy.AddComponent<Magnite>();
            Invoke(legacyMagnet, "Awake"); Invoke(legacyMagnet, "OnValidate");
            legacyMagnet.ActivateRadiusMultiplier(3f, 5f);
            Check(Mathf.Approximately(legacyHitbox.radius, .05f) && legacyHitbox.offset == Vector2.zero
                && !legacyHitbox.isTrigger, "Legacy root magnet never changes damage collider");
        }
        finally { UnityEngine.Object.DestroyImmediate(fallback); UnityEngine.Object.DestroyImmediate(legacy); }
        return new { hulls, passed = checks, absorptionRadius = .2f, attractionRadius = 2f, hitboxRadius = .05f };
    }
    private static void Invoke(object value, string name) => value.GetType().GetMethod(name, Private).Invoke(value, null);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
}
