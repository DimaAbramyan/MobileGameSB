using System;
using UnityEngine;

[Serializable]
public struct WaveMetalDropSettings
{
    [SerializeField, Min(0)] private int minMetal;
    [SerializeField, Min(0)] private int maxMetal;
    [SerializeField, Range(0f, 1f)] private float carrierChance;

    public int MinMetal => Mathf.Max(0, minMetal);
    public int MaxMetal => Mathf.Max(MinMetal, maxMetal);
    public float CarrierChance => Mathf.Clamp01(carrierChance);
    public bool IsEnabled => MaxMetal > 0 && CarrierChance > 0f;

    public int RollMetal()
    {
        int min = MinMetal;
        int max = MaxMetal;
        return max <= min ? min : UnityEngine.Random.Range(min, max + 1);
    }

    public void Validate()
    {
        minMetal = Mathf.Max(0, minMetal);
        maxMetal = Mathf.Max(minMetal, maxMetal);
        carrierChance = Mathf.Clamp01(carrierChance);
    }
}
