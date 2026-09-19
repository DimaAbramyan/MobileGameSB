using UnityEngine;

[CreateAssetMenu(
    fileName = "SlowDebuff",
    menuName = "Game/Enemy Debuffs/Slow")]
public sealed class EnemySlowDebuffConfig : EnemyDebuffConfig
{
    [Header("Slow")]
    [SerializeField, Range(0f, 100f)] private float maximumSlowPercent = 50f;

    [Header("Temperature Normalization")]
    [SerializeField, Min(0f)] private float normalizationDelay = 0.5f;
    [SerializeField, Range(0f, 100f)]
    private float normalizationPercentPerSecond = 25f;

    public float MaximumSlowPercent => Mathf.Clamp(maximumSlowPercent, 0f, 100f);
    public float NormalizationDelay => Mathf.Max(0f, normalizationDelay);
    public float NormalizationPercentPerSecond => Mathf.Clamp(
        normalizationPercentPerSecond,
        0f,
        100f);
}
