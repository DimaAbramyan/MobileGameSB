using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class InfoAboutSubWave : MonoBehaviour
{
    [Inject] EnemyManager enemyManager;
    int childCount = 0;
    List<Enemy> EnemiesInWave;
    public event Action OnSubWaveCleared;
    private void Awake()
    {
        EnemiesInWave = transform.GetComponentsInChildren<Enemy>().Where(enemy => enemy.CanContainBuff() == true).ToList();
        childCount = EnemiesInWave.Count;
        enemyManager.OnEnemyDestroyed += WhenEnemyKilled;
    }
    private void OnDestroy()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= WhenEnemyKilled;
    }
    public void WhenEnemyKilled(Enemy enemy)
    {
        if (!EnemiesInWave.Contains(enemy))
            return;
        childCount--;
        if (childCount <= 0)
        {
            OnSubWaveCleared?.Invoke();
            Debug.Log("”ничтожил волну");
        }
    }
}
