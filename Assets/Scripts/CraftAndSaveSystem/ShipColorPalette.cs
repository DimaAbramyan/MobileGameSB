using System;
using UnityEngine;

[Serializable]
public sealed class ShipColorPalette
{
    public Color primary = Color.white;
    public Color secondary = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Color accent = new Color(0.25f, 0.75f, 1f, 1f);

    public ShipColorPalette Clone()
    {
        return new ShipColorPalette
        {
            primary = primary,
            secondary = secondary,
            accent = accent
        };
    }
}
