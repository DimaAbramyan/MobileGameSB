using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class FourWayEnemy : Enemy, IEnemyBurstAttackExecutor,
    IFormationAttackActivation,
    IEnemyBurstAttackSettingsOverrideReceiver
{
    private const int CurrentBurstSettingsVersion = 2;

    [Inject] private DiContainer container;

    [Header("Projectile")]
    [SerializeField] private EnemyBullet projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Vector3 projectileSpawnOffset;

    [Header("Attack Pattern")]
    [SerializeField, InspectorName("Attack Pattern")]
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

    private int remainingAttackShots;
    private int remainingBurstShots;
    private float nextAttackTime;
    private float nextShotTime;
    private Transform projectileDirectionTransform;
    private bool isWaveAttackControlled;
    private bool isFormationAttackReady = true;

    public bool CanPerformWaveAttack =>
        isActiveAndEnabled
        && !isDead
        && projectilePrefab != null
        && container != null;

    public EnemyBurstAttackSettings BurstAttackSettings => burstAttackSettings;

    public void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.CopyFrom(settings);
        ResetAttackSchedule();
    }

    public override void Awake()
    {
        base.Awake();
        MigrateLegacyBurstSettings();
        ResetAttackSchedule();
    }

    private void OnEnable()
    {
        ResetAttackSchedule();
    }

    private void Update()
    {
        if (!isFormationAttackReady
            || isWaveAttackControlled
            || !CanPerformWaveAttack)
            return;

        float currentTime = Time.time;
        if (remainingAttackShots <= 0)
        {
            if (currentTime < nextAttackTime)
                return;

            remainingAttackShots =
                burstAttackSettings.GetAttackShotCountForFireRate(
                    FireRateMultiplier);
            remainingBurstShots = 0;
            nextShotTime = currentTime;
        }

        if (currentTime < nextShotTime)
            return;

        if (burstAttackSettings.RepeatBurst && remainingBurstShots <= 0)
            remainingBurstShots = burstAttackSettings.BurstShotCount;

        FireFourWayVolley();

        if (burstAttackSettings.RepeatBurst)
        {
            remainingBurstShots--;
            if (remainingBurstShots > 0)
            {
                nextShotTime = currentTime
                    + burstAttackSettings.BurstShotInterval;
                return;
            }

            remainingAttackShots--;
            if (remainingAttackShots > 0)
            {
                nextShotTime = currentTime
                    + burstAttackSettings.AttackShotInterval;
                return;
            }

            nextAttackTime = currentTime
                + burstAttackSettings.AttackCooldown / FireRateMultiplier;
            return;
        }

        remainingAttackShots--;
        if (remainingAttackShots > 0)
        {
            nextShotTime = currentTime
                + burstAttackSettings.AttackShotInterval / FireRateMultiplier;
            return;
        }

        nextAttackTime = currentTime
            + burstAttackSettings.AttackCooldown / FireRateMultiplier;
    }

    private void OnValidate()
    {
        MigrateLegacyBurstSettings();
        burstAttackSettings.Validate();
    }

    private void ResetAttackSchedule()
    {
        remainingAttackShots = 0;
        remainingBurstShots = 0;
        float attackStartTime = Time.time
            + burstAttackSettings.AttackStartDelay / FireRateMultiplier;
        nextAttackTime = attackStartTime;
        nextShotTime = attackStartTime;
    }

    private void FireFourWayVolley()
    {
        Transform directionTransform = projectileDirectionTransform != null
            ? projectileDirectionTransform
            : transform;
        Vector3 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : directionTransform.TransformPoint(projectileSpawnOffset);
        FireFourWayVolley(
            spawnPosition,
            directionTransform.up,
            burstAttackSettings);
    }

    public void SetWaveAttackControl(bool isControlled)
    {
        if (isWaveAttackControlled == isControlled)
            return;

        isWaveAttackControlled = isControlled;
        if (!isWaveAttackControlled)
            ResetAttackSchedule();
    }

    public void SetFormationAttackReady(bool isReady)
    {
        if (isFormationAttackReady == isReady)
            return;

        isFormationAttackReady = isReady;
        ResetAttackSchedule();
    }

    public bool TryFireAt(
        Vector3 targetPosition,
        EnemyBurstAttackSettings attackSettings)
    {
        if (!CanPerformWaveAttack)
            return false;

        Transform directionTransform = projectileDirectionTransform != null
            ? projectileDirectionTransform
            : transform;
        Vector3 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : directionTransform.TransformPoint(projectileSpawnOffset);
        FireFourWayVolley(
            spawnPosition,
            directionTransform.up,
            attackSettings);
        return true;
    }

    public bool TryFireInDirection(
        Vector3 direction,
        EnemyBurstAttackSettings attackSettings)
    {
        if (!CanPerformWaveAttack)
            return false;

        Transform directionTransform = projectileDirectionTransform != null
            ? projectileDirectionTransform
            : transform;
        Vector3 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : directionTransform.TransformPoint(projectileSpawnOffset);
        FireFourWayVolley(spawnPosition, direction, attackSettings);
        return true;
    }

    public void SetProjectileDirectionTransform(Transform value)
    {
        projectileDirectionTransform = value;
    }

    private void SpawnProjectile(Vector3 spawnPosition, Vector3 direction)
    {
        EnemyBullet projectile = container.InstantiatePrefabForComponent<EnemyBullet>(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity,
            null);
        projectile.SetDamageMultiplier(DamageMultiplier);
        projectile.Launch(direction);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
    }

    private void FireFourWayVolley(
        Vector3 spawnPosition,
        Vector3 primaryDirection,
        EnemyBurstAttackSettings attackSettings)
    {
        if (primaryDirection.sqrMagnitude < 0.0001f)
            primaryDirection = transform.up;
        else
            primaryDirection.Normalize();

        Vector3 perpendicularDirection = new Vector3(
            -primaryDirection.y,
            primaryDirection.x,
            0f);
        SpawnSpreadProjectiles(
            spawnPosition,
            primaryDirection,
            attackSettings);
        SpawnSpreadProjectiles(
            spawnPosition,
            perpendicularDirection,
            attackSettings);
        SpawnSpreadProjectiles(
            spawnPosition,
            -primaryDirection,
            attackSettings);
        SpawnSpreadProjectiles(
            spawnPosition,
            -perpendicularDirection,
            attackSettings);
    }

    private void SpawnSpreadProjectiles(
        Vector3 spawnPosition,
        Vector3 baseDirection,
        EnemyBurstAttackSettings attackSettings)
    {
        EnemyBurstAttackSettings effectiveSettings = attackSettings
            ?? burstAttackSettings;
        int projectileCount = effectiveSettings != null
            ? effectiveSettings.ProjectilesPerShot
            : 1;
        for (int projectileIndex = 0;
             projectileIndex < projectileCount;
             projectileIndex++)
        {
            Vector3 projectileDirection = effectiveSettings != null
                ? effectiveSettings.GetProjectileDirection(
                    baseDirection,
                    projectileIndex)
                : baseDirection;
            SpawnProjectile(spawnPosition, projectileDirection);
        }
    }

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
