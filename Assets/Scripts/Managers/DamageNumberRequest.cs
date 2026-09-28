using UnityEngine;

public readonly struct DamageNumberRequest
{
    public Vector3 WorldPosition { get; }
    public float Damage { get; }
    public float ModifierPercent { get; }
    public int AggregationKey { get; }
    public Enemy Target { get; }

    public DamageNumberRequest(
        Vector3 worldPosition,
        float damage,
        float modifierPercent,
        int aggregationKey = 0,
        Enemy target = null)
    {
        WorldPosition = worldPosition;
        Damage = Mathf.Max(0f, damage);
        ModifierPercent = modifierPercent;
        AggregationKey = aggregationKey;
        Target = target;
    }
}
