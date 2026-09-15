using UnityEngine;

using System;
using Zenject;

public class Weapon : MonoBehaviour
{
    [Inject] DiContainer container;
    [Inject] private ProjectilePoolController projectilePoolController;

    [SerializeField] protected Projectile projectilePrefab;
    [SerializeField] protected Transform projectileSpawn;
    [SerializeField] public  WeaponData weaponData;
    private ParentShip owner;
    private SpriteRenderer spriteRenderer;

    protected float reloadTime;
    protected float currentReloadTime;
    protected int level;
    protected float maxAngle;
    protected Enemy target;
    private WeaponRuntimeStats currentStats;
    private int identicalWeaponCount = 1;
    private float identicalWeaponFireRateMultiplier = 1f;
    private float identicalWeaponDamageMultiplier = 1f;

    private bool ableToShoot;
    private bool subscribedToOwnerLevel;
    private bool reportedUnsupportedConfiguredProjectile;

    public event Action<int> OnLevelChanged;
    public event Action<Weapon> OnShot;

    public int Level => level;
    public Enemy Target => target;
    protected ParentShip Owner => owner;
    protected WeaponRuntimeStats CurrentStats => currentStats;
    protected float Damage => currentStats.Damage * identicalWeaponDamageMultiplier;
    protected bool IsAbleToShoot => ableToShoot;
    public int IdenticalWeaponCount => identicalWeaponCount;
    public float IdenticalWeaponFireRateMultiplier =>
        Mathf.Max(1f, identicalWeaponFireRateMultiplier);

