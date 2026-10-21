using UnityEngine;

public sealed class DictatorDamagePassiveAbility : PassiveAbility,
    IOutgoingDamageModifier
{
    private float kineticDamageBonusPercent = 15f;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out DictatorShipMetaContract contract))
            return;

        kineticDamageBonusPercent = contract.KineticDamageBonusPercent;
    }

    public float ModifyOutgoingDamage(EnemyDamageType damageType, float damage)
    {
        if (!isActive || damage <= 0f)
            return damage;

        float bonusPercent = damageType == EnemyDamageType.Kinetick
            ? kineticDamageBonusPercent : 0f;

        return damage * (1f + Mathf.Max(0f, bonusPercent) / 100f);
    }
}
