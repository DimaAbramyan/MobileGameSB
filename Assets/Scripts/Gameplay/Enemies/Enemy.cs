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
    private SpriteRenderer spriteRenderer;
    Animator animator;
    public void Awake()
    {
        animator = GetComponent<Animator>();
        if (DoHaveBuff)
        enemyManager.AddEnemy(this);
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (Buff != null)
        {
            PointsCollector.MaxPoints += _maxHealth;
        }
        _currentHealth = _maxHealth;
    }
    public void TakeDamage(float t)
    {
        if (isDead)
            return;

        GameObject ShowIcon = null;
        _currentHealth -= t;
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
    public void Dying()
    {
        if (isDead) return;

        isDead = true;

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

    public void OnDeathAnimationFinished()
    {
        if (isDead)
            Destroy(gameObject);
    }
    public bool CanContainBuff()
    {
        return DoHaveBuff;
    }
    
}
