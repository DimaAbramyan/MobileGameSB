using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class FourWayEnemy : Enemy, IEnemyBurstAttackExecutor,
    IFormationAttackActivation,
    IEnemyBurstAttackSettingsOverrideReceiver,
    IEnemyProjectileTypeOverrideReceiver, IEnemyShootingSettingsReceiver,
    IEnemyAttackSettingsOverrideState, IEnemyAttackPatternSettingsReceiver, IEnemyAttackDirectionProvider,
    IEnemyAttackAimReceiver, IEnemyAttackSequenceExecutor, IEnemyLockedAimAttackExecutor,
    IEnemyEmissionAimAttackExecutor, IEnemyPendingEmissionExecutor, IEnemyStationaryAttackExecutor, IEnemyEmissionOriginReceiver,
    IEnemyAttackCompletionReceiver, IEnemyAttackNumericSource, IEnemySectorLaunchOrderSource
{
    private const int CurrentBurstSettingsVersion = 2;

    [Inject] private EnemyProjectileEcsSpawner projectileSpawner;

    [Header("Projectile")]
    [SerializeField, HideInInspector] private EnemyBullet projectilePrefab;
    [SerializeField, HideInInspector] private Transform projectileSpawnPoint;
    [SerializeField, HideInInspector] private Vector3 projectileSpawnOffset;

    [Header("Attack Pattern")]
    [SerializeField, HideInInspector]
    private EnemyBurstAttackSettings burstAttackSettings =
        new EnemyBurstAttackSettings();
    [SerializeField, HideInInspector, FormerlySerializedAs("volleysPerBurst")]
    private int legacyVolleysPerBurst = 1;
    [SerializeField, HideInInspector, FormerlySerializedAs("burstsPerSecond")]
    private float legacyBurstsPerSecond = 1f;
    [SerializeField, HideInInspector,
    FormerlySerializedAs("intervalBetweenVolleys")]
    private float legacyIntervalBetweenVolleys = 0.15f;
    [SerializeField, HideInInspector] private int burstSettingsVersion;

    [SerializeField, HideInInspector] private bool sharedExecutorMigrated;
    private SimpleEnemyAttackExecutor executor;

    public SimpleEnemyAttackExecutor EnsureExecutorConfigured()
    {
        if (executor == null)
            executor = GetComponent<SimpleEnemyAttackExecutor>();
        if (sharedExecutorMigrated)
            return executor;

        MigrateLegacyBurstSettings();
        sharedExecutorMigrated = true;
        if (executor == null)
            executor = gameObject.AddComponent<SimpleEnemyAttackExecutor>();
        FourWayEnemyRotationController legacyRotation = GetComponent<FourWayEnemyRotationController>();
        executor.ConfigureLegacyFourWay(projectilePrefab, projectileSpawnPoint, projectileSpawnOffset,
            burstAttackSettings, legacyRotation != null ? legacyRotation.LegacyRotationTarget : null,
            legacyRotation != null ? legacyRotation.CreateLegacySettings() : null,
            legacyRotation != null && legacyRotation.enabled);
        executor.SetLegacyProjectileSpawner(projectileSpawner);
        return executor;
    }

    public bool CanPerformWaveAttack => EnsureExecutorConfigured() is { } shared && shared.CanPerformWaveAttack;
    public EnemyBurstAttackSettings BurstAttackSettings => EnsureExecutorConfigured().BurstAttackSettings;
    public bool HasAttackSettingsOverride => EnsureExecutorConfigured().HasAttackSettingsOverride;
    public Vector3 AttackForward => EnsureExecutorConfigured().AttackForward;

    public override void Awake()
    {
        base.Awake();
        EnsureExecutorConfigured()?.SetLegacyProjectileSpawner(projectileSpawner);
    }

    private void OnValidate() => MigrateLegacyBurstSettings();

    public void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().ApplyBurstAttackSettingsOverride(settings);
    public void ApplyProjectileTypeOverride(EnemyProjectileType type) =>
        EnsureExecutorConfigured().ApplyProjectileTypeOverride(type);
    public void SetWaveShootingSettings(EnemyShootingSettings settings) =>
        EnsureExecutorConfigured().SetWaveShootingSettings(settings);
    public void SetWaveAttackSettings(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().SetWaveAttackSettings(settings);
    public void SetWaveAimTarget(IEnemyAttackAimTarget target) =>
        EnsureExecutorConfigured().SetWaveAimTarget(target);
    public void SetWaveAttackControl(bool isControlled) =>
        EnsureExecutorConfigured().SetWaveAttackControl(isControlled);
    public EnemyAttackNumericRegistry NumericRegistry => EnsureExecutorConfigured().NumericRegistry;
    public void SetNumericOwner(int slot) => EnsureExecutorConfigured().SetNumericOwner(slot);
    public void SetAttackCompletionTracker(EnemyAttackCompletionTracker tracker) =>
        EnsureExecutorConfigured().SetAttackCompletionTracker(tracker);
    public void SetFormationAttackReady(bool isReady) =>
        EnsureExecutorConfigured().SetFormationAttackReady(isReady);
    public bool TryFireAt(Vector3 targetPosition, EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().TryFireAt(targetPosition, settings);
    public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().TryFireInDirection(direction, settings);
    public bool TryFireStationary(Vector3 directionOrPoint, bool worldPoint, EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().TryFireStationary(directionOrPoint, worldPoint, settings);
    public Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().GetWaveAimOrigin(settings);
    public bool TryFireInAimedDirection(Vector3 direction, EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().TryFireInAimedDirection(direction, settings);
    public bool TryFireWithLockedEmissionAim(Vector3 target, EnemyBurstAttackSettings settings, bool refresh) =>
        EnsureExecutorConfigured().TryFireWithLockedEmissionAim(target, settings, refresh);
    public void BeginEmissionOriginAttack(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().BeginEmissionOriginAttack(settings);
    public bool PrepareEmissionOriginShot(EnemyBurstAttackSettings settings, bool burstStart, Vector3? capturedPlayer = null, Vector2? capturedOffsets = null, EnemyAttackNumericState numericOverride = null) =>
        EnsureExecutorConfigured().PrepareEmissionOriginShot(settings, burstStart, capturedPlayer, capturedOffsets, numericOverride);
    public void BeginAttackSequence(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().BeginAttackSequence(settings);
    public void BeginSectorLaunchOrder(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().BeginSectorLaunchOrder(settings);
    public void EndAttackSequence(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().EndAttackSequence(settings);
    public bool HasPendingEmissions(EnemyBurstAttackSettings settings) =>
        EnsureExecutorConfigured().HasPendingEmissions(settings);
    public void SetProjectileDirectionTransform(Transform value) =>
        EnsureExecutorConfigured().SetRotationTarget(value);

    private void MigrateLegacyBurstSettings()
    {
        burstAttackSettings ??= new EnemyBurstAttackSettings();
        if (burstSettingsVersion < CurrentBurstSettingsVersion)
        {
            if (burstSettingsVersion < 1)
            {
                burstAttackSettings.ConfigureLegacyRepeatedBurst(
                    legacyVolleysPerBurst,
                    1f / Mathf.Max(0.01f, legacyBurstsPerSecond),
                    legacyIntervalBetweenVolleys);
                burstSettingsVersion = 1;
            }

            if (burstSettingsVersion < CurrentBurstSettingsVersion)
            {
                burstAttackSettings.Validate();
                burstSettingsVersion = CurrentBurstSettingsVersion;
            }

            return;
        }

        burstAttackSettings.Validate();
    }
}
