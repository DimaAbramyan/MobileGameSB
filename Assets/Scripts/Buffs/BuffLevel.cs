using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuffLevel : Buff
{
    private void FixedUpdate()
    {
        transform.localPosition += new Vector3(0, -1, 0) * speed;
    }
    private void OnTriggerEnter2D(Collider2D collision2D)
    {
        ParentShip colliderShip = collision2D.gameObject.GetComponent<ParentShip>();
        if (colliderShip == null)
            return;

        colliderShip.LevelUp();
            PointsCollector.Bonuses += 1;
            Destroy(gameObject);    
    }
}
