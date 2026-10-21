using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Wave))]
public sealed class WaveEnemyDifficultyModifier : MonoBehaviour
{
    [Header("Health Multipliers")]
    [SerializeField, Min(0.01f)] private float hullHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float shieldHealthMultiplier = 1f;

    // Preserve the serialized key and public API for older wave presets.
#pragma warning disable CS0414 // Legacy serialized data is intentionally no longer read.
    [SerializeField, HideInInspector] private float damageMultiplier = 1f;
#pragma warning restore CS0414

    [Header("Fire Rate Multiplier")]
    [SerializeField, Min(0.01f)] private float fireRateMultiplier = 1f;

    private readonly List<InfoAboutSubWave> subscribedSubWaves = new();
    private float levelHullHealthMultiplier = 1f;
    private float levelShieldHealthMultiplier = 1f;
    private float levelFireRateMultiplier = 1f;

    public float HullHealthMultiplier => hullHealthMultiplier;
    public float ShieldHealthMultiplier => shieldHealthMultiplier;
    public float DamageMultiplier => 1f;
    public float FireRateMultiplier => fireRateMultiplier;
    public float LevelHullHealthMultiplier => levelHullHealthMultiplier;
    public float LevelShieldHealthMultiplier => levelShieldHealthMultiplier;
    public float LevelDamageMultiplier => 1f;
    public float LevelFireRateMultiplier => levelFireRateMultiplier;
    public float TotalHullHealthMultiplier =>
        hullHealthMultiplier * levelHullHealthMultiplier;
    public float TotalShieldHealthMultiplier =>
        shieldHealthMultiplier * levelShieldHealthMultiplier;
    public float TotalDamageMultiplier => 1f;
    public float TotalFireRateMultiplier =>
        fireRateMultiplier * levelFireRateMultiplier;

    public void ConfigureLevelMultipliers(LevelConfig levelConfig)
    {
        levelHullHealthMultiplier = levelConfig != null
            ? levelConfig.EnemyHullHealthMultiplier
            : 1f;
        levelShieldHealthMultiplier = levelConfig != null
            ? levelConfig.EnemyShieldHealthMultiplier
            : 1f;
        levelFireRateMultiplier = levelConfig != null
            ? levelConfig.EnemyFireRateMultiplier
            : 1f;
    }

    public void PrepareForWave(IReadOnlyList<InfoAboutSubWave> subWaves)
    {
        UnsubscribeFromSubWaves();

        if (subWaves == null)
            return;

        for (int index = 0; index < subWaves.Count; index++)
        {
            InfoAboutSubWave subWave = subWaves[index];
            if (subWave == null)
                continue;

            subWave.OnEnemySpawned += HandleEnemySpawned;
            subscribedSubWaves.Add(subWave);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromSubWaves();
    }

    private void HandleEnemySpawned(
        Enemy enemy,
        int spawnIndex,
        int plannedEnemyCount)
    {
        if (enemy == null)
            return;

        enemy.MultiplyHealth(TotalHullHealthMultiplier);
        enemy.MultiplyFireRate(TotalFireRateMultiplier);

        EnemyShieldModifier shieldModifier =
            enemy.GetComponent<EnemyShieldModifier>();
        if (shieldModifier != null)
            shieldModifier.MultiplyShieldHealth(TotalShieldHealthMultiplier);
    }

    private void UnsubscribeFromSubWaves()
    {
        for (int index = 0; index < subscribedSubWaves.Count; index++)
        {
            InfoAboutSubWave subWave = subscribedSubWaves[index];
            if (subWave != null)
                subWave.OnEnemySpawned -= HandleEnemySpawned;
        }

        subscribedSubWaves.Clear();
    }

    private void OnValidate()
    {
        hullHealthMultiplier = Mathf.Max(0.01f, hullHealthMultiplier);
        shieldHealthMultiplier = Mathf.Max(0.01f, shieldHealthMultiplier);
        damageMultiplier = 1f;
        fireRateMultiplier = Mathf.Max(0.01f, fireRateMultiplier);
    }
}
