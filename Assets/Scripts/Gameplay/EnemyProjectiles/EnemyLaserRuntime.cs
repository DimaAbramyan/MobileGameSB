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
    protected override void OnUpdate()
    {
        foreach (var owner in SystemAPI.Query<EnemyLaserSimulation>())
            owner.Runtime.Tick((float)SystemAPI.Time.ElapsedTime);
    }
}

// This is projectile lifetime/presentation, not an attack scheduler. Every beam arrives through the
// existing volley builder/spawner, including delayed Sector emissions and nested child attacks.
public sealed class EnemyLaserRuntime : IDisposable
{
    private readonly EntityManager manager;
    private readonly EntityQuery registries;
    private readonly List<Beam> beams = new();
    private readonly Dictionary<(Camera, float), Rect> boundsCache = new();
    private float boundsTime = float.NaN;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MaterialPropertyBlock properties = new();
    private readonly Entity template;
    private sealed class Beam
    {
        public Entity Entity;
        public EnemyLaserSettings Settings;
        public EnemyLaserOrigin Origin;
        public Vector3 Position, Translation, Direction;
        public float Birth, ActiveBaseLength, TelegraphBaseLength, NextDamage;
        public bool Activated, WarningStarted;
        public Color Colour, RenderColour;
        public Matrix4x4 Matrix;
        public readonly HashSet<int> Contacts = new();
        public readonly HashSet<int> CurrentContacts = new();
    }
    public Entity Template => template;
    public int Count => beams.Count;

