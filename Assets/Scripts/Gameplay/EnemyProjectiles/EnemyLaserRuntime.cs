using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class EnemyLaserSimulation : IComponentData { public EnemyLaserRuntime Runtime; }

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileLifetimeSystem))]
[UpdateBefore(typeof(EnemyChildAttackSystem))]
[UpdateBefore(typeof(ProjectileCleanupSystem))]
public partial class EnemyLaserSystem : SystemBase
{
    private readonly List<EnemyLaserRuntime> runtimes = new();
    protected override void OnUpdate()
    {
        runtimes.Clear();
        foreach (var owner in SystemAPI.Query<EnemyLaserSimulation>())
            runtimes.Add(owner.Runtime);
        // Tick can create/destroy reflected visual entities. Finish the owner query
        // first: structural changes are forbidden while its enumerator is alive.
        float time = (float)SystemAPI.Time.ElapsedTime;
        for (int i = 0; i < runtimes.Count; i++) runtimes[i].Tick(time);
        runtimes.Clear();
    }
}

// This is projectile lifetime/presentation, not an attack scheduler. Every beam arrives through the
// existing volley builder/spawner, including delayed Sector emissions and nested child attacks.
public sealed class EnemyLaserRuntime : IDisposable
{
    private readonly EntityManager manager;
    private readonly World world;
    private bool disposed;
    private readonly EntityQuery registries;
    private readonly List<Beam> beams = new();
    private readonly Dictionary<(Camera, float), Rect> boundsCache = new();
    private float boundsTime = float.NaN;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MaterialPropertyBlock properties = new();
    private readonly Entity template;
    private readonly Entity segmentTemplate;
    private sealed class Beam
    {
        public Entity Entity;
        public EnemyLaserSettings Settings;
        public EnemyLaserOrigin Origin;
        public EnemyProjectileScreenSettings Screen;
        public Vector3 Position, Translation, Direction;
        public float BaseSize, Birth, ActiveBaseLength, TelegraphBaseLength, NextDamage;
        public bool Activated, WarningStarted;
        public Color Colour, RenderColour;
        public readonly List<EnemyLaserState> Segments = new();
        public readonly List<Entity> VisualSegments = new();
        public readonly HashSet<int> Contacts = new();
        public readonly HashSet<int> CurrentContacts = new();
    }
    public Entity Template => template;
    public int Count => beams.Count;

