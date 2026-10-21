using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ExtraHealthPassive : PassiveAbility
{
    public event Action<float> OnExtraHealthChanged;
    public event Action<float> OnMaximumExtraHealthChanged;

    public float ExtraHealth { get; private set; }
    [SerializeField]
    public float MaximumExtraHealth = 1f;

    private bool subscribed;

    private void Awake() => Init(GetComponent<ParentShip>());
    private void OnEnable() => Init(GetComponent<ParentShip>());
    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    private void OnValidate()
    {
        MaximumExtraHealth = Mathf.Max(0f, MaximumExtraHealth);
        OnMaximumExtraHealthChanged?.Invoke(MaximumExtraHealth);
        SetExtraHealth(ExtraHealth);
    }

    public void SubscribeExtraHealth(Action<float> handler) => OnExtraHealthChanged += handler;
    public void UnsubscribeExtraHealth(Action<float> handler) => OnExtraHealthChanged -= handler;

    public override void Init(ParentShip ship)
    {
        if (subscribed && owner == ship)
            return;
        Unbind();
        owner = ship;
        if (owner == null || !isActiveAndEnabled)
            return;

        // Подписываемся на пайплайн урона
        owner.OnDamagePipeline += HandleDamage;

        // Подписываемся на переполнение исцеления
        owner.OnHealOverflow += HandleOverflow;
        owner.OnDied += ClearExtraHealth;
        subscribed = true;
    }

    private void Unbind()
    {
        if (subscribed && owner != null)
        {
            owner.OnDamagePipeline -= HandleDamage;
            owner.OnHealOverflow -= HandleOverflow;
            owner.OnDied -= ClearExtraHealth;
        }
        subscribed = false;
    }

    private void ClearExtraHealth() => SetExtraHealth(0f);

    public float AddExtraHealth(float amount)
    {
        if (amount <= 0f || owner == null || owner.IsDestroyed)
            return 0f;
        float addedHealth = Mathf.Min(amount,
            Mathf.Max(0f, MaximumExtraHealth - ExtraHealth));
        SetExtraHealth(ExtraHealth + addedHealth);
        return addedHealth;
    }

    // При получении урона сначала тратим ExtraHealth
    private float HandleDamage(float incomingDamage)
    {
        if (ExtraHealth <= 0f || incomingDamage <= 0f)
            return incomingDamage; // если сверхздоровья нет, передаём урон дальше

        float damageTaken = Mathf.Min(ExtraHealth, incomingDamage);
        ExtraHealth -= damageTaken;

        OnExtraHealthChanged?.Invoke(ExtraHealth);

        return incomingDamage - damageTaken;
    }

    private float HandleOverflow(float overflow)
    {
        float space = Mathf.Max(0f, MaximumExtraHealth - ExtraHealth);
        float taken = Mathf.Min(space, Mathf.Max(0f, overflow));

        SetExtraHealth(ExtraHealth + taken);

        return overflow - taken;
    }

    public void SetExtraHealth(float newExtraHealth)
    {
        ExtraHealth = Mathf.Clamp(newExtraHealth, 0f, Mathf.Max(0f, MaximumExtraHealth));
        OnExtraHealthChanged?.Invoke(ExtraHealth);
    }
}
