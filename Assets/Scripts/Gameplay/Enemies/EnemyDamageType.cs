public enum EnemyDamageType
{
    // Explicit values preserve the meaning of already serialized weapon data.
    Kinetic = 0,
    Plasma = 1,
    Chemical = 2,
    Electric = 3,
    Resonance = 4,
    Explosion = 5,
    Microwave = 6
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
            EnemyDamageType.Kinetic => new EnemyDamageProfile(0.75f, 1.25f, 0f),
            EnemyDamageType.Explosion => new EnemyDamageProfile(0.9f, 1.1f, 0f),
            EnemyDamageType.Resonance => new EnemyDamageProfile(1f, 1f, 0f),
            EnemyDamageType.Electric => new EnemyDamageProfile(1.1f, 0.9f, 0f),
            EnemyDamageType.Microwave => new EnemyDamageProfile(1.1f, 0.9f, 0f),
            EnemyDamageType.Plasma => new EnemyDamageProfile(1.25f, 0.75f, 0f),
            // A full bypass leaves the shield untouched and deals damage to the hull.
            EnemyDamageType.Chemical => new EnemyDamageProfile(1f, 1f, 1f),
            _ => new EnemyDamageProfile(1f, 1f, 0f)
        };
    }
}
