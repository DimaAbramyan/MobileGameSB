using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectedWaveEnemyOverride : MonoBehaviour
{
    private const int CurrentAttackOverrideVersion = 2;

    [SerializeField] private Enemy enemyPrefabOverride;

    [Header("Visual")]
    [SerializeField] private bool overrideSpriteTint;
    [SerializeField] private Color spriteTint = Color.white;

    // Legacy fields are retained for existing formations and are migrated to
    // the focused local attack override below.
    [SerializeField, HideInInspector] private bool overrideProjectileType;
    [SerializeField, HideInInspector] private EnemyProjectileType projectileType;
    [SerializeField, Tooltip("Overrides this enemy's complete Attack Pattern. Takes priority over the shared Wave Override.")]
    [HideInInspector] private bool overrideBurstAttackSettings;
    [SerializeField, HideInInspector] private EnemyBurstAttackSettings burstAttackSettings =
        new EnemyBurstAttackSettings();

    [Header("Attack Pattern")]
    [SerializeField, HideInInspector] private bool overrideAttackPattern;
    [SerializeField, HideInInspector] private bool inheritWaveFireMode;
    [SerializeField, HideInInspector] private DirectedWaveAttackFireMode fireMode =
        DirectedWaveAttackFireMode.Aimed;
    [SerializeField, HideInInspector] private DirectedWaveAttackAimMode aimMode;
    [SerializeField, HideInInspector, Range(0f, 180f)] private float forwardFireHalfAngle = 15f;
    [SerializeField, HideInInspector] private EnemyBurstAttackSettings attackPattern =
        new EnemyBurstAttackSettings();
    [SerializeField, HideInInspector] private bool overrideProjectileBaseSpeed;
    [SerializeField, HideInInspector, Min(0.01f)] private float projectileBaseSpeed = 1f;
    [SerializeField, HideInInspector] private bool useLegacyFullAttackSettings;
    [SerializeField, HideInInspector] private int attackOverrideVersion;

    [Header("Rotation")]
    [SerializeField] private bool overrideFourWayRotation;
    [SerializeField] private DirectedWaveFourWayRotationOverride fourWayRotation =
        new DirectedWaveFourWayRotationOverride();

    private readonly List<MonoBehaviour> attackComponents = new(4);
    private readonly List<SpriteRenderer> spriteRenderers = new(4);

    public Enemy EnemyPrefabOverride => enemyPrefabOverride;
    // Retained for source compatibility with old preview/editor code.
    public EnemyBurstAttackSettings AttackSettingsOverride => UsesLegacyFullAttackSettings ? burstAttackSettings : null;
    public bool HasAttackPatternOverride => overrideAttackPattern;
    public bool UsesLegacyFullAttackSettings => overrideAttackPattern && useLegacyFullAttackSettings;
    public EnemyRotationSettings RotationOverride => overrideFourWayRotation ? fourWayRotation : null;

    public DirectedWaveAttackFireMode ResolveFireMode(
        DirectedWaveAttackFireMode waveFireMode)
    {
        return overrideAttackPattern && !inheritWaveFireMode
            ? fireMode
            : waveFireMode;
    }

    public float ResolveForwardFireHalfAngle(float waveHalfAngle)
    {
        return overrideAttackPattern && !inheritWaveFireMode
            && fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead
                ? Mathf.Clamp(forwardFireHalfAngle, 0f, 180f)
                : waveHalfAngle;
    }

    public void ApplyAttackPatternOverride(
        EnemyBurstAttackSettings destination,
        float? sharedProjectileBaseSpeed = null)
    {
        if (!overrideAttackPattern || destination == null)
            return;

        destination.ApplyShootingOverrideFrom(attackPattern);
        if (sharedProjectileBaseSpeed.HasValue)
        {
            destination.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(
                sharedProjectileBaseSpeed.Value);
        }
        if (overrideProjectileBaseSpeed)
        {
            destination.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(
                projectileBaseSpeed);
        }
    }

    public DirectedWaveAttackAimMode ResolveAimMode(DirectedWaveAttackAimMode waveAimMode)
    {
        return overrideAttackPattern && !inheritWaveFireMode ? aimMode : waveAimMode;
    }

    public bool TryCopyLegacyFullAttackSettings(
        EnemyBurstAttackSettings destination)
    {
        if (!UsesLegacyFullAttackSettings || destination == null)
            return false;

        destination.CopyFrom(burstAttackSettings);
        return true;
    }

    public void InitializeAttackPatternOverride(
        DirectedWaveAttackSettings waveSettings,
        EnemyBurstAttackSettings resolvedAttackPattern)
    {
        overrideAttackPattern = true;
        inheritWaveFireMode = false;
        useLegacyFullAttackSettings = false;
        attackOverrideVersion = CurrentAttackOverrideVersion;
        if (waveSettings != null)
        {
            fireMode = waveSettings.FireMode;
            aimMode = waveSettings.AimMode;
            forwardFireHalfAngle = waveSettings.ForwardFireHalfAngle;
            overrideProjectileBaseSpeed =
                waveSettings.HasRuntimeProjectileBaseSpeedOverride;
            projectileBaseSpeed = overrideProjectileBaseSpeed
                ? waveSettings.RuntimeProjectileBaseSpeedOverride
                : projectileBaseSpeed;
        }

        attackPattern ??= new EnemyBurstAttackSettings();
        if (resolvedAttackPattern != null)
            attackPattern.CopyFrom(resolvedAttackPattern);
        attackPattern.Validate();
    }

    public void SetAttackSettingsOverride(EnemyBurstAttackSettings source)
    {
        if (source == null)
            return;

        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.CopyFrom(source);
        overrideBurstAttackSettings = true;
        attackOverrideVersion = 0;
        EnsureAttackOverrideMigration();
    }

    public void ApplyTo(Enemy enemy)
    {
        EnsureAttackOverrideMigration();
        if (enemy == null)
            return;

        if (overrideSpriteTint)
            ApplySpriteTint(enemy);

        if (UsesLegacyFullAttackSettings)
        {
            var legacySettings = new EnemyBurstAttackSettings();
            TryCopyLegacyFullAttackSettings(legacySettings);
            ApplyAttackPatternOverride(legacySettings);
            ApplyBurstAttackSettings(enemy, legacySettings);
        }

        if (overrideProjectileType && !overrideAttackPattern && !overrideBurstAttackSettings)
            ApplyProjectileType(enemy);

        if (overrideFourWayRotation)
        {
            SimpleEnemyAttackExecutor executor = enemy.GetComponent<SimpleEnemyAttackExecutor>();
            executor?.ApplyRotationOverride(fourWayRotation);
        }
    }

    private void OnValidate()
    {
        EnsureAttackOverrideMigration();
        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.Validate();
        attackPattern ??= new EnemyBurstAttackSettings();
        attackPattern.Validate();
        forwardFireHalfAngle = Mathf.Clamp(forwardFireHalfAngle, 0f, 180f);
        projectileBaseSpeed = Mathf.Max(0.01f, projectileBaseSpeed);
        fourWayRotation ??= new DirectedWaveFourWayRotationOverride();
        fourWayRotation.Validate();
    }

    public bool EnsureAttackOverrideMigration()
    {
        if (attackOverrideVersion >= CurrentAttackOverrideVersion)
            return false;

        if (attackOverrideVersion < 1 && overrideBurstAttackSettings)
        {
            attackPattern ??= new EnemyBurstAttackSettings();
            attackPattern.CopyFrom(burstAttackSettings);
            if (overrideProjectileType)
                attackPattern.SetProjectileTypeForAllPatterns(projectileType);
            overrideAttackPattern = true;
            // Old slot overrides used the shared Directed Wave Fire Mode.
            inheritWaveFireMode = true;
            // Their attack cadence was also local; preserve that only for the
            // migrated record so existing waves do not change on upgrade.
            useLegacyFullAttackSettings = true;
        }

        // Version 1 had already copied the old profile, but did not persist
        // whether its locally configured cadence must be retained.
        if (attackOverrideVersion < 2
            && overrideBurstAttackSettings
            && overrideAttackPattern
            && inheritWaveFireMode)
        {
            useLegacyFullAttackSettings = true;
        }

        attackOverrideVersion = CurrentAttackOverrideVersion;
        return true;
    }

    private void ApplySpriteTint(Enemy enemy)
    {
        spriteRenderers.Clear();
        enemy.GetComponentsInChildren(true, spriteRenderers);
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null)
                renderer.color *= spriteTint;
        }

        spriteRenderers.Clear();
    }

    private void ApplyBurstAttackSettings(Enemy enemy, EnemyBurstAttackSettings settings)
    {
        attackComponents.Clear();
        enemy.GetComponents(attackComponents);
        for (int i = 0; i < attackComponents.Count; i++)
        {
            if (attackComponents[i] is FourWayEnemy && enemy.GetComponent<SimpleEnemyAttackExecutor>() != null)
                continue;
            if (attackComponents[i] is IEnemyBurstAttackSettingsOverrideReceiver receiver)
                receiver.ApplyBurstAttackSettingsOverride(settings);
        }

        attackComponents.Clear();
    }

    private void ApplyProjectileType(Enemy enemy)
    {
        attackComponents.Clear();
        enemy.GetComponents(attackComponents);
        for (int i = 0; i < attackComponents.Count; i++)
        {
            if (attackComponents[i] is FourWayEnemy && enemy.GetComponent<SimpleEnemyAttackExecutor>() != null)
                continue;
            if (attackComponents[i] is IEnemyProjectileTypeOverrideReceiver receiver)
                receiver.ApplyProjectileTypeOverride(projectileType);
        }

        attackComponents.Clear();
    }
}

// The inherited field names preserve existing point override serialization.
[System.Serializable]
public sealed class DirectedWaveFourWayRotationOverride : EnemyRotationSettings
{
}
