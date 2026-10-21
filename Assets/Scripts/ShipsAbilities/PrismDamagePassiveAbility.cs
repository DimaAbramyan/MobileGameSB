using UnityEngine;

public sealed class PrismDamagePassiveAbility : PassiveAbility,
    IOutgoingDamageModifier
{
    private float plasmaDamageBonusPercent = 15f;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out PrismShipMetaContract contract))
            return;

        plasmaDamageBonusPercent = contract.PlasmaDamageBonusPercent;
    }

    public float ModifyOutgoingDamage(EnemyDamageType damageType, float damage)
    {
        if (!isActive || damage <= 0f)
            return damage;

        float bonusPercent = damageType == EnemyDamageType.Plazma
            ? plasmaDamageBonusPercent : 0f;

        return damage * (1f + Mathf.Max(0f, bonusPercent) / 100f);
    }
}
