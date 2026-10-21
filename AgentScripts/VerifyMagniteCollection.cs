using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated verification: all objects and resource data stay in memory, no player save is used.
public static class VerifyMagniteCollection
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");

        GameObject shipInstance = null;
        GameObject pickupInstance = null;
        GameObject verificationPickupInstance = null;
        ResourceCatalog catalog = null;
        ResourceDefinition metal = null;
        ResourceDefinition gold = null;
        ResourceDefinition chip = null;
        string savePath = Path.Combine(Application.temporaryCachePath,
            "magnite_collection_verification.json");
        try
        {
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefub/Ships/Arkanoid.prefab");
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<MetalPickup>(
                "Assets/Prefub/Buff/MetalPickup.prefab");
            shipInstance = UnityEngine.Object.Instantiate(shipPrefab);
            pickupInstance = UnityEngine.Object.Instantiate(pickupPrefab.gameObject);
            var ship = shipInstance.GetComponent<ParentShip>();
            var magnet = shipInstance.GetComponentInChildren<Magnite>(true);
            var magnetCollider = magnet.GetComponent<CircleCollider2D>();
            var hitbox = ship.DamageHitboxCollider;
            var pickup = pickupInstance.GetComponent<MetalPickup>();
            var pickupCollider = pickupInstance.GetComponent<Collider2D>();
            Check(magnet != null && magnet.transform != ship.transform,
                "Test requires child Magnet.");

            Invoke(magnet, "Awake");
            Check(ReferenceEquals(Get(magnet, "ownerShip"), ship),
                "Magnite must resolve ParentShip from its parent.");
            ConfigureWallet(pickup, out catalog, out metal, out gold, out chip, savePath);

            Vector2 center = magnet.GetMagnetCenter();
            pickupInstance.transform.position = center + Vector2.right * 1f;
            Physics2D.SyncTransforms();
            float metalBefore = ReadMetal(pickup);
            Invoke(typeof(CollectiblePickup), pickup, "OnTriggerEnter2D", magnetCollider);
            Check(ReadMetal(pickup) == metalBefore,
                "Entering Magnet must not collect metal.");
            Check(!pickup.IsCollected,
                "Magnet trigger must leave pickup active.");

            Vector2 beforePull = pickupInstance.transform.position;
            Invoke(magnet, "TryPullPickup", pickupCollider);
            Physics2D.SyncTransforms();
            Check(pickup.IsMagneticallyAttracted,
                "Magnet must mark pickup as attracted.");
            Check(Vector2.Distance(pickupInstance.GetComponent<Rigidbody2D>().position, center)
                < Vector2.Distance(beforePull, center),
                "Magnet must move pickup toward center.");
            Check(ReadMetal(pickup) == metalBefore,
                "Pickup outside absorption radius must not be collected.");

            pickupInstance.transform.position = center;
            Check(Vector2.Distance(pickupInstance.transform.position, center)
                <= magnet.MagnetRadius,
                "Pickup at Magnet center must be inside collection radius.");
            var acceptsCollision = typeof(CollectiblePickup).GetMethod(
                "IsDamageHitbox", PrivateStatic);
            Check(!(bool)acceptsCollision.Invoke(null, new object[] { magnetCollider, ship }),
                "Magnet must not be accepted as a direct collection hitbox.");
            Check((bool)acceptsCollision.Invoke(null, new object[] { hitbox, ship }),
                "Root damage hitbox must be accepted for direct collection.");

            verificationPickupInstance = new GameObject("Generic pickup verification");
            verificationPickupInstance.transform.position = center;
            CircleCollider2D verificationCollider =
                verificationPickupInstance.AddComponent<CircleCollider2D>();
            var verificationPickup =
                verificationPickupInstance.AddComponent<VerificationPickup>();
            ShipIntangibleState intangible = shipInstance
                .GetComponent<ShipIntangibleState>();
            if (intangible == null)
                intangible = shipInstance.AddComponent<ShipIntangibleState>();
            intangible.Enter();
            Check(ship.IsIntangible,
                "Test requires an intangible collector.");
            Invoke(magnet, "TryPullPickup", verificationCollider);
            Check(verificationPickup.CollectionCount == 1
                && verificationPickup.IsCollected,
                "Magnet collects a generic buff during invulnerability.");
            intangible.Exit();

            return new { magnetCenter = center.ToString("F3"),
                attractionRadius = magnet.AttractionRadius,
                absorptionRadius = magnet.MagnetRadius,
                checks = new[] { "Magnet contact does not collect", "Pickup moves toward center",
                    "Center is inside collection radius", "Only root hitbox allows direct collection",
                    "Magnet collects generic buff during invulnerability" } };
        }
        finally
        {
            if (pickupInstance != null) UnityEngine.Object.DestroyImmediate(pickupInstance);
            if (verificationPickupInstance != null)
                UnityEngine.Object.DestroyImmediate(verificationPickupInstance);
            if (shipInstance != null) UnityEngine.Object.DestroyImmediate(shipInstance);
            DestroyWalletAssets(catalog, metal, gold, chip);
            DeleteIfPresent(savePath);
        }
    }

    private static void ConfigureWallet(MetalPickup pickup, out ResourceCatalog catalog,
        out ResourceDefinition metal, out ResourceDefinition gold, out ResourceDefinition chip,
        string savePath)
    {
        catalog = ScriptableObject.CreateInstance<ResourceCatalog>();
        metal = Resource(ResourceKind.Metal);
        gold = Resource(ResourceKind.Gold);
        chip = Resource(ResourceKind.Chip);
        var catalogData = new SerializedObject(catalog);
        catalogData.FindProperty("metal").objectReferenceValue = metal;
        catalogData.FindProperty("gold").objectReferenceValue = gold;
        catalogData.FindProperty("chip").objectReferenceValue = chip;
        catalogData.ApplyModifiedPropertiesWithoutUndo();
        var wallet = new PlayerResourceWallet(catalog);
        Set(wallet, "loaded", true);
        Set(wallet, "savePath", savePath);
        Set(pickup, "resourceWallet", wallet);
    }

    private static ResourceDefinition Resource(ResourceKind kind)
    {
        var resource = ScriptableObject.CreateInstance<ResourceDefinition>();
        var data = new SerializedObject(resource);
        data.FindProperty("kind").enumValueIndex = (int)kind;
        data.ApplyModifiedPropertiesWithoutUndo();
        return resource;
    }

    private static int ReadMetal(MetalPickup pickup)
    {
        var wallet = (PlayerResourceWallet)Get(pickup, "resourceWallet");
        return wallet.Metal;
    }

    private static void DestroyWalletAssets(params UnityEngine.Object[] assets)
    {
        foreach (var asset in assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void Check(bool condition, string error)
    {
        if (!condition) throw new Exception(error);
    }

    private static object Get(object target, string field) =>
        target.GetType().GetField(field, Private).GetValue(target);
    private static T Get<T>(object target, string field) => (T)Get(target, field);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Invoke(
        Type type,
        object target,
        string method,
        params object[] args) =>
        type.GetMethod(method, Private).Invoke(target, args);
}

public sealed class VerificationPickup : CollectiblePickup
{
    public int CollectionCount { get; private set; }

    protected override bool TryApplyCollection(ParentShip collectorShip)
    {
        CollectionCount++;
        return true;
    }

    protected override void RemoveFromWorld()
    {
    }
}
