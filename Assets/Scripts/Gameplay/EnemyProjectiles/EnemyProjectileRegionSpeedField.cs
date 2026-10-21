using Unity.Entities;
using Unity.Mathematics;

// Independent active modifier; passive fields keep their original shape and stacking.
[InternalBufferCapacity(2)]
public struct EnemyProjectileRegionSpeedField : IBufferElementData
{
    public float2 Minimum, Maximum;
    public float DividerY, FrontMultiplier, RearMultiplier;

    public float EvaluateMultiplier(float2 position)
    {
        if (math.any(position < Minimum) || math.any(position > Maximum)) return 1f;
        // The line belongs to the front region; there is no neutral strip.
        return position.y >= DividerY ? FrontMultiplier : RearMultiplier;
    }

    public static float EvaluateMultiplier(float2 position, DynamicBuffer<EnemyProjectileRegionSpeedField> fields)
    {
        if (!fields.IsCreated) return 1f;
        float slowest = 1f, fastest = 1f;
        for (int i = 0; i < fields.Length; i++)
        {
            float value = fields[i].EvaluateMultiplier(position);
            slowest = math.min(slowest, value);
            fastest = math.max(fastest, value);
        }
        return slowest * fastest;
    }
}

public static class EnemyProjectileSpeedModifiers
{
    public static float EvaluateMultiplier(float2 position, DynamicBuffer<EnemyProjectileSlowField> passive,
        DynamicBuffer<EnemyProjectileRegionSpeedField> active, float projectileRadius = 0f)
        => EnemyProjectileSlowField.EvaluateMultiplier(position, passive, projectileRadius)
            * EnemyProjectileRegionSpeedField.EvaluateMultiplier(position, active);
}
