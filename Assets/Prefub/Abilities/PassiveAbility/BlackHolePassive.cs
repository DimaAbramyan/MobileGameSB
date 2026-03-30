using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHolePassive : PassiveAbility
{
    List<EnemyProjectile> enemyProjectiles;
    float maxLenght = 3;
    public void Awake()
    {
        enemyProjectiles = new List<EnemyProjectile>();
    }
    public override void Off()
    {
        isActive = false;
        if (enemyProjectiles != null && enemyProjectiles.Count > 0 )
        foreach ( var EnemyProjectile  in enemyProjectiles)
        {
            EnemyProjectile.SpeedMultiplier = 1;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive)
            return;
        EnemyProjectile enemyProjectile = collision.gameObject.GetComponent<EnemyProjectile>();
        if (enemyProjectile != null)
        enemyProjectiles.Add(enemyProjectile);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!isActive)
            return;
        EnemyProjectile enemyProjectile = collision.GetComponent<EnemyProjectile>();

        if (enemyProjectile != null)
        {
            enemyProjectile.SpeedMultiplier =
                CountMultiplierPerDistance(transform.position, collision.transform.position);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        EnemyProjectile enemyProjectile = collision.GetComponent<EnemyProjectile>();
        if (enemyProjectile!=null && enemyProjectiles.Contains(enemyProjectile))
        enemyProjectiles.Remove(enemyProjectile);
        if (enemyProjectile != null)
            enemyProjectile.SpeedMultiplier = 1;
    }
    private float CountMultiplierPerDistance(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);

        float normalized = Mathf.Clamp01(distance / maxLenght);

        return Mathf.Max(0.1f, normalized);
    }
}
