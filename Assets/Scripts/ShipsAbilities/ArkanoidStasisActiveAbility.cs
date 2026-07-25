using UnityEngine;

public sealed class ArkanoidStasisActiveAbility : ActiveAbility
{
    [SerializeField] private ArkanoidPassiveAbility arkanoidPassiveAbility;

    public override bool Activate(ParentShip owner)
    {
        if (arkanoidPassiveAbility == null && owner != null)
            arkanoidPassiveAbility = owner.GetComponent<ArkanoidPassiveAbility>();
        if (arkanoidPassiveAbility == null)
            arkanoidPassiveAbility = GetComponent<ArkanoidPassiveAbility>();

        return arkanoidPassiveAbility != null
            && arkanoidPassiveAbility.TryActivateStasis();
    }
}
