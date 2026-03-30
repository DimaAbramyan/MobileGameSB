using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHolePrefab : MonoBehaviour
{
    float liveTime;
    [SerializeField]
    float damage = 50;

    public void Init(float lifeTime)
    {
        liveTime = lifeTime;
        Destroy(gameObject, liveTime);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        EnemyProjectile proj = collision.gameObject.GetComponent<EnemyProjectile>();

        if (proj != null)
        {
            Destroy(proj.gameObject);
        }
        if (enemy != null)
        {
            enemy.TakeDamage(50);
        }
    }
}
