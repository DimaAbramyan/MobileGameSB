using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class InfoAboutSubWave : MonoBehaviour
{
    [Inject] EnemyManager enemyManager;
    MovementSequencePlayer movementSequencePlayer;
    int childCount = 0;
    List<Enemy> EnemiesInWave;
    public event Action OnSubWaveCleared;
    protected virtual void Awake()
    {
        movementSequencePlayer = GetComponent<MovementSequencePlayer>();
        EnemiesInWave = transform.GetComponentsInChildren<Enemy>().Where(enemy => enemy.CanContainBuff() == true).ToList();
        childCount = EnemiesInWave.Count;
        enemyManager.OnEnemyDestroyed += WhenEnemyKilled;
    }
    protected virtual void OnDestroy()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= WhenEnemyKilled;
    }
    public virtual void ActivateSubWave()
    {
        gameObject.SetActive(true);
    }
    public void WhenEnemyKilled(Enemy enemy)
    {
        if (!EnemiesInWave.Contains(enemy))
            return;
        childCount--;
        if (childCount <= 0)
        {
            NotifySubWaveCleared();
            Debug.Log("��������� �����");
        }
    }

    protected void NotifySubWaveCleared()
    {
        OnSubWaveCleared?.Invoke();
    }
}
