using UnityEngine;

public sealed class FireRateBuff : TeamTimedBuff
{
    [SerializeField, Min(0f)] private float fireRateMultiplier = 2f;
    [SerializeField, Min(0f)] private float duration = 6f;

    protected override void ApplyToTeam(PlayerController playerController)
    {
        playerController.MultiplyTeamFireRateForSeconds(
            fireRateMultiplier,
            duration);
    }
}
