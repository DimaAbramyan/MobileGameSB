using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealBuff : Buff
{
    ParentShip shipCreator;
    float timer = 0.25f;
    public float health;
    private void FixedUpdate()
    {
        transform.localPosition += new Vector3(0, -1, 0) * 0.46f;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ParentShip colliderShip = collision.gameObject.GetComponent<ParentShip>();
        if ((timer > 0f || shipCreator != colliderShip) && colliderShip == null)
            return;
        if (colliderShip.IsIntangible)
            return;

        colliderShip.SetHealthPoints(health);
        PointsCollector.Bonuses += 1;
        Destroy(gameObject);
    }
    public void Update()
    {
        timer -= Time.deltaTime;
    }
    public void Init(ParentShip parent, float extraHealth)
    {
        shipCreator = parent;
        health = extraHealth;
    }
    public void SetHealth(float health)
    {
        this.health = health;
    }
}
