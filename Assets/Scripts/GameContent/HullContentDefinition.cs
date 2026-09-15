using UnityEngine;

[CreateAssetMenu(fileName = "HullContent", menuName = "Game Content/Hull")]
public sealed class HullContentDefinition : CraftContentDefinition
{
    [SerializeField] private ShipData data;
    [SerializeField] private ShipColorPalette defaultColorPalette = new ShipColorPalette();
    [Header("Gameplay")]
    [SerializeField] private GameObject gameplayPrefab;

    public ShipData Data => data;
    public GameObject GameplayPrefab => gameplayPrefab;
    public ShipColorPalette DefaultColorPalette => defaultColorPalette != null
        ? defaultColorPalette.Clone()
        : new ShipColorPalette();
}
