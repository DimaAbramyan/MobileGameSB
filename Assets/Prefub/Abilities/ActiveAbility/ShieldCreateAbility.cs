using UnityEngine;

public sealed class ShieldCreateAbility : ActiveAbility
{
    private float shieldHealthMultiplier = 1f;

    // Kept only to preserve existing prefab data. The shared barrier is now
    // handled by TeamBarrierController and does not instantiate this visual.
    [SerializeField, HideInInspector] private ShieldAbilityPrefab shieldPrefab;

    protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        if (stats.TryGetContract(out HeavyShieldShipMetaContract contract))
            shieldHealthMultiplier = contract.ShieldHealthMultiplier;
    }

    public override bool Activate(ParentShip owner)
    {
        if (owner == null || owner.CurrentShieldPoints <= 0f)
            return false;

        PlayerController controller = owner.GetComponentInParent<PlayerController>();
        if (controller == null)
            return false;

        float barrierHealth = owner.CurrentShieldPoints * shieldHealthMultiplier;
        if (barrierHealth <= 0f || !controller.CreateTeamBarrier(barrierHealth))
            return false;

        owner.SetShieldPoints(0f);
        return true;
    }
}
