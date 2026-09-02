using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealBuff : Buff
{
    [SerializeField, Min(0f)] private float health;

    private bool isCollected;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected)
            return;

        ParentShip colliderShip =
            collision.GetComponentInParent<ParentShip>();
        if (colliderShip == null || colliderShip.IsIntangible)
            return;

        isCollected = true;
        colliderShip.HealHealth(health);
        PointsCollector.Bonuses += 1;
        Destroy(gameObject);
    }

    public void Init(ParentShip parent, float extraHealth)
    {
        health = extraHealth;
    }

    public void SetHealth(float health)
    {
        this.health = health;
    }
}
