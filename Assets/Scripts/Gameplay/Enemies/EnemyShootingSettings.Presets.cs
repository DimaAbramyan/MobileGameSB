using System.Collections.Generic;
using UnityEngine;

public sealed partial class EnemyShootingSettings
{
    [SerializeField] private EnemyProjectilePreset projectilePreset;
    [SerializeField] private EnemyPresetUseMode projectilePresetMode;
    [SerializeField] private List<string> projectilePresetOverrides = new();
    [SerializeField] private EnemyAttackPatternPreset attackPatternPreset;
    [SerializeField] private EnemyPresetUseMode attackPatternPresetMode;
    [SerializeField] private List<string> attackPatternPresetOverrides = new();
    [SerializeField] private EnemyPresetFiringSettings presetFiring = new();
    [SerializeField, Tooltip("Optional prefab carried by this projectile configuration. Empty keeps the enemy executor's prefab for the selected type.")]
    private EnemyBullet projectilePrefab;
    [SerializeField, HideInInspector] private bool independentProjectileSpeed;
    public bool UsesLegacyStackSpeed => !independentProjectileSpeed;

    public EnemyProjectilePreset ProjectilePreset => projectilePreset;
    public EnemyAttackPatternPreset AttackPatternPreset => attackPatternPreset;
    public EnemyPresetUseMode ProjectilePresetMode => projectilePresetMode;
    public EnemyPresetUseMode AttackPatternPresetMode => attackPatternPresetMode;
    public List<string> ProjectilePresetOverrides => projectilePresetOverrides ??= new();
    public List<string> AttackPatternPresetOverrides => attackPatternPresetOverrides ??= new();
    public EnemyPresetFiringSettings PresetFiring => presetFiring ??= new();
    internal EnemyChildAttackSettings AuthoredChild => useChildAttackPattern ? childAttack : null;
    internal bool HasLinkedPresets => projectilePreset != null && projectilePresetMode == EnemyPresetUseMode.Linked
        || attackPatternPreset != null && attackPatternPresetMode == EnemyPresetUseMode.Linked;
    public EnemyBullet ResolveProjectilePrefab(EnemyBullet fallback) => projectilePrefab != null ? projectilePrefab : fallback;

    public void LinkProjectile(EnemyProjectilePreset preset, EnemyPresetUseMode mode = EnemyPresetUseMode.Linked)
    {
        if (preset != null && EnemyPresetDependencies.HasCycle(preset))
            throw new System.InvalidOperationException("Cyclic projectile/pattern preset references are not allowed.");
        if (preset != null) CopyProjectileConfigurationFrom(preset.Settings);
        projectilePreset = preset; projectilePresetMode = mode; ProjectilePresetOverrides.Clear();
        EnemyPresetRevision.Changed();
    }

    public void LinkPattern(EnemyAttackPatternPreset preset, EnemyPresetUseMode mode = EnemyPresetUseMode.Linked)
    {
        if (preset != null && EnemyPresetDependencies.HasCycle(preset))
            throw new System.InvalidOperationException("Cyclic projectile/pattern preset references are not allowed.");
        attackPatternPreset = preset; attackPatternPresetMode = mode; AttackPatternPresetOverrides.Clear();
        EnemyPresetRevision.Changed();
    }

    public void ClearPresetLinks()
    {
        projectilePreset = null; attackPatternPreset = null;
        ProjectilePresetOverrides.Clear(); AttackPatternPresetOverrides.Clear();
    }

    internal void CopyPresetBindingsFrom(EnemyShootingSettings source)
    {
        projectilePreset = source.projectilePreset; projectilePresetMode = source.projectilePresetMode;
        attackPatternPreset = source.attackPatternPreset; attackPatternPresetMode = source.attackPatternPresetMode;
        projectilePresetOverrides = new List<string>(source.ProjectilePresetOverrides);
        attackPatternPresetOverrides = new List<string>(source.AttackPatternPresetOverrides);
        presetFiring = source.PresetFiring.Copy(); projectilePrefab = source.projectilePrefab;
        independentProjectileSpeed = source.independentProjectileSpeed;
    }

    public void CopyProjectileConfigurationFrom(EnemyShootingSettings source, IReadOnlyList<string> keepLocal = null)
    {
        if (source == null || ReferenceEquals(source, this)) return;
        EnemyPresetFields.Copy(this, source, EnemyPresetFields.Projectile, keepLocal);
        if (!EnemyPresetFields.Contains(keepLocal, "childAttack"))
            childAttack = EnemyChildAttackSettings.Copy(source.childAttack);
    }

    internal void ClearProjectileConfiguration() => CopyProjectileConfigurationFrom(new EnemyShootingSettings());

    internal void UseIndependentProjectileSpeed(EnemyShootingSettings source = null)
    {
        // Capture an authored legacy Stack speed once. New patterns cannot change a saved projectile's base speed.
        if (source != null && source.UsesShotGun && source.UsesLegacyStackSpeed && !source.overrideProjectileBaseSpeed)
        {
            SetProjectileBaseSpeedOverride(true, source.shotGunInitialSpeed);
            ProjectileBaseSpeedRandom.CopyFrom(source.shotGunInitialSpeedRandom);
        }
        independentProjectileSpeed = true;
    }

    internal void SetPresetFiring(EnemyPresetFiringSettings source, IReadOnlyList<string> keepLocal = null)
    {
        if (source == null) return;
        EnemyPresetFields.Copy(PresetFiring, source,
            new[] { "enabled", "fireMode", "aimMode", "stationaryFireSettings", "forwardFireHalfAngle" },
            keepLocal, "shooting.presetFiring.");
    }

    public void UnlinkPreset(bool projectile)
    {
        if (projectile) { projectilePreset = null; ProjectilePresetOverrides.Clear(); }
        else { attackPatternPreset = null; AttackPatternPresetOverrides.Clear(); }
    }
}
