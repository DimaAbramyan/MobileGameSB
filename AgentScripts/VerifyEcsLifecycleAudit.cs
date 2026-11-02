using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VerifyEcsLifecycleAudit
{
    private static int checks;
    private static readonly List<string> passed = new();
    private static readonly List<string> failures = new();
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Scenario(string name, Action run)
    { try { run(); passed.Add(name); } catch (Exception error) { failures.Add(name + ": " + error); } }
    private static EnemyProjectileSpawnRequest Request(EnemyBullet bullet, Vector3 position)
        => new(bullet, position, Vector3.up, 1f, 3f, default, 0f, null, default, 0f, 0f, null, 1f);

    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        checks = 0; passed.Clear(); failures.Clear();
        int worlds = World.All.Count;
        var defaultWorld = World.DefaultGameObjectInjectionWorld;
        Scenario("1024 projectile batch, real ECS movement and lifetime cleanup", BatchLifecycle);
        Scenario("Repeated isolated preview creation and disposal", RepeatedPreview);
        Scenario("Cleanup after the owned ECS World was already disposed", DestroyedWorldCleanup);
        Scenario("Laser presentation resources survive neither their World nor their owner", DestroyedWorldLaserCleanup);
        Scenario("World-first disposal releases pending child completion gates", DestroyedWorldChildCleanup);
        Check(World.All.Count == worlds, "Audit leaked an ECS World.");
        Check(ReferenceEquals(defaultWorld, World.DefaultGameObjectInjectionWorld), "Default World changed.");
        return new { checks, passed = passed.ToArray(), failures = failures.ToArray(), nativeExecution = true };
    }

    private static EnemyBullet Bullet()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab");
        Check(prefab != null && prefab.GetComponent<EnemyBullet>() != null, "Missing existing bullet fixture.");
        return prefab.GetComponent<EnemyBullet>();
    }

    private static void BatchLifecycle()
    {
        using var world = new World("Technical audit enemy ECS lifecycle", WorldFlags.Editor);
        using var spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world);
        Check(!spawner.TrySpawnBatch(null), "Null batch must not spawn.");
        Check(!spawner.TrySpawnBatch(new List<EnemyProjectileSpawnRequest>()), "Empty batch must not spawn.");
        var requests = new List<EnemyProjectileSpawnRequest>(1024);
        var bullet = Bullet();
        for (int i = 0; i < 1024; i++) requests.Add(Request(bullet, new Vector3(i * .01f, 0f)));
        Check(spawner.TrySpawnBatch(requests), "Batch spawn failed.");
        var manager = world.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        Check(query.CalculateEntityCount() == 1024, "Unexpected live projectile count.");
        world.SetTime(new Unity.Core.TimeData(.1, .1f));
        world.GetOrCreateSystem<ProjectileMovementSystem>().Update(world.Unmanaged);
        using (var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            foreach (var pose in poses) Check(Mathf.Abs(pose.Position.y - .3f) < .001f, "Movement differs from speed * delta.");
        var registry = manager.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
        manager.AddBuffer<EnemyProjectileResolutionEvent>(registry);
        var end = world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        world.SetTime(new Unity.Core.TimeData(10000, 10000f));
        world.GetOrCreateSystem<ProjectileLifetimeSystem>().Update(world.Unmanaged);
        world.GetOrCreateSystem<ProjectileCleanupSystem>().Update(world.Unmanaged);
        end.Update();
        Check(query.CalculateEntityCount() == 0, "Expired projectiles survived cleanup.");
    }

    private static void RepeatedPreview()
    {
        int worlds = World.All.Count;
        var bullet = Bullet();
        for (int i = 0; i < 16; i++)
        {
            var preview = new EnemyProjectilePreviewWorld();
            try
            {
                preview.Spawn(new List<EnemyProjectileSpawnRequest> { Request(bullet, Vector3.zero) });
                preview.Step(.1f, .1f, new Vector3(50f, 50f));
                Check(preview.Count == 1, "Preview projectile vanished unexpectedly.");
            }
            finally { preview.Dispose(); preview.Dispose(); }
            Check(World.All.Count == worlds, "Repeated preview leaked a World.");
        }
    }

    private static void DestroyedWorldCleanup()
    {
        var preview = new EnemyProjectilePreviewWorld();
        try
        {
            Check(HasCameraCallback(preview), "Render callback was not registered.");
            preview.SimulationWorld.Dispose();
            preview.Dispose();
            Check(!HasCameraCallback(preview), "Disposed preview remains subscribed to Camera.onPreCull.");
        }
        finally
        {
            // The regression must not leave a render callback behind on the unfixed code.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var builtin = (Camera.CameraCallback)Delegate.CreateDelegate(typeof(Camera.CameraCallback), preview,
                typeof(EnemyProjectilePreviewWorld).GetMethod("BeforeBuiltinCamera", flags));
            Camera.onPreCull -= builtin;
            var pipeline = (Action<ScriptableRenderContext, Camera>)Delegate.CreateDelegate(
                typeof(Action<ScriptableRenderContext, Camera>), preview,
                typeof(EnemyProjectilePreviewWorld).GetMethod("BeforePipelineCamera", flags));
            RenderPipelineManager.beginCameraRendering -= pipeline;
            preview.Dispose();
        }
    }
    private static bool HasCameraCallback(object target)
    {
        var callbacks = Camera.onPreCull?.GetInvocationList();
        if (callbacks != null) foreach (var callback in callbacks)
            if (ReferenceEquals(callback.Target, target)) return true;
        return false;
    }

    private static void DestroyedWorldLaserCleanup()
    {
        var preview = new EnemyProjectilePreviewWorld();
        Mesh mesh = null; Material material = null;
        try
        {
            var request = new EnemyProjectileSpawnRequest(Bullet(), Vector3.zero, Vector3.up,
                1f, 3f, default, 0f, null, default, 0f, 0f, null, 1f, laser: new EnemyLaserSettings());
            preview.Spawn(new List<EnemyProjectileSpawnRequest> { request });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var spawner = typeof(EnemyProjectilePreviewWorld).GetField("spawner", flags).GetValue(preview);
            var lasers = typeof(EnemyProjectileEcsSpawner).GetField("lasers", flags).GetValue(spawner);
            mesh = (Mesh)typeof(EnemyLaserRuntime).GetField("mesh", flags).GetValue(lasers);
            material = (Material)typeof(EnemyLaserRuntime).GetField("material", flags).GetValue(lasers);
            Check(mesh != null && material != null, "Laser fixture did not create resources.");
            preview.SimulationWorld.Dispose();
            preview.Dispose();
            Check(mesh == null && material == null, "Laser mesh/material leaked after World-first disposal.");
        }
        finally
        {
            preview.Dispose();
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void DestroyedWorldChildCleanup()
    {
        var preview = new EnemyProjectilePreviewWorld();
        var completion = new EnemyAttackCompletionTracker();
        try
        {
            var request = new EnemyProjectileSpawnRequest(Bullet(), Vector3.zero, Vector3.up,
                1f, 3f, default, 0f, null, default, 0f, 0f, null, 1f,
                childAttack: new EnemyChildAttackSettings(), completionTracker: completion);
            preview.Spawn(new List<EnemyProjectileSpawnRequest> { request });
            Check(completion.PendingChildAttacks == 1, "Child fixture did not acquire completion gate.");
            preview.SimulationWorld.Dispose();
            preview.Dispose();
            Check(completion.IsComplete, "Child completion gate leaked after World-first disposal.");
        }
        finally { preview.Dispose(); }
    }
}
