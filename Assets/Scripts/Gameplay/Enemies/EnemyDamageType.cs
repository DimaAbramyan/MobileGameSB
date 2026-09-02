public enum EnemyDamageType
{
    // Explicit values preserve the meaning of already serialized weapon data.
    Kinetic = 0,
    Beam = 1,
    Spray = 2,
    Energy = 3,
    Radiation = 4,
    Explosion = 5
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
            EnemyDamageType.Radiation => new EnemyDamageProfile(1f, 1f, 0f),
            EnemyDamageType.Energy => new EnemyDamageProfile(1.1f, 0.9f, 0f),
            EnemyDamageType.Beam => new EnemyDamageProfile(1.25f, 0.75f, 0f),
            // A full bypass leaves the shield untouched and deals damage to the hull.
            EnemyDamageType.Spray => new EnemyDamageProfile(1f, 1f, 1f),
            _ => new EnemyDamageProfile(1f, 1f, 0f)
        };
    }
}
