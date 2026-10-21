using UnityEngine;

public sealed partial class EnemyAttackNumericState
{
    private System.Collections.Generic.Dictionary<uint, NumericRandomStream> timing;
    private NumericRandomStream TimingStream(uint channel)
    {
        timing ??= new();
        return timing.TryGetValue(channel, out var stream) ? stream : new NumericRandomStream(lineage ^ 0x49c7b812u ^ channel);
    }
    public float SampleTiming(float fixedValue, RandomFloatRange range, float minimum = 0f, uint channel = 0)
    {
        if (!EnemyNumericSample.Enabled(range)) return fixedValue;
        var stream = TimingStream(channel); float value = range.Sample(fixedValue, ref stream, minimum); timing[channel] = stream; return value;
    }
    public int SampleTiming(int fixedValue, RandomIntRange range, uint channel = 0)
    {
        if (!EnemyNumericSample.Enabled(range)) return fixedValue;
        var stream = TimingStream(channel); int value = range.Sample(fixedValue, ref stream, 1); timing[channel] = stream; return value;
    }
}
