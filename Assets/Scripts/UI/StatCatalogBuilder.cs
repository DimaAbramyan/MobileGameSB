using System.Collections.Generic;
using UnityEngine;

// Walks actual configuration edges once when a view/selector is rebuilt.
public static class StatCatalogBuilder
{
    public static StatValueCatalog Build(ScriptableObject meta, int level = 0)
    {
        var catalog = new StatValueCatalog();
        if (meta == null) return catalog;
        catalog.Dependencies.Add(meta);
        if (meta is WeaponMetaConfig weapon)
        {
            var configuration = weapon.GetLevel(level);
            var basic = Source(catalog, configuration.BasicStats, "weapon/basic", "Weapon/Basic Stats", meta);
            configuration.BasicStats.CollectDisplayStats(basic);
            basic.Add("ShotsPerSecond", "Shots Per Second", () => weapon.GetDpsInfo(level).ShotsPerSecond);
            foreach (var contract in configuration.Contracts)
                if (contract != null)
                    contract.CollectDisplayStats(Source(catalog, contract, "weapon/" + contract.GetType().FullName,
                        "Weapon/" + contract.DisplayName, meta));
            var ancestors = new HashSet<ProjectileData>();
            foreach (var slot in weapon.ProjectileSlots)
                if (slot?.Projectile != null && !slot.SpawnedByAnotherProjectile)
                    Projectile(catalog, slot.Projectile, "projectile/" + slot.Id,
                        "Projectile " + slot.Projectile.name + " [" + StatSourceBuilder.ShortId(slot.Id) + "]", slot.Id, 0, ancestors);
        }
        else if (meta is ShipMetaConfig ship)
        {
            var configuration = ship.GetLevel(level);
            configuration.BasicStats.CollectDisplayStats(Source(catalog, configuration.BasicStats,
                "ship/basic", "Ship/Basic Stats", meta));
            foreach (var contract in configuration.Contracts)
                if (contract != null)
                    contract.CollectDisplayStats(Source(catalog, contract, "ship/" + contract.GetType().FullName,
                        "Ship/" + contract.DisplayName, meta));
        }
        else catalog.Warnings.Add("Unsupported Meta: " + meta.GetType().Name);
        return catalog;
    }

    private static StatSourceBuilder Source(StatValueCatalog catalog, IStatValueProvider provider,
        string path, string label, Object asset) => new(catalog, provider, path, label, asset);

    private static void Projectile(StatValueCatalog catalog, ProjectileData data, string path, string label,
        string slot, int depth, HashSet<ProjectileData> ancestors)
    {
        if (!ancestors.Add(data))
        {
            catalog.Warnings.Add("Cyclic projectile reference: " + label);
            return;
        }
        data.CollectDisplayStats(new StatSourceBuilder(catalog, data, path + "/basic", label + "/Basic Stats",
            data, data, slot, depth));
        foreach (var contract in data.Contracts)
            if (contract != null)
                contract.CollectDisplayStats(new StatSourceBuilder(catalog, contract,
                    path + "/" + contract.GetType().FullName, label + "/" + contract.DisplayName,
                    data, data, slot, depth));
        if (data.TryGetContract(out SpawnSecondaryProjectileContract spawn) && spawn.SecondaryProjectile != null)
            Projectile(catalog, spawn.SecondaryProjectile, path + "/child", label + "/Child " + spawn.SecondaryProjectile.name,
                slot, depth + 1, ancestors);
        ancestors.Remove(data); // Shared configurations remain selectable under each distinct path.
    }
}
