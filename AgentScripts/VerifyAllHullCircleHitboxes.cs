using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyAllHullCircleHitboxes
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0; int hulls = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefub/Ships", "Assets/GameContent/Hulls/Gameplay" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<ParentShip>() == null) continue;
            var clone = UnityEngine.Object.Instantiate(asset);
            var helper = new GameObject("Circle hitbox verification");
            using var world = new World("Hull circle verification");
            try
            {
                var ship = clone.GetComponent<ParentShip>();
                var circle = ship.DamageHitboxCollider as CircleCollider2D;
                Check(circle != null && clone.GetComponents<Collider2D>().Length == 1, "One root circle: " + path);
                Check(Mathf.Approximately(circle.radius, .05f), "Physical radius: " + path);
                var indicator = clone.GetComponent<PlayerHitboxIndicator>();
                Check(indicator != null && Mathf.Approximately(indicator.VisualRadius, .075f), "Larger visual: " + path);
                int colliders = clone.GetComponentsInChildren<Collider2D>(true).Length;
                indicator.Initialize(ship, circle);
                Check(clone.GetComponentsInChildren<Collider2D>(true).Length == colliders, "Visual adds no colliders: " + path);
                var controller = helper.AddComponent<PlayerController>(); Set(controller, "currentShip", ship);
                var registry = helper.AddComponent<EnemyProjectileCollisionRegistry>();
                var em = world.EntityManager;
                var entity = em.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
                em.AddBuffer<EnemyProjectileCollisionTarget>(entity);
                em.AddBuffer<EnemyProjectileSlowField>(entity);
                em.AddBuffer<EnemyProjectilePurgeField>(entity);
                em.AddBuffer<EnemyProjectileRegionSpeedField>(entity);
                em.AddBuffer<EnemyProjectileResolutionEvent>(entity);
                em.AddBuffer<EnemyProjectileBurstSpawnRequest>(entity);
                Set(registry, "playerController", controller); Set(registry, "entityWorld", world);
                Set(registry, "entityManager", em); Set(registry, "registryEntity", entity);
                Physics2D.SyncTransforms(); registry.RegisterShip(ship);
                registry.GetType().GetMethod("UpdateTargetSnapshot", Private).Invoke(registry, null);
                var targets = em.GetBuffer<EnemyProjectileCollisionTarget>(entity);
                Check(targets.Length == 1 && targets[0].IsCircle == 1, "ECS receives circle, not AABB: " + path);
                Check(Mathf.Approximately(targets[0].Radius, .05f), "ECS physical radius: " + path);
                float2 center = targets[0].Center;
                var system = world.GetOrCreateSystem<ProjectilePlayerCollisionSystem>();
                Hit(em, world, system, center, center, .001f, true, "Center hit");
                Hit(em, world, system, center + .049f, center + .049f, .001f, false, "Former box corner misses");
                Hit(em, world, system, center + new float2(.065f, 0f), center + new float2(.065f, 0f), .001f, false, "Visual ring is not a hitbox");
                Hit(em, world, system, center - new float2(1f, 0f), center + new float2(1f, 0f), .001f, true, "Fast crossing hits circle");
                Hit(em, world, system, center + new float2(.0505f, 0f), center + new float2(.0505f, 0f), .001f, true, "Projectile radius is respected");
                Hit(em, world, system, center + new float2(.04f, .04f), center + new float2(.05f, .05f), .001f, false, "Corner sweep misses circle");
                hulls++;
            }
            finally { UnityEngine.Object.DestroyImmediate(helper); UnityEngine.Object.DestroyImmediate(clone); }
        }
        return new { hulls, passed = checks, physicalRadius = .05f, visualRadius = .075f };
    }
    private static void Hit(EntityManager em, World world, SystemHandle system, float2 from, float2 to, float radius, bool hit, string name)
    {
        var projectile = em.CreateEntity(typeof(EnemyProjectileResolution), typeof(EnemyProjectilePreviousPosition), typeof(EnemyProjectileStaticData), typeof(LocalTransform));
        em.SetComponentData(projectile, new EnemyProjectilePreviousPosition { Value = from });
        em.SetComponentData(projectile, new EnemyProjectileStaticData { Radius = radius });
        em.SetComponentData(projectile, LocalTransform.FromPosition(new float3(to, 0f)));
        system.Update(world.Unmanaged);
        Check((em.GetComponentData<EnemyProjectileResolution>(projectile).Kind == EnemyProjectileResolutionKind.HitPlayer) == hit, name);
        em.DestroyEntity(projectile);
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
}
