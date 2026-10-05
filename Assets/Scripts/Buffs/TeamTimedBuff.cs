using UnityEngine;

public abstract class TeamTimedBuff : CollectiblePickup
{
    protected override bool TryApplyCollection(ParentShip collectorShip)
    {
        PlayerController playerController =
            collectorShip.GetComponentInParent<PlayerController>();
        if (playerController == null)
            return false;

        ApplyToTeam(playerController);
        return true;
    }

    protected abstract void ApplyToTeam(PlayerController playerController);
}
