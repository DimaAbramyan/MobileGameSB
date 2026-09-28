using UnityEngine;

using System;
using System.Collections.Generic;
using Zenject;

public class Weapon : MonoBehaviour
{
    [Inject] DiContainer container;
    [Inject] private ProjectilePoolController projectilePoolController;
    [Inject] private PlayerProjectileEcsSpawner playerProjectileEcsSpawner;

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
      private bool usesSweepFire;
      private float sweepTraversalDuration = 1f;
      private AnimationCurve sweepSpeedCurve;
      private float sweepStartTime;
      private FanFireWeaponMetaContract fanFireContract;
      private int fanFireGroupIndex;

      private bool ableToShoot;
      private bool subscribedToOwnerLevel;
      private bool reportedUnsupportedConfiguredProjectile;
      private int remainingBurstVolleys;
      private float burstDelayRemaining;
      private float burstReloadMultiplier = 1f;

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
        PrewarmEntityProjectileConfigurations();
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

          if (remainingBurstVolleys > 0)
          {
              burstDelayRemaining -= Time.deltaTime;
              if (burstDelayRemaining > 0f)
                  return false;

              Fire();
              remainingBurstVolleys--;
              if (remainingBurstVolleys > 0)
              {
                  burstDelayRemaining += GetBurstDelay();
              }
              else
              {
                  currentReloadTime = reloadTime * burstReloadMultiplier;
              }

              return false;
          }

          currentReloadTime -= Time.deltaTime;
          if (currentReloadTime <= 0f)
          {
              bool shotFired = BeginFireSequence();
              currentReloadTime = reloadTime;

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

      private bool BeginFireSequence(bool raiseShotEvent = true)
      {
          bool shotFired = Fire();
          if (!shotFired)
              return false;

          if (shotFired && raiseShotEvent)
              RaiseShotFired();

          int volleys = Mathf.Max(1, currentStats.VolleysPerActivation);
          remainingBurstVolleys = volleys - 1;
          if (remainingBurstVolleys <= 0)
              return shotFired;

          float volleyDelay = GetBurstDelay();
          if (volleyDelay > 0f)
          {
              burstDelayRemaining = volleyDelay;
              return shotFired;
          }

          while (remainingBurstVolleys > 0)
          {
              Fire();
              remainingBurstVolleys--;
          }

          return shotFired;
      }

      private float GetBurstDelay()
      {
          return Mathf.Max(0f, currentStats.DelayBetweenVolleys);
      }

      protected virtual bool Fire()
      {
          if (weaponData == null || projectileSpawn == null)
              return false;

          bool shotFired;
          if (weaponData.UsesProjectileData)
          {
              shotFired = FireConfiguredProjectiles();
          }
          else
          {
              if (projectilePrefab == null)
                  return false;

              shotFired = TryFireProjectileFan(
                  projectilePrefab,
                  CreateProjectileParams(),
                  CreateProjectileRuntimeConfig());
          }

          if (shotFired)
              AdvanceFanFirePattern();

          return shotFired;
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
            if (!weaponData.TryGetProjectileRuntimeStats(
                    projectileSlot.Id,
                    level,
                    out _,
                    out ProjectileRuntimeStats projectileStats))
            {
                continue;
            }

            ProjectileParams parameters = CreateProjectileParams(projectileStats);
            if (projectileData.DeliveryType == ProjectileDeliveryType.Contact)
            {
                firedAnyProjectile |= TryFireEntityContact(
                    projectileData,
                    parameters);
                continue;
            }

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

            if (projectileData.UsesEntities)
            {
                bool hasSecondaryRuntimeStats = TryGetPrimarySecondaryProjectileRuntimeStats(
                    projectileData,
                    out ProjectileRuntimeStats secondaryRuntimeStats);
                firedAnyProjectile |= TryFireEntityProjectileFan(
                    projectileData,
                    parameters,
                    hasSecondaryRuntimeStats,
                    secondaryRuntimeStats);
                continue;
            }

            ProjectileRuntimeConfig runtimeConfig =
                projectileData.CreateRuntimeConfig();
            runtimeConfig.explosionDamage *= identicalWeaponDamageMultiplier;
            ConfigureSecondaryProjectileRuntimeStats(runtimeConfig);
            firedAnyProjectile |= TryFireProjectileFan(
                projectileData.ProjectilePrefab,
                parameters,
                runtimeConfig);
        }

        return firedAnyProjectile;
    }

