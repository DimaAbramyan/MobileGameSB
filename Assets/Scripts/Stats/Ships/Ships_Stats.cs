using UnityEngine;

[CreateAssetMenu(fileName = "NewShipData", menuName = "Game/Ship Data")]
public class ShipData : ScriptableObject
{
    public const float DefaultHealthRegenCooldown = 30f;
    public const float DefaultShieldRegenCooldown = 15f;
    public const float DefaultRegenRatePercent = 5f;

    [Header("Controllability")]

    public float speed;
    public float mass;
    public float drag;

    [Space(10)]
    [Header("Health")]
    public float maximumHealthPoints;
    [SerializeField] private bool overrideHealthRegeneration;
    [SerializeField, HideInInspector, Min(0f)]
    private float healthRegenCooldown = DefaultHealthRegenCooldown;
    [SerializeField, HideInInspector, Min(0f)]
    private float healthRegenRate = DefaultRegenRatePercent;

    [Space(10)]
    [Header("Shield")]
    public float maximumShieldPoints;
    [SerializeField] private bool overrideShieldRegeneration;
    [SerializeField, HideInInspector, Min(0f)]
    private float shieldRegenCooldown = DefaultShieldRegenCooldown;
    [SerializeField, HideInInspector, Min(0f)]
    private float shieldRegenRate = DefaultRegenRatePercent;

    public float HealthRegenCooldown => overrideHealthRegeneration
        ? healthRegenCooldown
        : DefaultHealthRegenCooldown;
    public float ShieldRegenCooldown => overrideShieldRegeneration
        ? shieldRegenCooldown
        : DefaultShieldRegenCooldown;
    public float HealthRegenRatePercent => shipMetaConfig != null
        ? shipMetaConfig.GetRuntimeStats(0).HealthRegenRatePercent
        : overrideHealthRegeneration
            ? healthRegenRate
            : DefaultRegenRatePercent;
    public float ShieldRegenRatePercent => shipMetaConfig != null
        ? shipMetaConfig.GetRuntimeStats(0).ShieldRegenRatePercent
        : overrideShieldRegeneration
            ? shieldRegenRate
            : DefaultRegenRatePercent;

    [Space(10)]
    [Header("Build Limits")]
    [Min(0)] public int maximumEnergy = 10;
    [Min(0)] public int maximumWeaponCount = 4;

    [Space(10)]
    [Header("StartLevel")]
    public int currentLvl;

    [Space(10)]
    [Header("Meta")]
    public int shipId;
    [SerializeField, Tooltip(
        "The persistent meta-progression source for this hull. "
        + "When assigned, its level 0 supplies the base hull statistics.")]
    private ShipMetaConfig shipMetaConfig;

    public ShipMetaConfig ShipMetaConfig => shipMetaConfig;

    public ShipMetaRuntimeStats GetMetaRuntimeStats(int metaLevel)
    {
        if (shipMetaConfig != null)
            return shipMetaConfig.GetRuntimeStats(metaLevel);

        var fallback = new ShipMetaBasicStats();
        fallback.Configure(
            maximumHealthPoints,
            maximumShieldPoints,
            HealthRegenRatePercent,
            ShieldRegenRatePercent,
            speed,
            maximumEnergy);
        return new ShipMetaRuntimeStats(fallback, null);
    }

    public float MetaSpeed => GetMetaRuntimeStats(0).Speed;
    public float MetaMaximumHealthPoints =>
        GetMetaRuntimeStats(0).MaximumHealthPoints;
    public float MetaMaximumShieldPoints =>
        GetMetaRuntimeStats(0).MaximumShieldPoints;
    public int MetaMaximumEnergy => GetMetaRuntimeStats(0).MaximumEnergy;

    public void SetShipMetaConfig(ShipMetaConfig config)
    {
        shipMetaConfig = config;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        healthRegenCooldown = Mathf.Max(0f, healthRegenCooldown);
        shieldRegenCooldown = Mathf.Max(0f, shieldRegenCooldown);
        healthRegenRate = Mathf.Max(0f, healthRegenRate);
        shieldRegenRate = Mathf.Max(0f, shieldRegenRate);
        maximumEnergy = Mathf.Max(0, maximumEnergy);
        maximumWeaponCount = Mathf.Max(0, maximumWeaponCount);
    }
#endif
}

