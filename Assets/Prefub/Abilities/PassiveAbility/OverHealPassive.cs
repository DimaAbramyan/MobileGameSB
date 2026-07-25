using System;
using UnityEngine;

public class ExtraHealthPassive : PassiveAbility
{
    public event Action<float> OnExtraHealthChanged;

    public float ExtraHealth { get; private set; }
    [SerializeField]
    public float MaximumExtraHealth;

    private ParentShip owner;

    public void SubscribeExtraHealth(Action<float> handler) => OnExtraHealthChanged += handler;
    public void UnsubscribeExtraHealth(Action<float> handler) => OnExtraHealthChanged -= handler;

    public override void Init(ParentShip ship)
    {
        owner = ship;

        // Подписываемся на пайплайн урона
        owner.OnDamagePipeline += HandleDamage;

        // Подписываемся на переполнение исцеления
        owner.OnHealOverflow += HandleOverflow;
    }

    // При получении урона сначала тратим ExtraHealth
    private float HandleDamage(float incomingDamage)
    {
        if (ExtraHealth <= 0f)
            return incomingDamage; // если сверхздоровья нет, передаём урон дальше

        float damageTaken = Mathf.Min(ExtraHealth, incomingDamage);
        ExtraHealth -= damageTaken;

        OnExtraHealthChanged?.Invoke(ExtraHealth);

        return incomingDamage - damageTaken;
    }

    private float HandleOverflow(float overflow)
    {
        float space = MaximumExtraHealth - ExtraHealth;
        float taken = Mathf.Min(space, overflow);

        ExtraHealth += taken;

        return overflow - taken;
    }

    public void SetExtraHealth(float newExtraHealth)
    {
        ExtraHealth = Mathf.Clamp(newExtraHealth, 0, MaximumExtraHealth);
        OnExtraHealthChanged?.Invoke(ExtraHealth);
    }
}