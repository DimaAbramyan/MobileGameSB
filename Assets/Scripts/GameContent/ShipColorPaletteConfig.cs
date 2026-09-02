using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShipColorPaletteConfig", menuName = "Game Content/Ship Color Palette")]
public sealed class ShipColorPaletteConfig : ScriptableObject
{
    [SerializeField] private List<ShipColorPaletteColor> colors = new();

    public IReadOnlyList<ShipColorPaletteColor> Colors => colors;
}

[Serializable]
public sealed class ShipColorPaletteColor
{
    [SerializeField, Min(0)] private int colorNumber;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Sprite preview;

    public int ColorNumber => colorNumber;
    public Color Color => color;
    public Sprite Preview => preview;
}
