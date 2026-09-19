using UnityEngine;

[CreateAssetMenu(
    fileName = "DisintegrationDebuff",
    menuName = "Game/Enemy Debuffs/Disintegration")]
public sealed class EnemyDisintegrationDebuffConfig : EnemyDebuffConfig
{
    [Header("Charge Decay")]
    [SerializeField, Min(0f)] private float chargeDecayDelay = 0.5f;
    [SerializeField, Min(0f)] private float chargeDecayPerSecond = 12f;

    public float ChargeDecayDelay => Mathf.Max(0f, chargeDecayDelay);
    public float ChargeDecayPerSecond => Mathf.Max(0f, chargeDecayPerSecond);

    public EnemyDisintegrationProfile CreateProfile()
    {
        return new EnemyDisintegrationProfile(
            ChargeDecayDelay,
            ChargeDecayPerSecond);
    }
}
