using UnityEngine;

[CreateAssetMenu(
    fileName = "HeatDebuff",
    menuName = "Game/Enemy Debuffs/Heat")]
public sealed class EnemyHeatDebuffConfig : EnemyDebuffConfig
{
    [Header("Temperature Normalization")]
    [SerializeField, Min(0f)] private float coolingDelay = 0.5f;
    [SerializeField, Range(0f, 100f)] private float coolingPercentPerSecond = 25f;

    [Header("Overheat Explosion")]
    [SerializeField] private LayerMask affectedLayers = ~0;
    [SerializeField, Min(0f)] private float explosionRadius = 2f;
    [SerializeField, Min(0f)] private float explosionDamage = 30f;
    [SerializeField, Range(0f, 100f)] private float transferredHeatPercent = 50f;
    [SerializeField] private Explode explosionPrefab;

    [Header("Ignition At 100 Temperature")]
    [SerializeField, Min(0f)] private float burningDuration = 3f;
    [SerializeField, Min(0f)] private float burningDamagePerTick = 5f;
    [SerializeField, Min(0.01f)] private float burningDamageInterval = 0.5f;

    public float CoolingDelay => Mathf.Max(0f, coolingDelay);
    public float CoolingPercentPerSecond => Mathf.Clamp(
        coolingPercentPerSecond,
        0f,
        100f);
    public LayerMask AffectedLayers => affectedLayers;
    public float ExplosionRadius => Mathf.Max(0f, explosionRadius);
    public float ExplosionDamage => Mathf.Max(0f, explosionDamage);
    public float TransferredHeatPercent => Mathf.Clamp(
        transferredHeatPercent,
        0f,
        100f);
    public Explode ExplosionPrefab => explosionPrefab;
    public float BurningDuration => Mathf.Max(0f, burningDuration);
    public float BurningDamagePerTick => Mathf.Max(0f, burningDamagePerTick);
    public float BurningDamageInterval => Mathf.Max(0.01f, burningDamageInterval);

    public EnemyHeatProfile CreateProfile(ParentShip owner)
    {
        return new EnemyHeatProfile(
            owner,
            AffectedLayers,
            ExplosionRadius,
            ExplosionDamage,
            TransferredHeatPercent,
            CoolingDelay,
            CoolingPercentPerSecond,
            ExplosionPrefab,
            this);
    }
}
