using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissingHealthPassive : PassiveAbility
{
    [SerializeField, HideInInspector] float maxBonus = 0.5f;

    private WeaponController weaponController;

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (stats.TryGetContract(out HuskarShipMetaContract contract))
            maxBonus = contract.MissingHealthFireRateBonus;
    }

    public override void Init(ParentShip ship)
    {
        owner = ship;
        weaponController = ship.GetComponent<WeaponController>();

        owner.OnHealthChanged += UpdateReloadModifier;
    }

    private void UpdateReloadModifier(float currentHp)
    {

        float missingPercent = 1f -  currentHp / owner.MaximumHealthPoints;

        float multiplier = 1f - missingPercent * maxBonus;

        weaponController.SetReloadMultiplier(multiplier);
    }
}
