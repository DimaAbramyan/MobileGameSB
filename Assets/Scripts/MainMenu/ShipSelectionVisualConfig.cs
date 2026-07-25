using UnityEngine;

[CreateAssetMenu(
    fileName = "ShipSelectionVisualConfig",
    menuName = "Game/Main Menu/Ship Selection Visual Config")]
public sealed class ShipSelectionVisualConfig : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private ShipData shipData;

    [Header("Text")]
    [SerializeField] private string shipName;

    [TextArea(2, 6)]
    [SerializeField] private string activeAbilityDescription;

    [TextArea(2, 6)]
    [SerializeField] private string passiveAbilityDescription;

    [Header("Stats chart")]
    [SerializeField] private RadarChartConfig radarChartConfig;
    [SerializeField] private int[] radarChartValues;

    public ShipData ShipData => shipData;
    public string ShipName => shipName;
    public string ActiveAbilityDescription => activeAbilityDescription;
    public string PassiveAbilityDescription => passiveAbilityDescription;
    public RadarChartConfig RadarChartConfig => radarChartConfig;
    public int[] RadarChartValues => radarChartValues;
}
