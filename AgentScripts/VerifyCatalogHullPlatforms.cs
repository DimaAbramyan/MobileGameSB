using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

// Isolated Editor verification: use real preview/creation/save-data code, never write player saves.
public static class VerifyCatalogHullPlatforms
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        var weaponGuids = AssetDatabase.FindAssets("t:WeaponCatalog");
        Require(weaponGuids.Length == 1, "Expected one weapon catalogue.");
        var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(AssetDatabase.GUIDToAssetPath(weaponGuids[0]));
        WeaponContentDefinition cheapest = null;
        foreach (var weapon in weapons.Weapons)
            if (weapon != null && weapon.Data != null && weapon.RequiredPlatformTier == 1
                && (cheapest == null || weapon.Data.EnergyCost < cheapest.Data.EnergyCost)) cheapest = weapon;
        Require(cheapest != null, "No tier-one weapon available for verification.");

        // Populate only an in-memory ownership service; neither constructor nor disk-loading methods run.
        var progress = (ContentProgressService)FormatterServices.GetUninitializedObject(typeof(ContentProgressService));
        var entriesField = typeof(ContentProgressService).GetField("entriesById", PrivateInstance);
        var entries = (IDictionary)Activator.CreateInstance(entriesField.FieldType);
        entriesField.SetValue(progress, entries);
        Set(progress, "loaded", true);
        var entryType = typeof(ContentProgressService).GetNestedType("ContentProgressEntry", BindingFlags.NonPublic);
        foreach (var hull in catalog.Hulls) Own(entries, entryType, hull.Id);
        Own(entries, entryType, cheapest.Id);
        var catalogs = new ContentCatalogService(catalog, weapons);
        var factory = new PrefabFactory(Array.Empty<GameObject>(), Array.Empty<GameObject>());
        var results = new List<object>();

        foreach (var hull in catalog.Hulls)
        {
            GameObject clone = null;
            var owner = new GameObject("Catalogue mount verification");
            owner.hideFlags = HideFlags.HideAndDontSave;
            HullPreviewController preview = null;
            try
            {
                var flow = owner.AddComponent<CraftCreationFlowController>();
                typeof(CraftCreationFlowController).GetMethod("Construct", PrivateInstance)
                    .Invoke(flow, new object[] { progress, catalogs, factory });
                flow.SetSelectedHull(hull);
                Require(flow.SelectedHull == hull, "Hull selection failed: " + hull.DisplayName);
                clone = UnityEngine.Object.Instantiate(hull.Prefab);
                clone.hideFlags = HideFlags.HideAndDontSave;
                preview = owner.AddComponent<HullPreviewController>();
                Set(preview, "previewInstance", clone);
                var loadout = clone.GetComponent<HullLoadoutDefinition>();
                Require(loadout != null && loadout.IsConfigurationValid(out _), "Invalid preview: " + hull.DisplayName);
                typeof(HullPreviewController).GetMethod("CollectSlotsFromLoadout", PrivateInstance)
                    .Invoke(preview, new object[] { loadout, hull });
                var slots = (List<CraftWeaponSlotDefinition>)Get(preview, "slotDefinitions");
                flow.SetWeaponSlotDefinitions(slots);
                Require(slots.Count == hull.Data.maximumWeaponCount && flow.WeaponSlotCount == slots.Count,
                    "Menu slot count differs from ShipData: " + hull.DisplayName);
                var gameplay = hull.GameplayPrefab.GetComponent<HullLoadoutDefinition>();
                Require(gameplay != null && gameplay.ShipData == hull.Data
                    && gameplay.WeaponPlatforms.Count == slots.Count && gameplay.IsConfigurationValid(out _),
                    "Gameplay mount count differs: " + hull.DisplayName);
                var physical = new HashSet<Transform>();
                var ids = new HashSet<string>();
                foreach (var slot in slots)
                {
                    Require(ids.Add(slot.SlotId) && gameplay.TryGetWeaponPlatform(slot.SlotId, out var mount)
                        && mount.WeaponMount != null && physical.Add(mount.WeaponMount),
                        "Missing, duplicated or shared physical mount: " + hull.DisplayName);
                    bool assigned = (bool)typeof(CraftCreationFlowController).GetMethod("AssignWeaponToSlot", PrivateInstance)
                        .Invoke(flow, new object[] { slot.SlotId, cheapest });
                    Require(assigned, "Weapon assignment failed: " + hull.DisplayName + "/" + slot.SlotId);
                }
                Require(flow.TryValidateCurrentCraft(out string error), "Craft validation failed: " + hull.DisplayName + ": " + error);
                Require(flow.TryCreateWeaponSaveData(out var savedWeapons, out error) && savedWeapons.Length == slots.Count,
                    "Save-data generation failed: " + hull.DisplayName + ": " + error);
                var save = new SaveShip(hull.Data.shipId, savedWeapons, "mount_verification", "") { hullContentId = hull.Id };
                var roundTrip = JsonUtility.FromJson<SaveShip>(JsonUtility.ToJson(save));
                Require(roundTrip.weaponData.Length == slots.Count, "Save JSON lost weapon slots.");
                for (int i = 0; i < slots.Count; i++)
                    Require(roundTrip.weaponData[i].slotId == slots[i].SlotId
                        && roundTrip.weaponData[i].contentId == cheapest.Id
                        && roundTrip.weaponData[i].usesShipLocalPosition
                        && (roundTrip.weaponData[i].place - slots[i].LocalPosition).sqrMagnitude < 0.000001f,
                        "Save JSON lost weapon placement.");

                ((IDictionary)Get(flow, "weaponsBySlot")).Remove(slots[slots.Count - 1].SlotId);
                Require(!flow.TryCreateWeaponSaveData(out _, out _) && !flow.TryValidateCurrentCraft(out _),
                    "Incomplete craft can be saved.");
                var excess = new WeaponDataSer[slots.Count + 1];
                for (int i = 0; i < excess.Length; i++) excess[i] = new WeaponDataSer(0, Vector3.zero, 0);
                Require(!ShipBuildValidator.TryValidate(hull.Data, excess, out _, slots.Count), "Excess weapon count accepted.");
                var energy = new WeaponDataSer[slots.Count];
                for (int i = 0; i < energy.Length; i++) energy[i] = new WeaponDataSer(0, Vector3.zero, 0);
                energy[0].energyCost = hull.Data.MetaMaximumEnergy + 1;
                Require(!ShipBuildValidator.TryValidate(hull.Data, energy, out _, slots.Count), "Energy limit was bypassed.");
                results.Add(new { hull = hull.DisplayName, slots = slots.Count, gameplayMounts = physical.Count,
                    weapon = cheapest.DisplayName, saveJsonRoundTrip = true, incompleteRejected = true,
                    excessRejected = true, energyRejected = true });
            }
            finally
            {
                // MonoBehaviour cleanup uses deferred Destroy; detach our clone before immediate Editor cleanup.
                if (preview != null) Set(preview, "previewInstance", null);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
        return new { hullsVerified = results.Count, results, playerSavesWritten = false };
    }

    private static void Own(IDictionary entries, Type entryType, string id)
    {
        var entry = Activator.CreateInstance(entryType, true);
        entryType.GetField("contentId").SetValue(entry, id);
        entryType.GetField("isOwned").SetValue(entry, true);
        entries[id] = entry;
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static void Require(bool condition, string error) { if (!condition) throw new Exception(error); }
}
