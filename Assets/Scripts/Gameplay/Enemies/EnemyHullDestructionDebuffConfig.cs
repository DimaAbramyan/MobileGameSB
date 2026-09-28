using UnityEngine;

[CreateAssetMenu(
    fileName = "HullDestructionDebuff",
    menuName = "Game/Enemy Debuffs/Hull Destruction")]
public sealed class EnemyHullDestructionDebuffConfig : EnemyDebuffConfig
{
    [Header("Incoming Hull Damage")]
    [SerializeField, Min(0f)] private float maximumBonusPercent = 100f;

    public float MaximumBonusPercent => Mathf.Max(0f, maximumBonusPercent);
}
