using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

public sealed partial class EnemyProjectilePreviewWorld
{
    internal void RestoreClock(float time) => world.SetTime(new Unity.Core.TimeData(time, WaveShootingPreviewSession.StepInterval));
    internal object SnapshotManagedRoot { get { spawner.PrepareEditorPreviewSnapshots(); return spawner; } }
    internal EntityQuery SnapshotEntities() => world.EntityManager.CreateEntityQuery(new EntityQueryDesc
    {
        Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab,
        None = new[] { ComponentType.ReadOnly<EnemyChildAttackSimulation>(), ComponentType.ReadOnly<EnemyLaserSimulation>() }
    });
    internal sealed class Snapshot : IDisposable
    {
        private readonly World storage = new("Wave Preview snapshot", WorldFlags.Editor);
        private readonly Entity[] original, copies;
        private readonly Entity registry;
        private readonly long omitted;
        public long EstimatedBytes { get; }
        public Snapshot(EnemyProjectilePreviewWorld source)
        {
            using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotNative);
            registry = source.registry; omitted = source.omitted;
            try
            {
                using var query = source.SnapshotEntities();
                using var entities = query.ToEntityArray(Allocator.Temp);
                using var output = new NativeArray<Entity>(entities.Length, Allocator.Temp);
                storage.EntityManager.CopyEntitiesFrom(source.world.EntityManager, entities, output);
                original = entities.ToArray(); copies = output.ToArray();
                EstimatedBytes = 1024L * 1024L + query.CalculateChunkCount() * 16384L + original.Length * 32L;
            }
            catch { storage.Dispose(); throw; }
        }
        public Dictionary<Entity, Entity> Restore(EnemyProjectilePreviewWorld destination)
        {
            using var query = destination.SnapshotEntities();
            destination.world.EntityManager.CompleteAllTrackedJobs();
            destination.world.EntityManager.DestroyEntity(query);
            using var input = new NativeArray<Entity>(copies, Allocator.Temp);
            using var output = new NativeArray<Entity>(copies.Length, Allocator.Temp);
            destination.world.EntityManager.CopyEntitiesFrom(storage.EntityManager, input, output);
            var map = new Dictionary<Entity, Entity>(original.Length);
            for (int i = 0; i < original.Length; i++) map.Add(original[i], output[i]);
            destination.registry = map[registry]; destination.omitted = omitted;
            return map;
        }
        public void Dispose() { if (storage.IsCreated) storage.Dispose(); }
    }
}
