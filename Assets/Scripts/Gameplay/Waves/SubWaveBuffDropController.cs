using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class SubWaveBuffDropController : MonoBehaviour
{
    [InjectOptional] private DiContainer container;

    [SerializeField, Min(0)] private int maxBuffs = 1;

    private readonly Dictionary<int, Buff> rewardsBySpawnIndex = new();
    private readonly List<int> availableSpawnIndices = new();
    private InfoAboutSubWave subWave;

    public int MaxBuffs => Mathf.Max(0, maxBuffs);

    private void Awake()
    {
        subWave = GetComponent<InfoAboutSubWave>();
        if (subWave == null)
        {
            Debug.LogError(
                $"{nameof(SubWaveBuffDropController)} requires an "
                + $"{nameof(InfoAboutSubWave)} on the same object.",
                this);
        }
    }

    private void OnEnable()
    {
        if (subWave != null)
            subWave.OnEnemySpawned += HandleEnemySpawned;
    }

    private void OnDisable()
    {
        if (subWave != null)
            subWave.OnEnemySpawned -= HandleEnemySpawned;
    }

    internal void PrepareForWave(int plannedEnemyCount)
    {
        rewardsBySpawnIndex.Clear();
        availableSpawnIndices.Clear();

        for (int i = 0; i < plannedEnemyCount; i++)
            availableSpawnIndices.Add(i);
    }

    internal bool TryAssignReward(Buff rewardPrefab)
    {
        if (rewardPrefab == null || availableSpawnIndices.Count == 0)
            return false;

        int availableIndex = Random.Range(0, availableSpawnIndices.Count);
        int spawnIndex = availableSpawnIndices[availableIndex];
        availableSpawnIndices[availableIndex] =
            availableSpawnIndices[availableSpawnIndices.Count - 1];
        availableSpawnIndices.RemoveAt(availableSpawnIndices.Count - 1);
        rewardsBySpawnIndex[spawnIndex] = rewardPrefab;
        return true;
    }

    private void HandleEnemySpawned(
        Enemy enemy,
        int spawnIndex,
        int plannedEnemyCount)
    {
        if (enemy == null
            || !rewardsBySpawnIndex.TryGetValue(spawnIndex, out Buff rewardPrefab))
        {
            return;
        }

        rewardsBySpawnIndex.Remove(spawnIndex);

        EnemyBuffDrop enemyDrop = enemy.GetComponent<EnemyBuffDrop>();
        if (enemyDrop == null)
            enemyDrop = enemy.gameObject.AddComponent<EnemyBuffDrop>();

        enemyDrop.Configure(rewardPrefab, container);
    }

    private void OnValidate()
    {
        maxBuffs = Mathf.Max(0, maxBuffs);
    }
}
