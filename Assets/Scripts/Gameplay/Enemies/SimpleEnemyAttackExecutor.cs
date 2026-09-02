using UnityEngine;
using Zenject;

[RequireComponent(typeof(Enemy))]
public sealed class SimpleEnemyAttackExecutor : MonoBehaviour,
    IEnemyBurstAttackExecutor,
    IEnemyBurstAttackSettingsOverrideReceiver
{
    [Inject] private DiContainer container;

    [SerializeField] private EnemyBullet projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Vector3 projectileSpawnOffset =
        new Vector3(0f, 0.25f, 0f);

    [Header("Attack Pattern")]
    [SerializeField, InspectorName("Attack Pattern")]
    private EnemyBurstAttackSettings burstAttackSettings =
        new EnemyBurstAttackSettings();

    private Enemy enemy;
    private CircleShip circleShip;

    public bool CanPerformWaveAttack =>
        isActiveAndEnabled
        && enemy != null
        && !enemy.isDead
        && projectilePrefab != null
        && container != null;

    public EnemyBurstAttackSettings BurstAttackSettings => burstAttackSettings;

    public void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.CopyFrom(settings);
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        circleShip = GetComponent<CircleShip>();
    }

    private void OnValidate()
    {
        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.Validate();
    }

    public void SetWaveAttackControl(bool isControlled)
    {
        if (circleShip != null)
            circleShip.SetWaveAttackControl(isControlled);
    }

    public bool TryFireAt(
        Vector3 targetPosition,
        EnemyBurstAttackSettings attackSettings)
    {
        if (!CanPerformWaveAttack)
            return false;

        Vector3 spawnPosition = GetProjectileSpawnPosition();
        return TryLaunchProjectiles(
            spawnPosition,
            targetPosition - spawnPosition,
            attackSettings);
    }

    public bool TryFireInDirection(
        Vector3 direction,
        EnemyBurstAttackSettings attackSettings)
    {
        if (!CanPerformWaveAttack)
            return false;

        return TryLaunchProjectiles(
            GetProjectileSpawnPosition(),
            direction,
            attackSettings);
    }

    private Vector3 GetProjectileSpawnPosition()
    {
        return projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.TransformPoint(projectileSpawnOffset);
    }

    private bool TryLaunchProjectiles(
        Vector3 spawnPosition,
        Vector3 direction,
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
                ? effectiveSettings.GetProjectileDirection(direction, projectileIndex)
                : direction;
            if (projectileDirection.sqrMagnitude < 0.0001f)
                projectileDirection = transform.up;
            else
                projectileDirection.Normalize();

            EnemyBullet projectile = container.InstantiatePrefabForComponent<EnemyBullet>(
                projectilePrefab,
                spawnPosition,
                Quaternion.identity,
                null);
            projectile.SetDamageMultiplier(enemy.DamageMultiplier);
            projectile.Launch(projectileDirection);

            float angle = Mathf.Atan2(
                projectileDirection.y,
                projectileDirection.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                angle + 90f);
        }

        return true;
    }
}
