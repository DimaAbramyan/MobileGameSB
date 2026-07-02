using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

public class Wave : MonoBehaviour
{
    [Inject] DiContainer container;
    [SerializeField] public List<GameObject> SubWavesToCreate;
    private List<InfoAboutSubWave> subWavesInfo;
    private int subWavesLeft;
    WaveManager waveManager;
    public void Init(WaveManager waveManager)
    {
        this.waveManager = waveManager;

        subWavesInfo = new List<InfoAboutSubWave>();

        subWavesLeft = SubWavesToCreate.Count;

        foreach (GameObject prefab in SubWavesToCreate)
        {
            GameObject instance = container.InstantiatePrefab(prefab, transform);
            InfoAboutSubWave subWave = instance.GetComponent<InfoAboutSubWave>();

            subWave.OnSubWaveCleared += WhenSubWaveCleared;

            instance.SetActive(false);

            subWavesInfo.Add(subWave);
        }
        SpawnSubWave();
    }
    public void SpawnSubWave()
    {
        SpawnSubWaves();
    }
    void SpawnSubWaves()
    {
        foreach (var subWave in subWavesInfo)
        {
            subWave.ActivateSubWave();
        }
    }
    public void WhenSubWaveCleared()
    {
        subWavesLeft--;
        Debug.Log("Вызвали, осталось: " + subWavesLeft);
        if (subWavesLeft <= 0)
        {
            waveManager?.GoToNextWave();
            Destroy(gameObject);
        }
    }
    void OnDestroy()
    {
        if (subWavesInfo == null)
            return;

        foreach (var subWave in subWavesInfo)
        {
            if (subWave != null)
                subWave.OnSubWaveCleared -= WhenSubWaveCleared;
        }
    }

    
}
