using System;
using UnityEngine;

[Serializable]
public sealed class RadarChartParameter
{
    [SerializeField] private string name = "Parameter";
    [SerializeField] private float value;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue = 100f;

    public string Name => name;
    public float Value => value;
    public float MinValue => minValue;
    public float MaxValue => maxValue;

    public float NormalizedValue
    {
        get
        {
            if (Mathf.Approximately(minValue, maxValue))
                return 0f;

            float min = Mathf.Min(minValue, maxValue);
            float max = Mathf.Max(minValue, maxValue);
            return Mathf.Clamp01(Mathf.InverseLerp(min, max, value));
        }
    }
}
