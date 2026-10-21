using Unity.Entities;
using Unity.Mathematics;

// Shared by runtime, child events, collision verification and the animated editor preview.
public struct EnemyLaserState : IComponentData
{
    public float2 Start, Direction;
    public float Length, Width;
    public byte Active, Telegraph, HitPlayer, Cancelled;
}
