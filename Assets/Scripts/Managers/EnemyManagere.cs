using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager instance;
    public event Action<Enemy> OnEnemyDestroyed;
    public List<Enemy> enemyList { get; private set; }
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        enemyList = new List<Enemy>();
    }
    public void AddEnemy(Enemy enemy)
    {
        enemyList.Add(enemy);
    }

    public Enemy FindNearestEnemy(Vector3 fromPosition)
    {
        Enemy bestEnemy = null;
        float bestDistance = float.MaxValue;

        foreach (Enemy enemy in enemyList)
        {
            if (enemy == null)
                continue;

            float dist = Vector3.Distance(fromPosition, enemy.transform.position);

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }
    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        Debug.Log("ß ÓÁÈË");
        enemyList.Remove(enemy);
        OnEnemyDestroyed?.Invoke(enemy);
    }
}
