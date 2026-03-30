using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GainHealthPassive : PassiveAbility
{
    [Header("Настройки")]
    [SerializeField] private float healthIncreasePerKill = 10f;
    
    private float totalAddedHealth = 0f;

    public event Action<float> OnMaxHealthIncreased;

    public float CurrentBonusHealth => totalAddedHealth;

    public override void Init(ParentShip ship)
    {
        owner = ship;
        EnemyManager.instance.OnEnemyDestroyed += OnEnemyKilled;
    }

    private void OnEnemyKilled(Enemy enemy)
    {
        if (!isActive)
            return;

        owner.AddMaxHealthPoints(healthIncreasePerKill);
    }

    void OnDestroy()
    {
        if (owner != null)
        {
            owner.OnEnemyKilledPipeline -= OnEnemyKilled;
        }
    }
}
