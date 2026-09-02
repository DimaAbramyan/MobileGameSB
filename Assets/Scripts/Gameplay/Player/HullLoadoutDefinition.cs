using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes the weapon hardpoints and build data of a hull prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class HullLoadoutDefinition : MonoBehaviour
{
    [SerializeField] private ShipData shipData;
    [SerializeField] private List<HullWeaponPlatform> weaponPlatforms = new();

    public ShipData ShipData => shipData;
    public IReadOnlyList<HullWeaponPlatform> WeaponPlatforms => weaponPlatforms;

    public bool TryGetWeaponPlatform(
        string slotId,
        out HullWeaponPlatform platform)
    {
        platform = null;

        if (string.IsNullOrWhiteSpace(slotId) || weaponPlatforms == null)
            return false;

        for (int i = 0; i < weaponPlatforms.Count; i++)
        {
            HullWeaponPlatform candidate = weaponPlatforms[i];
            if (candidate == null
                || !string.Equals(candidate.SlotId, slotId, StringComparison.Ordinal))
            {
                continue;
            }

            platform = candidate;
            return true;
        }

        return false;
    }

    public int GetMaxWeaponTier(string slotId)
    {
        if (!TryGetWeaponPlatform(slotId, out HullWeaponPlatform platform))
            return 1;

        int hullLevel = shipData != null ? shipData.currentLvl : 0;
        return platform.GetMaxWeaponTier(hullLevel);
    }

    public bool IsConfigurationValid(out string error)
    {
        if (shipData == null)
        {
            error = "Hull loadout has no ShipData assigned.";
            return false;
        }

        if (weaponPlatforms == null || weaponPlatforms.Count == 0)
        {
            error = "Hull loadout has no weapon platforms.";
            return false;
        }

        for (int i = 0; i < weaponPlatforms.Count; i++)
        {
            HullWeaponPlatform platform = weaponPlatforms[i];
            if (platform == null)
            {
                error = $"Weapon platform at index {i} is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(platform.SlotId))
            {
                error = $"Weapon platform at index {i} has no slot id.";
                return false;
            }

            if (platform.WeaponMount == null)
            {
                error = $"Weapon platform '{platform.SlotId}' has no weapon mount.";
                return false;
            }

            if (platform.WeaponMount != transform
                && !platform.WeaponMount.IsChildOf(transform))
            {
                error = $"Weapon platform '{platform.SlotId}' references a mount outside this hull prefab.";
                return false;
            }

            if (!platform.HasTierProgression)
            {
                error = $"Weapon platform '{platform.SlotId}' has no tier progression.";
                return false;
            }

            if (platform.HasInvalidTierValues)
            {
                error = $"Weapon platform '{platform.SlotId}' contains a tier below 1.";
                return false;
            }

            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                HullWeaponPlatform previous = weaponPlatforms[previousIndex];
                if (previous != null
                    && string.Equals(previous.SlotId, platform.SlotId, StringComparison.Ordinal))
                {
                    error = $"Weapon platform slot id '{platform.SlotId}' is duplicated.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        issues.Clear();

        if (shipData == null)
            issues.Add("Assign ShipData for this hull.");

        if (weaponPlatforms == null || weaponPlatforms.Count == 0)
        {
            issues.Add("Add at least one weapon platform.");
            return;
        }

        for (int i = 0; i < weaponPlatforms.Count; i++)
        {
            HullWeaponPlatform platform = weaponPlatforms[i];
            if (platform == null)
            {
                issues.Add($"Weapon platform at index {i} is missing.");
                continue;
            }

            string platformLabel = string.IsNullOrWhiteSpace(platform.SlotId)
                ? $"Weapon platform at index {i}"
                : $"Weapon platform '{platform.SlotId}'";

            if (string.IsNullOrWhiteSpace(platform.SlotId))
                issues.Add($"{platformLabel} has no slot id.");

            if (platform.WeaponMount == null)
            {
                issues.Add($"{platformLabel} has no weapon mount.");
            }
            else if (platform.WeaponMount != transform
                     && !platform.WeaponMount.IsChildOf(transform))
            {
                issues.Add($"{platformLabel} references a mount outside this hull prefab.");
            }

            if (!platform.HasTierProgression)
                issues.Add($"{platformLabel} has no tier progression.");
            else if (platform.HasInvalidTierValues)
                issues.Add($"{platformLabel} contains a tier below 1.");

            if (!string.IsNullOrWhiteSpace(platform.SlotId))
            {
                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    HullWeaponPlatform previous = weaponPlatforms[previousIndex];
                    if (previous != null
                        && string.Equals(
                            previous.SlotId,
                            platform.SlotId,
                            StringComparison.Ordinal))
                    {
                        issues.Add($"Slot id '{platform.SlotId}' is duplicated.");
                        break;
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        weaponPlatforms ??= new List<HullWeaponPlatform>();

        for (int i = 0; i < weaponPlatforms.Count; i++)
            weaponPlatforms[i]?.Normalize();
    }
#endif
}

[Serializable]
public sealed class HullWeaponPlatform
{
    [SerializeField] private string slotId = "weapon_slot";
    [SerializeField] private Transform weaponMount;
    [SerializeField] private Vector2 previewButtonOffset;
    [SerializeField] private Vector2 previewButtonSize = new(64f, 64f);
    [SerializeField] private Vector2 previewWeaponIconScale = Vector2.one;
    [Tooltip("Maximum supported weapon tier for each hull upgrade level. Element 0 is hull level 0.")]
    [SerializeField] private List<int> maxWeaponTierByHullUpgradeLevel = new() { 1 };

    public string SlotId => slotId;
    public Transform WeaponMount => weaponMount;
    public Vector2 PreviewButtonOffset => previewButtonOffset;
    public Vector2 PreviewButtonSize => new(
        Mathf.Max(1f, previewButtonSize.x),
        Mathf.Max(1f, previewButtonSize.y));
    public Vector2 PreviewWeaponIconScale => new(
        Mathf.Max(0.01f, previewWeaponIconScale.x),
        Mathf.Max(0.01f, previewWeaponIconScale.y));
    public bool HasTierProgression => maxWeaponTierByHullUpgradeLevel != null
        && maxWeaponTierByHullUpgradeLevel.Count > 0;
    public bool HasInvalidTierValues
    {
        get
        {
            if (maxWeaponTierByHullUpgradeLevel == null)
                return false;

            for (int i = 0; i < maxWeaponTierByHullUpgradeLevel.Count; i++)
            {
                if (maxWeaponTierByHullUpgradeLevel[i] < 1)
                    return true;
            }

            return false;
        }
    }

    public int GetMaxWeaponTier(int hullUpgradeLevel)
    {
        if (!HasTierProgression)
            return 1;

        int levelIndex = Mathf.Clamp(
            Mathf.Max(0, hullUpgradeLevel),
            0,
            maxWeaponTierByHullUpgradeLevel.Count - 1);
        return Mathf.Max(1, maxWeaponTierByHullUpgradeLevel[levelIndex]);
    }

#if UNITY_EDITOR
    internal void Normalize()
    {
        slotId = slotId?.Trim();
        previewButtonSize.x = Mathf.Max(1f, previewButtonSize.x);
        previewButtonSize.y = Mathf.Max(1f, previewButtonSize.y);
        previewWeaponIconScale.x = Mathf.Max(0.01f, previewWeaponIconScale.x);
        previewWeaponIconScale.y = Mathf.Max(0.01f, previewWeaponIconScale.y);
    }
#endif
}
