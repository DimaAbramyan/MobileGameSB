using Unity.Entities;
using Unity.Mathematics;

public struct DamageNumberData : IComponentData
{
    public float3 Position;
    public float Damage;
    public float ModifierPercent;
    public float Age;
    public float Lifetime;
    public float FadeStartDelay;
    public float FadeDuration;
    public float UpwardSpeed;
}
