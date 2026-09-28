using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ContentCatalogService
{
    private readonly HullCatalog hullCatalog;
    private readonly WeaponCatalog weaponCatalog;
    private readonly List<HullContentDefinition> sortedHulls;
    private readonly List<WeaponContentDefinition> sortedWeapons;

    public ContentCatalogService(HullCatalog hullCatalog, WeaponCatalog weaponCatalog)
    {
        this.hullCatalog = hullCatalog ?? throw new ArgumentNullException(nameof(hullCatalog));
        this.weaponCatalog = weaponCatalog ?? throw new ArgumentNullException(nameof(weaponCatalog));
        sortedHulls = CreateSortedList(this.hullCatalog.Hulls);
        sortedWeapons = CreateSortedList(this.weaponCatalog.Weapons);
    }

    public IReadOnlyList<HullContentDefinition> Hulls => sortedHulls;
    public IReadOnlyList<WeaponContentDefinition> Weapons => sortedWeapons;

    public bool TryGetHull(string contentId, out HullContentDefinition hull)
    {
        hull = null;
        if (string.IsNullOrWhiteSpace(contentId))
            return false;

        IReadOnlyList<HullContentDefinition> hulls = Hulls;
        for (int i = 0; i < hulls.Count; i++)
        {
            HullContentDefinition candidate = hulls[i];
            if (candidate != null
                && string.Equals(candidate.Id, contentId, StringComparison.Ordinal))
            {
                hull = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetHullByShipId(int shipId, out HullContentDefinition hull)
    {
        hull = null;
        IReadOnlyList<HullContentDefinition> hulls = Hulls;
        for (int i = 0; i < hulls.Count; i++)
        {
            HullContentDefinition candidate = hulls[i];
            if (candidate == null
                || candidate.Data == null
                || candidate.Data.shipId != shipId)
            {
                continue;
            }

            if (hull != null)
            {
                hull = null;
                return false;
            }

            hull = candidate;
        }

        return hull != null;
    }

    public bool TryGetWeaponByPrefab(GameObject prefab, out WeaponContentDefinition weapon)
    {
        weapon = null;
        if (prefab == null)
            return false;

        IReadOnlyList<WeaponContentDefinition> weapons = Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponContentDefinition candidate = weapons[i];
            if (candidate != null && candidate.Prefab == prefab)
            {
                weapon = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetWeapon(string contentId, out WeaponContentDefinition weapon)
    {
        weapon = null;
        if (string.IsNullOrWhiteSpace(contentId))
            return false;

        IReadOnlyList<WeaponContentDefinition> weapons = Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponContentDefinition candidate = weapons[i];
            if (candidate != null
                && string.Equals(candidate.Id, contentId, StringComparison.Ordinal))
            {
                weapon = candidate;
                return true;
            }
        }

        return false;
    }

    private static List<T> CreateSortedList<T>(IReadOnlyList<T> source)
        where T : CraftContentDefinition
    {
        var result = new List<T>(source?.Count ?? 0);
        if (source == null)
            return result;

        for (int index = 0; index < source.Count; index++)
        {
            T content = source[index];
            if (content != null)
                result.Add(content);
        }

        result.Sort(CompareContent);
        return result;
    }

    private static int CompareContent(CraftContentDefinition first, CraftContentDefinition second)
    {
        int rarityComparison = first.Rarity.CompareTo(second.Rarity);
        if (rarityComparison != 0)
            return rarityComparison;

        int nameComparison = StringComparer.OrdinalIgnoreCase.Compare(
            first.DisplayName ?? string.Empty,
            second.DisplayName ?? string.Empty);
        if (nameComparison != 0)
            return nameComparison;

        return StringComparer.Ordinal.Compare(
            first.Id ?? string.Empty,
            second.Id ?? string.Empty);
    }
}