    private void ConfigureSecondaryProjectileRuntimeStats(
        ProjectileRuntimeConfig runtimeConfig)
    {
        if (!TryGetSecondaryProjectileRuntimeStats(
                runtimeConfig.secondaryProjectile,
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

    private bool TryGetPrimarySecondaryProjectileRuntimeStats(
        ProjectileData primaryProjectile,
        out ProjectileRuntimeStats secondaryStats)
    {
        secondaryStats = default;
        if (primaryProjectile == null)
            return false;

        return TryGetSecondaryProjectileRuntimeStats(
            primaryProjectile.CreateRuntimeConfig().secondaryProjectile,
            out secondaryStats);
    }

    private bool TryGetSecondaryProjectileRuntimeStats(
        ProjectileData secondaryProjectile,
        out ProjectileRuntimeStats secondaryStats)
    {
        secondaryStats = default;
        if (secondaryProjectile == null
            || weaponData == null
            || !weaponData.TryGetProjectileRuntimeStats(
                secondaryProjectile,
                level,
                out _,
                out secondaryStats))
        {
            return false;
        }

        secondaryStats = new ProjectileRuntimeStats(
            secondaryStats.Damage * identicalWeaponDamageMultiplier,
            secondaryStats.Range,
            secondaryStats.Speed);
        return true;
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
            direction = GetProjectileDirection(),
            maxAngle = usesSweepFire ? 0f : currentStats.Angle,
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
            direction = GetProjectileDirection(),
            maxAngle = usesSweepFire ? 0f : currentStats.Angle,
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

      private bool TryFireProjectileFan(
          Projectile projectileToSpawn,
          ProjectileParams baseParameters,
          ProjectileRuntimeConfig runtimeConfig)
      {
            int projectileCount = GetFanProjectileCount();
          bool firedAnyProjectile = false;
          for (int projectileIndex = 0;
               projectileIndex < projectileCount;
               projectileIndex++)
          {
              ProjectileParams parameters = baseParameters;
                if (UsesFanFire())
                {
                    float angleOffset = fanFireContract.GetAngleOffset(
                        projectileIndex,
                        fanFireGroupIndex);
                  parameters.direction = Quaternion.Euler(0f, 0f, angleOffset)
                      * baseParameters.direction;
                  parameters.maxAngle = 0f;
              }

              firedAnyProjectile |= TrySpawnProjectile(
                  projectileToSpawn,
                  parameters,
                  runtimeConfig);
          }

          return firedAnyProjectile;
      }

    protected bool TrySpawnEntityBallLightning(
            ProjectileData projectileData,
            ProjectileParams parameters,
            float areaDamage,
            float areaRadius,
            float areaTickInterval,
            int damageLayers)
        {
            if (playerProjectileEcsSpawner == null || projectileSpawn == null)
            {
                Debug.LogError(
                    $"Weapon '{name}' requires {nameof(PlayerProjectileEcsSpawner)} "
                    + "and a projectile spawn point for Ball Lightning.",
                    this);
                return false;
            }

            return playerProjectileEcsSpawner.TrySpawnBallLightning(
                projectileData,
                projectileSpawn.position,
                parameters,
                owner,
                areaDamage,
                areaRadius,
                areaTickInterval,
                damageLayers);
        }

    private bool TryFireEntityContact(
        ProjectileData projectileData,
        ProjectileParams parameters)
    {
        if (playerProjectileEcsSpawner == null || projectileSpawn == null)
        {
            Debug.LogError(
                $"Weapon '{name}' requires {nameof(PlayerProjectileEcsSpawner)} "
                + "and a contact spawn point for Contact delivery.",
                this);
            return false;
        }

        return playerProjectileEcsSpawner.TrySpawnContact(
            projectileData,
            projectileSpawn.position,
            parameters,
            owner);
    }

      private bool TryFireEntityProjectileFan(
          ProjectileData projectileData,
          ProjectileParams baseParameters,
          bool hasSecondaryRuntimeStats = false,
          ProjectileRuntimeStats secondaryRuntimeStats = default)
      {
          if (playerProjectileEcsSpawner == null)
          {
              Debug.LogError(
                  $"Weapon '{name}' requires {nameof(PlayerProjectileEcsSpawner)} "
                  + "for its Entity projectile.",
                  this);
              return false;
          }

          int projectileCount = GetFanProjectileCount();
          bool firedAnyProjectile = false;
          for (int projectileIndex = 0;
               projectileIndex < projectileCount;
               projectileIndex++)
          {
              ProjectileParams parameters = baseParameters;
              if (UsesFanFire())
              {
                  float angleOffset = fanFireContract.GetAngleOffset(
                      projectileIndex,
                      fanFireGroupIndex);
                  parameters.direction = Quaternion.Euler(0f, 0f, angleOffset)
                      * baseParameters.direction;
                  parameters.maxAngle = 0f;
              }

               firedAnyProjectile |= playerProjectileEcsSpawner.TrySpawn(
                   projectileData,
                   projectileSpawn.position,
                   parameters,
                   owner,
                   hasSecondaryRuntimeStats,
                   secondaryRuntimeStats);
          }

          return firedAnyProjectile;
      }

    private void PrewarmEntityProjectileConfigurations()
    {
        if (weaponData?.WeaponMetaConfig == null || playerProjectileEcsSpawner == null)
            return;

        IReadOnlyList<WeaponProjectileSlot> projectileSlots =
            weaponData.WeaponMetaConfig.ProjectileSlots;
        for (int index = 0; index < projectileSlots.Count; index++)
        {
            ProjectileData projectileData = projectileSlots[index]?.Projectile;
            if (projectileData != null && projectileData.UsesEntities)
                playerProjectileEcsSpawner.Prewarm(projectileData);
        }
    }

      public virtual void Reload(float multiplier)
      {
          burstReloadMultiplier = Mathf.Max(0f, multiplier);
          if (remainingBurstVolleys == 0)
              currentReloadTime = reloadTime * burstReloadMultiplier;
    }

    public virtual void SetIdenticalWeaponCount(int count)
    {
        int weaponCount = Mathf.Max(1, count);
        SetIdenticalWeaponMultipliers(weaponCount, weaponCount, 1f);
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
          ConfigureSweepFire();
          ConfigureFanFire();
      }

      private void ConfigureFanFire()
      {
          WeaponMetaConfig metaConfig = weaponData != null
              ? weaponData.WeaponMetaConfig
              : null;
          fanFireContract = metaConfig != null
              && metaConfig.GetLevel(0).TryGetContract(
                  out FanFireWeaponMetaContract contract)
              ? contract
              : null;
          fanFireGroupIndex = 0;
      }

      private bool UsesFanFire()
      {
          return fanFireContract != null && fanFireContract.IsEnabled;
      }

      private int GetFanProjectileCount()
      {
          return UsesFanFire()
              ? fanFireContract.GetProjectileCount(fanFireGroupIndex)
              : 1;
      }

      private void AdvanceFanFirePattern()
      {
          if (fanFireContract == null || !fanFireContract.CyclesPartialFanGroups)
              return;

          fanFireGroupIndex++;
      }

    private void ConfigureSweepFire()
    {
        WeaponMetaConfig metaConfig = weaponData != null
            ? weaponData.WeaponMetaConfig
            : null;
        if (metaConfig == null)
        {
            usesSweepFire = false;
            return;
        }

        WeaponMetaRuntimeStats metaStats = metaConfig.GetRuntimeStats(0);
        bool wasUsingSweepFire = usesSweepFire;
        usesSweepFire = metaStats.UsesSweepFire;
        sweepTraversalDuration = Mathf.Max(
            0.02f,
            metaStats.SweepTraversalDuration);
        sweepSpeedCurve = metaStats.SweepSpeedCurve;

        if (usesSweepFire && !wasUsingSweepFire)
            sweepStartTime = Time.time;
    }

    private Vector3 GetProjectileDirection()
    {
        Vector3 initialDirection = Quaternion.Euler(
            0f,
            0f,
            currentStats.InitialDirectionAngle) * transform.up;

        if (!usesSweepFire || currentStats.Angle <= 0f)
            return initialDirection;

        float traversalProgress = Mathf.PingPong(
            (Time.time - sweepStartTime) / sweepTraversalDuration,
            1f);
        float curveProgress = sweepSpeedCurve != null
            ? Mathf.Clamp01(sweepSpeedCurve.Evaluate(traversalProgress))
            : traversalProgress;
        float angle = Mathf.Lerp(
            -currentStats.Angle,
            currentStats.Angle,
            curveProgress);
        return Quaternion.Euler(0f, 0f, angle) * initialDirection;
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
