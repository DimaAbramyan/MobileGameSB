using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Game Content/Weapon Catalog")]
public sealed class WeaponCatalog : ScriptableObject
{
    [SerializeField] private List<WeaponContentDefinition> weapons = new();

    public IReadOnlyList<WeaponContentDefinition> Weapons => weapons;
}
