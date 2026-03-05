using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ParentShip : MonoBehaviour, iDamagable
{
    [SerializeField] private ActiveAbility ActiveAbility;
    [SerializeField] private PassiveAbility passiveAbility;
    [SerializeField] public Transform ShieldAnchor;
    public ShipData ShipData;

    bool IsVisible;
    WaveManager waveManager;
    public event Action<float> OnShieldChanged;
    public event Action<float> OnHealthChanged;

    public event Func<float, float> OnHealOverflow;

    private float _currentShieldPoints;
    private float _currentHealthPoints;
    public void SubscribeHealth(Action<float> action)
    {
        OnHealthChanged += action;
    }
    public void UnsubscribeHealth(Action<float> action)
    {
        OnHealthChanged -= action;
    }
    public void SubscribeShield(Action<float> action)
    {
        OnShieldChanged += action;
    }
    public void UnsubscribeShield(Action<float> action)
    {
        OnShieldChanged -= action;
    }
    public void SetHealthPoints(float healthPoints)
    {
        CurrentHealthPoints = healthPoints;
    }
    public void SetShieldPoints(float shieldPoints)
    {
        CurrentShieldPoints = shieldPoints;
    }
    public void UseAbility()
    {
        ActiveAbility.TryActivate(this);
    }
    public float CurrentShieldPoints
    {
        get => _currentShieldPoints;
        private set
        {
            if (_currentShieldPoints == value)
                return;

            _currentShieldPoints = Mathf.Max(0, value);

            OnShieldChanged?.Invoke(_currentShieldPoints);
        }
    }
    public float CurrentHealthPoints
    {
        get => _currentHealthPoints;
        private set
        {
            if (_currentHealthPoints == value)
                return;

            _currentHealthPoints = Mathf.Max(0, value);

            OnHealthChanged?.Invoke(_currentHealthPoints);
        }
    }
    float currentHealthRegenTimer;
    float currentShieldRegenTimer;
    public void Update()
    {
        currentShieldRegenTimer -= Time.deltaTime;
        if (currentShieldRegenTimer <= 0)
        {
            HealShield(ShipData.shieldRegenRate * Time.deltaTime);
            currentShieldRegenTimer = 0;
        }
        if (!IsVisible)
        {
            currentHealthRegenTimer -= Time.deltaTime;
            if (currentHealthRegenTimer <= 0)
            {
                HealHealth(ShipData.healthRegenRate * Time.deltaTime);
                currentHealthRegenTimer = 0;
            }
        }
    }
    public void Start()
    {
        waveManager = FindObjectOfType<WaveManager>();

        SetHealthPoints(ShipData.maximumHealthPoints);
        SetShieldPoints(ShipData.maximumShieldPoints);
        ShipData.currentLvl = 0;
        if (passiveAbility != null)
        passiveAbility.Init(this);

    }
    public void Dying()
    {
        waveManager.MainHeroIsDead();
        Destroy(this.gameObject);
    }
    public virtual void HealHealth(float heal)
    {
        CurrentHealthPoints += heal;
        if (CurrentHealthPoints > ShipData.maximumHealthPoints)
        {
            CurrentHealthPoints = ShipData.maximumHealthPoints;
        }
    }
    public virtual void HealShield(float heal)
    {
        CurrentShieldPoints += heal;
        if (CurrentShieldPoints > ShipData.maximumShieldPoints)
        {
            CurrentShieldPoints = ShipData.maximumShieldPoints;
        }
    }
    public virtual void TakeDamage(float damage)
    {
        currentHealthRegenTimer = ShipData.healthRegenCooldown;
        currentShieldRegenTimer = ShipData.shieldRegenCooldown;
        if (CurrentShieldPoints > 0)
        {
            CurrentShieldPoints -= damage;
            Debug.Log("”Â·‡ÎË ÔÓ Ÿ»“”");
            Debug.Log($"Ÿ»“: {CurrentShieldPoints}");
            Debug.Log($"{ShipData.shipId}");
            return;
        }
        SetHealthPoints(CurrentHealthPoints - damage);
        Debug.Log("”Â·‡ÎË ÔÓ «ƒŒ–Œ¬‹ﬁ");
        Debug.Log($"«ƒŒ–Œ¬‹≈: {CurrentHealthPoints}");
        if (CurrentHealthPoints <= 0)
        {
            Dying();
        }
    }
    public void LvlUp()
    {
        if (ShipData.currentLvl < 4)
        {
            ShipData.currentLvl++;
        }
    }
    public void ShowShip()
    {
        IsVisible = true;
    }
    public void HideShip()
    {
        IsVisible = false;
        currentHealthRegenTimer = ShipData.healthRegenCooldown;
    }
}


