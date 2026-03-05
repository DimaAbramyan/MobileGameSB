using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealBuff : Buff
{
    [SerializeField] ParentShip shipCreator;
    [SerializeField] GameObject parentObject;
    float timer = 0.25f;
    public float health;
    private void FixedUpdate()
    {
        parentObject.transform.localPosition += new Vector3(0, -1, 0) * speed;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ParentShip colliderShip = collision.gameObject.GetComponent<ParentShip>();
        if ((timer > 0f || shipCreator != colliderShip) && colliderShip == null)
            return;

        colliderShip.SetHealthPoints(health);
        PointsCollector.Bonuses += 1;
        Destroy(parentObject);
    }
    public void Update()
    {
        timer -= Time.deltaTime;
    }
    public void SetHealth(float health)
    {
        this.health = health;
    }
}
