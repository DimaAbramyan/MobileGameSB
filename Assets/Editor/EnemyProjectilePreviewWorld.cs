using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

// Owns simulation and presentation resources for one inspector. No default world or GameObjects per bullet.
public sealed partial class EnemyProjectilePreviewWorld : IDisposable
{
    public const int MaximumProjectiles = 2048;
    private readonly World world = new World("Scene shooting preview", WorldFlags.Editor);
    private readonly EnemyProjectileEcsSpawner spawner;
    private readonly EntityQuery projectiles;
    private readonly EntityQuery renderProjectiles;
    private Entity registry;
    private readonly SystemHandle speed, scale, homing, burst, movement, lifetime;
    private readonly Dictionary<(Mesh, Material, float4), RenderBatch> batches = new();
    private readonly List<RenderBatch> batchList = new();
    private readonly HashSet<Camera> previewCameras = new();
    private bool visible = true;
    private sealed class RenderBatch
    {
        public Mesh Mesh;
        public Material Material;
        public readonly Matrix4x4[] Matrices = new Matrix4x4[MaximumProjectiles];
        public int Count;
    }
    private readonly Matrix4x4[] drawMatrices = new Matrix4x4[1023];
    public int Count => projectiles.CalculateEntityCount();
    private long omitted;
    public long Omitted { get => omitted + spawner.OmittedChildProjectiles; private set => omitted = value - spawner.OmittedChildProjectiles; }
    public World SimulationWorld => world;

