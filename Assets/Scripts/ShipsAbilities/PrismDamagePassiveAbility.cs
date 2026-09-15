using UnityEngine;

public sealed class PrismDamagePassiveAbility : PassiveAbility,
    IOutgoingDamageModifier
{
    private float beamDamageBonusPercent = 15f;
    private float energyDamageBonusPercent = 10f;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out PrismShipMetaContract contract))
            return;

        beamDamageBonusPercent = contract.BeamDamageBonusPercent;
        energyDamageBonusPercent = contract.EnergyDamageBonusPercent;
    }

    public float ModifyOutgoingDamage(EnemyDamageType damageType, float damage)
    {
        if (!isActive || damage <= 0f)
            return damage;

        float bonusPercent = damageType switch
        {
            EnemyDamageType.Beam => beamDamageBonusPercent,
            EnemyDamageType.Energy => energyDamageBonusPercent,
            _ => 0f
        };

        return damage * (1f + Mathf.Max(0f, bonusPercent) / 100f);
    }
}
