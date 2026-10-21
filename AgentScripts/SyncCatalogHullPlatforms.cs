using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Scoped asset migration. Run in the existing Editor; ShipData limits remain untouched.
public static class SyncCatalogHullPlatforms
{
    private const string CatalogPath = "Assets/GameContent/Catalogs/HullCatalog.asset";
    private const string BlackPreviewPath = "Assets/GameContent/Hulls/HighResVisual/BlackHoleHigh.prefab";

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");

        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>(CatalogPath);
        if (catalog == null || catalog.Hulls.Count != 8)
            throw new Exception("The audited catalogue has changed.");
        var blade = Find(catalog, "Assets/GameContent/Hulls/Data/BladeContent.asset");
        var shield = Find(catalog, "Assets/GameContent/Hulls/Data/ShieldContent.asset");
        var black = Find(catalog, "Assets/GameContent/Hulls/Data/BlackHoleContent.asset");
        Require(blade.Data.maximumWeaponCount == 3 && shield.Data.maximumWeaponCount == 6
            && black.Data.maximumWeaponCount == 3, "ShipData limits changed since audit.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(BlackPreviewPath) == null,
            "BlackHoleHigh already exists; inspect before running again.");
        Require(AssetDatabase.GetAssetPath(black.Prefab) == "Assets/GameContent/Hulls/HighResVisual/ArkanoidHigh.prefab",
            "Black Hole no longer uses the audited shared preview.");
        Require(blade.Prefab.GetComponent<HullLoadoutDefinition>().WeaponPlatforms.Count == 3,
            "Blade preview changed.");
        Require(shield.Prefab.GetComponent<HullLoadoutDefinition>() == null
            && shield.Prefab.GetComponentsInChildren<CraftWeaponSlotAnchor>(true).Length == 0,
            "Shield preview changed.");
        Require(blade.GameplayPrefab.GetComponent<HullLoadoutDefinition>() == null
            && shield.GameplayPrefab.GetComponent<HullLoadoutDefinition>() == null,
            "Missing gameplay loadouts were already changed.");
        CheckRemovable(black.Prefab, "3");
        CheckRemovable(black.GameplayPrefab, "3");

        var protectedHashes = new Dictionary<string, string>();
        foreach (var hull in catalog.Hulls)
            protectedHashes[AssetDatabase.GetAssetPath(hull.Data)] = Hash(AssetDatabase.GetAssetPath(hull.Data));
        protectedHashes[CatalogPath] = Hash(CatalogPath);
        protectedHashes[AssetDatabase.GetAssetPath(black.Prefab)] = Hash(AssetDatabase.GetAssetPath(black.Prefab));

        EditPrefab(blade.Prefab, root =>
        {
            var loadout = root.GetComponent<HullLoadoutDefinition>();
            foreach (var platform in loadout.WeaponPlatforms)
            {
                var anchor = platform.WeaponMount.GetComponent<CraftWeaponSlotAnchor>();
                Require(anchor != null, "Blade platform does not have a physical anchor.");
                var serialized = new SerializedObject(anchor);
                serialized.FindProperty("slotId").stringValue = platform.SlotId;
                serialized.ApplyModifiedProperties();
            }
        });

