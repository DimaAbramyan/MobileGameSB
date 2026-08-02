using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;


public class WaveManager : MonoBehaviour
{
    [Inject] DiContainer container;
    [Inject] LevelCatalog levelCatalog;
    [SerializeField] private GameObject GameOver;
    [SerializeField] private GameObject EndGame;
    [SerializeField] private WaveProgressPopup waveProgressPopup;
    [SerializeField] private bool enableDebugLogs = true;
    public event Action OnWaveCleared;
    private List<GameObject> wavePrefabs;
    private GameObject currentWaveInstance;
    private InfoAboutSubWave currentInlineSubWave;
    private int currentWaveIndex = 0;
    private Coroutine waveRoutine;
    private bool canStartWaves;

    private WaveProgressPopup WaveProgressPopup =>
        waveProgressPopup ??= WaveProgressPopup.FindScenePopup();

    private void Awake()
    {
        Time.timeScale = 1f;
        wavePrefabs = new List<GameObject>();

        LevelConfig config = LevelLoader.GetSelectedLevel(levelCatalog);
        if (config == null)
        {
            LogError("Selected LevelConfig is null. Waves cannot be loaded.");
            return;
        }

        Log(
            $"Selected level: id={config.Id}, name={config.name}, configured waves={config.Waves?.Count ?? 0}");

        foreach (GameObject prefab in config.Waves)
        {
            if (prefab == null)
            {
                LogWarning("Skipped null wave prefab in LevelConfig.Waves.");
                continue;
            }

            Log($"Registered wave prefab: {prefab.name}", prefab);
            wavePrefabs.Add(prefab);
        }

        if (wavePrefabs.Count == 0)
        {
            Debug.LogError(
                $"No waves configured for level {config.Id} ({config.name}).",
                config);
            return;
        }

        canStartWaves = true;
    }
    void Start()
    {
        Time.timeScale = 1f;

        if (!canStartWaves || waveRoutine != null)
            return;

        waveRoutine = StartCoroutine(ActivateWaveRoutine(showPopupBeforeWave: true));
    }
    public void GoToNextWave()
    {
        currentWaveIndex++;
        Log($"GoToNextWave called. Next index={currentWaveIndex}, total={wavePrefabs?.Count ?? 0}");

        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        waveRoutine = StartCoroutine(ActivateWaveRoutine(showPopupBeforeWave: true));
    }

    private IEnumerator ActivateWaveRoutine(bool showPopupBeforeWave)
    {
        if (currentWaveIndex < wavePrefabs.Count)
        {
            Log(
                $"Preparing wave {currentWaveIndex + 1}/{wavePrefabs.Count}: {wavePrefabs[currentWaveIndex]?.name}");

            if (showPopupBeforeWave && WaveProgressPopup != null)
            {
                yield return WaveProgressPopup.ShowAndWait(
                    currentWaveIndex + 1,
                    wavePrefabs.Count);
            }

            Activate();
        }
        else
        {
            Log("All waves completed. Returning to map/end screen.");
            ReturnToMap();
        }
    }
    private void Activate()
    {
        CleanupCurrentInlineSubWave();

        GameObject prefab = wavePrefabs[currentWaveIndex];
        if (prefab == null)
        {
            LogError($"Wave prefab at index {currentWaveIndex} is null. Skipping.");
            GoToNextWave();
            return;
        }

        Log($"Instantiating wave prefab: {prefab.name}", prefab);
        currentWaveInstance = container.InstantiatePrefab(prefab, transform);

        if (currentWaveInstance == null)
        {
            LogError($"Failed to instantiate wave prefab: {prefab.name}", prefab);
            GoToNextWave();
            return;
        }

        Wave wave = currentWaveInstance.GetComponent<Wave>();
        if (wave != null)
        {
            wave.Init(this);
            Log($"Activated Wave component: {currentWaveInstance.name}", currentWaveInstance);
            return;
        }

        currentInlineSubWave =
            currentWaveInstance.GetComponent<InfoAboutSubWave>();
        if (currentInlineSubWave != null)
        {
            currentInlineSubWave.OnSubWaveCleared += WhenInlineSubWaveCleared;
            currentInlineSubWave.ActivateSubWave();
            Log(
                $"Activated inline subwave as wave: {currentWaveInstance.name}",
                currentWaveInstance);
            return;
        }

        Debug.LogError(
            $"Wave prefab {prefab.name} has no Wave or InfoAboutSubWave component.",
            prefab);
        Destroy(currentWaveInstance);
        currentWaveInstance = null;
        GoToNextWave();
    }

    private void WhenInlineSubWaveCleared()
    {
        Log($"Inline subwave cleared: {currentWaveInstance?.name}");
        CleanupCurrentInlineSubWave();

        if (currentWaveInstance != null)
            Destroy(currentWaveInstance);

        currentWaveInstance = null;
        GoToNextWave();
    }

    private void CleanupCurrentInlineSubWave()
    {
        if (currentInlineSubWave == null)
            return;

        Log($"Cleaning inline subwave subscription: {currentInlineSubWave.name}", currentInlineSubWave);
        currentInlineSubWave.OnSubWaveCleared -= WhenInlineSubWaveCleared;
        currentInlineSubWave = null;
    }
    void ReturnToMap()
    {
        EndGame.SetActive(true);
    }
    public void MainHeroIsDead()
    {
        GameOver.SetActive(true);
    }

    private void Log(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[WaveManager] {message}", context != null ? context : this);
    }

    private void LogWarning(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.LogWarning($"[WaveManager] {message}", context != null ? context : this);
    }

    private void LogError(string message, UnityEngine.Object context = null)
    {
        Debug.LogError($"[WaveManager] {message}", context != null ? context : this);
    }

    private void OnDestroy()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        CleanupCurrentInlineSubWave();
    }
}
