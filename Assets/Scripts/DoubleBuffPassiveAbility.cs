using System;
using UnityEngine;

public class DoubleBuffPassiveAbility : PassiveAbility
{
    private bool isTriggeredByBuff;
    Action OnLevelUpAction;
    private void OnEnable()
    {
        owner.OnLevelChanged += OnOwnerLevelChanged;
    }
    private void OnDisable()
    {
        owner.OnLevelChanged -= OnOwnerLevelChanged;
    }

    private void OnOwnerLevelChanged(int ship)
    {
        if (isTriggeredByBuff) return;
        isTriggeredByBuff = true;
        owner.LevelUp();
        isTriggeredByBuff = false;
    }
}
