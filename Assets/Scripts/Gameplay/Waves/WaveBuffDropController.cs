using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
[RequireComponent(typeof(Wave))]
public sealed class WaveBuffDropController : MonoBehaviour
{
    [InjectOptional] private PlayerController playerController;

    [Header("Buff Count")]
    [SerializeField, Min(0)] private int minBuffs;
    [SerializeField, Min(0)] private int maxBuffs;

    [Header("Weights")]
    [SerializeField] private WaveBuffDropWeightProfile weightProfile;

    private readonly List<SubWaveBuffDropController> availableSlots = new();

    public void PrepareDropAssignments(IReadOnlyList<InfoAboutSubWave> subWaves)
    {
        availableSlots.Clear();

        if (subWaves == null || subWaves.Count == 0)
            return;

        for (int i = 0; i < subWaves.Count; i++)
        {
            InfoAboutSubWave subWave = subWaves[i];
            if (subWave == null)
                continue;

            SubWaveBuffDropController subWaveController =
                subWave.GetComponent<SubWaveBuffDropController>();
            if (subWaveController == null)
                continue;

            int enemyCount = Mathf.Max(
                0,
                subWave.GetRewardEligibleEnemyCount());
            int capacity = Mathf.Min(
                enemyCount,
                subWaveController.MaxBuffs);

            subWaveController.PrepareForWave(enemyCount);
            for (int slot = 0; slot < capacity; slot++)
                availableSlots.Add(subWaveController);
        }

        if (availableSlots.Count == 0)
            return;

        if (weightProfile == null)
        {
            if (minBuffs > 0 || maxBuffs > 0)
            {
                Debug.LogWarning(
                    $"{nameof(WaveBuffDropController)} on {name} has no "
                    + $"{nameof(WaveBuffDropWeightProfile)} assigned.",
                    this);
            }

            return;
        }

        int requestedMin = Mathf.Min(minBuffs, availableSlots.Count);
        int requestedMax = Mathf.Min(
            Mathf.Max(minBuffs, maxBuffs),
            availableSlots.Count);
        if (minBuffs > availableSlots.Count)
        {
            Debug.LogWarning(
                $"{nameof(WaveBuffDropController)} on {name} requested "
                + $"at least {minBuffs} buffs, but subwaves can carry only "
                + $"{availableSlots.Count}.",
                this);
        }

        int buffCount = Random.Range(requestedMin, requestedMax + 1);
        ParentShip player = playerController != null
            ? playerController.CurrentShip
            : null;
        WaveBuffDropRuntimeWeights runtimeWeights =
            weightProfile.CreateRuntimeWeights(player);

        for (int i = 0; i < buffCount; i++)
        {
            if (!runtimeWeights.TryPick(out Buff rewardPrefab))
            {
                Debug.LogWarning(
                    $"{nameof(WaveBuffDropController)} on {name} could not "
                    + "select a reward. Check the weight profile.",
                    this);
                break;
            }

            int slotIndex = Random.Range(0, availableSlots.Count);
            SubWaveBuffDropController targetSubWave =
                availableSlots[slotIndex];
            availableSlots[slotIndex] = availableSlots[availableSlots.Count - 1];
            availableSlots.RemoveAt(availableSlots.Count - 1);

            if (!targetSubWave.TryAssignReward(rewardPrefab))
            {
                Debug.LogWarning(
                    $"{nameof(SubWaveBuffDropController)} on "
                    + $"{targetSubWave.name} rejected a planned reward.",
                    targetSubWave);
            }
        }
    }

    private void OnValidate()
    {
        minBuffs = Mathf.Max(0, minBuffs);
        maxBuffs = Mathf.Max(minBuffs, maxBuffs);
    }
}
