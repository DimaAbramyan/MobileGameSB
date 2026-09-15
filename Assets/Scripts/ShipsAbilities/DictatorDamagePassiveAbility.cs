using UnityEngine;

public sealed class DictatorDamagePassiveAbility : PassiveAbility,
    IOutgoingDamageModifier
{
    private float kineticDamageBonusPercent = 15f;
    private float explosionDamageBonusPercent = 10f;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out DictatorShipMetaContract contract))
            return;

        kineticDamageBonusPercent = contract.KineticDamageBonusPercent;
        explosionDamageBonusPercent = contract.ExplosionDamageBonusPercent;
    }

    public float ModifyOutgoingDamage(EnemyDamageType damageType, float damage)
    {
        if (!isActive || damage <= 0f)
            return damage;

        float bonusPercent = damageType switch
        {
            EnemyDamageType.Kinetic => kineticDamageBonusPercent,
            EnemyDamageType.Explosion => explosionDamageBonusPercent,
            _ => 0f
        };

        return damage * (1f + Mathf.Max(0f, bonusPercent) / 100f);
    }
}
