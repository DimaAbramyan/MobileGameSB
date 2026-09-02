using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ContentCatalogService
{
    private readonly HullCatalog hullCatalog;
    private readonly WeaponCatalog weaponCatalog;

    public ContentCatalogService(HullCatalog hullCatalog, WeaponCatalog weaponCatalog)
    {
        this.hullCatalog = hullCatalog ?? throw new ArgumentNullException(nameof(hullCatalog));
        this.weaponCatalog = weaponCatalog ?? throw new ArgumentNullException(nameof(weaponCatalog));
    }

    public IReadOnlyList<HullContentDefinition> Hulls => hullCatalog.Hulls;
    public IReadOnlyList<WeaponContentDefinition> Weapons => weaponCatalog.Weapons;

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
}
