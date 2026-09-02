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

    private bool ableToShoot;
    private bool subscribedToOwnerLevel;

    public event Action<int> OnLevelChanged;
    public event Action<Weapon> OnShot;

    public int Level => level;
    public Enemy Target => target;
    protected ParentShip Owner => owner;
    protected WeaponRuntimeStats CurrentStats => currentStats;
    protected bool IsAbleToShoot => ableToShoot;

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
        currentReloadTime = reloadTime * Mathf.Max(0f, reloadMultiplier);

        // A forced shot deliberately does not raise OnShot, preventing trigger loops.
        return shotFired;
    }

    protected virtual bool Fire()
    {
        if (weaponData == null || projectilePrefab == null || projectileSpawn == null)
            return false;

        return TrySpawnProjectile(
            CreateProjectileParams(),
            CreateProjectileRuntimeConfig());
    }

    protected virtual ProjectileParams CreateProjectileParams()
    {
        return new ProjectileParams
        {
            speed = currentStats.Speed,
            damage = currentStats.Damage,
            maxLength = currentStats.Range,
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
        if (projectilePrefab == null
            || projectileSpawn == null
            || projectilePoolController == null)
        {
            return false;
        }

        Projectile proj = projectilePoolController.Spawn(
            projectilePrefab,
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
        currentReloadTime = reloadTime;
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
