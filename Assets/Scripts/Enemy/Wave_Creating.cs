using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

public class Wave : MonoBehaviour
{
    [Inject] DiContainer container;
    [SerializeField] public List<GameObject> WavesToCreate;
    private int subWavesLeft;
    WaveManager waveManager;
    public void Init(WaveManager waveManager)
    {
        this.waveManager = waveManager;

        List<GameObject> instantiatedWaves = new List<GameObject>();

        subWavesLeft = WavesToCreate.Count;
        Debug.Log("Все подволны: " + subWavesLeft);
        foreach (GameObject wave in WavesToCreate)
        {
            GameObject waveInstance = container.InstantiatePrefab(wave, transform);

            InfoAboutSubWave subWave = waveInstance.GetComponent<InfoAboutSubWave>();

            subWave.OnSubWaveCleared += WhenSubWaveCleared;

            instantiatedWaves.Add(waveInstance);
        }
        WavesToCreate = instantiatedWaves;
    }
    public void WhenSubWaveCleared()
    {
        subWavesLeft--;
        Debug.Log("Вызвали, осталось: " + subWavesLeft);
        if (subWavesLeft <= 0)
        {
            waveManager.GoToNextWave();
            Destroy(gameObject);
        }
    }
    void OnDestroy()
    {
        foreach (GameObject wave in WavesToCreate)
        {
            if (wave != null)
                wave.GetComponent<InfoAboutSubWave>().OnSubWaveCleared -= WhenSubWaveCleared;
        }
    }
}
