using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;


public class WaveManager : MonoBehaviour
{
    [Inject] DiContainer container;
    [SerializeField] private GameObject GameOver;
    [SerializeField] private GameObject EndGame;
    public event Action OnWaveCleared;
    private List<Wave> waves;
    private int currentWaveIndex = 0;
    private int levelID;
    private void Awake()
    {
        waves = new List<Wave>();
        levelID = LevelLoader.LevelIndex;
        Debug.Log(levelID);
        List<GameObject> loadedWaves = Resources.LoadAll<GameObject>("Levels/Level_" + levelID + "/").ToList<GameObject>();
        foreach (GameObject prefab in loadedWaves)
        {
            GameObject Instance = 
                container.InstantiatePrefab(prefab,
                transform);
            Wave waveInstance = Instance.GetComponent<Wave>();
            waveInstance.Init(this);
            Instance.SetActive(false);
            waves.Add(waveInstance);
        }
        Debug.Log(waves[0].GetComponentsInChildren<Wave>().Length);
    }
    void Start()
    {
        Time.timeScale = 1.0f;
        ActivateWave();
    }
    public void GoToNextWave()
    {
        currentWaveIndex++;
        ActivateWave();
    }
    private void ActivateWave()
    {
        if (currentWaveIndex < waves.Count)
        {
            Activate();
        }
        else
        {
            ReturnToMap();
        }
    }
    private void Activate()
    {
        waves[currentWaveIndex].gameObject.SetActive(true);
        Debug.Log($"Активировали волну : " +waves[currentWaveIndex].gameObject.name);
    }
    void ReturnToMap()
    {
        EndGame.SetActive(true);
    }
    public void MainHeroIsDead()
    {
        GameOver.SetActive(true);
    }
}
