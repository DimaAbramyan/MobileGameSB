using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyPlayerEcsAudit
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        checks = 0;
        int worlds = World.All.Count;
        var defaultWorld = World.DefaultGameObjectInjectionWorld;
        var data = BasicProjectile();
        string dataPath = AssetDatabase.GetAssetPath(data);
        for (int iteration = 0; iteration < 3; iteration++) Batch(data);
        Check(PlayerProjectileCollisionMath.IntersectsExpandedBounds(new(1.000002f, 0), new(.999998f, 0), new(-1, -1), new(1, 1), 0),
            "Short swept edge missed.");
        Check(World.All.Count == worlds && ReferenceEquals(defaultWorld, World.DefaultGameObjectInjectionWorld),
            "Audit changed or leaked a World.");
        return new { passed = true, checks, batches = 3, projectilesPerBatch = 512, dataPath,
            nativeMovement = true, sweptCollision = true, resolutionEvents = true, repeatedCleanup = true };
    }

    private static ProjectileData BasicProjectile()
    {
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:ProjectileData", new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(path);
            if (!data.UsesEntities || data.ProjectilePrefab == null) continue;
            var config = data.CreateRuntimeConfig();
            if (config.flightMode == ProjectileFlightMode.Straight && config.contactMode == ProjectileContactMode.DamageAndDestroy
                && !config.fadeDuringLifetime && !config.disableColliderAfterFirstPhysicsStep && config.damageMultiplierByDistance == null)
                return data;
        }
        throw new InvalidOperationException("No existing basic ECS player projectile asset for the fixture.");
    }

    private static void Batch(ProjectileData data)
    {
        using var world = new World("Technical audit player ECS", WorldFlags.Editor);
        using var spawner = new PlayerProjectileEcsSpawner();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PlayerProjectileEcsSpawner).GetField("entityWorld", flags).SetValue(spawner, world);
        typeof(PlayerProjectileEcsSpawner).GetField("entityManager", flags).SetValue(spawner, world.EntityManager);
        Check(!spawner.Prewarm(null), "Null config must be rejected.");
        Check(spawner.Prewarm(data), "Existing projectile prewarm failed.");
        var parameters = new ProjectileParams { speed = 20, damage = 3, maxLength = 1000, direction = Vector3.up };
        for (int i = 0; i < 512; i++) Check(spawner.TrySpawn(data, Vector3.zero, parameters, null), "Player spawn failed.");
        var manager = world.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(PlayerProjectileStaticData), typeof(LocalTransform));
        Check(query.CalculateEntityCount() == 512, "Unexpected live count.");
        var registry = manager.CreateEntity(typeof(PlayerProjectileCollisionRegistryTag), typeof(PlayerProjectileCollisionGridSettings));
        manager.SetComponentData(registry, new PlayerProjectileCollisionGridSettings { CellSize = 2 });
        manager.AddBuffer<PlayerProjectileCollisionTarget>(registry).Add(new PlayerProjectileCollisionTarget
            { TargetId = 7, Kind = PlayerProjectileCollisionTargetKind.Enemy, Min = new float2(-1, .9f), Max = new float2(1, 1.1f) });
        var grid = manager.AddBuffer<PlayerProjectileCollisionGridEntry>(registry);
        grid.Add(new PlayerProjectileCollisionGridEntry { CellX = -1, CellY = 0, TargetIndex = 0 });
        grid.Add(new PlayerProjectileCollisionGridEntry { CellX = 0, CellY = 0, TargetIndex = 0 });
        manager.AddBuffer<PlayerProjectileResolutionEvent>(registry);
        manager.AddBuffer<PlayerProjectileEntityDamageReceiverTarget>(registry);
        manager.AddBuffer<PlayerProjectileEntityDamageReceiverGridEntry>(registry);
        manager.AddBuffer<PlayerProjectileHomingTarget>(registry);
        manager.AddBuffer<PlayerProjectileExplosionRequest>(registry);
        manager.AddBuffer<PlayerProjectileSecondarySpawnRequest>(registry);
        var end = world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        world.SetTime(new Unity.Core.TimeData(.1, .1f));
        world.GetOrCreateSystem<PlayerProjectileMovementSystem>().Update(world.Unmanaged);
        using (var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            foreach (var pose in poses) Check(Mathf.Abs(pose.Position.y - 2f) < .001f, "Player velocity was not applied.");
        world.GetOrCreateSystem<PlayerProjectileEnemyCollisionSystem>().Update(world.Unmanaged);
        world.GetOrCreateSystem<PlayerProjectileCleanupSystem>().Update(world.Unmanaged);
        var events = manager.GetBuffer<PlayerProjectileResolutionEvent>(registry);
        Check(events.Length == 512, "Swept collision did not emit exactly one hit per projectile: " + events.Length);
        foreach (var hit in events) Check(hit.TargetId == 7 && Mathf.Abs(hit.Damage - 3f) < .001f, "Unexpected damage event.");
        end.Update();
        Check(query.CalculateEntityCount() == 0, "Resolved player projectiles survived cleanup.");
    }
}
