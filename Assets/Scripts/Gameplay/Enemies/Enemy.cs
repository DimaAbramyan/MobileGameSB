using System;
using System.Collections;

using System.Collections.Generic;
using UnityEngine.UI;
//using Unity.VisualScripting;
using UnityEngine;
using TMPro;
using static UnityEngine.GraphicsBuffer;
using Zenject;

public enum EnemyType
{
    Default,
    MiniBoss,
    Boss
}

public class Enemy : MonoBehaviour, iDamagable 
{
    private static readonly int DeathStateHash =
        Animator.StringToHash("Base Layer.Death");

    [Inject] EnemyManager enemyManager;
    [InjectOptional] private DealDamageManager damageNumberManager;
    [InjectOptional] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    private EnemyAttackClock attackClock;
    public WaveAttackPermissions AttackPermissions { get; internal set; }
    public float AttackSpeedMultiplier => projectileCollisionRegistry != null
        ? projectileCollisionRegistry.EvaluateActiveSpeedMultiplier(transform.position, true) : 1f;
    public float AttackTime => attackClock.Tick(Time.time,
        AttackPermissions != null && AttackPermissions.Interrupted ? 0f : AttackSpeedMultiplier);
    protected virtual void LateUpdate() { _ = AttackTime; }
    [SerializeField] private bool DoHaveBuff = true;
    public bool isDead = false;
    [SerializeField] TextMeshPro DamageShowing;
    [SerializeField] public GameObject Buff;
    [SerializeField] HealthBar healthBar;
    [SerializeField] protected float _maxHealth;
    public float _currentHealth;
    [SerializeField] protected float _fireRate;
    [SerializeField] protected float _damage;
    [SerializeField] protected float _speed;
    [Header("Classification")]
    [SerializeField] private EnemyType enemyType = EnemyType.Default;
    [Header("Q-Beam")]
    [SerializeField, Min(0f)] private float qBeamChargeMultiplier = 1f;
    [Header("Rewards")]
    [SerializeField, Min(0.01f)] private float metalMultiplier = 1f;
    [Header("Shield")]
    [SerializeField, Min(0f)] private float shieldPoints = 10f;
    private SpriteRenderer spriteRenderer;
    private EnemyShieldModifier shieldModifier;
    private bool bypassShieldForNextDamage;
    private bool forceShieldFirstForNextDamage;
    private bool hasDamageTypeForNextDamage;
    private EnemyDamageType damageTypeForNextDamage = EnemyDamageType.Default;
    private float damageMultiplier = 1f;
    private float fireRateMultiplier = 1f;
    private float movementSlowPercent;
    private float hullDamageVulnerabilityPercent;
    private float temperaturePercent;
    private bool isBurning;
    Animator animator;
    public event Action<Enemy> OnDied;
    public event Action<EnemyDebuffThresholdEvent> OnDebuffThresholdReached;
    public event Action<Enemy> OnBurningStarted;
    public event Action<float, float> OnQBeamChargeChanged;

    public EnemyType Type => enemyType;
    public bool ShowsQBeamCharge => enemyType != EnemyType.Default;
    public float QBeamChargeMultiplier => Mathf.Max(0f, qBeamChargeMultiplier);
    public virtual void Awake()
    {
        isBurning = false;
        animator = GetComponent<Animator>();
        if (DoHaveBuff)
        enemyManager.AddEnemy(this);
        spriteRenderer = GetComponent<SpriteRenderer>();
        shieldModifier = GetComponent<EnemyShieldModifier>();
        
        if (Buff != null)
        {
            PointsCollector.MaxPoints += _maxHealth;
        }
        _currentHealth = _maxHealth;
    }
    public virtual void TakeDamage(float t)
    {
        if (isDead)
            return;

        float healthBeforeDamage = _currentHealth;
        float shieldBeforeDamage = shieldModifier != null ? shieldModifier.CurrentShieldPoints : 0f;
        GameObject ShowIcon = null;
        EnemyDamageProfile profile = EnemyDamageProfiles.Get(
            hasDamageTypeForNextDamage
                ? damageTypeForNextDamage
                : EnemyDamageType.Default);
        float hullDamage = CalculateHullDamage(t, profile);
        if (Mathf.Approximately(hullDamage, 0f))
        {
            ReportDirectDamage(t, healthBeforeDamage, shieldBeforeDamage);
            return;
        }

        _currentHealth -= hullDamage;
        if (healthBar != null)
        {
            healthBar.SetHealth(_currentHealth / (_maxHealth / 100));
        }
        ReportDirectDamage(t, healthBeforeDamage, shieldBeforeDamage);
        if (_currentHealth <= 0)
        {
            Dying();
        }
        if (ShowIcon)
        {
            ShowIcon.GetComponent<RectTransform>().position = Camera.main.WorldToScreenPoint(this.transform.position);
        }
    }

