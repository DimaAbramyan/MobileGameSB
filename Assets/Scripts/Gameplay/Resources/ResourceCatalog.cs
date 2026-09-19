using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceCatalog", menuName = "Game/Resources/Resource Catalog")]
public sealed class ResourceCatalog : ScriptableObject
{
    [SerializeField] private ResourceDefinition metal;
    [SerializeField] private ResourceDefinition gold;
    [SerializeField] private ResourceDefinition chip;

    public ResourceDefinition Get(ResourceKind kind)
    {
        ResourceDefinition resource = kind switch
        {
            ResourceKind.Metal => metal,
            ResourceKind.Gold => gold,
            ResourceKind.Chip => chip,
            _ => null
        };

        if (resource == null)
        {
            throw new InvalidOperationException(
                $"{nameof(ResourceCatalog)} has no definition for {kind}.");
        }

        if (resource.Kind != kind)
        {
            throw new InvalidOperationException(
                $"{nameof(ResourceCatalog)} definition '{resource.name}' has kind "
                + $"{resource.Kind}, but is assigned to {kind}.");
        }

        return resource;
    }
}
