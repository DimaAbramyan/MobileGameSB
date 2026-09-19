using UnityEngine;

public enum ResourceKind
{
    Metal,
    Gold,
    Chip
}

[CreateAssetMenu(fileName = "Resource", menuName = "Game/Resources/Resource Definition")]
public sealed class ResourceDefinition : ScriptableObject
{
    [SerializeField] private ResourceKind kind;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    public ResourceKind Kind => kind;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? kind.ToString()
        : displayName;
    public Sprite Icon => icon;
}
