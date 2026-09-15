using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager
{
    public event Action<Enemy> OnEnemyDestroyed;
    public List<Enemy> enemyList { get; private set; }
    public void AddEnemy(Enemy enemy)
    {
        if (enemyList == null)
        {
            enemyList = new List<Enemy>();
        }
        enemyList.Add(enemy);
    }

    public Enemy FindNearestEnemy(Vector3 fromPosition)
    {
        return FindNearestEnemy(fromPosition, 0f, null);
    }

    public Enemy FindNearestEnemy(
        Vector3 fromPosition,
        float maxDistance,
        IReadOnlyList<Enemy> excludedEnemies)
    {
        if (enemyList == null || enemyList.Count == 0)
            return null;

        Enemy bestEnemy = null;
        float bestDistanceSqr = maxDistance > 0f
            ? maxDistance * maxDistance
            : float.PositiveInfinity;

        for (int enemyIndex = 0; enemyIndex < enemyList.Count; enemyIndex++)
        {
            Enemy enemy = enemyList[enemyIndex];
            if (enemy == null
                || enemy.isDead
                || !enemy.isActiveAndEnabled
                || IsExcluded(enemy, excludedEnemies))
            continue;

            float distanceSqr = (enemy.transform.position - fromPosition).sqrMagnitude;

            if (distanceSqr <= bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    public Enemy FindNearestEnemyInCone(
        Vector3 fromPosition,
        Vector3 forwardDirection,
        float maxDistance,
        float coneAngle,
        IReadOnlyList<Enemy> excludedEnemies)
    {
        if (enemyList == null || enemyList.Count == 0)
            return null;

        Vector2 forward = forwardDirection;
        if (forward.sqrMagnitude <= Mathf.Epsilon)
            return FindNearestEnemy(fromPosition, maxDistance, excludedEnemies);

        forward.Normalize();
        float clampedConeAngle = Mathf.Clamp(coneAngle, 0f, 360f);
        float halfConeAngle = clampedConeAngle * 0.5f;
        float maxDistanceSqr = maxDistance > 0f
            ? maxDistance * maxDistance
            : float.PositiveInfinity;
        Enemy bestEnemy = null;

        for (int enemyIndex = 0; enemyIndex < enemyList.Count; enemyIndex++)
        {
            Enemy enemy = enemyList[enemyIndex];
            if (enemy == null
                || enemy.isDead
                || !enemy.isActiveAndEnabled
                || IsExcluded(enemy, excludedEnemies))
            {
                continue;
            }

            Vector2 offset = enemy.transform.position - fromPosition;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr <= Mathf.Epsilon
                || distanceSqr > maxDistanceSqr
                || (clampedConeAngle < 360f
                    && Vector2.Angle(forward, offset) > halfConeAngle))
            {
                continue;
            }

            maxDistanceSqr = distanceSqr;
            bestEnemy = enemy;
        }

        return bestEnemy;
    }

    private static bool IsExcluded(
        Enemy enemy,
        IReadOnlyList<Enemy> excludedEnemies)
    {
        if (excludedEnemies == null)
            return false;

        for (int index = 0; index < excludedEnemies.Count; index++)
        {
            if (excludedEnemies[index] == enemy)
                return true;
        }

        return false;
    }
    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        enemyList.Remove(enemy);
        OnEnemyDestroyed?.Invoke(enemy);
    }
}
