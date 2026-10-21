public enum EnemyDamageType
{
    // Explicit values preserve the meaning of already serialized weapon data.
    Kinetick = 0,
    Plazma = 1,
    Chemical = 2,
    Default = 4
}

public readonly struct EnemyDamageProfile
{
    public EnemyDamageProfile(
        float shieldMultiplier,
        float hullMultiplier,
        float shieldBypassFraction)
    {
        ShieldMultiplier = shieldMultiplier;
        HullMultiplier = hullMultiplier;
        ShieldBypassFraction = shieldBypassFraction;
    }

    public float ShieldMultiplier { get; }
    public float HullMultiplier { get; }
    public float ShieldBypassFraction { get; }
}

public static class EnemyDamageProfiles
{
    public static EnemyDamageProfile Get(EnemyDamageType damageType)
    {
        return damageType switch
        {
            EnemyDamageType.Kinetick => new EnemyDamageProfile(0.65f, 1.35f, 0f),
            EnemyDamageType.Plazma => new EnemyDamageProfile(1.35f, 0.65f, 0f),
            EnemyDamageType.Default => new EnemyDamageProfile(1f, 1f, 0f),
            EnemyDamageType.Chemical => new EnemyDamageProfile(1f, 1f, 1f),
            _ => new EnemyDamageProfile(1f, 1f, 0f)
        };
    }
}
