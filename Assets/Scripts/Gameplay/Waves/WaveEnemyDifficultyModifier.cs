using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Wave))]
public sealed class WaveEnemyDifficultyModifier : MonoBehaviour
{
    [Header("Health Multipliers")]
    [SerializeField, Min(0.01f)] private float hullHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float shieldHealthMultiplier = 1f;

    [Header("Damage Multipliers")]
    [SerializeField, Min(0.01f)] private float damageMultiplier = 1f;

    [Header("Fire Rate Multiplier")]
    [SerializeField, Min(0.01f)] private float fireRateMultiplier = 1f;

    private readonly List<InfoAboutSubWave> subscribedSubWaves = new();
    private float levelHullHealthMultiplier = 1f;
    private float levelShieldHealthMultiplier = 1f;
    private float levelDamageMultiplier = 1f;
    private float levelFireRateMultiplier = 1f;

    public float HullHealthMultiplier => hullHealthMultiplier;
    public float ShieldHealthMultiplier => shieldHealthMultiplier;
    public float DamageMultiplier => damageMultiplier;
    public float FireRateMultiplier => fireRateMultiplier;
    public float LevelHullHealthMultiplier => levelHullHealthMultiplier;
    public float LevelShieldHealthMultiplier => levelShieldHealthMultiplier;
    public float LevelDamageMultiplier => levelDamageMultiplier;
    public float LevelFireRateMultiplier => levelFireRateMultiplier;
    public float TotalHullHealthMultiplier =>
        hullHealthMultiplier * levelHullHealthMultiplier;
    public float TotalShieldHealthMultiplier =>
        shieldHealthMultiplier * levelShieldHealthMultiplier;
    public float TotalDamageMultiplier => damageMultiplier * levelDamageMultiplier;
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
        levelDamageMultiplier = levelConfig != null
            ? levelConfig.EnemyDamageMultiplier
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
        enemy.MultiplyDamage(TotalDamageMultiplier);
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
        damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        fireRateMultiplier = Mathf.Max(0.01f, fireRateMultiplier);
    }
}
