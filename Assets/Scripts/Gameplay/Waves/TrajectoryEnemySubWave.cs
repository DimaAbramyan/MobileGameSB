using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class TrajectoryEnemySubWave : InfoAboutSubWave
{
    [Inject] private DiContainer container;
    [Inject] private EnemyManager enemyManager;

    [Header("Spawn")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField, Min(1)] private int enemyCount = 1;
    [SerializeField, Min(0f)] private float spawnInterval = 0.5f;
    [SerializeField] private Transform spawnParent;

    [Header("Trajectory")]
    [SerializeField] private MovementCommandData[] trajectoryCommands;

    private readonly HashSet<Enemy> aliveSpawnedEnemies = new();
    private Coroutine spawnRoutine;
    private bool spawnFinished;
    private bool activated;

    protected override void Awake()
    {
    }

    protected override void OnDestroy()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
    }

    public override void ActivateSubWave()
    {
        gameObject.SetActive(true);

        if (activated)
            return;

        activated = true;
        spawnFinished = false;
        aliveSpawnedEnemies.Clear();

        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError(
                $"{nameof(TrajectoryEnemySubWave)} on {name} has no enemy prefab.",
                this);
            FinishSpawning();
            yield break;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();

            if (spawnInterval > 0f && i < enemyCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        FinishSpawning();
    }

    private void SpawnEnemy()
    {
        Transform parent = spawnParent != null ? spawnParent : transform;
        Enemy enemy = container != null
            ? container.InstantiatePrefabForComponent<Enemy>(
                enemyPrefab,
                parent.position,
                parent.rotation,
                parent)
            : Instantiate(enemyPrefab, parent.position, parent.rotation, parent);

        if (enemy == null)
            return;

        aliveSpawnedEnemies.Add(enemy);
        ConfigureTrajectory(enemy.gameObject);
    }

    private void ConfigureTrajectory(GameObject enemyObject)
    {
        if (enemyObject == null
            || trajectoryCommands == null
            || trajectoryCommands.Length == 0)
        {
            return;
        }

        bool wasActive = enemyObject.activeSelf;
        enemyObject.SetActive(false);

        MovementSequencePlayer movement =
            enemyObject.GetComponent<MovementSequencePlayer>();
        if (movement == null)
            movement = enemyObject.AddComponent<MovementSequencePlayer>();

        movement.SetCommands(trajectoryCommands);
        enemyObject.SetActive(wasActive);
    }

    private void FinishSpawning()
    {
        spawnFinished = true;
        spawnRoutine = null;
        TryComplete();
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || !aliveSpawnedEnemies.Remove(enemy))
            return;

        TryComplete();
    }

    private void TryComplete()
    {
        if (!spawnFinished || aliveSpawnedEnemies.Count > 0)
            return;

        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        NotifySubWaveCleared();
    }
}
