using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuffLevel : Buff
{
    [SerializeField] GameObject parentObject;
    private void FixedUpdate()
    {
        parentObject.transform.localPosition += new Vector3(0, -1, 0) * speed;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ParentShip colliderShip = collision.gameObject.GetComponent<ParentShip>();
        if (colliderShip == null)
            return;

        Debug.Log(colliderShip.gameObject.transform.position);
        colliderShip.LvlUp();
            PointsCollector.Bonuses += 1;
            Destroy(parentObject);    
    }
}
