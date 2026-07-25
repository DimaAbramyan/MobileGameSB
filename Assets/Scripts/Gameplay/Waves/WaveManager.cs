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
    public event Action OnWaveCleared;
    private List<GameObject> wavePrefabs;
    private GameObject currentWaveInstance;
    private InfoAboutSubWave currentInlineSubWave;
    private int currentWaveIndex = 0;
    private Coroutine waveRoutine;

    private WaveProgressPopup WaveProgressPopup =>
        waveProgressPopup ??= WaveProgressPopup.FindScenePopup();

    private void Awake()
    {
        Time.timeScale = 1f;
        wavePrefabs = new List<GameObject>();

        LevelConfig config = LevelLoader.GetSelectedLevel(levelCatalog);
        if (config == null)
            return;

        foreach (GameObject prefab in config.Waves)
        {
            if (prefab == null)
                continue;

            wavePrefabs.Add(prefab);
        }

        if (wavePrefabs.Count == 0)
        {
            Debug.LogError(
                $"No waves configured for level {config.Id} ({config.name}).",
                config);
            return;
        }

        waveRoutine = StartCoroutine(ActivateWaveRoutine(showPopupBeforeWave: true));
    }
    void Start()
    {
        Time.timeScale = 1f;
    }
    public void GoToNextWave()
    {
        currentWaveIndex++;
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        waveRoutine = StartCoroutine(ActivateWaveRoutine(showPopupBeforeWave: true));
    }

    private IEnumerator ActivateWaveRoutine(bool showPopupBeforeWave)
    {
        if (currentWaveIndex < wavePrefabs.Count)
        {
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
            ReturnToMap();
        }
    }
    private void Activate()
    {
        CleanupCurrentInlineSubWave();

        GameObject prefab = wavePrefabs[currentWaveIndex];
        currentWaveInstance = container.InstantiatePrefab(prefab, transform);

        Wave wave = currentWaveInstance.GetComponent<Wave>();
        if (wave != null)
        {
            wave.Init(this);
            Debug.Log($"Активировали волну: {currentWaveInstance.name}");
            return;
        }

        currentInlineSubWave =
            currentWaveInstance.GetComponent<InfoAboutSubWave>();
        if (currentInlineSubWave != null)
        {
            currentInlineSubWave.OnSubWaveCleared += WhenInlineSubWaveCleared;
            currentInlineSubWave.ActivateSubWave();
            Debug.Log(
                $"Активировали подволны как волну: {currentWaveInstance.name}");
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

    private void OnDestroy()
    {
        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        CleanupCurrentInlineSubWave();
    }
}
