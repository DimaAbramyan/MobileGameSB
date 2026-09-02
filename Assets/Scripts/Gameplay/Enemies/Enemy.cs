using System;
using System.Collections;

using System.Collections.Generic;
using UnityEngine.UI;
//using Unity.VisualScripting;
using UnityEngine;
using TMPro;
using static UnityEngine.GraphicsBuffer;
using Zenject;

public class Enemy : MonoBehaviour, iDamagable 
{
    private static readonly int DeathStateHash =
        Animator.StringToHash("Base Layer.Death");

    [Inject] EnemyManager enemyManager;
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
    [Header("Rewards")]
    [SerializeField, Min(0.01f)] private float metalMultiplier = 1f;
    [Header("Shield")]
    [SerializeField, Min(0f)] private float shieldPoints = 10f;
    private SpriteRenderer spriteRenderer;
    private EnemyShieldModifier shieldModifier;
    private bool bypassShieldForNextDamage;
    private bool hasDamageTypeForNextDamage;
    private EnemyDamageType damageTypeForNextDamage = EnemyDamageType.Radiation;
    private float damageMultiplier = 1f;
    private float fireRateMultiplier = 1f;
    Animator animator;
    public event Action<Enemy> OnDied;
    public virtual void Awake()
    {
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

        GameObject ShowIcon = null;
        EnemyDamageProfile profile = EnemyDamageProfiles.Get(
            hasDamageTypeForNextDamage
                ? damageTypeForNextDamage
                : EnemyDamageType.Radiation);
        float hullDamage = CalculateHullDamage(t, profile);
        if (Mathf.Approximately(hullDamage, 0f))
            return;

        _currentHealth -= hullDamage;
        if (healthBar != null)
        {
            healthBar.SetHealth(_currentHealth / (_maxHealth / 100));
        }
        if (_currentHealth <= 0)
        {
            Dying();
        }
        if (ShowIcon)
        {
            ShowIcon.GetComponent<RectTransform>().position = Camera.main.WorldToScreenPoint(this.transform.position);
        }
    }
    public virtual void Dying()
    {
        if (isDead) return;

        isDead = true;
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
        TakeDamageWithType(damage, EnemyDamageType.Radiation, true);
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

    public float MetalMultiplier => Mathf.Max(0.01f, metalMultiplier);

    public float ShieldPoints => Mathf.Max(0f, shieldPoints);

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

    public void MultiplyShieldPoints(float multiplier)
    {
        shieldPoints *= Mathf.Max(0.01f, multiplier);
    }

    public void TakeDamageWithType(
        float damage,
        EnemyDamageType damageType,
        bool bypassesShield = false)
    {
        bool previousBypassShield = bypassShieldForNextDamage;
        bool previousHasDamageType = hasDamageTypeForNextDamage;
        EnemyDamageType previousDamageType = damageTypeForNextDamage;
        bypassShieldForNextDamage = bypassesShield;
        hasDamageTypeForNextDamage = true;
        damageTypeForNextDamage = damageType;
        try
        {
            TakeDamage(damage);
        }
        finally
        {
            bypassShieldForNextDamage = previousBypassShield;
            hasDamageTypeForNextDamage = previousHasDamageType;
            damageTypeForNextDamage = previousDamageType;
        }
    }

    private float CalculateHullDamage(
        float incomingDamage,
        EnemyDamageProfile profile)
    {
        if (incomingDamage <= 0f || bypassShieldForNextDamage)
            return incomingDamage * profile.HullMultiplier;

        float shieldInput = incomingDamage
            * (1f - profile.ShieldBypassFraction);
        float shieldOverflow = shieldModifier == null
            ? shieldInput
            : shieldModifier.AbsorbDamage(
                shieldInput,
                profile.ShieldMultiplier);
        float hullInput = incomingDamage * profile.ShieldBypassFraction
            + shieldOverflow;
        return hullInput * profile.HullMultiplier;
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
    }
    
}
