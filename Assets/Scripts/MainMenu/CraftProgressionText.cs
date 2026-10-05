using UnityEngine;

/// <summary>
/// Translates the player's persistent content level into runtime values and UI text.
/// UI callers should use this instead of reading a ScriptableObject's base values directly.
/// </summary>
public static class CraftProgressionText
{
    public static int GetUpgradeLevel(
        CraftContentDefinition content,
        ContentProgressService contentProgressService)
    {
        if (content == null || contentProgressService == null)
            return 0;

        return Mathf.Clamp(
            contentProgressService.GetUpgradeLevel(content),
            0,
            content.MaxUpgradeLevel);
    }

    public static WeaponRuntimeStats GetWeaponStats(
        WeaponContentDefinition weapon,
        ContentProgressService contentProgressService)
    {
        return GetWeaponStatsAtLevel(
            weapon,
            GetUpgradeLevel(weapon, contentProgressService));
    }

    public static WeaponRuntimeStats GetWeaponStatsAtLevel(
        WeaponContentDefinition weapon,
        int upgradeLevel)
    {
        if (weapon == null || weapon.Data == null)
            return default;

        int level = Mathf.Clamp(upgradeLevel, 0, weapon.MaxUpgradeLevel);
        WeaponData data = weapon.Data;
        if (data.WeaponMetaConfig != null)
            return data.WeaponMetaConfig.GetRuntimeStats(level).WeaponStats;

        return data.GetRuntimeStats(level);
    }

    public static ShipMetaRuntimeStats GetHullStats(
        HullContentDefinition hull,
        ContentProgressService contentProgressService)
    {
        return GetHullStatsAtLevel(
            hull,
            GetUpgradeLevel(hull, contentProgressService));
    }

    public static ShipMetaRuntimeStats GetHullStatsAtLevel(
        HullContentDefinition hull,
        int upgradeLevel)
    {
        if (hull == null || hull.Data == null)
            return default;

        return hull.Data.GetMetaRuntimeStats(
            Mathf.Clamp(upgradeLevel, 0, hull.MaxUpgradeLevel));
    }

    public static string GetWeaponDamageText(
        WeaponContentDefinition weapon,
        ContentProgressService contentProgressService)
    {
        return FormatDamage(GetWeaponStats(weapon, contentProgressService).Damage);
    }

    public static string GetWeaponCooldownText(
        WeaponContentDefinition weapon,
        ContentProgressService contentProgressService)
    {
        return FormatCooldown(GetWeaponStats(weapon, contentProgressService).ReloadTime);
    }

    public static string GetWeaponRangeText(
        WeaponContentDefinition weapon,
        ContentProgressService contentProgressService)
    {
        return FormatRange(GetWeaponStats(weapon, contentProgressService).Range);
    }

    public static string GetAbilityCooldownText(
        HullContentDefinition hull,
        ContentProgressService contentProgressService)
    {
        return TryGetAbilityRecovery(hull, contentProgressService, out AbilityMetaRecoverySettings settings)
            ? FormatCooldown(settings.Cooldown)
            : string.Empty;
    }

    public static string GetAbilityChargesText(
        HullContentDefinition hull,
        ContentProgressService contentProgressService)
    {
        return TryGetAbilityRecovery(hull, contentProgressService, out AbilityMetaRecoverySettings settings)
            ? FormatCharges(settings.MaxCharges)
            : string.Empty;
    }

    public static string GetAbilityDamageText(
        HullContentDefinition hull,
        ContentProgressService contentProgressService)
    {
        if (hull == null || hull.Data == null)
            return string.Empty;

        ShipMetaRuntimeStats stats = GetHullStats(hull, contentProgressService);
        float damageMultiplier = hull.Data.GetAbilityDamageMultiplier(
            GetBattleLevel(hull, contentProgressService));

        if (stats.TryGetContract(out BlackHoleShipMetaContract blackHole))
            return $"Black hole {FormatDamage(blackHole.Damage * damageMultiplier)}";

        if (stats.TryGetContract(out BladeShipMetaContract blade))
            return $"Blade {FormatDamage(blade.Damage * damageMultiplier)}";

        if (stats.TryGetContract(out DictatorShipMetaContract dictator))
            return $"Turret {FormatDamage(dictator.TurretProjectileDamage * damageMultiplier)}";

        if (stats.TryGetContract(out PrismShipMetaContract prism))
            return $"Plasma {FormatDamage(prism.PlasmaDamage)}";

        if (stats.TryGetContract(out ArkanoidShipMetaContract arkanoid))
        {
            return $"Ball {FormatDamage(arkanoid.BallDamage)} • "
                + $"Stasis DPS: {arkanoid.StasisDamagePerSecond:0.#}";
        }

        return string.Empty;
    }

    public static string FormatDamage(float value)
    {
        return $"Damage: {Mathf.Max(0f, value):0.#}";
    }

    public static string FormatCooldown(float value)
    {
        return $"Cooldown: {Mathf.Max(0f, value):0.##} s";
    }

    public static string FormatCharges(int value)
    {
        return $"Charges: {Mathf.Max(0, value)}";
    }

    public static string FormatRange(float value)
    {
        return $"Range: {Mathf.Max(0f, value):0.#}";
    }

    private static bool TryGetAbilityRecovery(
        HullContentDefinition hull,
        ContentProgressService contentProgressService,
        out AbilityMetaRecoverySettings settings)
    {
        ShipMetaRuntimeStats stats = GetHullStats(hull, contentProgressService);
        if (stats.TryGetContract(out AbilityRecoveryShipMetaContract recovery))
        {
            settings = recovery.Settings;
            return true;
        }

        settings = default;
        return false;
    }

    private static int GetBattleLevel(
        HullContentDefinition hull,
        ContentProgressService contentProgressService)
    {
        return ParentShip.MinWeaponLevel
            + GetUpgradeLevel(hull, contentProgressService);
    }
}
