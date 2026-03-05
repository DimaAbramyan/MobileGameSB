using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExtraHealthPassive : PassiveAbility
{
    public event Action<float> OnExtraHealthChanged;

    public float ExtraHealth { get; private set; }
    [SerializeField]
    public float MaxExtraHealth;

    public void SubscribeExtraHealth(Action<float> handler)
    {
        OnExtraHealthChanged += handler;
    }

    public void UnsubscribeExtraHealth(Action<float> handler)
    {
        OnExtraHealthChanged -= handler;
    }

    public override void Init(ParentShip ship)
    {
        Debug.Log("ExtraHealthPassive initialized");
        ship.OnHealOverflow += HandleOverflow;
    }

    float HandleOverflow(float overflow)
    {
        float space = MaxExtraHealth - ExtraHealth;

        float taken = Mathf.Min(space, overflow);

        ExtraHealth += taken;

        OnExtraHealthChanged?.Invoke(ExtraHealth);

        return overflow - taken;
    }
}