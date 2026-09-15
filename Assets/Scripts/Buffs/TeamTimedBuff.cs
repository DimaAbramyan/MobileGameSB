using UnityEngine;

public abstract class TeamTimedBuff : Buff
{
    private bool isCollected;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected)
            return;

        ParentShip collectorShip =
            collision.GetComponentInParent<ParentShip>();
        if (collectorShip == null || collectorShip.IsIntangible)
            return;

        PlayerController playerController =
            collectorShip.GetComponentInParent<PlayerController>();
        if (playerController == null)
            return;

        isCollected = true;
        ApplyToTeam(playerController);
        PointsCollector.Bonuses += 1;
        Destroy(gameObject);
    }

    protected abstract void ApplyToTeam(PlayerController playerController);
}
