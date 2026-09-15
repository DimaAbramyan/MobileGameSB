public sealed class AbilityCooldownResetBuff : TeamTimedBuff
{
    public override bool CanBeSelectedForDrop(ParentShip player)
    {
        if (player == null)
            return false;

        PlayerController playerController =
            player.GetComponentInParent<PlayerController>();
        return playerController != null
            && playerController.GetTeamAbilityRecoveryNeed() > 0f;
    }

    protected override void ApplyToTeam(PlayerController playerController)
    {
        playerController.RestoreTeamAbilityRecoveryResources();
    }
}
