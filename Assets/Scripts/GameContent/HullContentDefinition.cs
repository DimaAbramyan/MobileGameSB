using UnityEngine;

[CreateAssetMenu(fileName = "HullContent", menuName = "Game Content/Hull")]
public sealed class HullContentDefinition : CraftContentDefinition
{
    [SerializeField] private ShipData data;
    [SerializeField] private ShipColorPalette defaultColorPalette = new ShipColorPalette();

    public ShipData Data => data;
    public ShipColorPalette DefaultColorPalette => defaultColorPalette != null
        ? defaultColorPalette.Clone()
        : new ShipColorPalette();
}