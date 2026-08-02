using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[System.Serializable]
public sealed class WaveSubWaveCue
{
    [SerializeField] private GameObject subWavePrefab;
    [SerializeField, Min(0f)] private float startDelay;

    public WaveSubWaveCue()
    {
    }

    public WaveSubWaveCue(GameObject subWavePrefab, float startDelay)
    {
        this.subWavePrefab = subWavePrefab;
        this.startDelay = Mathf.Max(0f, startDelay);
    }

    public GameObject SubWavePrefab => subWavePrefab;
    public float StartDelay => Mathf.Max(0f, startDelay);
}

public class Wave : MonoBehaviour
{
    [Inject] DiContainer container;
    [SerializeField] private List<WaveSubWaveCue> scheduledSubWaves = new();
    [SerializeField] public List<GameObject> SubWavesToCreate;
    [SerializeField] private bool enableDebugLogs = true;

    private List<InfoAboutSubWave> subWavesInfo;
    private readonly List<Coroutine> activationRoutines = new();
    private int subWavesLeft;
    WaveManager waveManager;

    public IReadOnlyList<WaveSubWaveCue> ScheduledSubWaves => scheduledSubWaves;

    public void Init(WaveManager waveManager)
    {
        this.waveManager = waveManager;

        subWavesInfo = new List<InfoAboutSubWave>();
        activationRoutines.Clear();
        List<WaveSubWaveCue> schedule = GetEffectiveSchedule();

        if (schedule.Count == 0)
        {
            LogWarning("No subwaves configured. Completing wave immediately.");
            waveManager?.GoToNextWave();
            Destroy(gameObject);
            return;
        }

        subWavesLeft = 0;
        Log($"Initializing wave. Scheduled subwaves: {schedule.Count}");

        foreach (WaveSubWaveCue cue in schedule)
        {
            GameObject prefab = cue.SubWavePrefab;
            if (prefab == null)
            {
                LogWarning("Skipped null subwave prefab.");
                continue;
            }

            Log(
                $"Instantiating subwave prefab: {prefab.name}, "
                + $"delay={cue.StartDelay:0.###}s",
                prefab);
            GameObject instance = container.InstantiatePrefab(prefab, transform);
            if (instance == null)
            {
                LogError($"Failed to instantiate subwave prefab: {prefab.name}", prefab);
                continue;
            }

            InfoAboutSubWave subWave = instance.GetComponent<InfoAboutSubWave>();
            if (subWave == null)
            {
                LogError(
                    $"Subwave prefab {prefab.name} has no InfoAboutSubWave component.",
                    prefab);
                Destroy(instance);
                continue;
            }

            subWave.OnSubWaveCleared += WhenSubWaveCleared;

            instance.SetActive(false);

            subWavesInfo.Add(subWave);
            subWavesLeft++;
            Log($"Registered subwave instance: {instance.name}", instance);

            Coroutine routine = StartCoroutine(
                ActivateSubWaveAfterDelay(subWave, cue.StartDelay));
            activationRoutines.Add(routine);
        }

        if (subWavesLeft <= 0)
        {
            LogWarning("No valid subwaves were registered. Completing wave immediately.");
            waveManager?.GoToNextWave();
            Destroy(gameObject);
            return;
        }
    }

    public void SpawnSubWave()
    {
        SpawnSubWaves();
    }
    void SpawnSubWaves()
    {
        Log($"Activating {subWavesInfo.Count} subwaves.");
        foreach (var subWave in subWavesInfo)
        {
            if (subWave == null)
            {
                LogWarning("Skipped null subwave instance during activation.");
                continue;
            }

            Log($"Activating subwave: {subWave.name}", subWave);
            subWave.ActivateSubWave();
        }
    }

    private IEnumerator ActivateSubWaveAfterDelay(
        InfoAboutSubWave subWave,
        float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (subWave == null)
        {
            LogWarning("Scheduled subwave disappeared before activation.");
            yield break;
        }

        Log($"Activating scheduled subwave: {subWave.name}", subWave);
        subWave.ActivateSubWave();
    }

    public void WhenSubWaveCleared()
    {
        subWavesLeft--;
        Log($"Subwave cleared. Remaining: {subWavesLeft}");
        if (subWavesLeft <= 0)
        {
            Log("Wave cleared. Moving to next wave.");
            waveManager?.GoToNextWave();
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < activationRoutines.Count; i++)
        {
            if (activationRoutines[i] != null)
                StopCoroutine(activationRoutines[i]);
        }

        activationRoutines.Clear();

        if (subWavesInfo == null)
            return;

        foreach (var subWave in subWavesInfo)
        {
            if (subWave != null)
                subWave.OnSubWaveCleared -= WhenSubWaveCleared;
        }
    }

    private List<WaveSubWaveCue> GetEffectiveSchedule()
    {
        if (scheduledSubWaves != null && scheduledSubWaves.Count > 0)
            return scheduledSubWaves;

        List<WaveSubWaveCue> legacySchedule = new();
        if (SubWavesToCreate == null)
            return legacySchedule;

        for (int i = 0; i < SubWavesToCreate.Count; i++)
        {
            if (SubWavesToCreate[i] == null)
                continue;

            legacySchedule.Add(new WaveSubWaveCue(SubWavesToCreate[i], 0f));
        }

        return legacySchedule;
    }

    private void Log(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[Wave] {message}", context != null ? context : this);
    }

    private void LogWarning(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.LogWarning($"[Wave] {message}", context != null ? context : this);
    }

    private void LogError(string message, UnityEngine.Object context = null)
    {
        Debug.LogError($"[Wave] {message}", context != null ? context : this);
    }

    
}
