using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class DictatorTurretActiveAbility : ActiveAbility
{
    [Inject] private ProjectilePoolController projectilePoolController;
    [Inject] private EnemyManager enemyManager;

    [Header("Default Meta Contract")]
    [SerializeField, HideInInspector] private DictatorTurret turretPrefab;
    [SerializeField, HideInInspector] private Projectile defaultProjectilePrefab;
    [SerializeField, HideInInspector, Min(0.05f)]
    private float defaultTurretLifetime = 8f;
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultTurretProjectileDamage = 10f;
    [SerializeField, HideInInspector, Min(0.01f)]
    private float defaultTurretReloadTime = 0.5f;
    [SerializeField, HideInInspector, Min(0.01f)]
    private float defaultTurretProjectileSpeed = 8f;
    [SerializeField, HideInInspector, Min(0.01f)]
    private float defaultTurretTargetingRange = 10f;
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultKineticDamageBonusPercent = 15f;
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultExplosionDamageBonusPercent = 10f;
    [SerializeField, HideInInspector]
    private List<DictatorTurretSpawnGroup> defaultTurretSpawnGroups = new()
    {
        new DictatorTurretSpawnGroup(new Vector2(-0.75f, 0f)),
        new DictatorTurretSpawnGroup(new Vector2(0.75f, 0f))
    };

    private DictatorShipMetaContract metaContract;

    protected override void Awake()
    {
        base.Awake();
        if (cooldown <= 0f)
            cooldown = 12f;
    }

    protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        stats.TryGetContract(out metaContract);
    }

    public DictatorShipMetaContract CreateDefaultMetaContract()
    {
        var contract = new DictatorShipMetaContract();
        contract.Configure(
            defaultProjectilePrefab,
            EnemyDamageType.Kinetic,
            defaultTurretLifetime,
            defaultTurretProjectileDamage,
            defaultTurretReloadTime,
            defaultTurretProjectileSpeed,
            defaultTurretTargetingRange,
            defaultTurretSpawnGroups,
            defaultKineticDamageBonusPercent,
            defaultExplosionDamageBonusPercent);
        return contract;
    }

    public override bool Activate(ParentShip activationOwner)
    {
        if (activationOwner == null
            || turretPrefab == null
            || metaContract == null
            || metaContract.TurretProjectilePrefab == null
            || projectilePoolController == null
            || enemyManager == null)
        {
            Debug.LogError(
                "Dictator turrets require a configured meta contract, turret "
                + "prefab, projectile prefab and battle services.",
                this);
            return false;
        }

        IReadOnlyList<DictatorTurretSpawnGroup> spawnGroups =
            metaContract.TurretSpawnGroups;
        bool spawnedAnyTurret = false;
        for (int groupIndex = 0;
             spawnGroups != null && groupIndex < spawnGroups.Count;
             groupIndex++)
        {
            DictatorTurretSpawnGroup group = spawnGroups[groupIndex];
            if (group == null)
                continue;

            for (int turretIndex = 0;
                 turretIndex < group.Count;
                 turretIndex++)
            {
                float centeredIndex = turretIndex - (group.Count - 1) * 0.5f;
                Vector2 localOffset = group.LocalPosition
                    + Vector2.right * centeredIndex * group.HorizontalSpacing;
                Vector3 worldPosition = activationOwner.transform.TransformPoint(
                    localOffset);
                DictatorTurret turret = Instantiate(
                    turretPrefab,
                    worldPosition,
                    activationOwner.transform.rotation);
                turret.Initialize(
                    activationOwner,
                    projectilePoolController,
                    enemyManager,
                    metaContract);
                spawnedAnyTurret = true;
            }
        }

        return spawnedAnyTurret;
    }
}
