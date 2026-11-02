using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class ProbePerformanceWorld
{
    public static async Task<object> Run()
    {
        World owned = null;
        foreach (var world in World.All) if (world.Name == "Android Performance Suite") owned = world;
        if (owned == null) throw new InvalidOperationException("Start a sustained Suite scenario first.");
        var manager = owned.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform), typeof(EnemyProjectileRemainingLifetime));
        Entity target = Entity.Null;
        using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
                if (manager.GetComponentData<EnemyProjectileRemainingLifetime>(entity).Value > 1)
                { target = entity; break; }
        if (target == Entity.Null) throw new InvalidOperationException("No stable live projectile sample.");
        double beforeTime = owned.Time.ElapsedTime;
        var before = manager.GetComponentData<LocalTransform>(target).Position;
        float beforeLifetime = manager.GetComponentData<EnemyProjectileRemainingLifetime>(target).Value;
        await Task.Delay(300);
        if (!owned.IsCreated || !manager.Exists(target)) throw new InvalidOperationException("Projectile/World ended while probing.");
        var after = manager.GetComponentData<LocalTransform>(target).Position;
        float remaining = manager.GetComponentData<EnemyProjectileRemainingLifetime>(target).Value;
        double distance = math.distance(before, after);
        bool passed = distance > .01 && remaining < beforeLifetime && owned.Time.ElapsedTime > beforeTime;
        if (!passed) throw new InvalidOperationException($"Runtime did not advance: distance={distance}, lifetime={beforeLifetime}->{remaining}.");
        return new { passed, distance, lifetimeBefore = beforeLifetime, lifetimeAfter = remaining, worldSeconds = owned.Time.ElapsedTime - beforeTime };
    }
}
