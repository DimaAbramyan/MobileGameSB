using System;
using UnityEngine;

[Serializable]
public sealed class ShipAbilityDamageProgression
{
    public const int DefaultLevelCount =
        ParentShip.MaxWeaponLevel - ParentShip.MinWeaponLevel + 1;

    [SerializeField] private float[] damageBonusPercentByLevel =
        new float[DefaultLevelCount];

    public float GetDamageMultiplier(int battleLevel)
    {
        if (damageBonusPercentByLevel == null
            || damageBonusPercentByLevel.Length == 0)
        {
            return 1f;
        }

        int levelIndex = Mathf.Clamp(
            battleLevel - ParentShip.MinWeaponLevel,
            0,
            damageBonusPercentByLevel.Length - 1);
        float totalBonusPercent = 0f;
        for (int index = 0; index <= levelIndex; index++)
        {
            totalBonusPercent += Mathf.Max(
                0f,
                damageBonusPercentByLevel[index]);
        }

        return 1f + totalBonusPercent / 100f;
    }

    public void EnsureLevelCount(int levelCount)
    {
        int requestedCount = Mathf.Max(1, levelCount);
        if (damageBonusPercentByLevel == null
            || damageBonusPercentByLevel.Length != requestedCount)
        {
            Array.Resize(ref damageBonusPercentByLevel, requestedCount);
        }

        for (int index = 0; index < damageBonusPercentByLevel.Length; index++)
        {
            damageBonusPercentByLevel[index] = Mathf.Max(
                0f,
                damageBonusPercentByLevel[index]);
        }
    }
}