    public EnemyLaserRuntime(EntityManager manager)
    {
        this.manager = manager;
        world = manager.World;
        registries = manager.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag), typeof(EnemyProjectileCollisionTarget));
        mesh = new Mesh { name = "Enemy Laser Shared Quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] { new Vector3(-.5f, 0f), new Vector3(.5f, 0f), new Vector3(.5f, 1f), new Vector3(-.5f, 1f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("Enemy Laser requires the project's URP Unlit shader.");
        material = new Material(shader) { name = "Enemy Laser Shared Material", enableInstancing = true,
            hideFlags = HideFlags.HideAndDontSave, renderQueue = (int)RenderQueue.Overlay + 99 };
        material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0); material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetColor("_BaseColor", Color.white);
        template = manager.CreateEntity(typeof(EnemyLaserState), typeof(EnemyProjectileStaticData),
            typeof(EnemyProjectileVisualStaticData), typeof(EnemyProjectileMovement), typeof(EnemyProjectileVelocity),
            typeof(EnemyProjectilePreviousPosition), typeof(EnemyProjectileResolution), typeof(EnemyProjectileDamage),
            typeof(EnemyProjectileRemainingLifetime), typeof(LocalTransform), typeof(PostTransformMatrix),
            typeof(EnemyChildAttackLifetimeHold), typeof(EnemyChildAttackLineage), typeof(EnemyProjectileScreenState),
            typeof(EnemyChildAttackScreenHold), typeof(Prefab));
        manager.SetComponentEnabled<EnemyChildAttackLifetimeHold>(template, false);
        manager.SetComponentEnabled<EnemyChildAttackScreenHold>(template, false);
        manager.SetComponentEnabled<EnemyChildAttackLineage>(template, false);
        manager.SetComponentData(template, LocalTransform.Identity);
        manager.AddComponentData(template,new EnemyProjectileTargetPosition());manager.SetComponentEnabled<EnemyProjectileTargetPosition>(template,false);
        manager.AddComponent<EnemyProjectileHiddenAnchor>(template);manager.SetComponentEnabled<EnemyProjectileHiddenAnchor>(template,false);
        manager.AddComponentData(template, new EnemyProjectileCollisionState());
        manager.SetComponentData(template, new PostTransformMatrix { Value = float4x4.identity });
        RenderMeshUtility.AddComponents(template, manager, new RenderMeshDescription(ShadowCastingMode.Off, false),
            new RenderMeshArray(new[] { material }, new[] { mesh }), MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        manager.AddComponentData(template, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
        // Reflected legs are presentation only: no projectile lifetime, collision or child scheduler.
        segmentTemplate = manager.CreateEntity(typeof(LocalTransform), typeof(PostTransformMatrix), typeof(Prefab));
        RenderMeshUtility.AddComponents(segmentTemplate, manager, new RenderMeshDescription(ShadowCastingMode.Off, false),
            new RenderMeshArray(new[] { material }, new[] { mesh }), MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        manager.AddComponentData(segmentTemplate, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
    }

    public void Register(Entity entity, EnemyProjectileSpawnRequest request, float time)
    {
        if (boundsTime != time) { boundsCache.Clear(); boundsTime = time; }
        Vector3 direction = request.Direction; direction.z = 0f;
        direction = direction.sqrMagnitude > .00000001f ? direction.normalized : Vector3.down;
        Color colour = request.OverwritesBaseColour ? request.ProjectileColour : Color.white;
        EnemyProjectileCharacteristic characteristic = EnemyProjectileCharacteristic.Normal;
        if (request.LegacyPrefab != null && request.LegacyPrefab.TryGetComponent<EnemyProjectileAuthoring>(out var authoring))
        {
            characteristic = authoring.Characteristic;
            if (!request.OverwritesBaseColour && authoring.VisualRenderer != null) colour = authoring.VisualRenderer.color;
        }
        if (request.OverwritesCharacteristic) characteristic = request.Characteristic;
        if (characteristic == EnemyProjectileCharacteristic.Fragile) colour = EnemyProjectileCharacteristics.FragileColour;
        manager.SetComponentData(entity, new EnemyProjectileStaticData { Characteristic = characteristic,
            BaseDamage = (request.OverridesDamage ? request.Damage : request.Laser.Damage * request.DamageMultiplier), BaseLifetime = request.Laser.TotalDuration });
        manager.SetComponentData(entity, new EnemyProjectileDamage { Value = (request.OverridesDamage ? request.Damage : request.Laser.Damage * request.DamageMultiplier) });
        manager.SetComponentData(entity, new EnemyProjectileMovement { ForwardDirection = new float2(direction.x, direction.y) });
        var beam = new Beam { Entity = entity, Settings = request.Laser, Origin = request.LaserOrigin, Screen = request.ScreenBoundary,
            BaseSize = request.BaseSize, Position = request.Position, Direction = direction, Birth = time, Colour = colour };
        beams.Add(beam);
        UpdateGeometry(beam, time, GetBounds(beam));
    }

    public void Tick(float time, Camera previewCamera = null)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Laser);
#endif
        boundsCache.Clear();
        boundsTime = time;
        Entity registry = registries.IsEmptyIgnoreFilter ? Entity.Null : registries.GetSingletonEntity();
        for (int i = beams.Count - 1; i >= 0; i--)
        {
            var beam = beams[i];
            if (!manager.Exists(beam.Entity)) { RemoveBeam(i); continue; }
            if (manager.GetComponentData<EnemyProjectileResolution>(beam.Entity).Kind != EnemyProjectileResolutionKind.None)
            { RemoveBeam(i); continue; }
            bool alive = beam.Origin.IsAlive(manager);
            if (!alive && (beam.Origin.CancelWithSource || !beam.Settings.SurviveSourceDestruction))
            {
                var state = manager.GetComponentData<EnemyLaserState>(beam.Entity); state.Cancelled = 1;
                state.Active = state.Telegraph = 0; state.Length = state.Width = 0;
                manager.SetComponentData(beam.Entity, state);
                SetVisual(beam.Entity, state, manager.GetComponentData<LocalTransform>(beam.Entity).Position.z, Color.clear);
                manager.SetComponentData(beam.Entity, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
                RemoveBeam(i); continue;
            }
            if(EnemyProjectileTargetPositionUtility.IsHidden(manager,beam.Entity))
            { beam.RenderColour=Color.clear; HideVisualSegments(beam); continue; }
            Rect bounds = GetBounds(beam, previewCamera);
            if (alive && beam.Settings.FollowOrigin) beam.Translation = beam.Origin.Translation(manager, bounds);
            UpdateGeometry(beam, time, bounds);
            if (registry != Entity.Null) Collide(beam, registry, time);
            if (manager.GetComponentData<EnemyProjectileResolution>(beam.Entity).Kind != EnemyProjectileResolutionKind.None)
            { RemoveBeam(i); continue; }
            float remaining = beam.Settings.TotalDuration - Mathf.Max(0f, time - beam.Birth);
            manager.SetComponentData(beam.Entity, new EnemyProjectileRemainingLifetime { Value = remaining });
            if (remaining > .000001f || manager.IsComponentEnabled<EnemyChildAttackLifetimeHold>(beam.Entity)) continue;
            manager.SetComponentData(beam.Entity, new EnemyProjectileResolution
                { Kind = EnemyProjectileResolutionKind.Expired, LifetimeExpired = 1 });
            RemoveBeam(i);
        }
    }
    private Rect GetBounds(Beam beam, Camera camera = null)
    {
        camera = camera != null ? camera : beam.Origin.Camera != null ? beam.Origin.Camera : Camera.main;
        var key = (camera, beam.Position.z);
        if (boundsCache.TryGetValue(key, out var bounds)) return bounds;
        EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, beam.Position.z, out bounds);
        boundsCache[key] = bounds;
        return bounds;
    }
    private void UpdateGeometry(Beam beam, float time, Rect bounds)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.LaserGeometry);
