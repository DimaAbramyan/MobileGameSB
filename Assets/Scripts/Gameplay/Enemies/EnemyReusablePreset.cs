using UnityEngine;

public enum EnemyPresetUseMode { Linked, Copy }

// Configuration revisions invalidate materialized copies; execution state never lives on an asset.
public static class EnemyPresetRevision
{
    public static int Value { get; private set; }
    public static void Changed() { unchecked { Value++; } }
}

public abstract class EnemyReusablePreset : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private string category;
    [SerializeField] private string[] tags = System.Array.Empty<string>();

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string Category => category;
    public string[] Tags => tags;
    public void NotifyChanged() => EnemyPresetRevision.Changed();
    protected virtual void OnValidate() => NotifyChanged();
    protected virtual void OnEnable() => NotifyChanged();
}
