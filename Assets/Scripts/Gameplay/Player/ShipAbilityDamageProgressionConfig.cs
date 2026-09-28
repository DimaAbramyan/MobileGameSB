using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ShipAbilityDamageProgression",
    menuName = "Game/Ship Ability Damage Progression")]
public sealed class ShipAbilityDamageProgressionConfig : ScriptableObject
{
    [SerializeField] private float[] damageBonusPercentByLevel =
        new float[ShipAbilityDamageProgression.DefaultLevelCount];

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

    public void SetUniformBonusPercent(float bonusPercent)
    {
        EnsureLevelCount(ShipAbilityDamageProgression.DefaultLevelCount);
        float clampedBonus = Mathf.Max(0f, bonusPercent);
        for (int index = 0; index < damageBonusPercentByLevel.Length; index++)
            damageBonusPercentByLevel[index] = clampedBonus;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureLevelCount(ShipAbilityDamageProgression.DefaultLevelCount);
    }
#endif
}
