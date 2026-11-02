using UnityEngine;

// Resolves supported final values from live owners. Other values remain explicitly
// BaseConfiguration, particularly values requiring a target or a spawned-object state.
public sealed class StatRuntimeContext : IStatValueResolver
{
    private readonly Weapon weapon;
    private readonly ParentShip ship;
    private readonly WeaponController controller;
    private StatValueCatalog abilityValues;
    private IStatValueProvider activeSnapshot, passiveSnapshot;
    public bool IsRuntime => true;
    public bool IsAvailable => weapon != null || ship != null;
    public StatRuntimeContext(Weapon weapon, WeaponController controller = null)
    { this.weapon = weapon; this.controller = controller; }
    public StatRuntimeContext(ParentShip ship) { this.ship = ship; }

    private void RefreshAbilitySources()
    {
        var active = ship.ActiveAbility != null ? ship.ActiveAbility.DisplayStatSnapshot : null;
        var passive = ship.PassiveAbility != null ? ship.PassiveAbility.DisplayStatSnapshot : null;
        if (abilityValues != null && ReferenceEquals(active, activeSnapshot) && ReferenceEquals(passive, passiveSnapshot)) return;
        activeSnapshot = active; passiveSnapshot = passive;
        abilityValues = new StatValueCatalog();
        AddAbility(active); AddAbility(passive);
    }
    private void AddAbility(IStatValueProvider provider)
    {
        if (provider == null) return;
        provider.CollectDisplayStats(new StatSourceBuilder(abilityValues, provider,
            "ship/" + provider.GetType().FullName, "Runtime Ability", ship.ShipData.ShipMetaConfig));
    }

    public bool TryRead(StatValueDefinition d, out StatValueResult result)
    {
        object value = null;
        if (!IsAvailable) { result = new StatValueResult(null, StatValueState.Missing); return true; }
        if (weapon != null)
        {
            var stats = weapon.RuntimeStats;
            if (d.ProviderType == typeof(WeaponMetaBasicStats))
            {
                var parameters = weapon.RuntimeProjectileParameters;
                switch (d.Property)
                {
                    case "Damage": value = parameters.damage; break;
                    case "Range": value = parameters.maxLength; break;
                    case "ProjectileSpeed": value = parameters.speed; break;
                    case "ShotsPerSecond": value = weapon.GetRuntimeShotsPerSecond(controller); break;
                    case "Angle": value = stats.Angle; break;
                    case "InitialDirectionAngle": value = stats.InitialDirectionAngle; break;
                    case "ReloadTime": value = weapon.RuntimeReloadTime * weapon.GetRuntimeReloadMultiplier(controller); break;
                }
            }
            else if (d.ProviderType == typeof(BurstFireWeaponMetaContract))
            {
                switch (d.Property)
                {
                    case "VolleysPerActivation": value = stats.VolleysPerActivation; break;
                    case "ProjectilesPerVolley": value = stats.ProjectilesPerVolley; break;
                    case "DelayBetweenVolleys": value = stats.DelayBetweenVolleys; break;
                }
            }
            else if (d.ProviderType == typeof(TargetingWeaponMetaContract))
            {
                if (d.Property == "MaxTargets") value = stats.MaxTargets;
                if (d.Property == "TargetSearchRadius") value = stats.TargetSearchRadius;
            }
            else if (d.ProviderType == typeof(ProjectileData) && d.Projectile != null
                && weapon.TryGetFiredProjectileStats(d.RootSlot, d.Projectile, d.ProjectileDepth, out var projectile))
            {
                switch (d.Property)
                {
                    case "Damage": value = projectile.Damage; break;
                    case "Range": value = projectile.Range; break;
                    case "Speed": value = projectile.Speed; break;
                }
            }
        }
        if (ship != null)
        {
            if (d.ProviderType == typeof(ShipMetaBasicStats))
            {
                if (d.Property == "MaximumHealthPoints") value = ship.MaximumHealthPoints;
                if (d.Property == "MaximumShieldPoints") value = ship.MaximumShieldPoints;
            }
            var ability = ship.ActiveAbility;
            if (ability != null && (d.ProviderType == typeof(AbilityMetaRecoverySettings)
                || d.ProviderType == typeof(AbilityRecoveryShipMetaContract)))
            {
                var settings = ability.ExportMetaRecoverySettings();
                switch (d.Property)
                {
                    case "Mode": case "AbilityMode": value = ability.AbilityMode; break;
                    case "Cooldown": value = ability.CooldownDuration; break;
                    case "MaxCharges": value = ability.MaxCharges; break;
                    case "ToggleMaximumTime": value = ability.ToggleMaximumTime; break;
                    case "ToggleTimeCostPerSecond": value = settings.ToggleTimeCostPerSecond; break;
                    case "ToggleRechargeStartTime": value = settings.ToggleRechargeStartTime; break;
                    case "ToggleRechargeDuration": value = settings.ToggleRechargeDuration; break;
                }
            }
            if (value == null && ability is IStatValueResolver active && active.TryRead(d, out result)) return true;
            if (value == null && ship.PassiveAbility is IStatValueResolver passive && passive.TryRead(d, out result)) return true;
            if (value == null)
            {
                RefreshAbilitySources();
                if (abilityValues.TryFind(d.SourcePath, d.Id, out var runtimeDefinition)) value = runtimeDefinition.Read(null).Value;
            }
        }
        result = new StatValueResult(value, StatValueState.Runtime);
        return value != null;
    }
}
