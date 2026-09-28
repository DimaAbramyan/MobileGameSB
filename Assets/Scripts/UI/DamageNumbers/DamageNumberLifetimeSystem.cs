using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct DamageNumberLifetimeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageNumberData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach (RefRW<DamageNumberData> damageNumber
                 in SystemAPI.Query<RefRW<DamageNumberData>>())
        {
            damageNumber.ValueRW.Age += deltaTime;
        }
    }
}