        var template = blade.Prefab.GetComponent<HullLoadoutDefinition>().WeaponPlatforms[0]
            .WeaponMount.GetComponent<CraftWeaponSlotAnchor>();
        EditPrefab(shield.Prefab, root =>
        {
            var loadout = Undo.AddComponent<HullLoadoutDefinition>(root);
            var mounts = new List<Transform>();
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 2; column++)
                {
                    var anchorObject = UnityEngine.Object.Instantiate(template.gameObject, root.transform);
                    Undo.RegisterCreatedObjectUndo(anchorObject, "Add catalogue hull weapon platform");
                    int index = mounts.Count;
                    anchorObject.name = "WeaponAnchor_" + index;
                    anchorObject.transform.localPosition = new Vector3(column == 0 ? -0.26f : 0.26f, 0.3f - row * 0.3f, 0f);
                    anchorObject.transform.localRotation = Quaternion.identity;
                    var anchor = anchorObject.GetComponent<CraftWeaponSlotAnchor>();
                    var serialized = new SerializedObject(anchor);
                    serialized.FindProperty("slotId").stringValue = index.ToString();
                    serialized.FindProperty("weaponMount").objectReferenceValue = anchor.transform;
                    serialized.FindProperty("buttonOffset").vector2Value = Vector2.zero;
                    serialized.FindProperty("buttonSize").vector2Value = new Vector2(64, 64);
                    serialized.ApplyModifiedProperties();
                    mounts.Add(anchor.transform);
                }
            Configure(loadout, shield.Data, mounts, new Vector2(64, 64), template.WeaponIconScale);
        });

        // A separate variant is required: Arkanoid, Huskar and Ghost retain the shared four-slot visual.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(black.Prefab);
        GameObject newPreview;
        try
        {
            instance.name = "BlackHoleHigh";
            newPreview = PrefabUtility.SaveAsPrefabAsset(instance, BlackPreviewPath, out bool saved);
            Require(saved && newPreview != null, "Could not create Black Hole preview variant.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        EditPrefab(newPreview, root => RemovePlatform(root, black.Data, "3"));
        Undo.RecordObject(black, "Assign three-slot Black Hole preview");
        var blackSerialized = new SerializedObject(black);
        blackSerialized.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BlackPreviewPath);
        blackSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(black);

        AddGameplayMounts(blade);
        AddGameplayMounts(shield);
        EditPrefab(black.GameplayPrefab, root => RemovePlatform(root, black.Data, "3"));
        AssetDatabase.SaveAssets();
        foreach (var protectedFile in protectedHashes)
            Require(Hash(protectedFile.Key) == protectedFile.Value, "Unexpected change to protected asset: " + protectedFile.Key);
        return new { changed = new[] { AssetDatabase.GetAssetPath(blade.Prefab), AssetDatabase.GetAssetPath(shield.Prefab),
            BlackPreviewPath, AssetDatabase.GetAssetPath(black), AssetDatabase.GetAssetPath(blade.GameplayPrefab),
            AssetDatabase.GetAssetPath(shield.GameplayPrefab), AssetDatabase.GetAssetPath(black.GameplayPrefab) },
            protectedAssetsUnchanged = protectedHashes.Count };
    }

    private static void AddGameplayMounts(HullContentDefinition hull)
    {
        var preview = hull.Prefab.GetComponent<HullLoadoutDefinition>();
        EditPrefab(hull.GameplayPrefab, root =>
        {
            var loadout = Undo.AddComponent<HullLoadoutDefinition>(root);
            var mounts = new List<Transform>();
            foreach (var platform in preview.WeaponPlatforms)
            {
                var mount = new GameObject("WeaponMount_" + platform.SlotId);
                Undo.RegisterCreatedObjectUndo(mount, "Add catalogue hull weapon mount");
                mount.transform.SetParent(root.transform, false);
                mount.transform.localPosition = hull.Prefab.transform.InverseTransformPoint(platform.WeaponMount.position);
                mounts.Add(mount.transform);
            }
            Configure(loadout, hull.Data, mounts, new Vector2(64, 64), Vector2.one);
        });
    }

    private static void Configure(HullLoadoutDefinition loadout, ShipData data, List<Transform> mounts,
        Vector2 buttonSize, Vector2 iconScale)
    {
        var serialized = new SerializedObject(loadout);
        serialized.FindProperty("shipData").objectReferenceValue = data;
        var platforms = serialized.FindProperty("weaponPlatforms");
        platforms.arraySize = mounts.Count;
        for (int i = 0; i < mounts.Count; i++)
        {
            var platform = platforms.GetArrayElementAtIndex(i);
            platform.FindPropertyRelative("slotId").stringValue = i.ToString();
            platform.FindPropertyRelative("weaponMount").objectReferenceValue = mounts[i];
            platform.FindPropertyRelative("previewButtonOffset").vector2Value = Vector2.zero;
            platform.FindPropertyRelative("previewButtonSize").vector2Value = buttonSize;
            platform.FindPropertyRelative("previewWeaponIconScale").vector2Value = iconScale;
            var tiers = platform.FindPropertyRelative("maxWeaponTierByHullUpgradeLevel");
            tiers.arraySize = 1;
            tiers.GetArrayElementAtIndex(0).intValue = 1;
        }
        serialized.ApplyModifiedProperties();
        Require(loadout.IsConfigurationValid(out string error), error);
    }

    private static void RemovePlatform(GameObject root, ShipData data, string slotId)
    {
        CheckRemovable(root, slotId);
        var loadout = root.GetComponent<HullLoadoutDefinition>();
        Require(loadout.TryGetWeaponPlatform(slotId, out var extra), "Surplus platform missing.");
        var physicalMount = extra.WeaponMount.gameObject;
        var serialized = new SerializedObject(loadout);
        serialized.FindProperty("shipData").objectReferenceValue = data;
        var platforms = serialized.FindProperty("weaponPlatforms");
        for (int i = platforms.arraySize - 1; i >= 0; i--)
            if (platforms.GetArrayElementAtIndex(i).FindPropertyRelative("slotId").stringValue == slotId)
                platforms.DeleteArrayElementAtIndex(i);
        serialized.ApplyModifiedProperties();
        Undo.DestroyObjectImmediate(physicalMount);
        Require(loadout.WeaponPlatforms.Count == data.maximumWeaponCount && loadout.IsConfigurationValid(out _),
            "Reduced loadout is invalid.");
    }

    private static void CheckRemovable(GameObject root, string slotId)
    {
        var loadout = root.GetComponent<HullLoadoutDefinition>();
        Require(loadout != null && loadout.WeaponPlatforms.Count == 4
            && loadout.TryGetWeaponPlatform(slotId, out _), "Expected audited four-slot loadout.");
        loadout.TryGetWeaponPlatform(slotId, out var platform);
        var mount = platform.WeaponMount;
        Require(mount != null && mount != root.transform && mount.IsChildOf(root.transform)
            && mount.childCount == 0, "Unsafe surplus platform hierarchy.");
        foreach (var component in mount.GetComponents<Component>())
            Require(component is Transform || component is SpriteRenderer || component is CraftWeaponSlotAnchor,
                "Surplus mount has gameplay components.");
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component == loadout || component.gameObject == mount.gameObject) continue;
            var serialized = new SerializedObject(component);
            var field = serialized.GetIterator();
            while (field.Next(true))
                if (field.propertyType == SerializedPropertyType.ObjectReference)
                    Require(field.objectReferenceValue != mount && field.objectReferenceValue != mount.gameObject,
                        "Surplus mount is referenced by " + component.GetType().Name + "." + field.propertyPath);
        }
    }

    private static void EditPrefab(GameObject prefab, Action<GameObject> edit)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            Require(saved, "Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static HullContentDefinition Find(HullCatalog catalog, string path)
    {
        foreach (var hull in catalog.Hulls)
            if (hull != null && AssetDatabase.GetAssetPath(hull) == path) return hull;
        throw new Exception("Hull is not in catalogue: " + path);
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new Exception(error);
    }
}
