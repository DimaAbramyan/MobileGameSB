using UnityEngine;

public sealed class MetalDropBoostBuff : TeamTimedBuff
{
    [SerializeField, Min(1f)] private float metalDropMultiplier = 2f;
    [SerializeField, Min(0f)] private float duration = 16f;

    protected override void ApplyToTeam(PlayerController playerController)
    {
        playerController.MultiplyMetalDropsForSeconds(
            metalDropMultiplier,
            duration);
    }
}
