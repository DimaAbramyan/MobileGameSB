using System;
using System.Collections.Generic;
using UnityEngine;

// Companion ranges preserve the original serialized numeric field and its Prefab Overrides.
[Serializable]
public sealed class RandomFloatRange
{
    public bool enabled;
    public float min, max;
    public EnemyNumericRandomizeOn randomizeOn;
    [HideInInspector] public bool initialized;
    public void CopyFrom(RandomFloatRange source)
    { enabled = source != null && source.enabled; min = source?.min ?? 0f; max = source?.max ?? 0f; initialized = source != null && source.initialized; randomizeOn = source?.randomizeOn ?? default; }
    public RandomFloatRange Copy() { var copy = new RandomFloatRange(); copy.CopyFrom(this); return copy; }
    public void Scale(float multiplier) { min *= multiplier; max *= multiplier; }
    public float Sample(float fixedValue, ref NumericRandomStream stream, float minimum = float.MinValue)
        => enabled ? Sample01(stream.Next01(), minimum) : Mathf.Max(minimum, fixedValue);
    public float Sample01(float value, float minimum = float.MinValue)
    {
        GetBounds(out float a, out float b, minimum);
        return (float)((double)a + ((double)b - a) * value);
    }
    public void GetBounds(out float low, out float high, float minimum = float.MinValue)
    {
        float a = float.IsFinite(min) ? Mathf.Max(minimum, min) : minimum == float.MinValue ? 0f : minimum;
        float b = float.IsFinite(max) ? Mathf.Max(minimum, max) : a;
        low = Mathf.Min(a, b); high = Mathf.Max(a, b);
    }
}

