using UnityEngine;

[CreateAssetMenu(
    fileName = "HeatDebuff",
    menuName = "Game/Enemy Debuffs/Heat")]
public sealed class EnemyHeatDebuffConfig : EnemyDebuffConfig
{
    [Header("Temperature Normalization")]
    [SerializeField, Min(0f)] private float coolingDelay = 0.5f;
    [SerializeField, Range(0f, 100f)] private float coolingPercentPerSecond = 25f;

    [Header("Thermal Explosion At Maximum Temperature")]
    [SerializeField] private LayerMask affectedLayers = ~0;
    [SerializeField, Min(0f)] private float explosionRadius = 2f;
    [InspectorName("Explosion Damage (% Source Max Health)")]
    [SerializeField, Range(0f, 100f), Tooltip(
        "Damage dealt to every enemy in the radius as a percent of the defeated enemy's maximum health.")]
    private float explosionDamage = 30f;
    [InspectorName("Transferred Temperature (%)")]
    [SerializeField, Range(0f, 100f), Tooltip(
        "The share of the defeated enemy's signed temperature applied to every enemy in the radius.")]
    private float transferredHeatPercent = 50f;
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
    public float ExplosionDamagePercent => Mathf.Clamp(explosionDamage, 0f, 100f);
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
            ExplosionDamagePercent,
            TransferredHeatPercent,
            CoolingDelay,
            CoolingPercentPerSecond,
            ExplosionPrefab,
            this);
    }
}
