using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;


public class WaveManager : MonoBehaviour
{
    [Inject] DiContainer container;
    [Inject] LevelCatalog levelCatalog;
    [Inject] private PlayerController playerController;
    [Inject] private EnemyManager enemyManager;
    [Inject] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    [SerializeField] private GameObject GameOver;
    [SerializeField] private GameObject EndGame;
    [SerializeField] private WaveProgressPopup waveProgressPopup;
    [SerializeField] private bool enableDebugLogs = true;
    [Header("Wave Clear Projectile Explosion")]
    [SerializeField, Range(0.01f, 1f)] private float waveClearTimeScale = 0.3f;
    [SerializeField, Min(0f), Tooltip("Real seconds, excluding pause.")]
    private float waveClearSlowMotionDuration = 0.8f;
    [SerializeField, Min(0f), Tooltip("Radius growth in world units per real second, excluding pause.")]
    private float waveClearExplosionExpansionSpeed = 2f;
    [SerializeField, Min(0f), Tooltip("Real seconds, excluding pause. No visuals are spawned yet.")]
    private float waveClearExplosionLifetime = 7f;
    private readonly List<ProjectileClearExplosion> projectileClearExplosions = new();
    private readonly List<Collider2D> explosionHits = new(64);
    private int nextExplosionId;
    private int waveStartKillCount;
    private bool waveClearEffectStarted;
    private float slowMotionRemaining;
    private float previousTimeScale = 1f;
    private float appliedSlowTimeScale;
    private struct ProjectileClearExplosion
    {
        public int Id;
        public Vector2 Center;
        public float Age;
        public float Radius;
    }
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
    private bool battleFailed;

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
    public void CompleteCurrentWave()
    {
        if (battleFailed)
            return;
        TryStartWaveClearExplosion();
        playerController?.ReviveDefeatedShips();
        OnWaveCleared?.Invoke();
        GoToNextWave();
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
                while (slowMotionRemaining > 0f || Time.timeScale <= 0f)
                    yield return null;

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
        waveStartKillCount = enemyManager != null ? enemyManager.EnemyKillCount : 0;
        waveClearEffectStarted = false;
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
        CompleteCurrentWave();
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
        StopWaveClearEffects();
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
        battleFailed = true;
        GameOver.SetActive(true);
    }

    private void TryStartWaveClearExplosion()
    {
        if (waveClearEffectStarted || enemyManager == null
            || enemyManager.EnemyKillCount <= waveStartKillCount) return;
        waveClearEffectStarted = true;
        Vector2 center = enemyManager.LastKilledEnemyPosition;
        if (waveClearExplosionLifetime > 0f)
        {
            projectileClearExplosions.Add(new ProjectileClearExplosion
            {
                Id = ++nextExplosionId,
                Center = center
            });
        }
        if (waveClearSlowMotionDuration <= 0f || Time.timeScale <= 0f) return;
        if (slowMotionRemaining <= 0f) previousTimeScale = Time.timeScale;
        slowMotionRemaining = waveClearSlowMotionDuration;
        appliedSlowTimeScale = Mathf.Min(previousTimeScale, Mathf.Clamp(waveClearTimeScale, 0.01f, 1f));
        Time.timeScale = appliedSlowTimeScale;
    }

    private void Update()
    {
        TickWaveClearEffects(Time.unscaledDeltaTime);
    }

    private void TickWaveClearEffects(float deltaTime)
    {
        if (Time.timeScale <= 0f || deltaTime <= 0f) return;
        if (slowMotionRemaining > 0f)
        {
            // Resume after PauseOff restores the explosion's remaining slowdown.
            Time.timeScale = appliedSlowTimeScale;
            slowMotionRemaining = Mathf.Max(0f, slowMotionRemaining - deltaTime);
            if (slowMotionRemaining <= 0f) RestoreWaveClearTimeScale();
        }
        for (int i = projectileClearExplosions.Count - 1; i >= 0; i--)
        {
            ProjectileClearExplosion explosion = projectileClearExplosions[i];
            explosion.Age = Mathf.Min(waveClearExplosionLifetime, explosion.Age + deltaTime);
            explosion.Radius = explosion.Age * Mathf.Max(0f, waveClearExplosionExpansionSpeed);
            ClearLegacyProjectiles(explosion.Center, explosion.Radius);
            projectileCollisionRegistry?.SetWaveClearPurgeField(explosion.Id, new EnemyProjectilePurgeField
            {
                Center = new Unity.Mathematics.float2(explosion.Center.x, explosion.Center.y),
                Radius = explosion.Radius,
                DestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile
            });
            if (explosion.Age >= waveClearExplosionLifetime)
            {
                projectileCollisionRegistry?.ClearEnemyProjectiles(explosion.Center, explosion.Radius);
                projectileCollisionRegistry?.RemoveWaveClearPurgeField(explosion.Id);
                projectileClearExplosions.RemoveAt(i);
            }
            else projectileClearExplosions[i] = explosion;
        }
    }

    private void ClearLegacyProjectiles(Vector2 center, float radius)
    {
        if (radius <= 0f) return;
        var filter = new ContactFilter2D { useTriggers = true };
        explosionHits.Clear();
        Physics2D.OverlapCircle(center, radius, filter, explosionHits);
        float radiusSquared = radius * radius;
        for (int i = 0; i < explosionHits.Count; i++)
        {
            EnemyProjectile projectile = explosionHits[i].GetComponentInParent<EnemyProjectile>();
            if (projectile != null
                && ((Vector2)projectile.transform.position - center).sqrMagnitude <= radiusSquared)
                projectile.TryDestroyByAbility(true);
        }
    }

    private void RestoreWaveClearTimeScale()
    {
        if (Time.timeScale > 0f && Mathf.Approximately(Time.timeScale, appliedSlowTimeScale))
            Time.timeScale = previousTimeScale;
    }

    private void StopWaveClearEffects()
    {
        if (slowMotionRemaining > 0f) RestoreWaveClearTimeScale();
        slowMotionRemaining = 0f;
        for (int i = 0; i < projectileClearExplosions.Count; i++)
            projectileCollisionRegistry?.RemoveWaveClearPurgeField(projectileClearExplosions[i].Id);
        projectileClearExplosions.Clear();
    }

    private void OnDisable() => StopWaveClearEffects();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < projectileClearExplosions.Count; i++)
            Gizmos.DrawWireSphere(projectileClearExplosions[i].Center, projectileClearExplosions[i].Radius);
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
        StopWaveClearEffects();
    }
}
