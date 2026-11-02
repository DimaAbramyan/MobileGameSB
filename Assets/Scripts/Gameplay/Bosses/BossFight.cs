using System;

using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class BossFight : MonoBehaviour, IWaveEncounter
{
    [Inject] private DiContainer container;

    [Header("Boss")]
    [SerializeField] private BossController bossPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnOffset;

    [Header("Encounter")]
    [SerializeField] private bool destroyEncounterOnDefeat = true;

    private WaveManager waveManager;
    private BossController bossInstance;
    private bool completed;
    private bool initialized;

    public BossController ActiveBoss => bossInstance;
    public event Action<BossController> BossSpawned;
    public event Action BossDefeated;

    public void Init(WaveManager owner)
    {
        if (initialized || completed)
            return;

        initialized = true;
        waveManager = owner;

        if (container == null)
        {
            FailEncounter("Zenject container is not injected. Instantiate the encounter through Zenject.");
            return;
        }

        if (bossPrefab == null)
        {
            FailEncounter("Boss prefab is not assigned.");
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        Vector3 position = point.position + spawnOffset;
        bossInstance = container.InstantiatePrefabForComponent<BossController>(
            bossPrefab,
            position,
            point.rotation,
            null);

        if (bossInstance == null)
        {
            FailEncounter("Boss prefab has no BossController component.");
            return;
        }

        bossInstance.Defeated += OnBossDefeated;
        BossSpawned?.Invoke(bossInstance);
    }

    private void OnBossDefeated(BossController defeatedBoss)
    {
        if (completed || defeatedBoss != bossInstance)
            return;

        completed = true;
        Unsubscribe();
        BossDefeated?.Invoke();
        waveManager?.CompleteCurrentWave();

        if (destroyEncounterOnDefeat)
            Destroy(gameObject);
    }

    private void FailEncounter(string reason)
    {
        Debug.LogError($"BossFight '{name}' cannot start. {reason}", this);
        completed = true;
        Unsubscribe();
        waveManager?.GoToNextWave();
        Destroy(gameObject);
    }

    private void Unsubscribe()
    {
        if (bossInstance != null)
            bossInstance.Defeated -= OnBossDefeated;
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (!completed && bossInstance != null)
            Destroy(bossInstance.gameObject);
    }
}
