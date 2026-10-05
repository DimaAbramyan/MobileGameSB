using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealBuff : CollectiblePickup
{
    [SerializeField, Min(0f)] private float health;

    protected override bool TryApplyCollection(ParentShip collectorShip)
    {
        collectorShip.HealHealth(health);
        return true;
    }

    public void Init(ParentShip parent, float extraHealth)
    {
        health = extraHealth;
    }

    public void SetHealth(float health)
    {
        this.health = health;
    }

    public override bool CanBeSelectedForDrop(ParentShip player)
    {
        return player != null
            && player.CurrentHealthPoints < player.MaximumHealthPoints;
    }
}
