using System;
using UnityEngine;

public enum EnemyNumericRandomizeOn { PerShot, PerAttack, PerDirection, PerProjectile }
[Flags]
public enum EnemyNumericScopes { None = 0, PerAttack = 1, PerShot = 2, PerDirection = 4, PerProjectile = 8,
    Volley = PerAttack | PerShot, Direction = Volley | PerDirection, All = Direction | PerProjectile }

// Immutable identity of a shot/direction/projectile. New channels cannot perturb existing origin/speed/cooldown streams.
// Samples are materialized by schedulers and spawn builders; movement systems only consume stored ECS values.
public readonly struct EnemyNumericSample
{
    public readonly bool Active;
    private readonly uint attack, shot;
    private readonly int direction, projectile;
    public int DirectionIndex => direction;
    public EnemyNumericSample(uint attack, uint shot, int direction = 0, int projectile = 0)
    { Active = true; this.attack = attack; this.shot = shot; this.direction = direction; this.projectile = projectile; }
    public EnemyNumericSample ForDirection(int index) => Active ? new(attack, shot, index, projectile) : default;
    public EnemyNumericSample ForProjectile(int index) => Active ? new(attack, shot, direction, index) : default;
    private uint Key(uint channel, EnemyNumericRandomizeOn frequency)
    {
        uint value = frequency == EnemyNumericRandomizeOn.PerAttack ? attack : shot;
        if (frequency == EnemyNumericRandomizeOn.PerDirection || frequency == EnemyNumericRandomizeOn.PerProjectile)
            value ^= NumericRandomStream.Hash((uint)direction + 0x6f12d87bu);
        if (frequency == EnemyNumericRandomizeOn.PerProjectile)
            value ^= NumericRandomStream.Hash((uint)projectile + 0x937ac15du);
        return NumericRandomStream.Hash(value ^ channel);
    }
    public float Float(RandomFloatRange range, float fixedValue, uint channel, float minimum = float.MinValue)
    {
        if (!Active || range == null || !range.enabled) return fixedValue;
        var stream = new NumericRandomStream(Key(channel, range.randomizeOn));
        return range.Sample(fixedValue, ref stream, minimum);
    }
    public int Int(RandomIntRange range, int fixedValue, uint channel, int minimum = 1, int maximum = int.MaxValue)
    {
        if (!Active || range == null || !range.enabled) return fixedValue;
        var stream = new NumericRandomStream(Key(channel, range.randomizeOn));
        return range.Sample(fixedValue, ref stream, minimum, maximum);
    }
    public float Unit(uint channel, EnemyNumericRandomizeOn frequency = EnemyNumericRandomizeOn.PerProjectile)
        => NumericRandomStream.At(Key(channel, frequency), 0);
    public static bool Enabled(RandomFloatRange range) => range != null && range.enabled;
    public static bool Enabled(RandomIntRange range) => range != null && range.enabled;
    public static bool PerDirection(RandomFloatRange range) => Enabled(range) && range.randomizeOn == EnemyNumericRandomizeOn.PerDirection;
    public static bool PerDirection(RandomIntRange range) => Enabled(range) && range.randomizeOn == EnemyNumericRandomizeOn.PerDirection;
}
