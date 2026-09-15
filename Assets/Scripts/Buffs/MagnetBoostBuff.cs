using UnityEngine;

public sealed class MagnetBoostBuff : TeamTimedBuff
{
    [SerializeField, Min(0f)] private float radiusMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float duration = 12f;

    protected override void ApplyToTeam(PlayerController playerController)
    {
        playerController.MultiplyTeamMagnetRadiusForSeconds(
            radiusMultiplier,
            duration);
    }
}
