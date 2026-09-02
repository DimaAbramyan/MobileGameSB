using System;
using System.Collections.Generic;
using UnityEngine;

public enum WaveBuffDropStat
{
    PlayerHealthPercent,
    WeaponLevel
}

public enum WaveBuffDropComparison
{
    AtMost,
    AtLeast
}

[Serializable]
public sealed class WaveBuffDropWeightModifier
{
    [SerializeField] private WaveBuffDropStat stat;
    [SerializeField, HideInInspector] private WaveBuffDropComparison comparison =
        WaveBuffDropComparison.AtMost;
    [SerializeField, HideInInspector, Min(0f)] private float threshold = 0.5f;
    [SerializeField, HideInInspector, Min(0f)] private float weightMultiplier = 1f;
    [SerializeField, Tooltip(
        "X is player health from 0 to 1, or weapon level. Y multiplies this reward's base weight.")]
    private AnimationCurve weightMultiplierCurve;

    public float Apply(
        float weight,
        float playerHealthPercent,
        int weaponLevel)
    {
        float value = stat == WaveBuffDropStat.PlayerHealthPercent
            ? playerHealthPercent
            : weaponLevel;

        return weight * EvaluateWeightMultiplier(value);
    }

    public void EnsureWeightMultiplierCurve()
    {
        if (weightMultiplierCurve != null
            && weightMultiplierCurve.length > 0)
        {
            return;
        }

        float multiplier = Mathf.Max(0f, weightMultiplier);
        float maximumValue = stat == WaveBuffDropStat.PlayerHealthPercent
            ? 1f
            : Mathf.Max(1f, threshold + 1f);
        float clampedThreshold = Mathf.Clamp(threshold, 0f, maximumValue);
        float transition = Mathf.Min(0.001f, maximumValue * 0.001f);

        weightMultiplierCurve = comparison == WaveBuffDropComparison.AtMost
            ? CreateAtMostCurve(
                clampedThreshold,
                maximumValue,
                multiplier,
                transition)
            : CreateAtLeastCurve(
                clampedThreshold,
                maximumValue,
                multiplier,
                transition);
    }

    private float EvaluateWeightMultiplier(float value)
    {
        if (weightMultiplierCurve == null
            || weightMultiplierCurve.length == 0)
        {
            bool matches = comparison == WaveBuffDropComparison.AtMost
                ? value <= threshold
                : value >= threshold;
            return matches ? Mathf.Max(0f, weightMultiplier) : 1f;
        }

        return Mathf.Max(0f, weightMultiplierCurve.Evaluate(value));
    }

    private static AnimationCurve CreateAtMostCurve(
        float threshold,
        float maximumValue,
        float multiplier,
        float transition)
    {
        if (threshold >= maximumValue)
            return CreateFlatCurve(maximumValue, multiplier);

        if (threshold <= 0f)
        {
            return CreateCurve(
                new Keyframe(0f, multiplier),
                new Keyframe(Mathf.Min(maximumValue, transition), 1f),
                new Keyframe(maximumValue, 1f));
        }

        return CreateCurve(
            new Keyframe(0f, multiplier),
            new Keyframe(threshold, multiplier),
            new Keyframe(Mathf.Min(maximumValue, threshold + transition), 1f),
            new Keyframe(maximumValue, 1f));
    }

    private static AnimationCurve CreateAtLeastCurve(
        float threshold,
        float maximumValue,
        float multiplier,
        float transition)
    {
        if (threshold <= 0f)
            return CreateFlatCurve(maximumValue, multiplier);

        if (threshold >= maximumValue)
        {
            return CreateCurve(
                new Keyframe(0f, 1f),
                new Keyframe(Mathf.Max(0f, maximumValue - transition), 1f),
                new Keyframe(maximumValue, multiplier));
        }

        return CreateCurve(
            new Keyframe(0f, 1f),
            new Keyframe(Mathf.Max(0f, threshold - transition), 1f),
            new Keyframe(threshold, multiplier),
            new Keyframe(maximumValue, multiplier));
    }

