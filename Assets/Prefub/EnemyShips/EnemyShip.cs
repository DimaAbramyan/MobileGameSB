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
    Parameters par;
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
        par = FindAnyObjectByType<Parameters>();
    }
    public void TakeDamage(float t)
    {
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
        if (GetComponent<IHaveBuff>() != null && Buff != null)
        {
            Instantiate(Buff, transform.position, Quaternion.identity);
            
        }
        if (this.Buff != null) 
        PointsCollector.Points += _maxHealth;
        enemyManager.NotifyEnemyDestroyed(this);
        isDead = true;
        animator.SetBool("IsDead", true);
        //Destroy(gameObject);
    }
    public bool CanContainBuff()
    {
        return DoHaveBuff;
    }
    
}
    