    private void ReportDirectDamage(float incomingDamage, float healthBefore, float shieldBefore)
    {
        // Typed/managed hits publish once after their complete virtual damage call.
        if (hasDamageTypeForNextDamage) return;
        damageNumberManager?.NotifyDamageApplied(this, new EnemyDamageResult(
            healthBefore - _currentHealth,
            shieldBefore - (shieldModifier != null ? shieldModifier.CurrentShieldPoints : 0f)), incomingDamage);
    }
    public virtual void Dying()
    {
        if (isDead) return;

        isDead = true;
        SetQBeamCharge(0f, _currentHealth);
        OnDied?.Invoke(this);

        if (GetComponent<IHaveBuff>() != null && Buff != null)
        {
            Instantiate(Buff, transform.position, Quaternion.identity);
            
        }
        if (this.Buff != null) 
        PointsCollector.Points += _maxHealth;

        enemyManager?.NotifyEnemyDestroyed(this);
        DisableCombat();

        if (animator != null
            && animator.runtimeAnimatorController != null
            && animator.HasState(0, DeathStateHash))
        {
            animator.Play(DeathStateHash, 0, 0f);
            return;
        }

        Debug.LogWarning(
            $"Death animation is not configured for enemy {name}. Destroying immediately.",
            this);
        Destroy(gameObject);
    }

    private void DisableCombat()
    {
        foreach (Collider2D enemyCollider in GetComponentsInChildren<Collider2D>(true))
            enemyCollider.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == this)
                continue;

            behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }
    }

    public void TakeDamageIgnoringShield(float damage)
    {
        TakeDamageWithType(damage, EnemyDamageType.Default, true);
    }

    public void MultiplyHealth(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        _maxHealth *= safeMultiplier;
        _currentHealth *= safeMultiplier;

        if (healthBar != null)
            healthBar.SetHealth(_currentHealth / (_maxHealth / 100f));
    }

    public float DamageMultiplier => damageMultiplier;

    public float FireRateMultiplier => Mathf.Max(0.01f, fireRateMultiplier);

    public float MovementSlowPercent => Mathf.Clamp(
        movementSlowPercent,
        0f,
        100f);

    public float MovementSpeedMultiplier => Mathf.Clamp01(
        1f - MovementSlowPercent / 100f);

    public float HullDamageVulnerabilityPercent => Mathf.Max(
        0f,
        hullDamageVulnerabilityPercent);

    // Negative values mean cold/frozen, positive values mean accumulated heat.
    public float TemperaturePercent => Mathf.Clamp(
        temperaturePercent,
        -100f,
        100f);
    public float MaximumHealth => Mathf.Max(0f, _maxHealth);

    public bool IsBurning => isBurning;

    protected float EffectiveMoveSpeed => _speed * MovementSpeedMultiplier;

    public float MetalMultiplier => Mathf.Max(0.01f, metalMultiplier);

    public float ShieldPoints => Mathf.Max(0f, shieldPoints);

    public bool HasActiveShield => shieldModifier != null
        && shieldModifier.IsShieldActive;

    public Vector3 DamageNumberCenter => spriteRenderer != null
        ? spriteRenderer.bounds.center
        : transform.position;

    public void MultiplyDamage(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        damageMultiplier *= safeMultiplier;
        _damage *= safeMultiplier;
    }

    public void MultiplyFireRate(float multiplier)
    {
        fireRateMultiplier *= Mathf.Max(0.01f, multiplier);
    }

    public void ApplyMovementSlow(
        float slowPercentPerHit,
        float maximumSlowPercent)
    {
        if (isDead)
            return;

        float previousMultiplier = MovementSpeedMultiplier;
        float maximumSlow = Mathf.Clamp(maximumSlowPercent, 0f, 100f);
        float slowPerHit = Mathf.Clamp(slowPercentPerHit, 0f, maximumSlow);
        movementSlowPercent = Mathf.Min(
            maximumSlow,
            movementSlowPercent + slowPerHit);
        temperaturePercent = -movementSlowPercent;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null && previousMultiplier > Mathf.Epsilon)
            body.linearVelocity *= MovementSpeedMultiplier / previousMultiplier;
    }

    public float ApplyHullDamageVulnerability(
        float percentPerHit,
        float maximumPercent)
    {
        if (isDead || percentPerHit <= 0f)
            return HullDamageVulnerabilityPercent;

        float maximum = Mathf.Max(0f, maximumPercent);
        hullDamageVulnerabilityPercent = Mathf.Min(
            maximum,
            HullDamageVulnerabilityPercent + percentPerHit);
        return hullDamageVulnerabilityPercent;
    }

    public float RemoveMovementSlow(float amountPercent)
    {
        if (isDead || amountPercent <= 0f || movementSlowPercent <= 0f)
            return 0f;

        float previousMultiplier = MovementSpeedMultiplier;
        float removedAmount = Mathf.Min(
            movementSlowPercent,
            Mathf.Max(0f, amountPercent));
        movementSlowPercent -= removedAmount;
        temperaturePercent = -movementSlowPercent;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null && previousMultiplier > Mathf.Epsilon)
            body.linearVelocity *= MovementSpeedMultiplier / previousMultiplier;

        return removedAmount;
    }

    public void SetTemperaturePercent(float temperature)
    {
        temperaturePercent = Mathf.Clamp(temperature, -100f, 100f);
    }

    public void SetQBeamCharge(float charge, float maximumCharge)
    {
        if (!ShowsQBeamCharge)
            return;

        OnQBeamChargeChanged?.Invoke(
            Mathf.Max(0f, charge),
            Mathf.Max(0f, maximumCharge));
    }

    public void NotifyDebuffThresholdReached(
        EnemyDebuffThresholdEvent thresholdEvent)
    {
        OnDebuffThresholdReached?.Invoke(thresholdEvent);
    }

    public void BeginBurning()
    {
        if (isBurning)
            return;

        isBurning = true;
        OnBurningStarted?.Invoke(this);
    }

    public void StopBurning()
    {
        isBurning = false;
    }

    public void MultiplyShieldPoints(float multiplier)
    {
        shieldPoints *= Mathf.Max(0.01f, multiplier);
    }

    public EnemyDamageResult TakeDamageWithType(
        float damage,
        EnemyDamageType damageType,
        bool bypassesShield = false)
    {
        return TakeDamageWithType(damage, damageType, bypassesShield, false);
    }

    public EnemyDamageResult TakeDamageWithType(
        float damage,
        EnemyDamageType damageType,
        bool bypassesShield,
        bool forceShieldFirst)
    {
        return ApplyManagedDamage(damage, damageType, bypassesShield, forceShieldFirst,
            damageNumberManager, damage);
    }

    internal EnemyDamageResult ApplyManagedDamage(float damage, EnemyDamageType damageType,
        bool bypassesShield, bool forceShieldFirst, DealDamageManager reporter, float originalDamage)
    {
        bool previousBypassShield = bypassShieldForNextDamage;
        bool previousForceShieldFirst = forceShieldFirstForNextDamage;
        bool previousHasDamageType = hasDamageTypeForNextDamage;
        EnemyDamageType previousDamageType = damageTypeForNextDamage;
        bypassShieldForNextDamage = bypassesShield;
        forceShieldFirstForNextDamage = forceShieldFirst;
          hasDamageTypeForNextDamage = true;
          damageTypeForNextDamage = damageType;
          float healthBeforeDamage = _currentHealth;
          float shieldBeforeDamage = shieldModifier != null
              ? shieldModifier.CurrentShieldPoints
              : 0f;
          try
          {
              TakeDamage(damage);
        }
        finally
        {
            bypassShieldForNextDamage = previousBypassShield;
            forceShieldFirstForNextDamage = previousForceShieldFirst;
            hasDamageTypeForNextDamage = previousHasDamageType;
            damageTypeForNextDamage = previousDamageType;
        }

          var result = new EnemyDamageResult(
              healthBeforeDamage - _currentHealth,
              shieldBeforeDamage - (shieldModifier != null
                  ? shieldModifier.CurrentShieldPoints
                  : 0f));
          reporter?.NotifyDamageApplied(this, result, originalDamage);
          return result;
      }

    private float CalculateHullDamage(
        float incomingDamage,
        EnemyDamageProfile profile)
    {
        if (incomingDamage <= 0f || bypassShieldForNextDamage)
            return ApplyHullDamageVulnerability(
                incomingDamage * profile.HullMultiplier);

        float bypassFraction = forceShieldFirstForNextDamage ? 0f : profile.ShieldBypassFraction;
        float shieldInput = incomingDamage * (1f - bypassFraction);
        float shieldOverflow = shieldModifier == null
            ? shieldInput
            : shieldModifier.AbsorbDamage(
                shieldInput,
                profile.ShieldMultiplier);
        float hullInput = incomingDamage * bypassFraction
            + shieldOverflow;
        return ApplyHullDamageVulnerability(hullInput * profile.HullMultiplier);
    }

    private float ApplyHullDamageVulnerability(float hullDamage)
    {
        if (hullDamage <= 0f)
            return 0f;

        return hullDamage * (1f + HullDamageVulnerabilityPercent / 100f);
    }

    public void OnDeathAnimationFinished()
    {
        if (isDead)
            Destroy(gameObject);
    }
    public bool CanContainBuff()
    {
        return DoHaveBuff;
    }

    private void OnValidate()
    {
        shieldPoints = Mathf.Max(0f, shieldPoints);
        metalMultiplier = Mathf.Max(0.01f, metalMultiplier);
        qBeamChargeMultiplier = Mathf.Max(0f, qBeamChargeMultiplier);
    }
    
}
