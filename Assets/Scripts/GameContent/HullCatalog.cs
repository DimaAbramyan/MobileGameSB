using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HullCatalog", menuName = "Game Content/Hull Catalog")]
public sealed class HullCatalog : ScriptableObject
{
    [SerializeField] private List<HullContentDefinition> hulls = new();

    public IReadOnlyList<HullContentDefinition> Hulls => hulls;
}
