using System;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class ProbeWavePreviewSnapshotApi
{
    public static object Run()
    {
        int before = World.All.Count;
        using (var simulation = new EnemyProjectilePreviewWorld())
        using (var storage = new World("Snapshot API probe", WorldFlags.Editor))
        {
            var spawner = (EnemyProjectileEcsSpawner)typeof(EnemyProjectilePreviewWorld).GetField("spawner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            if (!spawner.TrySpawn(prefab, new Vector3(1, 2), Vector3.down, 1)) throw new Exception("Spawn failed");
            var manager = simulation.SimulationWorld.EntityManager;
            using var query = manager.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities,
                None = new[] { ComponentType.ReadOnly<EnemyChildAttackSimulation>(), ComponentType.ReadOnly<EnemyLaserSimulation>() } });
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var copies = new NativeArray<Entity>(entities.Length, Allocator.Temp);
            storage.EntityManager.CopyEntitiesFrom(manager, entities, copies);
            int count = entities.Length;
            manager.DestroyEntity(query);
            using var restored = new NativeArray<Entity>(count, Allocator.Temp);
            manager.CopyEntitiesFrom(storage.EntityManager, copies, restored);
            int moved = 0;
            for (int i = 0; i < restored.Length; i++)
                if (!manager.HasComponent<Prefab>(restored[i]) && manager.HasComponent<LocalTransform>(restored[i]))
                    if (Vector3.Distance((Vector3)manager.GetComponentData<LocalTransform>(restored[i]).Position, new Vector3(1, 2)) < .001f) moved++;
            if (moved != 1) throw new Exception("Copied projectile pose missing");
            var field = typeof(EnemyProjectilePreviewWorld).GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic);
            Entity original = (Entity)field.GetValue(simulation);
            for (int i = 0; i < count; i++) if (entities[i] == original) field.SetValue(simulation, restored[i]);
            simulation.Step(.1f, .1f, new Vector3(0, -3));
            return new { passed = true, copiedEntities = count, projectilePoses = moved, readonlyFieldRestore = true,
                liveWorlds = World.All.Count - before, chunks = query.CalculateChunkCount() };
        }
    }
}
