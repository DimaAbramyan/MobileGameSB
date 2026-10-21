using UnityEngine;

public sealed class OrbitExpansionActiveAbility : ActiveAbility
{
    [SerializeField] private OrbitPassiveAbility orbit;

    public override bool Activate(ParentShip ship)
    {
        return ship != null && orbit != null && orbit.BeginHold();
    }

    public override void Release(ParentShip ship) => orbit?.EndHold();

    protected override void OnDisable()
    {
        base.OnDisable();
        orbit?.EndHold();
    }
}