    private static AnimationCurve CreateFlatCurve(
        float maximumValue,
        float multiplier)
    {
        return CreateCurve(
            new Keyframe(0f, multiplier),
            new Keyframe(maximumValue, multiplier));
    }

    private static AnimationCurve CreateCurve(params Keyframe[] keys)
    {
        return new AnimationCurve(keys)
        {
            preWrapMode = WrapMode.Clamp,
            postWrapMode = WrapMode.Clamp
        };
    }
}

[Serializable]
public sealed class WaveBuffDropWeight
{
    [SerializeField] private Buff rewardPrefab;
    [SerializeField, Min(0f)] private float baseWeight = 1f;
    [SerializeField] private WaveBuffDropWeightModifier[] modifiers =
        Array.Empty<WaveBuffDropWeightModifier>();

    public Buff RewardPrefab => rewardPrefab;

    public void EnsureWeightMultiplierCurves()
    {
        if (modifiers == null)
            return;

        for (int i = 0; i < modifiers.Length; i++)
            modifiers[i]?.EnsureWeightMultiplierCurve();
    }

    public float Evaluate(
        float playerHealthPercent,
        int weaponLevel)
    {
        float weight = Mathf.Max(0f, baseWeight);
        if (modifiers == null)
            return weight;

        for (int i = 0; i < modifiers.Length; i++)
        {
            WaveBuffDropWeightModifier modifier = modifiers[i];
            if (modifier != null)
            {
                weight = modifier.Apply(
                    weight,
                    playerHealthPercent,
                    weaponLevel);
            }
        }

        return Mathf.Max(0f, weight);
    }
}

[CreateAssetMenu(
    fileName = "WaveBuffDropWeightProfile",
    menuName = "Game/Waves/Buff Drop Weight Profile")]
public sealed class WaveBuffDropWeightProfile : ScriptableObject
{
    [SerializeField] private WaveBuffDropWeight[] weights =
        Array.Empty<WaveBuffDropWeight>();

    public WaveBuffDropRuntimeWeights CreateRuntimeWeights(ParentShip player)
    {
        return new WaveBuffDropRuntimeWeights(weights, player);
    }

    private void OnValidate()
    {
        if (weights == null)
            return;

        for (int i = 0; i < weights.Length; i++)
            weights[i]?.EnsureWeightMultiplierCurves();
    }
}

public sealed class WaveBuffDropRuntimeWeights
{
    private readonly Buff[] rewardPrefabs;
    private readonly float[] weights;
    private int count;

    public WaveBuffDropRuntimeWeights(
        IReadOnlyList<WaveBuffDropWeight> sourceWeights,
        ParentShip player)
    {
        int sourceCount = sourceWeights != null ? sourceWeights.Count : 0;
        rewardPrefabs = new Buff[sourceCount];
        weights = new float[sourceCount];

        float playerHealthPercent = GetPlayerHealthPercent(player);
        int weaponLevel = player != null ? player.GetLevel() : 0;

        for (int i = 0; i < sourceCount; i++)
        {
            WaveBuffDropWeight sourceWeight = sourceWeights[i];
            if (sourceWeight == null || sourceWeight.RewardPrefab == null)
                continue;

            float weight = sourceWeight.Evaluate(
                playerHealthPercent,
                weaponLevel);
            if (weight <= 0f)
                continue;

            rewardPrefabs[count] = sourceWeight.RewardPrefab;
            weights[count] = weight;
            count++;
        }
    }

    public bool TryPick(out Buff rewardPrefab)
    {
        rewardPrefab = null;

        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f)
            return false;

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                rewardPrefab = rewardPrefabs[i];
                return true;
            }
        }

        rewardPrefab = rewardPrefabs[count - 1];
        return rewardPrefab != null;
    }

    private static float GetPlayerHealthPercent(ParentShip player)
    {
        if (player == null || player.MaximumHealthPoints <= 0f)
            return 1f;

        return Mathf.Clamp01(
            player.CurrentHealthPoints / player.MaximumHealthPoints);
    }
}
