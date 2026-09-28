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
    private List<int> waveConfigIndices;
    private readonly List<GameObject> preloadedWaveInstances = new();
    private LevelConfig selectedLevelConfig;
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

        selectedLevelConfig = LevelLoader.GetSelectedLevel(levelCatalog);
        if (selectedLevelConfig == null)
        {
            LogError("Selected LevelConfig is null. Waves cannot be loaded.");
            return;
        }

        Log(
            $"Selected level: id={selectedLevelConfig.Id}, name={selectedLevelConfig.name}, configured waves={selectedLevelConfig.Waves?.Count ?? 0}");

        waveConfigIndices = new List<int>();
        for (int configIndex = 0;
             configIndex < selectedLevelConfig.Waves.Count;
             configIndex++)
        {
            GameObject prefab = selectedLevelConfig.Waves[configIndex];
            if (prefab == null)
            {
                LogWarning("Skipped null wave prefab in LevelConfig.Waves.");
                continue;
            }

            Log($"Registered wave prefab: {prefab.name}", prefab);
            wavePrefabs.Add(prefab);
            waveConfigIndices.Add(configIndex);
        }

        if (wavePrefabs.Count == 0)
        {
            Debug.LogError(
                $"No waves configured for level {selectedLevelConfig.Id} ({selectedLevelConfig.name}).",
                selectedLevelConfig);
            return;
        }

        canStartWaves = true;
        PreloadWaves();
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

        bool wasPreloaded = TryTakePreloadedWave(
            currentWaveIndex,
            out currentWaveInstance);
        if (!wasPreloaded)
        {
            Log($"Instantiating wave prefab: {prefab.name}", prefab);
            currentWaveInstance = container.InstantiatePrefab(prefab, transform);
        }
        else
        {
            Log($"Activating preloaded wave prefab: {prefab.name}", prefab);
        }

        if (currentWaveInstance == null)
        {
            LogError($"Failed to instantiate wave prefab: {prefab.name}", prefab);
            GoToNextWave();
            return;
        }

        if (!wasPreloaded)
            ConfigureWaveInstance(currentWaveInstance, currentWaveIndex);

        IWaveEncounter encounter =
            currentWaveInstance.GetComponent<IWaveEncounter>();
        if (encounter != null)
        {
            if (wasPreloaded && encounter is Wave preparedWave)
                preparedWave.Begin();
            else
                encounter.Init(this);
            Log(
                $"Activated {encounter.GetType().Name}: "
                + currentWaveInstance.name,
                currentWaveInstance);
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
            $"Wave prefab {prefab.name} has no IWaveEncounter or "
            + "InfoAboutSubWave component.",
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

    private void ConfigureMetalDrops(GameObject waveInstance, int waveIndex)
    {
        if (selectedLevelConfig == null
            || waveConfigIndices == null
            || waveInstance == null
            || waveIndex < 0
            || waveIndex >= waveConfigIndices.Count)
        {
            return;
        }

        WaveMetalDropSettings settings =
            selectedLevelConfig.GetWaveMetalDropSettings(
                waveConfigIndices[waveIndex]);
        Wave wave = waveInstance.GetComponent<Wave>();
        if (wave != null)
        {
            wave.ConfigureMetalDrops(
                settings,
                selectedLevelConfig.MetalPickupPrefab);
            return;
        }

        if (settings.IsEnabled)
        {
            LogWarning(
                $"Metal drop is configured for {waveInstance.name}, but it has no {nameof(Wave)} component.",
                waveInstance);
        }
    }

    private void ConfigureEnemyDifficulty(GameObject waveInstance)
    {
        if (selectedLevelConfig == null || waveInstance == null)
            return;

        WaveEnemyDifficultyModifier modifier =
            waveInstance.GetComponent<WaveEnemyDifficultyModifier>();
        modifier?.ConfigureLevelMultipliers(selectedLevelConfig);
    }

    private void PreloadWaves()
    {
        preloadedWaveInstances.Clear();
        GameObject root = new GameObject("[Preloaded Waves]");
        Transform preloadedWavesRoot = root.transform;
        preloadedWavesRoot.SetParent(transform, false);
        root.SetActive(false);

        for (int waveIndex = 0; waveIndex < wavePrefabs.Count; waveIndex++)
        {
            GameObject prefab = wavePrefabs[waveIndex];
            if (!CanPreload(prefab))
            {
                preloadedWaveInstances.Add(null);
                continue;
            }

            GameObject instance = container.InstantiatePrefab(
                prefab,
                preloadedWavesRoot);
            if (instance == null)
            {
                LogWarning(
                    $"Could not preload wave prefab: {prefab.name}. It will be instantiated when needed.",
                    prefab);
                preloadedWaveInstances.Add(null);
                continue;
            }

            ConfigureWaveInstance(instance, waveIndex);

            Wave wave = instance.GetComponent<Wave>();
            if (wave != null)
                wave.Prepare(this);
            else
                instance.GetComponent<InfoAboutSubWave>()?.PrepareForActivation();

            preloadedWaveInstances.Add(instance);
        }
    }

    private static bool CanPreload(GameObject prefab)
    {
        return prefab != null
            && (prefab.GetComponent<Wave>() != null
                || prefab.GetComponent<InfoAboutSubWave>() != null);
    }

    private bool TryTakePreloadedWave(
        int waveIndex,
        out GameObject instance)
    {
        instance = null;
        if (waveIndex < 0 || waveIndex >= preloadedWaveInstances.Count)
            return false;

        instance = preloadedWaveInstances[waveIndex];
        if (instance == null)
            return false;

        preloadedWaveInstances[waveIndex] = null;
        instance.transform.SetParent(transform, false);
        return true;
    }

    private void ConfigureWaveInstance(GameObject waveInstance, int waveIndex)
    {
        ConfigureMetalDrops(waveInstance, waveIndex);
        ConfigureEnemyDifficulty(waveInstance);
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
        preloadedWaveInstances.Clear();
    }
}