    public EnemyLaserRuntime(EntityManager manager)
    {
        this.manager = manager;
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
            typeof(EnemyChildAttackLifetimeHold), typeof(EnemyChildAttackLineage), typeof(Prefab));
        manager.SetComponentEnabled<EnemyChildAttackLifetimeHold>(template, false);
        manager.SetComponentEnabled<EnemyChildAttackLineage>(template, false);
        manager.SetComponentData(template, LocalTransform.Identity);
        manager.SetComponentData(template, new PostTransformMatrix { Value = float4x4.identity });
        RenderMeshUtility.AddComponents(template, manager, new RenderMeshDescription(ShadowCastingMode.Off, false),
            new RenderMeshArray(new[] { material }, new[] { mesh }), MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        manager.AddComponentData(template, new URPMaterialPropertyBaseColor { Value = new float4(1f) });
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
            BaseDamage = request.Laser.Damage * request.DamageMultiplier, BaseLifetime = request.Laser.TotalDuration });
        manager.SetComponentData(entity, new EnemyProjectileDamage { Value = request.Laser.Damage * request.DamageMultiplier });
        manager.SetComponentData(entity, new EnemyProjectileMovement { ForwardDirection = new float2(direction.x, direction.y) });
        var beam = new Beam { Entity = entity, Settings = request.Laser, Origin = request.LaserOrigin,
            Position = request.Position, Direction = direction, Birth = time, Colour = colour };
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
            if (!manager.Exists(beam.Entity)) { beams.RemoveAt(i); continue; }
            if (manager.GetComponentData<EnemyProjectileResolution>(beam.Entity).Kind != EnemyProjectileResolutionKind.None)
            { beams.RemoveAt(i); continue; }
            bool alive = beam.Origin.IsAlive(manager);
            if (!alive && !beam.Settings.SurviveSourceDestruction)
            {
                var state = manager.GetComponentData<EnemyLaserState>(beam.Entity); state.Cancelled = 1;
                manager.SetComponentData(beam.Entity, state);
                manager.SetComponentData(beam.Entity, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
                beams.RemoveAt(i); continue;
            }
            Rect bounds = GetBounds(beam, previewCamera);
            if (alive && beam.Settings.FollowOrigin) beam.Translation = beam.Origin.Translation(manager, bounds);
            UpdateGeometry(beam, time, bounds);
            if (registry != Entity.Null) Collide(beam, registry, time);
            float remaining = beam.Settings.TotalDuration - Mathf.Max(0f, time - beam.Birth);
            manager.SetComponentData(beam.Entity, new EnemyProjectileRemainingLifetime { Value = remaining });
            if (remaining > .000001f || manager.IsComponentEnabled<EnemyChildAttackLifetimeHold>(beam.Entity)) continue;
            manager.SetComponentData(beam.Entity, new EnemyProjectileResolution
                { Kind = EnemyProjectileResolutionKind.Expired, LifetimeExpired = 1 });
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
        float length = geometry.LengthAt(baseLength, normalized), width = geometry.WidthAt(normalized);
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
        float2 origin = new(start.x, start.y), direction = new(beam.Direction.x, beam.Direction.y);
        var previous = manager.GetComponentData<LocalTransform>(beam.Entity).Position;
        manager.SetComponentData(beam.Entity, new EnemyProjectilePreviousPosition { Value = previous.xy });
        var state = manager.GetComponentData<EnemyLaserState>(beam.Entity);
        state.Start = origin; state.Direction = direction; state.Length = length; state.Width = width;
        state.Active = active ? (byte)1 : (byte)0; state.Telegraph = warning ? (byte)1 : (byte)0;
        manager.SetComponentData(beam.Entity, state);
        var rotation = quaternion.RotateZ(math.atan2(direction.y, direction.x) - math.PI * .5f);
        manager.SetComponentData(beam.Entity, LocalTransform.FromPositionRotationScale(start, rotation, 1f));
        var scale = float4x4.Scale(new float3(width, length, 1f));
        manager.SetComponentData(beam.Entity, new PostTransformMatrix { Value = scale });
        manager.SetComponentData(beam.Entity, new URPMaterialPropertyBaseColor { Value = EnemyProjectileCharacteristics.ToRenderColour(colour) });
        beam.Matrix = math.mul(float4x4.TRS(start, rotation, new float3(1f)), scale);
    }
    private static float Fade(float elapsed, float duration) => duration <= 0f ? elapsed >= 0f ? 1f : 0f : Mathf.Clamp01(elapsed / duration);

    private void Collide(Beam beam, Entity registry, float time)
    {
        var state = manager.GetComponentData<EnemyLaserState>(beam.Entity);
        if (state.Active == 0) { beam.Contacts.Clear(); return; }
        var targets = manager.GetBuffer<EnemyProjectileCollisionTarget>(registry, true);
        var characteristic = manager.GetComponentData<EnemyProjectileStaticData>(beam.Entity).Characteristic;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target.Kind != EnemyProjectileCollisionTargetKind.Interceptor
                || !EnemyProjectileCharacteristics.CanBeDestroyed(characteristic, target.DestroysNormal != 0)
                || !EnemyLaserGeometryUtility.Hits(state, target)) continue;
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
                if (field.Radius <= 0f || !EnemyProjectileCharacteristics.CanBeDestroyed(characteristic,
                    field.DestructionPolicy == EnemyProjectileDestructionPolicy.NormalAndFragile)) continue;
                var target = new EnemyProjectileCollisionTarget { IsCircle = 1, Center = field.Center,
                    PreviousCenter = field.Center, Radius = field.Radius };
                if (!EnemyLaserGeometryUtility.Hits(state, target)) continue;
                manager.SetComponentData(beam.Entity, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
                return;
            }
        }
        beam.CurrentContacts.Clear();
        bool periodicDue = time + .000001f >= beam.NextDamage;
        bool damaged = false;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target.Kind != EnemyProjectileCollisionTargetKind.PlayerShip || !EnemyLaserGeometryUtility.Hits(state, target)) continue;
            if (!beam.CurrentContacts.Add(target.TargetId)) continue;
            bool hit = beam.Settings.DamageMode == EnemyLaserDamageMode.Periodic ? periodicDue : !beam.Contacts.Contains(target.TargetId);
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
            if (beam.RenderColour.a <= 0f || !manager.Exists(beam.Entity)) continue;
            properties.SetColor("_BaseColor", beam.RenderColour);
            Graphics.DrawMesh(mesh, beam.Matrix, material, 0, camera, 0, properties, ShadowCastingMode.Off, false);
        }
    }
    public void Dispose()
    {
        for (int i = 0; i < beams.Count; i++) if (manager.Exists(beams[i].Entity)) manager.DestroyEntity(beams[i].Entity);
        beams.Clear(); registries.Dispose();
        if (manager.Exists(template)) manager.DestroyEntity(template);
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
