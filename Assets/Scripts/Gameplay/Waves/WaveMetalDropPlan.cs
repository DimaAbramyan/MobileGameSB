using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class WaveMetalDropPlan
{
    private readonly WaveMetalDropSettings settings;
    private readonly MetalPickup pickupPrefab;
    private readonly DiContainer container;
    private readonly MetalPickupController pickupController;
    private readonly Object context;
    private readonly List<InfoAboutSubWave> subscribedSubWaves = new();
    private readonly List<Enemy> candidates = new();

    private int expectedSpawnCount;
    private int observedSpawnCount;
    private int waveMetalBudget;
    private bool assignmentsPrepared;

    public WaveMetalDropPlan(
        WaveMetalDropSettings settings,
        MetalPickup pickupPrefab,
        DiContainer container,
        MetalPickupController pickupController,
        Object context)
    {
        this.settings = settings;
        this.pickupPrefab = pickupPrefab;
        this.container = container;
        this.pickupController = pickupController;
        this.context = context;
    }

    public void Prepare(IReadOnlyList<InfoAboutSubWave> subWaves)
    {
        Dispose();

        if (!settings.IsEnabled)
            return;

        if (pickupPrefab == null)
        {
            Debug.LogWarning(
                "Metal drop is configured, but Metal Pickup Prefab is missing.",
                context);
            return;
        }

        waveMetalBudget = settings.RollMetal();
        if (waveMetalBudget <= 0 || subWaves == null)
            return;

        for (int i = 0; i < subWaves.Count; i++)
        {
            InfoAboutSubWave subWave = subWaves[i];
            if (subWave == null)
                continue;

            expectedSpawnCount += Mathf.Max(
                0,
                subWave.GetRewardEligibleEnemyCount());
            subWave.OnEnemySpawned += HandleEnemySpawned;
            subscribedSubWaves.Add(subWave);
        }

        if (expectedSpawnCount <= 0)
        {
            Debug.LogWarning(
                $"Metal drop budget {waveMetalBudget} cannot be assigned because "
                + "this wave has no reward-eligible enemy spawns.",
                context);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < subscribedSubWaves.Count; i++)
        {
            InfoAboutSubWave subWave = subscribedSubWaves[i];
            if (subWave != null)
                subWave.OnEnemySpawned -= HandleEnemySpawned;
        }

        subscribedSubWaves.Clear();
        candidates.Clear();
        expectedSpawnCount = 0;
        observedSpawnCount = 0;
        waveMetalBudget = 0;
        assignmentsPrepared = false;
    }

    private void HandleEnemySpawned(
        Enemy enemy,
        int spawnIndex,
        int plannedEnemyCount)
    {
        if (assignmentsPrepared)
            return;

        observedSpawnCount++;
        if (enemy != null && !enemy.isDead && enemy.CanContainBuff())
            candidates.Add(enemy);

        if (observedSpawnCount >= expectedSpawnCount)
            AssignMetalDrops();
    }

    private void AssignMetalDrops()
    {
        assignmentsPrepared = true;
        RemoveUnavailableCandidates();

        if (candidates.Count == 0)
        {
            Debug.LogWarning(
                $"Metal drop budget {waveMetalBudget} could not be assigned: "
                + "no eligible enemies were spawned.",
                context);
            return;
        }

        int desiredCarrierCount = Mathf.CeilToInt(
            candidates.Count * settings.CarrierChance);
        int carrierCount = Mathf.Min(
            Mathf.Min(desiredCarrierCount, candidates.Count),
            waveMetalBudget);
        if (carrierCount <= 0)
            return;

        SelectCarriers(carrierCount);
        int[] metalByCarrier = DistributeMetal(carrierCount);
        for (int i = 0; i < carrierCount; i++)
        {
            int metalAmount = metalByCarrier[i];
            if (metalAmount <= 0)
                continue;

            Enemy carrier = candidates[i];
            EnemyMetalDrop metalDrop =
                carrier.GetComponent<EnemyMetalDrop>();
            if (metalDrop == null)
                metalDrop = carrier.gameObject.AddComponent<EnemyMetalDrop>();

            metalDrop.Configure(pickupPrefab, metalAmount, pickupController);
        }
    }

    private void RemoveUnavailableCandidates()
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            Enemy enemy = candidates[i];
            if (enemy == null || enemy.isDead || !enemy.CanContainBuff())
                candidates.RemoveAt(i);
        }
    }

    private void SelectCarriers(int carrierCount)
    {
        for (int i = 0; i < carrierCount; i++)
        {
            int selectedIndex = Random.Range(i, candidates.Count);
            Enemy selected = candidates[i];
            candidates[i] = candidates[selectedIndex];
            candidates[selectedIndex] = selected;
        }
    }

    private int[] DistributeMetal(int carrierCount)
    {
        int[] metalByCarrier = new int[carrierCount];
        float[] fractions = new float[carrierCount];
        int remainingMetal = waveMetalBudget - carrierCount;
        float totalMultiplier = 0f;

        for (int i = 0; i < carrierCount; i++)
        {
            metalByCarrier[i] = 1;
            totalMultiplier += candidates[i].MetalMultiplier;
        }

        if (remainingMetal <= 0 || totalMultiplier <= 0f)
            return metalByCarrier;

        int distributedMetal = 0;
        for (int i = 0; i < carrierCount; i++)
        {
            float exactShare = remainingMetal
                * candidates[i].MetalMultiplier
                / totalMultiplier;
            int wholeShare = Mathf.FloorToInt(exactShare);
            metalByCarrier[i] += wholeShare;
            fractions[i] = exactShare - wholeShare;
            distributedMetal += wholeShare;
        }

        int undistributedMetal = remainingMetal - distributedMetal;
        for (int unit = 0; unit < undistributedMetal; unit++)
        {
            int bestIndex = 0;
            for (int i = 1; i < carrierCount; i++)
            {
                if (fractions[i] > fractions[bestIndex])
                    bestIndex = i;
            }

            metalByCarrier[bestIndex]++;
            fractions[bestIndex] = -1f;
        }

        return metalByCarrier;
    }
}
