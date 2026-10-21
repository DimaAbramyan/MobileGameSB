using UnityEngine;

public sealed class ShieldCreateAbility : ActiveAbility
{
    private float extraHealthAmount = 1f;

    // Kept only to preserve existing prefab references to the previous ability.
    [SerializeField, HideInInspector] private ShieldAbilityPrefab shieldPrefab;

    // Start runs after ParentShip.Awake has applied the configured recovery settings.
    private void Start() => RestartRecovery();

    protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        if (stats.TryGetContract(out HeavyShieldShipMetaContract contract))
            extraHealthAmount = contract.ExtraHealthAmount;
    }

    public override bool Activate(ParentShip owner)
    {
        if (owner == null || owner.IsDestroyed || extraHealthAmount <= 0f)
            return false;

        ShipSelect team = owner.GetComponentInParent<ShipSelect>();
        if (team == null)
            return false;

        ParentShip target = team.GetLowestHealthShip();
        if (target == null)
            return false;

        ExtraHealthPassive extraHealth = target.GetComponent<ExtraHealthPassive>();
        if (extraHealth == null)
        {
            Debug.LogError($"ExtraHealth is missing on team ship '{target.name}'.", target);
            return false;
        }
        extraHealth.Init(target);
        return extraHealth.AddExtraHealth(extraHealthAmount) > 0f;
    }
}
