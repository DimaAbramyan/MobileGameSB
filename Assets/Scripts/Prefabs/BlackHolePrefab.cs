using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHolePrefab : MonoBehaviour
{
    float liveTime;
    [SerializeField, HideInInspector]
    float damage = 50;
    private float runtimeDamage = -1f;

    public void Init(float lifeTime, float metaDamage = -1f)
    {
        liveTime = lifeTime;
        runtimeDamage = metaDamage;
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
            enemy.TakeDamage(runtimeDamage >= 0f ? runtimeDamage : damage);
        }
    }
}