    public virtual void HideWeapon()
    {
        ableToShoot = false;
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    public virtual void ShowWeapon()
    {
        ableToShoot = true;
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected virtual void Start()
    {
        SubscribeToOwnerLevel();

        if (weaponData == null)
        {
            Debug.LogError($"Weapon '{name}' has no WeaponData assigned.", this);
            enabled = false;
            return;
        }

        ApplyLevel(level, false);
    }
    private void HandleLevelChanged(int newLevel)
    {
        SetLevel(newLevel);
    }

    public void SetOwner(ParentShip ownerShip)
    {
        if (owner == ownerShip)
        {
            SubscribeToOwnerLevel();
            return;
        }

        UnsubscribeFromOwnerLevel();
        owner = ownerShip;
        SubscribeToOwnerLevel();

        if (owner != null && weaponData != null)
            SetLevel(owner.GetLevel());
    }

    public virtual bool TryToShoot()
    {
        if (!ableToShoot) return false;

        currentReloadTime -= Time.deltaTime;
        if (currentReloadTime <= 0f)
        {
            bool shotFired = Fire();
            currentReloadTime = reloadTime;

            if (shotFired)
                RaiseShotFired();

            return true;
        }

        return false;
    }

    public virtual bool TryShootImmediately(float reloadMultiplier = 1f)
    {
        if (!ableToShoot || !gameObject.activeInHierarchy)
            return false;

        bool shotFired = Fire();
        currentReloadTime = reloadTime
            * Mathf.Max(0f, reloadMultiplier)
            / IdenticalWeaponFireRateMultiplier;

        // A forced shot deliberately does not raise OnShot, preventing trigger loops.
        return shotFired;
    }

    protected virtual bool Fire()
    {
        if (weaponData == null || projectileSpawn == null)
            return false;

        if (weaponData.UsesProjectileData)
            return FireConfiguredProjectiles();

        if (projectilePrefab == null)
            return false;

        return TrySpawnProjectile(
            CreateProjectileParams(),
            CreateProjectileRuntimeConfig());
    }

    private bool FireConfiguredProjectiles()
    {
        if (weaponData.WeaponMetaConfig == null)
            return false;

        bool firedAnyProjectile = false;
        for (int index = 0;
             index < weaponData.WeaponMetaConfig.ProjectileSlots.Count;
             index++)
        {
            WeaponProjectileSlot projectileSlot =
                weaponData.WeaponMetaConfig.ProjectileSlots[index];
            if (projectileSlot?.Projectile == null
                || projectileSlot.SpawnedByAnotherProjectile)
                continue;

            ProjectileData projectileData = projectileSlot.Projectile;
            if (projectileData.DeliveryType != ProjectileDeliveryType.Projectile)
            {
                ReportUnsupportedConfiguredProjectile(
                    "Beam ProjectileData requires a beam weapon component.");
                continue;
            }

            if (projectileData.ProjectilePrefab == null)
            {
                ReportUnsupportedConfiguredProjectile(
                    "ProjectileData has no physical projectile prefab.");
                continue;
            }

            if (!weaponData.TryGetProjectileRuntimeStats(
                    projectileSlot.Id,
                    level,
                    out _,
                    out ProjectileRuntimeStats projectileStats))
            {
                continue;
            }

            ProjectileRuntimeConfig runtimeConfig =
                projectileData.CreateRuntimeConfig();
            runtimeConfig.explosionDamage *= identicalWeaponDamageMultiplier;
            ConfigureSecondaryProjectileRuntimeStats(runtimeConfig);
            firedAnyProjectile |= TrySpawnProjectile(
                projectileData.ProjectilePrefab,
                CreateProjectileParams(projectileStats),
                runtimeConfig);
        }

        return firedAnyProjectile;
    }

    private void ConfigureSecondaryProjectileRuntimeStats(
        ProjectileRuntimeConfig runtimeConfig)
    {
        if (runtimeConfig.secondaryProjectile == null
            || !weaponData.TryGetProjectileRuntimeStats(
                runtimeConfig.secondaryProjectile,
                level,
                out _,
                out ProjectileRuntimeStats secondaryStats))
        {
            return;
        }

        runtimeConfig.hasSecondaryProjectileRuntimeStats = true;
        runtimeConfig.secondaryProjectileDamage = secondaryStats.Damage
            * identicalWeaponDamageMultiplier;
        runtimeConfig.secondaryProjectileRange = secondaryStats.Range;
        runtimeConfig.secondaryProjectileSpeed = secondaryStats.Speed;
    }

    private void ReportUnsupportedConfiguredProjectile(string reason)
    {
        if (reportedUnsupportedConfiguredProjectile)
            return;

        reportedUnsupportedConfiguredProjectile = true;
        Debug.LogError(
            $"Weapon '{name}' cannot fire its configured ProjectileData: {reason}",
            this);
    }

    protected virtual ProjectileParams CreateProjectileParams()
    {
        return new ProjectileParams
        {
            speed = currentStats.Speed,
            damage = Damage,
            maxLength = currentStats.Range,
            direction = transform.up,
            maxAngle = currentStats.Angle,
        };
    }

    protected virtual ProjectileParams CreateProjectileParams(
        ProjectileRuntimeStats projectileStats)
    {
        return new ProjectileParams
        {
            speed = projectileStats.Speed,
            damage = projectileStats.Damage * identicalWeaponDamageMultiplier,
            maxLength = projectileStats.Range,
            direction = transform.up,
            maxAngle = currentStats.Angle,
        };
    }

    protected virtual ProjectileRuntimeConfig CreateProjectileRuntimeConfig()
    {
        return new ProjectileRuntimeConfig
        {
            flightMode = weaponData.FlightMode,
            contactMode = weaponData.ContactMode,
            damageType = weaponData.DamageType,
            homingRotationSpeed = weaponData.HomingRotationSpeed,
            growDuringFlight = weaponData.GrowDuringFlight,
            scaleGrowthPerSecond = weaponData.ScaleGrowthPerSecond,
            projectileLifetime = weaponData.ProjectileLifetime,
            disableColliderAfterFirstPhysicsStep =
                weaponData.DisableColliderAfterFirstPhysicsStep,
            fadeDuringLifetime = weaponData.FadeDuringLifetime,
            fadeDuration = weaponData.FadeDuration,
            explosionPrefab = weaponData.ExplosionPrefab,
            explosionDamage = weaponData.ExplosionDamage,
            continuousDamageInterval = weaponData.ContinuousDamageInterval
        };
    }

    protected bool TrySpawnProjectile(
        ProjectileParams parameters,
        ProjectileRuntimeConfig runtimeConfig)
    {
        return TrySpawnProjectile(projectilePrefab, parameters, runtimeConfig);
    }

    protected bool TrySpawnProjectile(
        Projectile projectileToSpawn,
        ProjectileParams parameters,
        ProjectileRuntimeConfig runtimeConfig)
    {
        if (projectileToSpawn == null
            || projectileSpawn == null
            || projectilePoolController == null)
        {
            return false;
        }

        Projectile proj = projectilePoolController.Spawn(
            projectileToSpawn,
            projectileSpawn.position,
            Quaternion.identity);

        if (proj != null)
            proj.Init(parameters, runtimeConfig, owner);

        return proj != null;
    }

    public virtual void Reload(float multiplier)
    {
        currentReloadTime = reloadTime * multiplier;
    }

    public virtual void SetIdenticalWeaponCount(int count)
    {
        int weaponCount = Mathf.Max(1, count);
        bool isSpray = weaponData != null
            && weaponData.DamageType == EnemyDamageType.Spray;

        SetIdenticalWeaponMultipliers(
            weaponCount,
            isSpray ? 1f : weaponCount,
            isSpray ? weaponCount : 1f);
    }

    protected void SetIdenticalWeaponMultipliers(
        int count,
        float fireRateMultiplier,
        float damageMultiplier)
    {
        identicalWeaponCount = Mathf.Max(1, count);
        identicalWeaponFireRateMultiplier = Mathf.Max(1f, fireRateMultiplier);
        identicalWeaponDamageMultiplier = Mathf.Max(1f, damageMultiplier);
    }

    public void SetLevel(int newLevel)
    {
        ApplyLevel(newLevel, true);
    }

    private void ApplyLevel(int newLevel, bool notify)
    {
        if (weaponData == null)
            return;

        int configLevel = Mathf.Max(
            0,
            newLevel - ParentShip.MinWeaponLevel);
        level = weaponData.ClampLevel(configLevel);
        currentStats = weaponData.GetRuntimeStats(level);
        reloadTime = currentStats.ReloadTime;
        currentReloadTime = reloadTime / IdenticalWeaponFireRateMultiplier;
        OnLevelApplied();

        if (notify)
            OnLevelChanged?.Invoke(level);
    }

    protected virtual void OnLevelApplied()
    {
    }

    public virtual void AbleToShoot(bool newAble)
    {
        if (spriteRenderer != null && !spriteRenderer.isVisible)
        {
            ableToShoot = false;
            return;
        }
        ableToShoot = newAble;
    }

    protected void RaiseShotFired()
    {
        OnShot?.Invoke(this);
    }

    private void SubscribeToOwnerLevel()
    {
        if (owner == null || subscribedToOwnerLevel)
            return;

        owner.OnLevelChanged += HandleLevelChanged;
        subscribedToOwnerLevel = true;
    }

    private void UnsubscribeFromOwnerLevel()
    {
        if (owner == null || !subscribedToOwnerLevel)
            return;

        owner.OnLevelChanged -= HandleLevelChanged;
        subscribedToOwnerLevel = false;
    }

    private void OnDestroy()
    {
        UnsubscribeFromOwnerLevel();
    }
}
