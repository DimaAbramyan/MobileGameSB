using System;
using System.Collections.Generic;

// Presentation only. Gameplay values remain in the existing configurations.
public interface IStatDisplaySource
{
    StatDisplaySettings StatDisplay { get; }
}

[Serializable]
public sealed class StatDisplaySettings
{
    public StatDisplayLayout baseStats = new();
    public StatDisplayLayout secondaryStats = new();
}

[Serializable]
public sealed class StatDisplayLayout
{
    public List<StatDisplayRow> rows = new();
}

[Serializable]
public sealed class StatDisplayRow
{
    public List<StatDisplayCell> cells = new();
}

// Manual text remains the default for previously authored cells.
[Serializable]
public class StatDisplayCell
{
    public string label = "Stat";
    public string value = "—";
    public string labelLocalizationKey = "";
    // Missing fields in existing Meta cells preserve manual text mode.
    public bool useBinding;
    public string sourcePath = "";
    public string statId = "";
    public UnityEngine.Object sourceAsset;
    public bool overrideLabel;
    public string valueFormat = "";
}
