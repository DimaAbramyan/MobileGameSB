public static class EnemyAttackSettingsResolver
{
    // Both wave controllers use the same source, legacy/local override and speed priority.
    public static EnemyBurstAttackSettings ResolveForWave(
        Enemy enemy,
        IWaveAttackExecutor executor,
        DirectedWaveAttackSettings settings,
        DirectedWaveEnemyOverride local,
        System.Collections.Generic.Dictionary<Enemy, EnemyBurstAttackSettings> cache,
        out bool setWaveAttackSettings,
        float timingMultiplier = 1f)
    {
        bool usesEnemySettings = UsesEnemySettings(executor, settings.UsesEnemyBurstSettings);
        EnemyBurstAttackSettings source = Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings);
        bool hasLocalOverride = local != null && local.HasAttackPatternOverride;
        bool usesLegacyFullSettings = local != null && local.UsesLegacyFullAttackSettings;
        bool needsRuntimeCopy = hasLocalOverride || settings.HasRuntimeProjectileBaseSpeedOverride || timingMultiplier != 1f;
        if (!needsRuntimeCopy || enemy == null || source == null && !usesLegacyFullSettings)
        {
            setWaveAttackSettings = !usesEnemySettings;
            return source;
        }

        if (!cache.TryGetValue(enemy, out EnemyBurstAttackSettings copy))
        {
            copy = CreateRuntimeCopy(source, local, settings.HasRuntimeProjectileBaseSpeedOverride
                ? settings.RuntimeProjectileBaseSpeedOverride : (float?)null);
            ApplyRuntimeSpeedRandomization(copy, local, settings);
            if (timingMultiplier != 1f) copy.ScaleAttackTiming(timingMultiplier);
            cache.Add(enemy, copy);
        }
        setWaveAttackSettings = !usesEnemySettings || needsRuntimeCopy;
        return copy;
    }

    public static EnemyBurstAttackSettings CreateRuntimeCopy(
        EnemyBurstAttackSettings source, DirectedWaveEnemyOverride local, float? sharedSpeed)
    {
        source = source?.ResolveAllPresets();
        var copy = new EnemyBurstAttackSettings();
        if (local == null || !local.TryCopyLegacyFullAttackSettings(copy))
            copy.CopyFrom(source);
        copy.MaterializePresets();
        if (local != null && local.HasAttackPatternOverride)
        {
            local.ApplyAttackPatternOverride(copy, sharedSpeed);
            // Local shooting data can be reused by several independent attack sources.
            // Keep their order scopes distinct while retaining the local pattern indices.
            if (source != null && source.PatternCount > 0)
                EnemySectorLaunchOrderRegistry.BindSource(copy, source.GetPattern(0).Shooting.EmissionLayout.OrderKey);
        }
        else if (sharedSpeed.HasValue)
            copy.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(sharedSpeed.Value);
        return copy;
    }

    public static bool UsesEnemySettings(IWaveAttackExecutor executor, bool usesEnemySettings)
    {
        return usesEnemySettings
            || executor is IEnemyAttackSettingsOverrideState { HasAttackSettingsOverride: true };
    }

    public static void ApplyRuntimeSpeedRandomization(EnemyBurstAttackSettings copy, DirectedWaveEnemyOverride local,
        DirectedWaveAttackSettings settings)
    {
        if (!settings.HasRuntimeProjectileBaseSpeedOverride || settings.RuntimeProjectileBaseSpeedRandom == null
            || !settings.RuntimeProjectileBaseSpeedRandom.enabled) return;
        for (int i = 0; i < copy.PatternCount; i++)
        {
            var shooting = copy.GetPattern(i).Shooting;
            if (local != null && local.HasAttackPatternOverride && shooting.OverridesProjectileBaseSpeed) continue;
            shooting.SetRuntimeRandomProjectileBaseSpeedOverride(settings.RuntimeProjectileBaseSpeedOverride,
                settings.RuntimeProjectileBaseSpeedRandom, settings.RuntimeProjectileBaseSpeedRandomizeOn);
        }
    }

    public static EnemyBurstAttackSettings Resolve(
        IWaveAttackExecutor executor,
        bool usesEnemySettings,
        EnemyBurstAttackSettings waveSettings)
    {
        if (UsesEnemySettings(executor, usesEnemySettings)
            && executor is IEnemyBurstAttackExecutor burstExecutor
            && burstExecutor.BurstAttackSettings != null)
        {
            return burstExecutor.BurstAttackSettings;
        }

        return waveSettings;
    }
}
