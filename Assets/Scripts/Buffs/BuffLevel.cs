using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BuffLevel : CollectiblePickup
{
    protected override bool TryApplyCollection(ParentShip collectorShip)
    {
        int previousLevel = collectorShip.GetLevel();
        int upgradedShips = collectorShip.LevelUpAllPlayerShips();
        int currentLevel = collectorShip.GetLevel();
        if (currentLevel > previousLevel)
        {
            Debug.Log(
                $"Level-up buff collected. {upgradedShips} ship(s) reached "
                + $"level {currentLevel}.",
                collectorShip);
        }

        return true;
    }

    public override bool CanBeSelectedForDrop(ParentShip player)
    {
        return player != null && !player.IsWeaponLevelMax;
    }
}
