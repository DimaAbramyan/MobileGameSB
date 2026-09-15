using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PlayerWeaponMetaProgressionEntry
{
    [SerializeField] private WeaponData weapon;
    [SerializeField, Min(0)] private int level;

    public WeaponData Weapon => weapon;
    public int Level => Mathf.Max(0, level);
}

[CreateAssetMenu(
    fileName = "PlayerWeaponMetaProgression",
    menuName = "Game/Player Profile/Weapon Meta Progression")]
public sealed class PlayerWeaponMetaProgressionConfig : ScriptableObject
{
    [SerializeField] private List<PlayerWeaponMetaProgressionEntry> weapons =
        new();

    public IReadOnlyList<PlayerWeaponMetaProgressionEntry> Weapons => weapons;

    public int GetLevel(WeaponData weapon)
    {
        if (weapon == null || weapons == null)
            return 0;

        for (int index = 0; index < weapons.Count; index++)
        {
            PlayerWeaponMetaProgressionEntry entry = weapons[index];
            if (entry != null && entry.Weapon == weapon)
                return entry.Level;
        }

        return 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (weapons == null)
            weapons = new List<PlayerWeaponMetaProgressionEntry>();
    }
#endif
}