#endif
        float age = Mathf.Max(0f, time - beam.Birth);
        var settings = beam.Settings;
        bool warning = age >= settings.ActivationDelay && age < settings.ActiveStart;
        bool active = age >= settings.ActiveStart && age < settings.ActiveStart + settings.LaserDuration;
        var geometry = warning ? settings.TelegraphGeometry : settings.Geometry;
        Vector3 start = beam.Position + beam.Translation + (Vector3)geometry.LaserOffset;
        if (warning && !beam.WarningStarted)
        { beam.WarningStarted = true; beam.TelegraphBaseLength = geometry.ResolveLength(start, beam.Direction, bounds); }
        if (age >= settings.ActiveStart && !beam.Activated)
        { beam.Activated = true; beam.ActiveBaseLength = geometry.ResolveLength(start, beam.Direction, bounds); }
        float normalized = warning ? (age - settings.ActivationDelay) / Mathf.Max(.0001f, settings.TelegraphDuration)
            : (age - settings.ActiveStart) / settings.LaserDuration;
        float baseLength = geometry.ConstantLength ? warning ? beam.TelegraphBaseLength : beam.ActiveBaseLength
            : geometry.ResolveLength(start, beam.Direction, bounds);
        float length = geometry.LengthAt(baseLength, normalized) * beam.BaseSize, width = geometry.WidthAt(normalized) * beam.BaseSize;
        if (beam.BaseSize == 0f)
            manager.SetComponentData(beam.Entity, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
        float alpha;
        Color colour;
        if (warning)
        {
            float elapsed = age - settings.ActivationDelay;
            alpha = Fade(elapsed, settings.TelegraphFadeIn) * Fade(settings.TelegraphDuration - elapsed, settings.TelegraphFadeOut);
            colour = settings.TelegraphColor; colour.a *= settings.TelegraphOpacity;
        }
        else
        {
            float elapsed = age - settings.ActiveStart;
            alpha = age < settings.ActiveStart ? 0f : Fade(elapsed, settings.FadeInDuration)
                * (elapsed <= settings.LaserDuration ? 1f : Fade(settings.LaserDuration + settings.FadeOutDuration - elapsed, settings.FadeOutDuration));
            colour = beam.Colour;
        }
        colour.a *= alpha; beam.RenderColour = colour;
        EnemyLaserGeometryUtility.BuildPath(start, beam.Direction, length, width, bounds, beam.Screen, beam.Segments);
        var first = beam.Segments[0];
        var previous = manager.GetComponentData<LocalTransform>(beam.Entity).Position;
        manager.SetComponentData(beam.Entity, new EnemyProjectilePreviousPosition { Value = previous.xy });
        var state = manager.GetComponentData<EnemyLaserState>(beam.Entity);
        state.Start = first.Start; state.Direction = first.Direction; state.Length = first.Length; state.Width = width;
        state.Active = active ? (byte)1 : (byte)0; state.Telegraph = warning ? (byte)1 : (byte)0;
        manager.SetComponentData(beam.Entity, state);
        SetVisual(beam.Entity, first, start.z, colour);
        int extra = beam.Segments.Count - 1;
        while (beam.VisualSegments.Count < extra) beam.VisualSegments.Add(manager.Instantiate(segmentTemplate));
        // Keep spare entities invisible for animated paths. Reuse them on the next frame.
        for (int i = 0; i < beam.VisualSegments.Count; i++)
        {
            if (i < extra) SetVisual(beam.VisualSegments[i], beam.Segments[i + 1], start.z, colour);
            else manager.SetComponentData(beam.VisualSegments[i], new PostTransformMatrix { Value = float4x4.Scale(float3.zero) });
        }
    }

    private void SetVisual(Entity entity, EnemyLaserState segment, float z, Color colour)
    {
        var rotation = quaternion.RotateZ(math.atan2(segment.Direction.y, segment.Direction.x) - math.PI * .5f);
        manager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(new float3(segment.Start, z), rotation, 1f));
        manager.SetComponentData(entity, new PostTransformMatrix { Value = float4x4.Scale(new float3(segment.Width, segment.Length, 1f)) });
        manager.SetComponentData(entity, new URPMaterialPropertyBaseColor { Value = EnemyProjectileCharacteristics.ToRenderColour(colour) });
    }

    private void HideVisualSegments(Beam beam)
    {
        for (int i = 0; i < beam.VisualSegments.Count; i++)
            if (manager.Exists(beam.VisualSegments[i]))
                manager.SetComponentData(beam.VisualSegments[i], new PostTransformMatrix { Value = float4x4.Scale(float3.zero) });
    }

    private void RemoveBeam(int index)
    {
        var beam = beams[index];
        for (int i = 0; i < beam.VisualSegments.Count; i++)
            if (manager.Exists(beam.VisualSegments[i])) manager.DestroyEntity(beam.VisualSegments[i]);
        beams.RemoveAt(index);
    }
    private static float Fade(float elapsed, float duration) => duration <= 0f ? elapsed >= 0f ? 1f : 0f : Mathf.Clamp01(elapsed / duration);

    private void Collide(Beam beam, Entity registry, float time)
    {
        bool collisionDisabled = manager.GetComponentData<EnemyProjectileCollisionState>(beam.Entity).Disabled != 0;
        var state = manager.GetComponentData<EnemyLaserState>(beam.Entity);
        if (state.Active == 0) { beam.Contacts.Clear(); return; }
        var targets = manager.GetBuffer<EnemyProjectileCollisionTarget>(registry, true);
        var characteristic = manager.GetComponentData<EnemyProjectileStaticData>(beam.Entity).Characteristic;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (collisionDisabled || target.Kind != EnemyProjectileCollisionTargetKind.Interceptor
                || !EnemyProjectileCharacteristics.CanBeDestroyed(characteristic, target.DestroysNormal != 0)
                || !EnemyLaserGeometryUtility.Hits(beam.Segments, target)) continue;
            manager.SetComponentData(beam.Entity, new EnemyProjectileResolution
                { Kind = EnemyProjectileResolutionKind.Intercepted, TargetId = target.TargetId });
            return;
        }
        if (manager.HasBuffer<EnemyProjectilePurgeField>(registry))
        {
            var fields = manager.GetBuffer<EnemyProjectilePurgeField>(registry, true);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field.Radius <= 0f || field.SystemCleanup == 0 && !EnemyProjectileCharacteristics.CanBeDestroyed(characteristic,
                    field.DestructionPolicy == EnemyProjectileDestructionPolicy.NormalAndFragile)) continue;
                var target = new EnemyProjectileCollisionTarget { IsCircle = 1, Center = field.Center,
                    PreviousCenter = field.Center, Radius = field.Radius };
                if (!EnemyLaserGeometryUtility.Hits(beam.Segments, target)) continue;
                manager.SetComponentData(beam.Entity, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
                return;
            }
        }
        if (collisionDisabled) return;
        beam.CurrentContacts.Clear();
        bool periodicDue = time + .000001f >= beam.NextDamage;
        bool damaged = false;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target.Kind != EnemyProjectileCollisionTargetKind.PlayerShip || !EnemyLaserGeometryUtility.Hits(beam.Segments, target)) continue;
            if (!beam.CurrentContacts.Add(target.TargetId)) continue;
            bool hit = characteristic != EnemyProjectileCharacteristic.Indestructible
                && beam.Settings.DamageMode == EnemyLaserDamageMode.Periodic ? periodicDue : !beam.Contacts.Contains(target.TargetId);
            if (!hit) continue;
            state.HitPlayer = 1;
            if (manager.HasBuffer<EnemyProjectileResolutionEvent>(registry))
                manager.GetBuffer<EnemyProjectileResolutionEvent>(registry).Add(new EnemyProjectileResolutionEvent
                { TargetId = target.TargetId, Kind = EnemyProjectileResolutionKind.HitPlayer,
                    Damage = manager.GetComponentData<EnemyProjectileDamage>(beam.Entity).Value, UseConfiguredDamage = 1 });
            damaged = true;
        }
        beam.Contacts.Clear();
        foreach (int id in beam.CurrentContacts) beam.Contacts.Add(id);
        if (damaged) beam.NextDamage = time + beam.Settings.DamageInterval;
        manager.SetComponentData(beam.Entity, state);
    }

    // Editor preview uses one shared material/property block; animated alpha cannot grow a material cache.
    public void DrawPreview(Camera camera)
    {
        for (int i = 0; i < beams.Count; i++)
        {
            var beam = beams[i];
            if (beam.RenderColour.a <= 0f || !manager.Exists(beam.Entity) || EnemyProjectileTargetPositionUtility.IsHidden(manager,beam.Entity)) continue;
            properties.SetColor("_BaseColor", beam.RenderColour);
            float z = manager.GetComponentData<LocalTransform>(beam.Entity).Position.z;
            for (int j = 0; j < beam.Segments.Count; j++)
            {
                var segment = beam.Segments[j];
                var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(segment.Direction.y, segment.Direction.x) * Mathf.Rad2Deg - 90f);
                var matrix = Matrix4x4.TRS(new Vector3(segment.Start.x, segment.Start.y, z), rotation,
                    new Vector3(segment.Width, segment.Length, 1f));
                Graphics.DrawMesh(mesh, matrix, material, 0, camera, 0, properties, ShadowCastingMode.Off, false);
            }
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (world.IsCreated)
        {
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                if (manager.Exists(beams[i].Entity)) manager.DestroyEntity(beams[i].Entity);
                RemoveBeam(i);
            }
            registries.Dispose();
            if (manager.Exists(template)) manager.DestroyEntity(template);
            if (manager.Exists(segmentTemplate)) manager.DestroyEntity(segmentTemplate);
        }
        beams.Clear();
        Destroy(mesh); Destroy(material);
    }
    private static void Destroy(UnityEngine.Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(obj); return; }
#endif
        UnityEngine.Object.Destroy(obj);
    }
}
