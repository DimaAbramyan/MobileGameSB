using System;

using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ParentShip : MonoBehaviour, iDamagable
{
    public const int MinWeaponLevel = 1;
    public const int MaxWeaponLevel = 10;
    public const float EnemyHitDamage = 1f;
    private const float HitInvulnerabilityDuration = 0.5f;

    [Inject] SoundManager soundManager;
    [Inject] AudioDatabase audioDatabase;
    [Inject] private PlayerController playerController;
    [Inject] private WaveManager waveManager;
    [Inject] private EnemyProjectileCollisionRegistry enemyProjectileCollisionRegistry;

    [Header("Abilities")]
    [SerializeField] private ActiveAbility activeAbility;
    [SerializeField] private PassiveAbility passiveAbility;
    [SerializeField] private ShipIntangibleState intangibleState;

    [Header("References")]
    [SerializeField] public Transform ShieldAnchor;
    public ShipData ShipData;
    [SerializeField] private Magnite buffMagnet;
    [Tooltip("The root collider used by ECS enemy projectiles to damage this ship.")]
    [SerializeField] private Collider2D damageHitboxCollider;
    [Header("Health Damage")]
    [SerializeField, Min(0f), Tooltip("Clears all enemy projectile types in this radius after health damage. 0 disables the clear.")]
    private float healthDamageProjectileClearRadius = 1.75f;
    public float HealthDamageProjectileClearRadius => Mathf.Max(0f, healthDamageProjectileClearRadius);
    [HideInInspector] public bool IsVisible;
    public bool IsDestroyed { get; private set; }
    private HealthRegeneration healthRegeneration;
    private ShieldRegeneration shieldRegeneration;

    private int currentLevel = MinWeaponLevel;
    private PlayerController teamLevelOwner;
    private float currentShieldPoints;
    private float currentHealthPoints;
    private float damageInvulnerableUntil;
    private float healthRegenerationCooldownProgress = 1f;
    private float shieldRegenerationCooldownProgress = 1f;

    public float MaximumHealthPoints { get; private set; }
    public float MaximumShieldPoints { get; private set; }
    public ActiveAbility ActiveAbility => activeAbility;
    public PassiveAbility PassiveAbility => passiveAbility;
    public float AbilityRecoveryNeed => activeAbility != null
        ? activeAbility.RecoveryNeed
        : 0f;
    public bool IsIntangible =>
        intangibleState != null && intangibleState.IsActive;
    public bool IsDamageInvulnerable =>
        Time.time < damageInvulnerableUntil;
    public float HealthRegenerationCooldownProgress =>
        healthRegenerationCooldownProgress;
    public float ShieldRegenerationCooldownProgress =>
        shieldRegenerationCooldownProgress;
    public Collider2D DamageHitboxCollider
    {
        get
        {
            CacheDamageHitboxCollider();
            return damageHitboxCollider;
        }
    }

    private void OnEnable()
    {
        enemyProjectileCollisionRegistry?.RegisterShip(this);
    }

    private void OnDisable()
    {
        enemyProjectileCollisionRegistry?.UnregisterShip(this);
    }

    #region Events
    public event Action<float> OnHealthChanged;
    public event Action<float> OnShieldChanged;
    public event Action OnHealthRegenerationStarted;
    public event Action OnShieldRegenerationStarted;
    public event Action OnHealthFullyRegenerated;
    public event Action OnShieldFullyRegenerated;
    public event Action<float> OnHealthRegenerationCooldownProgressChanged;
    public event Action<float> OnShieldRegenerationCooldownProgressChanged;
    public event Action<float> OnDamageTaken;
    public event Action<float> OnHealthDamageTaken;
    public event Action OnDied;

    public event Action<int> OnLevelChanged;
    public event Func<float, float> OnHealOverflow;
    public event Action<float> OnDamageDealt;
    public event Func<float, float> OnDamagePipeline;

    public event Action<float> OnMaxHealthChanged;
    public event Action<float> OnMaxShieldChanged;
    #endregion

    #region Properties
    public float CurrentHealthPoints
    {
        get => currentHealthPoints;
        private set
        {
            float newValue = Mathf.Max(0, value);
            if (Mathf.Approximately(currentHealthPoints, newValue)) return;
            currentHealthPoints = newValue;
            OnHealthChanged?.Invoke(currentHealthPoints);
        }
    }

    public float CurrentShieldPoints
    {
        get => currentShieldPoints;
        private set
        {
            float newValue = Mathf.Clamp(value, 0, MaximumShieldPoints);
            if (Mathf.Approximately(currentShieldPoints, newValue)) return;
            currentShieldPoints = newValue;
            OnShieldChanged?.Invoke(currentShieldPoints);
        }
    }
    #endregion

    #region Initialization
    public virtual void Awake()
    {
        ExtraHealthPassive extraHealth = GetComponent<ExtraHealthPassive>();
        if (extraHealth == null)
            extraHealth = gameObject.AddComponent<ExtraHealthPassive>();
        extraHealth.Init(this);
        healthRegeneration = GetComponent<HealthRegeneration>();
        shieldRegeneration = GetComponent<ShieldRegeneration>();
        CacheDamageHitboxCollider();
        EnsureHitboxIndicator();
        EnsureIntangibleState();
        EnsureBuffMagnet();

        ShipMetaRuntimeStats metaStats = ShipData != null
            ? ShipData.GetMetaRuntimeStats(0)
            : default;
        MaximumHealthPoints = metaStats.MaximumHealthPoints;
        MaximumShieldPoints = metaStats.MaximumShieldPoints;

        CurrentHealthPoints = MaximumHealthPoints;
        CurrentShieldPoints = MaximumShieldPoints;
        activeAbility?.ApplyShipMetaStats(metaStats);
        passiveAbility?.ApplyShipMetaStats(metaStats);
    }

    private void OnValidate()
    {
        CacheDamageHitboxCollider();
    }

    private void CacheDamageHitboxCollider()
    {
        if (damageHitboxCollider != null
            && damageHitboxCollider.gameObject == gameObject)
        {
            return;
        }

        damageHitboxCollider = GetComponent<Collider2D>();
    }

    private void EnsureHitboxIndicator()
    {
        if (damageHitboxCollider == null)
            return;

        PlayerHitboxIndicator indicator = GetComponent<PlayerHitboxIndicator>();
        if (indicator == null)
            indicator = gameObject.AddComponent<PlayerHitboxIndicator>();

        indicator.Initialize(this, damageHitboxCollider);
    }

      public virtual void Start()
      {
          currentLevel = GetLevel();
          ApplyAbilityDamageProgression();

          enemyProjectileCollisionRegistry?.RegisterShip(this);

          passiveAbility?.Init(this);
      }
    #endregion

    #region Health & Shield
    public void SetHealthPoints(float healthPoints)
    {
        CurrentHealthPoints = healthPoints;
    }

    public void SetShieldPoints(float shieldPoints)
    {
        CurrentShieldPoints = shieldPoints;
    }

    public virtual void HealHealth(float heal)
    {
        CurrentHealthPoints += heal;
        if (CurrentHealthPoints > MaximumHealthPoints)
        {
            float difference = CurrentHealthPoints - MaximumHealthPoints;
            CurrentHealthPoints = MaximumHealthPoints;
            OnHealOverflow?.Invoke(difference);
        }
    }

    public virtual void HealShield(float heal)
    {
        CurrentShieldPoints += heal;
        if (CurrentShieldPoints > MaximumShieldPoints)
            CurrentShieldPoints = MaximumShieldPoints;
    }

    public void NotifyHealthRegenerationStarted()
    {
        OnHealthRegenerationStarted?.Invoke();
    }

    public void NotifyShieldRegenerationStarted()
    {
        OnShieldRegenerationStarted?.Invoke();
    }

    public void NotifyHealthFullyRegenerated()
    {
        OnHealthFullyRegenerated?.Invoke();
    }

    public void NotifyShieldFullyRegenerated()
    {
        OnShieldFullyRegenerated?.Invoke();
    }

    public void SetHealthRegenerationCooldownProgress(float progress)
    {
        SetRegenerationCooldownProgress(
            ref healthRegenerationCooldownProgress,
            progress,
            OnHealthRegenerationCooldownProgressChanged);
    }

    public void SetShieldRegenerationCooldownProgress(float progress)
    {
        SetRegenerationCooldownProgress(
            ref shieldRegenerationCooldownProgress,
            progress,
            OnShieldRegenerationCooldownProgressChanged);
    }

    private static void SetRegenerationCooldownProgress(
        ref float currentProgress,
        float progress,
        Action<float> changed)
    {
        float normalizedProgress = Mathf.Clamp01(progress);
        if (Mathf.Approximately(currentProgress, normalizedProgress))
            return;

        currentProgress = normalizedProgress;
        changed?.Invoke(currentProgress);
    }

    public void AddMaxHealthPoints(float addedHealth)
    {
        MaximumHealthPoints += addedHealth;
        OnMaxHealthChanged?.Invoke(MaximumHealthPoints);
        HealHealth(addedHealth);
    }

    public void AddMaxShieldPoints(float addedShield)
    {
        MaximumShieldPoints += addedShield;
        OnMaxShieldChanged?.Invoke(MaximumShieldPoints);
        HealShield(addedShield);
    }
    #endregion

    #region Damage
    public virtual void TakeDamage(float damage)
    {
        TryTakeDamage(damage);
    }

    /// <summary>
    /// Applies damage and opens the shared post-hit invulnerability window.
    /// Callers can use the result to avoid impact side effects when damage
    /// was rejected by the same window.
    /// </summary>
    public virtual bool TryTakeDamage(float damage)
        => ApplyIncomingDamage(damage, false);

    // Explicit opt-in for authored beam damage. Legacy enemy impacts retain their one-unit rule.
    public bool TryTakeConfiguredDamage(float damage) => ApplyIncomingDamage(damage, true);

    private bool ApplyIncomingDamage(float damage, bool useConfiguredDamage)
    {
        if (IsDestroyed || damage <= 0f)
            return false;
        if (IsIntangible || IsDamageInvulnerable)
            return false;

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (playerController != null && playerController.ControlsLocked)
            return false;

        // Incoming enemy hits use one health/shield unit regardless of legacy
        // projectile values or difficulty. Defensive abilities still run below.
        if (!useConfiguredDamage) damage = EnemyHitDamage;
        if (OnDamagePipeline != null)
        {
            foreach (Func<float, float> handler in OnDamagePipeline.GetInvocationList())
                damage = handler.Invoke(damage);
        }

        if (damage <= 0f)
            return false;

        if (CurrentShieldPoints > 0)
        {
            CurrentShieldPoints -= damage;
            GrantDamageInvulnerability(HitInvulnerabilityDuration);
            OnDamageTaken?.Invoke(damage);
            return true;
        }

        float healthBeforeDamage = CurrentHealthPoints;
        CurrentHealthPoints -= damage;
        if (CurrentHealthPoints < healthBeforeDamage)
        {
            OnHealthDamageTaken?.Invoke(healthBeforeDamage - CurrentHealthPoints);
            ClearEnemyProjectilesAfterHealthDamage();
        }
        GrantDamageInvulnerability(HitInvulnerabilityDuration);
        OnDamageTaken?.Invoke(damage);

        OnDamageDealt?.Invoke(damage);

        if (CurrentHealthPoints <= 0)
            Dying();

        return true;
    }

    private void ClearEnemyProjectilesAfterHealthDamage()
    {
        float radius = HealthDamageProjectileClearRadius;
        if (radius <= 0f) return;
        Vector2 center = transform.position;
        float radiusSquared = radius * radius;
        // Damage is infrequent; an uncapped overlap avoids leaving projectiles behind
        // in dense patterns or when one projectile has multiple colliders.
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyProjectile projectile = hits[i].GetComponentInParent<EnemyProjectile>();
            if (projectile != null
                && ((Vector2)projectile.transform.position - center).sqrMagnitude <= radiusSquared)
                projectile.TryDestroyByAbility(true);
        }
        enemyProjectileCollisionRegistry?.ClearEnemyProjectiles(center, radius);
    }

    private void GrantDamageInvulnerability(float duration)
    {
        if (duration <= 0f)
            return;

        damageInvulnerableUntil = Mathf.Max(
            damageInvulnerableUntil,
            Time.time + duration);
    }

    public void SetDamageInvulnerableForSeconds(float duration)
    {
        GrantDamageInvulnerability(duration);
    }

    public void NotifyDamageDealt(float damage)
    {
        OnDamageDealt?.Invoke(damage);
    }
    #endregion

    #region Abilities
    public void UseAbility()
    {
        if (IsDestroyed)
            return;
        activeAbility?.TryActivate(this);
    }

    public void ReleaseAbility()
    {
        activeAbility?.TryRelease(this);
    }

    public bool RestoreAbilityRecoveryResources()
    {
        return activeAbility != null
            && activeAbility.RestoreAllRecoveryResources();
    }

    public void LockShipSwitching(float duration)
    {
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        playerController?.LockShipSwitching(duration);
    }

    public void EnterIntangibleState()
    {
        EnsureIntangibleState();
        intangibleState.Enter();
    }

    public void ExitIntangibleState()
    {
        if (intangibleState == null)
            return;

        intangibleState.Exit();
    }

    public void SetIntangibleForSeconds(float duration)
    {
        if (duration <= 0f)
            return;

        EnsureIntangibleState();
        intangibleState.ActivateForSeconds(duration);
    }

    private void EnsureIntangibleState()
    {
        if (intangibleState == null)
            intangibleState = GetComponent<ShipIntangibleState>();
        if (intangibleState == null)
            intangibleState = gameObject.AddComponent<ShipIntangibleState>();
        intangibleState.ConfigureDefaultHitbox(DamageHitboxCollider);
    }

    private void EnsureBuffMagnet()
    {
        if (buffMagnet == null)
            TryGetComponent(out buffMagnet);
        if (buffMagnet == null)
            buffMagnet = GetComponentInChildren<Magnite>(true);
        if (buffMagnet == null)
        {
            var magnetObject = new GameObject("Magnet");
            magnetObject.transform.SetParent(transform, false);
            int magnetLayer = LayerMask.NameToLayer("Magnet");
            if (magnetLayer >= 0)
                magnetObject.layer = magnetLayer;
            var magnetZone = magnetObject.AddComponent<CircleCollider2D>();
            magnetZone.isTrigger = true;
            buffMagnet = magnetObject.AddComponent<Magnite>();
        }
    }

    public void MultiplyMagnetRadiusForSeconds(
        float multiplier,
        float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        EnsureBuffMagnet();
        buffMagnet.ActivateRadiusMultiplier(multiplier, duration);
    }

    public void ShowShip()
    {
        if (IsDestroyed)
            return;
        IsVisible = true;
        healthRegeneration?.SetPaused(true);
        passiveAbility?.On();
    }

    public void HideShip()
    {
        IsVisible = false;
        healthRegeneration?.SetPaused(IsDestroyed || CurrentHealthPoints <= 0f);
        passiveAbility?.Off();
    }
    #endregion

    #region Leveling
    public int GetLevel() => teamLevelOwner != null ? teamLevelOwner.TeamBattleLevel : currentLevel;
    public bool IsWeaponLevelMax => GetLevel() >= MaxWeaponLevel;

    public int LevelUpAllPlayerShips()
    {
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        return playerController != null ? playerController.LevelUpAllShips() : 0;
    }

    internal void BindTeamBattleLevel(PlayerController owner)
    {
        teamLevelOwner = owner;
        if (owner != null) ApplyTeamBattleLevel(owner.TeamBattleLevel);
    }

    public void SetLevel(int newLevel)
    {
        if (teamLevelOwner != null)
        {
            teamLevelOwner.SetTeamBattleLevel(newLevel);
            return;
        }
        ApplyTeamBattleLevel(newLevel);
    }

    internal bool ApplyTeamBattleLevel(int level)
    {
        int nextLevel = Mathf.Clamp(level, MinWeaponLevel, MaxWeaponLevel);
        if (currentLevel == nextLevel) return false;
        currentLevel = nextLevel;
        ApplyAbilityDamageProgression();
        OnLevelChanged?.Invoke(currentLevel);
        return true;
    }

    public void LevelUp()
    {
        if (teamLevelOwner != null)
        {
            teamLevelOwner.LevelUpAllShips();
            return;
        }
        if (IsWeaponLevelMax) return;
        PlayBattleLevelUpSound();
        ApplyTeamBattleLevel(currentLevel + 1);
    }

    internal void PlayBattleLevelUpSound()
    {
        if (soundManager != null && audioDatabase != null)
            soundManager.PlaySound(audioDatabase.LevelUp, transform.position);
    }

    private void ApplyAbilityDamageProgression()
    {
        float multiplier = ShipData != null
            ? ShipData.GetAbilityDamageMultiplier(GetLevel())
            : 1f;
        activeAbility?.ApplyBattleAbilityDamageMultiplier(multiplier);
        passiveAbility?.ApplyBattleAbilityDamageMultiplier(multiplier);
    }
    #endregion

    #region Death
    public void Dying()
    {
        if (IsDestroyed)
            return;
        IsDestroyed = true;
        CurrentHealthPoints = 0f;
        CurrentShieldPoints = 0f;
        OnDied?.Invoke();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        bool isTeamShip = playerController != null && playerController.IsTeamShip(this);
        if (playerController == null || !playerController.HandleShipDeath(this))
            waveManager?.MainHeroIsDead();

        if (!isTeamShip)
            Destroy(gameObject);
    }

    public bool ReviveAfterWave()
    {
        if (!IsDestroyed)
            return false;

        IsDestroyed = false;
        CurrentHealthPoints = 1f;
        CurrentShieldPoints = 0f;
        damageInvulnerableUntil = 0f;
        ExitIntangibleState();
        HideShip();
        healthRegeneration?.RestartCooldown();
        shieldRegeneration?.RestartCooldown();
        SetHealthRegenerationCooldownProgress(0f);
        SetShieldRegenerationCooldownProgress(0f);
        activeAbility?.RestartRecovery();
        gameObject.SetActive(true);
        return true;
    }
    #endregion

    #region Event Helpers
    public void SubscribeHealth(Action<float> action) => OnHealthChanged += action;
    public void UnsubscribeHealth(Action<float> action) => OnHealthChanged -= action;

    public void SubscribeShield(Action<float> action) => OnShieldChanged += action;
    public void UnsubscribeShield(Action<float> action) => OnShieldChanged -= action;
    #endregion
}
