using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ParentShip : MonoBehaviour, iDamagable
{
    [Inject] SoundManager soundManager;
    [Inject] AudioDatabase audioDatabase;

    [Header("Abilities")]
    [SerializeField] private ActiveAbility activeAbility;
    [SerializeField] private PassiveAbility passiveAbility;

    [Header("References")]
    [SerializeField] public Transform ShieldAnchor;
    public ShipData ShipData;

    [HideInInspector] public bool IsVisible;

    private WaveManager waveManager;
    private PlayerController playerController;

    private int currentLevel;
    private float currentShieldPoints;
    private float currentHealthPoints;

    public float MaximumHealthPoints { get; private set; }
    public float MaximumShieldPoints { get; private set; }

    #region Events
    public event Action<float> OnShieldChanged;
    public event Action<float> OnHealthChanged;

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
        MaximumHealthPoints = ShipData.maximumHealthPoints;
        MaximumShieldPoints = ShipData.maximumShieldPoints;

        CurrentHealthPoints = MaximumHealthPoints;
        CurrentShieldPoints = MaximumShieldPoints;
    }
    public virtual void Start()
    {
        waveManager = FindAnyObjectByType<WaveManager>();
        playerController = GetComponent<PlayerController>();


        currentLevel = 0;

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
            float difference = CurrentHealthPoints - MaximumHealthPoints ;
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
        if (OnDamagePipeline != null)
        {
            foreach (Func<float, float> handler in OnDamagePipeline.GetInvocationList())
                damage = handler.Invoke(damage);
        }

        if (CurrentShieldPoints > 0)
        {
            CurrentShieldPoints -= damage;
            return;
        }

        CurrentHealthPoints -= damage;

        OnDamageDealt?.Invoke(damage);

        if (CurrentHealthPoints <= 0)
            Dying();
    }

    public void NotifyDamageDealt(float damage)
    {
        OnDamageDealt?.Invoke(damage);
    }
    #endregion

    #region Abilities
    public void UseAbility()
    {
        activeAbility?.TryActivate(this);
    }

    public void ShowShip()
    {
        IsVisible = true;
        passiveAbility?.On();
    }

    public void HideShip()
    {
        IsVisible = false;
        passiveAbility?.Off();
    }
    #endregion

    #region Leveling
    public int GetLevel() => currentLevel;

    public void SetLevel(int newLevel) => currentLevel = newLevel;

    public void LevelUp()
    {
        if (currentLevel >= 4) return;
        soundManager.PlaySound(audioDatabase.LevelUp, transform.position);
        currentLevel++;
        Debug.Log($"Новый уровень: {currentLevel}");
        OnLevelChanged?.Invoke(currentLevel);
    }
    #endregion

    #region Death
    public void Dying()
    {
        waveManager?.MainHeroIsDead();
        Destroy(gameObject);
    }
    #endregion

    #region Event Helpers
    public void SubscribeHealth(Action<float> action) => OnHealthChanged += action;
    public void UnsubscribeHealth(Action<float> action) => OnHealthChanged -= action;

    public void SubscribeShield(Action<float> action) => OnShieldChanged += action;
    public void UnsubscribeShield(Action<float> action) => OnShieldChanged -= action;
    #endregion
}