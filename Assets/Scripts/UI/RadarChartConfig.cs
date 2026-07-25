using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RadarChartConfig",
    menuName = "Game/UI/Radar Chart Config")]
public sealed class RadarChartConfig : ScriptableObject
{
    [Min(1)]
    [SerializeField] private int maxValue = 10;
    [SerializeField] private string[] parameterNames =
    {
        "A",
        "B",
        "C",
        "D",
        "E",
        "F"
    };

    public int MaxValue => Mathf.Max(1, maxValue);
    public IReadOnlyList<string> ParameterNames => parameterNames;
}
