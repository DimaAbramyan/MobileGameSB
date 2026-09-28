using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates the first ECS enemy-projectile slice from existing enemy attack behaviours.
/// Mesh and material are created once per projectile configuration and reused by all shots.
/// </summary>
public sealed class EnemyProjectileEcsSpawner : IDisposable
{
    private const int ProjectileRenderQueue = 3100;

    private readonly Dictionary<EnemyProjectileAuthoring, RuntimeVisual>
        visualsByConfiguration = new();
    private readonly HashSet<EnemyProjectileAuthoring> invalidConfigurationsLogged = new();

    private EntityManager entityManager;
    private World entityWorld;

    public bool TrySpawn(
        EnemyBullet legacyPrefab,
        Vector3 position,
        Vector3 direction,
        float damageMultiplier)
    {
        if (!TryGetConfiguration(legacyPrefab, out EnemyProjectileAuthoring configuration)
            || !EnsureEntityManager()
            || !TryGetVisual(configuration, out RuntimeVisual visual))
            return false;

        float2 normalizedDirection = new float2(direction.x, direction.y);
        if (math.lengthsq(normalizedDirection) < 0.0001f)
            normalizedDirection = new float2(0f, -1f);
        else
            normalizedDirection = math.normalize(normalizedDirection);

        float effectiveDamage = configuration.BaseDamage
            * Mathf.Max(0.01f, damageMultiplier);
        float effectiveLifetime = Mathf.Max(0.01f, configuration.BaseLifetime);

        Entity entity = entityManager.CreateEntity();
        entityManager.AddComponentData(entity, new EnemyProjectileStaticData
        {
              BaseDamage = configuration.BaseDamage,
              Radius = configuration.EffectiveRadius,
            BaseLifetime = configuration.BaseLifetime,
            BaseSpeed = configuration.BaseSpeed
        });
        entityManager.AddComponentData(entity, new EnemyProjectileVisualStaticData
        {
            Size = visual.Size
        });
        entityManager.AddComponentData(entity, new EnemyProjectileVelocity
        {
            Value = normalizedDirection * configuration.BaseSpeed
        });
        entityManager.AddComponentData(entity, new EnemyProjectileRemainingLifetime
        {
            Value = effectiveLifetime
        });
        entityManager.AddComponentData(entity, new EnemyProjectileDamage
        {
            Value = effectiveDamage
        });
        entityManager.AddComponentData(entity, new EnemyProjectilePreviousPosition
        {
            Value = new float2(position.x, position.y)
        });
        entityManager.AddComponentData(entity, new EnemyProjectileResolution
        {
            Kind = EnemyProjectileResolutionKind.None,
            TargetId = 0
        });
          entityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
              position,
              GetRotation(normalizedDirection),
              1f));
          entityManager.AddComponentData(entity, new PostTransformMatrix
          {
              Value = float4x4.Scale(new float3(
                  configuration.VisualScale.x,
                  configuration.VisualScale.y,
                  1f))
          });

        RenderMeshUtility.AddComponents(
            entity,
            entityManager,
            visual.Description,
            visual.RenderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        return true;
    }

    public void Dispose()
    {
        foreach (RuntimeVisual visual in visualsByConfiguration.Values)
        {
            if (visual.Material != null)
                UnityEngine.Object.Destroy(visual.Material);

            if (visual.Mesh != null)
                UnityEngine.Object.Destroy(visual.Mesh);
        }

        visualsByConfiguration.Clear();
        invalidConfigurationsLogged.Clear();
        entityWorld = null;
    }

    private bool TryGetConfiguration(
        EnemyBullet legacyPrefab,
        out EnemyProjectileAuthoring configuration)
    {
        configuration = legacyPrefab != null
            ? legacyPrefab.GetComponent<EnemyProjectileAuthoring>()
            : null;
        if (configuration != null && configuration.IsConfigured)
            return true;

        if (configuration != null && invalidConfigurationsLogged.Add(configuration))
        {
            Debug.LogError(
                $"{nameof(EnemyProjectileAuthoring)} on '{configuration.name}' is incomplete. "
                + "Enemy projectile was not spawned.",
                configuration);
        }
        else if (configuration == null)
        {
            Debug.LogError(
                $"Enemy projectile prefab '{legacyPrefab?.name ?? "missing"}' needs "
                + $"{nameof(EnemyProjectileAuthoring)}. Enemy projectile was not spawned.");
        }

        return false;
    }

    private bool EnsureEntityManager()
    {
        if (entityWorld != null && entityWorld.IsCreated)
            return true;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError(
                $"{nameof(EnemyProjectileEcsSpawner)} needs an active default ECS World.");
            return false;
        }

        entityWorld = world;
        entityManager = world.EntityManager;
        return true;
    }

    private bool TryGetVisual(
        EnemyProjectileAuthoring configuration,
        out RuntimeVisual visual)
    {
        if (visualsByConfiguration.TryGetValue(configuration, out visual))
            return true;

        SpriteRenderer renderer = configuration.VisualRenderer;
        Sprite sprite = renderer != null ? renderer.sprite : null;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (sprite == null || shader == null)
        {
            LogIncompleteVisual(configuration, sprite == null
                ? "SpriteRenderer does not have a Sprite."
                : "URP Unlit shader was not found.");
            return false;
        }

        Mesh mesh = CreateSpriteMesh(sprite, configuration.name);
        Material material = CreateSpriteMaterial(shader, sprite, renderer.color,
            configuration.name);
        RenderMeshArray renderMeshArray = new(
            new[] { material },
            new[] { mesh });
        visual = new RuntimeVisual(
            mesh,
            material,
            renderMeshArray,
            new RenderMeshDescription(ShadowCastingMode.Off, false),
            sprite.bounds.size);
        visualsByConfiguration.Add(configuration, visual);
        return true;
    }

    private void LogIncompleteVisual(
        EnemyProjectileAuthoring configuration,
        string reason)
    {
        if (!invalidConfigurationsLogged.Add(configuration))
            return;

        Debug.LogError(
            $"{nameof(EnemyProjectileAuthoring)} on '{configuration.name}' cannot "
            + $"prepare ECS rendering: {reason}",
            configuration);
    }

    private static Mesh CreateSpriteMesh(Sprite sprite, string configurationName)
    {
        Vector2[] sourceVertices = sprite.vertices;
        Vector3[] vertices = new Vector3[sourceVertices.Length];
        for (int index = 0; index < sourceVertices.Length; index++)
        {
            Vector2 source = sourceVertices[index];
            vertices[index] = new Vector3(source.x, source.y, 0f);
        }

        ushort[] sourceTriangles = sprite.triangles;
        int[] triangles = new int[sourceTriangles.Length];
        for (int index = 0; index < sourceTriangles.Length; index++)
            triangles[index] = sourceTriangles[index];

        var mesh = new Mesh
        {
            name = $"{configurationName} ECS Projectile Mesh"
        };
        mesh.vertices = vertices;
        mesh.uv = sprite.uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateSpriteMaterial(
        Shader shader,
        Sprite sprite,
        Color color,
        string configurationName)
    {
        var material = new Material(shader)
        {
            name = $"{configurationName} ECS Projectile Material",
            renderQueue = ProjectileRenderQueue
        };
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", sprite.texture);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    private static quaternion GetRotation(float2 direction)
    {
        return quaternion.RotateZ(math.atan2(direction.y, direction.x)
            + math.PI * 0.5f);
    }

    private sealed class RuntimeVisual
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly RenderMeshArray RenderMeshArray;
        public readonly RenderMeshDescription Description;
        public readonly float2 Size;

        public RuntimeVisual(
            Mesh mesh,
            Material material,
            RenderMeshArray renderMeshArray,
            RenderMeshDescription description,
            Vector2 size)
        {
            Mesh = mesh;
            Material = material;
            RenderMeshArray = renderMeshArray;
            Description = description;
            Size = new float2(size.x, size.y);
        }
    }
}
