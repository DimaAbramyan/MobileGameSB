using UnityEngine;

public sealed partial class EnemyBurstAttackSettings
{
    [System.NonSerialized] private EnemyBurstAttackSettings presetResolved;
    [System.NonSerialized] private int presetRevision = -1;
    [System.NonSerialized] private bool resolvingPresets;
    [System.NonSerialized] private EnemyBurstAttackSettings resolvedPatterns;
    [System.NonSerialized] private int resolvedPatternsRevision = -1;
    [System.ThreadStatic] private static int presetResolveDepth;
    public EnemyShootingSettings AuthoredShooting => shooting;

    // One materialization per owner/configuration revision. Plain legacy profiles return themselves.
    public EnemyBurstAttackSettings ResolvePresets()
    {
        if (HasMultiplePatterns || !shooting.HasLinkedPresets || resolvingPresets) return this;
        if (presetResolved != null && presetRevision == EnemyPresetRevision.Value) return presetResolved;
        if (presetResolveDepth >= EnemyChildAttackSettings.MaximumDepth)
            throw new System.InvalidOperationException("Preset reference resolution exceeded its depth limit.");
        resolvingPresets = true; presetResolveDepth++;
        try
        {
            var resolved = new EnemyBurstAttackSettings();
            resolved.CopyFrom(this);
            var pattern = shooting.AttackPatternPreset;
            if (pattern != null && shooting.AttackPatternPresetMode == EnemyPresetUseMode.Linked)
            {
                if (EnemyPresetDependencies.HasCycle(pattern))
                    throw new System.InvalidOperationException("Cyclic attack pattern preset references are not allowed.");
                var data = pattern.Pattern.RuntimeSettings.ResolvePresets();
                EnemyPresetFields.Copy(resolved, data, EnemyPresetFields.Timing, shooting.AttackPatternPresetOverrides);
                EnemyPresetFields.Copy(resolved.shooting, data.Shooting, EnemyPresetFields.Layout,
                    shooting.AttackPatternPresetOverrides, "shooting.");
                if (!EnemyPresetFields.Contains(shooting.AttackPatternPresetOverrides, "shooting.presetFiring"))
                    resolved.shooting.SetPresetFiring(pattern.Firing, shooting.AttackPatternPresetOverrides);
            }
            var projectile = shooting.ProjectilePreset;
            if (projectile != null && shooting.ProjectilePresetMode == EnemyPresetUseMode.Linked)
            {
                if (EnemyPresetDependencies.HasCycle(projectile))
                    throw new System.InvalidOperationException("Cyclic projectile preset references are not allowed.");
                resolved.shooting.CopyProjectileConfigurationFrom(projectile.Settings, shooting.ProjectilePresetOverrides);
            }
            // Clearing a root binding does not touch references in nested child configurations.
            resolved.shooting.ClearPresetLinks();
            resolved.Validate();
            resolved.shooting.EmissionLayout.BindOrderSource(this, 0);
            presetResolved = resolved; presetRevision = EnemyPresetRevision.Value;
            return resolved;
        }
        finally { presetResolveDepth--; resolvingPresets = false; }
    }

    public void ApplyPatternPreset(EnemyAttackPatternPreset preset, EnemyPresetUseMode mode)
    {
        if (preset == null) return;
        if (EnemyPresetDependencies.HasCycle(preset)) throw new System.InvalidOperationException("Cyclic preset references.");
        var source = preset.Pattern.RuntimeSettings.ResolvePresets();
        EnemyPresetFields.Copy(this, source, EnemyPresetFields.Timing);
        EnemyPresetFields.Copy(shooting, source.Shooting, EnemyPresetFields.Layout);
        shooting.SetPresetFiring(preset.Firing);
        shooting.LinkPattern(preset, mode);
        Validate();
    }

    public void MaterializePresets()
    {
        var effective = ResolvePresets();
        if (!ReferenceEquals(effective, this))
        {
            EnemyPresetFields.Copy(this, effective, EnemyPresetFields.Timing);
            shooting.CopyFrom(effective.shooting);
        }
        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i].AuthoredShooting.HasLinkedPresets)
                patterns[i].CopyFrom(patterns[i].RuntimeSettings.ResolvePresets());
        presetResolved = null;
    }

    // Share immutable resolved configuration before making enemy-specific runtime copies.
    public EnemyBurstAttackSettings ResolveAllPresets()
    {
        if (!HasMultiplePatterns) return ResolvePresets();
        bool linked = false;
        for (int i = 0; i < patterns.Count; i++) linked |= patterns[i].AuthoredShooting.HasLinkedPresets;
        if (!linked) return this;
        if (resolvedPatterns != null && resolvedPatternsRevision == EnemyPresetRevision.Value) return resolvedPatterns;
        var copy = new EnemyBurstAttackSettings(); copy.CopyFrom(this);
        copy.shooting.ClearPresetLinks();
        for (int i = 0; i < patterns.Count; i++) copy.patterns[i].CopyFrom(GetPattern(i).ResolvePresets());
        copy.Validate(); resolvedPatterns = copy; resolvedPatternsRevision = EnemyPresetRevision.Value;
        return copy;
    }

    // Flatten the selected category while retaining the other independent binding.
    public void UnlinkPreset(bool projectile)
    {
        var effective = ResolvePresets();
        if (projectile) shooting.CopyProjectileConfigurationFrom(effective.Shooting);
        else
        {
            EnemyPresetFields.Copy(this, effective, EnemyPresetFields.Timing);
            EnemyPresetFields.Copy(shooting, effective.Shooting, EnemyPresetFields.Layout);
        }
        shooting.UnlinkPreset(projectile);
        Validate();
    }
}