[Serializable]
public sealed class RandomIntRange
{
    public bool enabled;
    public int min, max;
    public EnemyNumericRandomizeOn randomizeOn;
    [HideInInspector] public bool initialized;
    public void CopyFrom(RandomIntRange source)
    { enabled = source != null && source.enabled; min = source?.min ?? 0; max = source?.max ?? 0; initialized = source != null && source.initialized; randomizeOn = source?.randomizeOn ?? default; }
    public RandomIntRange Copy() { var copy = new RandomIntRange(); copy.CopyFrom(this); return copy; }
    public int Sample(int fixedValue, ref NumericRandomStream stream, int minimum = int.MinValue, int maximum = int.MaxValue)
    {
        if (!enabled) return fixedValue;
        long low = Mathf.Clamp(Math.Min(min, max), minimum, maximum);
        long high = Mathf.Clamp(Math.Max(min, max), minimum, maximum);
        ulong count = (ulong)(high - low + 1L);
        ulong limit = (1UL << 32) / count * count;
        uint value; do { value = stream.Next(); } while ((ulong)value >= limit);
        return (int)(low + (long)((ulong)value % count));
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class RandomizableAttribute : PropertyAttribute
{
    public readonly string Range;
    public readonly string YRange;
    public readonly float Minimum;
    public readonly EnemyNumericScopes Scopes;
    public readonly EnemyNumericRandomizeOn DefaultScope;
    public RandomizableAttribute(string range, float minimum = float.MinValue, EnemyNumericScopes scopes = EnemyNumericScopes.None,
        EnemyNumericRandomizeOn defaultScope = EnemyNumericRandomizeOn.PerShot)
    { Range = range; Minimum = minimum; Scopes = scopes; DefaultScope = defaultScope; }
    public RandomizableAttribute(string xRange, string yRange, EnemyNumericScopes scopes = EnemyNumericScopes.None,
        EnemyNumericRandomizeOn defaultScope = EnemyNumericRandomizeOn.PerShot)
    { Range = xRange; YRange = yRange; Minimum = float.MinValue; Scopes = scopes; DefaultScope = defaultScope; }
}

public struct NumericRandomStream
{
    private uint state;
    public NumericRandomStream(uint seed) { state = seed; }
    public uint Next() { state += 0x9e3779b9u; return Hash(state); }
    public float Next01() => (Next() >> 8) * (1f / 16777216f);
    public static uint Hash(uint value)
    { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ value >> 16; }
    public static float At(uint seed, int index) => (Hash(seed ^ Hash((uint)index + 0x9e3779b9u)) >> 8) * (1f / 16777216f);
}

public enum EnemySpeedRandomizeOn { PerAttack, PerBurst, PerShot, PerProjectile }

// Immutable per-volley snapshot: delayed Sector launches never read a later volley's sample.
public readonly struct EnemyRandomSpeedSample
{
    public readonly bool Active;
    public readonly uint SeedKey;
    private readonly bool perProjectile;
    private readonly float value, min, max, delta;
    private readonly uint seed;
    public readonly EnemyNumericSample Numeric;
    public EnemyRandomSpeedSample(float value, uint key)
    { Active = true; SeedKey = key; perProjectile = false; this.value = value; min = max = delta = 0f; seed = 0; Numeric = default; }
    public EnemyRandomSpeedSample(float min, float max, float delta, uint seed, uint key)
    { Active = perProjectile = true; SeedKey = key; value = 0f; this.min = min; this.max = max; this.delta = delta; this.seed = seed; Numeric = default; }
    private EnemyRandomSpeedSample(uint key)
    { Active = perProjectile = false; SeedKey = key; value = min = max = delta = 0f; seed = 0; Numeric = default; }
    private EnemyRandomSpeedSample(EnemyRandomSpeedSample source, EnemyNumericSample numeric)
    { Active = source.Active; SeedKey = source.SeedKey; perProjectile = source.perProjectile; value = source.value;
        min = source.min; max = source.max; delta = source.delta; seed = source.seed; Numeric = numeric; }
    public EnemyRandomSpeedSample WithNumeric(EnemyNumericSample numeric) => new(this, numeric);
    public static EnemyRandomSpeedSample Identity(uint key) => new(key);
    public float Evaluate(int index) => Mathf.Max(.01f, perProjectile
        ? Mathf.Lerp(Mathf.Min(min, max), Mathf.Max(min, max), NumericRandomStream.At(seed, index)) + delta : value);
}

public sealed partial class EnemyAttackNumericState
{
    private NumericRandomStream offsetX, offsetY, speed, cooldown;
    private bool hasX, hasY, hasSpeed;
    private float x, y, sampledSpeed;
    private readonly uint lineage;
    private uint shot;
    private uint numericAttack;
    public Vector2 Offsets => new(x, y);
    public EnemyAttackNumericState(int seed, int owner, uint attack)
    {
        uint key = NumericRandomStream.Hash((uint)seed ^ NumericRandomStream.Hash((uint)owner) ^ NumericRandomStream.Hash(attack));
        lineage = key;
        offsetX = new NumericRandomStream(key ^ 0x15e39a71u); offsetY = new NumericRandomStream(key ^ 0xa28b163fu);
        speed = new NumericRandomStream(key ^ 0x94732fb1u); cooldown = new NumericRandomStream(key ^ 0xd356a19bu);
    }
    public void BeginOrigin(EnemyEmissionOriginSettings origin)
    {
        hasX = hasY = false;
        if (origin.X.RandomizeOn == EnemyEmissionOriginCaptureMode.OnAttackStart) SampleAxis(origin.X, ref x, ref hasX, ref offsetX);
        if (origin.Y.RandomizeOn == EnemyEmissionOriginCaptureMode.OnAttackStart) SampleAxis(origin.Y, ref y, ref hasY, ref offsetY);
    }
    public void PrepareOrigin(EnemyEmissionOriginSettings origin, bool burstStart)
    {
        if (!hasX || Refresh(origin.X.RandomizeOn, burstStart)) SampleAxis(origin.X, ref x, ref hasX, ref offsetX);
        if (!hasY || Refresh(origin.Y.RandomizeOn, burstStart)) SampleAxis(origin.Y, ref y, ref hasY, ref offsetY);
    }
    private static bool Refresh(EnemyEmissionOriginCaptureMode mode, bool burst) => mode == EnemyEmissionOriginCaptureMode.OnShot
        || mode == EnemyEmissionOriginCaptureMode.OnBurstStart && burst;
    private static void SampleAxis(EnemyEmissionOriginAxis axis, ref float value, ref bool present, ref NumericRandomStream stream)
    { value = axis.OffsetRandom.Sample(axis.Offset, ref stream); present = true; }
    public void BeginSpeed() { hasSpeed = false; numericAttack++; }
    public EnemyRandomSpeedSample PrepareSpeed(EnemyShootingSettings shooting, bool burstStart, float consistentDelta)
    {
        uint key = NumericRandomStream.Hash(lineage ^ ++shot);
        var numeric = new EnemyNumericSample(NumericRandomStream.Hash(lineage ^ numericAttack), key);
        if (!shooting.TryGetRandomSpeedRange(out float min, out float max)) return EnemyRandomSpeedSample.Identity(key).WithNumeric(numeric);
        if (shooting.SpeedRandomizeOn == EnemySpeedRandomizeOn.PerProjectile)
            return new EnemyRandomSpeedSample(min, max, consistentDelta, speed.Next(), key).WithNumeric(numeric);
        if (!hasSpeed || shooting.SpeedRandomizeOn == EnemySpeedRandomizeOn.PerShot
            || shooting.SpeedRandomizeOn == EnemySpeedRandomizeOn.PerBurst && burstStart)
        { sampledSpeed = Mathf.Lerp(min, max, speed.Next01()); hasSpeed = true; }
        return new EnemyRandomSpeedSample(sampledSpeed + consistentDelta, key).WithNumeric(numeric);
    }
    public float SampleCooldown(float fixedValue, RandomFloatRange range)
        => range != null ? range.Sample(fixedValue, ref cooldown, 0f) : Mathf.Max(0f, fixedValue);
}

// One registry per physical enemy/preview slot; entries are released after an attack.
public sealed class EnemyAttackNumericRegistry
{
    private readonly Dictionary<EnemyBurstAttackSettings, EnemyAttackNumericState> states = new();
    private uint attack;
    private int owner, seedOffset;
    public void Configure(int owner, int seedOffset = 0)
    {
        if (this.owner == owner && this.seedOffset == seedOffset) return;
        states.Clear(); attack = 0; this.owner = owner; this.seedOffset = seedOffset;
    }
    public EnemyAttackNumericState Get(EnemyBurstAttackSettings pattern, bool force = false)
    {
        if (!force && !pattern.UsesRandomTiming && !pattern.AttackCooldownRandom.enabled && !pattern.Shooting.UsesNumericRandomization) return null;
        if (!states.TryGetValue(pattern, out var state))
        { state = CreateDetached(pattern, true); states.Add(pattern, state); }
        return state;
    }
    public EnemyAttackNumericState CreateDetached(EnemyBurstAttackSettings pattern, bool force = false)
        => force || pattern.UsesRandomTiming || pattern.AttackCooldownRandom.enabled || pattern.Shooting.UsesNumericRandomization
            ? new(unchecked(pattern.Shooting.NumericRandomSeed + seedOffset), owner, ++attack) : null;
    public void Release(EnemyBurstAttackSettings pattern) => states.Remove(pattern);
    public void ReleaseAll() => states.Clear();
    public void Clear() { states.Clear(); attack = 0; }
}

public interface IEnemyAttackNumericSource
{
    EnemyAttackNumericRegistry NumericRegistry { get; }
    void SetNumericOwner(int slot);
}

public static class EnemyAttackCooldownSampler
{
    public static float Sample(EnemyBurstAttackSettings profile, EnemyAttackNumericRegistry registry,
        DirectedWaveAttackSettings wave = null)
    {
        if (wave != null && wave.OverridesEnemyAttackCooldown)
            return wave.EnemyAttackCooldownRandom.enabled
                ? registry.Get(profile.GetPattern(0), true).SampleCooldown(wave.EnemyAttackCooldown, wave.EnemyAttackCooldownRandom)
                : Mathf.Max(0f, wave.EnemyAttackCooldown);
        float maximum = 0f;
        for (int i = 0; i < profile.PatternCount; i++)
        {
            var pattern = profile.GetPattern(i);
            maximum = Mathf.Max(maximum, pattern.AttackCooldownRandom.enabled
                ? registry.Get(pattern).SampleCooldown(pattern.AttackCooldown, pattern.AttackCooldownRandom) : pattern.AttackCooldown);
        }
        return maximum;
    }
}