    public EnemyProjectilePreviewWorld()
    {
        spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world);
        var manager = world.EntityManager;
        registry = manager.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
        manager.AddBuffer<EnemyProjectileCollisionTarget>(registry);
        manager.AddBuffer<EnemyProjectileBurstSpawnRequest>(registry);
        manager.AddBuffer<EnemyProjectileResolutionEvent>(registry);
        projectiles = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform), typeof(EnemyProjectileResolution));
        renderProjectiles = manager.CreateEntityQuery(new EntityQueryDesc {
            All = new[] { ComponentType.ReadOnly<EnemyProjectileMovement>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyProjectileResolution>() },
            None = new[] { ComponentType.ReadOnly<EnemyLaserState>() } });
        speed = world.GetOrCreateSystem<ProjectileCustomSpeedSystem>();
        scale = world.GetOrCreateSystem<ProjectileCustomScaleSystem>();
        homing = world.GetOrCreateSystem<ProjectileHomingSystem>();
        burst = world.GetOrCreateSystem<ProjectileBurstAtPointSystem>();
        movement = world.GetOrCreateSystem<ProjectileMovementSystem>();
        lifetime = world.GetOrCreateSystem<ProjectileLifetimeSystem>();
        RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
        Camera.onPreCull += BeforeBuiltinCamera;
    }

    public void RegisterCamera(Camera camera)
    {
        if (camera != null) previewCameras.Add(camera);
    }

    public void SetVisible(bool value) => visible = value;

    private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera)
        => DrawBeforeCamera(camera);

    private void BeforeBuiltinCamera(Camera camera)
    {
        if (GraphicsSettings.currentRenderPipeline == null) DrawBeforeCamera(camera);
    }

    private void DrawBeforeCamera(Camera camera)
    {
        if (!visible || !world.IsCreated || Application.isPlaying
            || !EnemyShootingPreview.ShowFormationVolley || !previewCameras.Contains(camera)) return;
        // Graphics submissions last one rendered frame. Submit before every camera render,
        // including repaints that do not invoke the selected inspector's OnSceneGUI.
        Draw(camera);
    }

    public void Spawn(List<EnemyProjectileSpawnRequest> requests)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Spawn);
        int available = Mathf.Max(0, WavePreviewProfiler.EffectiveProjectileLimit(MaximumProjectiles) - Count);
        if (requests.Count > available)
        {
            Omitted += requests.Count - available;
            requests.RemoveRange(available, requests.Count - available);
        }
        if (requests.Count > 0) spawner.TrySpawnBatch(requests);
    }

    public void Step(float time, float delta, Vector3 target, Camera gameplayCamera = null)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.ProjectileStep);
        world.SetTime(new Unity.Core.TimeData(time, delta));
        var targets = world.EntityManager.GetBuffer<EnemyProjectileCollisionTarget>(registry);
        targets.Clear();
        targets.Add(new EnemyProjectileCollisionTarget { Kind = EnemyProjectileCollisionTargetKind.PlayerShip, IsCircle = 1,
            Center = new float2(target.x, target.y), PreviousCenter = new float2(target.x, target.y), Radius = .05f,
            Min = new float2(target.x, target.y), Max = new float2(target.x, target.y) });
        world.EntityManager.GetBuffer<EnemyProjectileResolutionEvent>(registry).Clear();
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Homing)) homing.Update(world.Unmanaged);
        BoundBurstRequests();
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Burst)) burst.Update(world.Unmanaged);
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SpeedCurve)) speed.Update(world.Unmanaged);
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SizeCurve)) scale.Update(world.Unmanaged);
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Movement)) movement.Update(world.Unmanaged);
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Lifetime)) lifetime.Update(world.Unmanaged);
        spawner.UpdateLasers(time, gameplayCamera);
        spawner.UpdateChildAttacks(time, delta, target, WavePreviewProfiler.EffectiveProjectileLimit(MaximumProjectiles), gameplayCamera);
        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Cleanup))
        using (var entities = projectiles.ToEntityArray(Allocator.Temp))
        using (var resolutions = projectiles.ToComponentDataArray<EnemyProjectileResolution>(Allocator.Temp))
        using (var expired = new NativeList<Entity>(Allocator.Temp))
        {
            for (int i = 0; i < entities.Length; i++)
                if (resolutions[i].Kind != EnemyProjectileResolutionKind.None)
                    expired.Add(entities[i]);
            if (expired.Length > 0) world.EntityManager.DestroyEntity(expired.AsArray());
        }
        using var burstSpawnProfile = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.BurstSpawn);
        var pending = world.EntityManager.GetBuffer<EnemyProjectileBurstSpawnRequest>(registry);
        int count = Mathf.Min(pending.Length, Mathf.Max(0, WavePreviewProfiler.EffectiveProjectileLimit(MaximumProjectiles) - Count));
        // Spawning can invalidate dynamic buffer handles. Copy before making structural changes.
        using var burstRequests = pending.ToNativeArray(Allocator.Temp);
        Omitted += pending.Length - count;
        pending.Clear();
        for (int i = 0; i < count; i++) spawner.TrySpawnEditorPreviewBurst(burstRequests[i]);
    }

    public void CaptureRenderState()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.CaptureRender);
        for (int i = 0; i < batchList.Count; i++) batchList[i].Count = 0;
        var manager = world.EntityManager;
        var poseHandle = manager.GetComponentTypeHandle<LocalTransform>(true);
        var postHandle = manager.GetComponentTypeHandle<PostTransformMatrix>(true);
        var infoHandle = manager.GetComponentTypeHandle<MaterialMeshInfo>(true);
        var colourHandle = manager.GetComponentTypeHandle<URPMaterialPropertyBaseColor>(true);
        var renderHandle = manager.GetSharedComponentTypeHandle<RenderMeshArray>();
        using var chunks = renderProjectiles.ToArchetypeChunkArray(Allocator.Temp);
        for (int i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            int sharedIndex = chunk.GetSharedComponentIndex(renderHandle);
            var render = manager.GetSharedComponentManaged<RenderMeshArray>(sharedIndex);
            var infos = chunk.GetNativeArray(ref infoHandle);
            var colours = chunk.GetNativeArray(ref colourHandle);
            var poses = chunk.GetNativeArray(ref poseHandle);
            var postTransforms = chunk.GetNativeArray(ref postHandle);
            for (int j = 0; j < chunk.Count; j++)
            {
                Mesh mesh = render.GetMesh(infos[j]);
                Material source = render.GetMaterial(infos[j]);
                if (mesh == null || source == null) continue;
                float4 value = colours[j].Value;
                var key = (mesh, source, value);
                if (!batches.TryGetValue(key, out var batch))
                {
                    var material = new Material(source) { enableInstancing = true };
                    var colour = EnemyProjectileCharacteristics.FromRenderColour(value);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
                    batch = new RenderBatch { Mesh = mesh, Material = material };
                    batches.Add(key, batch); batchList.Add(batch);
                }
                batch.Matrices[batch.Count++] = math.mul(poses[j].ToMatrix(), postTransforms[j].Value);
            }
        }
    }

    public void Draw(Camera camera)
    {
        spawner.DrawLaserPreview(camera);
        for (int i = 0; i < batchList.Count; i++)
        {
            var batch = batchList[i];
            for (int offset = 0; offset < batch.Count; offset += drawMatrices.Length)
            {
                int count = Mathf.Min(drawMatrices.Length, batch.Count - offset);
                Array.Copy(batch.Matrices, offset, drawMatrices, 0, count);
                Graphics.DrawMeshInstanced(batch.Mesh, 0, batch.Material, drawMatrices, count, null,
                    ShadowCastingMode.Off, false, 0, camera, LightProbeUsage.Off);
            }
        }
    }

    private void BoundBurstRequests()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.BurstBounds);
        using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileBurstAtPoint));
        using var entities = query.ToEntityArray(Allocator.Temp);
        int budget = 512;
        for (int i = 0; i < entities.Length; i++)
        {
            var value = world.EntityManager.GetComponentData<EnemyProjectileBurstAtPoint>(entities[i]);
            int allowed = Mathf.Min(value.ProjectilesPerWave, budget);
            budget -= allowed;
            if (allowed == value.ProjectilesPerWave) continue;
            Omitted += value.ProjectilesPerWave - allowed;
            value.ProjectilesPerWave = allowed;
            world.EntityManager.SetComponentData(entities[i], value);
        }
    }

    public void Dispose()
    {
        if (!world.IsCreated) return;
        RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
        Camera.onPreCull -= BeforeBuiltinCamera;
        previewCameras.Clear();
        projectiles.Dispose();
        renderProjectiles.Dispose();
        // Spawner destroys entities/templates before freeing the meshes and materials they refer to.
        spawner.Dispose();
        world.Dispose();
        for (int i = 0; i < batchList.Count; i++)
            UnityEngine.Object.DestroyImmediate(batchList[i].Material);
        batches.Clear(); batchList.Clear();
    }
}
