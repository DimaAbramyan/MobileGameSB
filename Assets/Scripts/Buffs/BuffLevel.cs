using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BuffLevel : Buff
{
    private bool isCollected;

    private void OnTriggerEnter2D(Collider2D collision2D)
    {
        if (isCollected)
            return;

        ParentShip colliderShip =
            collision2D.GetComponentInParent<ParentShip>();
        if (colliderShip == null || colliderShip.IsIntangible)
            return;

        isCollected = true;
        int previousLevel = colliderShip.GetLevel();
        int upgradedShips = colliderShip.LevelUpAllPlayerShips();
        int currentLevel = colliderShip.GetLevel();
        if (currentLevel > previousLevel)
        {
            Debug.Log(
                $"Level-up buff collected. {upgradedShips} ship(s) reached "
                + $"level {currentLevel}.",
                colliderShip);
        }

        PointsCollector.Bonuses += 1;
        Destroy(gameObject);
    }
}
