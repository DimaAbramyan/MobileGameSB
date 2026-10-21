using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyArkanoidProjectileHitbox
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        const string shipPath = "Assets/Prefub/Ships/Arkanoid.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(shipPath);
        var clone = UnityEngine.Object.Instantiate(asset);
        var helper = new GameObject("Hitbox verification only");
        var world = new World("Arkanoid hitbox verification");
        try
        {
            var ship = clone.GetComponent<ParentShip>();
            var magnet = clone.GetComponentInChildren<Magnite>(true);
            Invoke(magnet, "Awake");
            Physics2D.SyncTransforms();
            var hitbox = ship.DamageHitboxCollider;
            var magnetCollider = magnet.GetComponent<CircleCollider2D>();
            var controller = helper.AddComponent<PlayerController>();
            Set(controller, "currentShip", ship);
            var registry = helper.AddComponent<EnemyProjectileCollisionRegistry>();
            var em = world.EntityManager;
            var registryEntity = em.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
            em.AddBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            em.AddBuffer<EnemyProjectileSlowField>(registryEntity);
            em.AddBuffer<EnemyProjectileResolutionEvent>(registryEntity);
            em.AddBuffer<EnemyProjectileBurstSpawnRequest>(registryEntity);
            Set(registry, "playerController", controller);
            Set(registry, "entityWorld", world);
            Set(registry, "entityManager", em);
            Set(registry, "registryEntity", registryEntity);
            registry.RegisterShip(ship);
            registry.RegisterShip(ship);
            Invoke(registry, "UpdateTargetSnapshot");
            var snapshot = em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            Assert(snapshot.Length == 1, "Only one damage target, including duplicate registration");
            var target = snapshot[0];
            Assert(math.distance(target.Min, new float2(hitbox.bounds.min.x, hitbox.bounds.min.y)) < .00001f,
                "Snapshot must match the root hitbox, not Magnet");
            var bulletAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab");
            var bullet = bulletAsset.GetComponent<EnemyProjectileAuthoring>();
            float radius = bullet.EffectiveRadius;
            Bounds shipBounds = hitbox.bounds;
            Vector2 center = shipBounds.center;
            Vector2 magnetOnly = center + Vector2.right * .15f;
            Assert(magnetCollider.OverlapPoint(magnetOnly), "Test projectile must be inside Magnet: point=" + magnetOnly
                + ", ship=" + hitbox.bounds + ", magnet=" + magnetCollider.bounds
                + ", radius=" + magnetCollider.radius + ", scale=" + magnetCollider.transform.lossyScale
                + ", enabled=" + magnetCollider.enabled + ", active=" + magnetCollider.gameObject.activeInHierarchy);
            var system = world.GetOrCreateSystem<ProjectilePlayerCollisionSystem>();
            var checks = new List<object>();
            Check(em, world, system, center, center, radius, EnemyProjectileResolutionKind.HitPlayer, "Root hitbox", checks);
            Check(em, world, system, magnetOnly, magnetOnly, .01f, EnemyProjectileResolutionKind.None, "Small projectile inside Magnet, outside ship", checks);
            Check(em, world, system, magnetOnly, magnetOnly, .2f, EnemyProjectileResolutionKind.HitPlayer,
                "Original oversized Bullet reproduces early hit", checks);
            Check(em, world, system, magnetOnly, magnetOnly, radius, EnemyProjectileResolutionKind.None,
                "Corrected Bullet beside ship inside Magnet", checks);
            Check(em, world, system, magnetOnly + Vector2.up, magnetOnly - Vector2.up, .01f,
                EnemyProjectileResolutionKind.None, "Moving through Magnet beside ship", checks);
            Check(em, world, system, center + Vector2.up, center - Vector2.up, radius,
                EnemyProjectileResolutionKind.HitPlayer, "Fast projectile crossing ship", checks);
            magnetCollider.radius = 2f;
            Physics2D.SyncTransforms();
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em, world, system, center + Vector2.right, center + Vector2.right, radius,
                EnemyProjectileResolutionKind.None, "Configured Bullet inside enlarged Magnet only", checks);
            magnetCollider.enabled = false;
            Invoke(registry, "UpdateTargetSnapshot");
            Assert(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 1, "Magnet enable state cannot change damage target");
            hitbox.enabled = false;
            Invoke(registry, "UpdateTargetSnapshot");
            Assert(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 0, "Disabled ship hitbox cannot receive hits");
            Vector2 spriteSize = bullet.VisualRenderer.sprite.bounds.size;
            return new { shipPath, registeredDamageTargets = 1, hitboxObject = hitbox.name,
                hitboxSize = shipBounds.size.ToString(), configuredHitboxSize = ((BoxCollider2D)hitbox).size.ToString(),
                bulletRadius = radius,
                bulletVisualSize = Vector2.Scale(spriteSize, bullet.VisualScale).ToString(), checks };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(helper);
            UnityEngine.Object.DestroyImmediate(clone);
            world.Dispose();
        }
    }

    private static void Check(EntityManager em, World world, SystemHandle system, Vector2 from, Vector2 to,
        float radius, EnemyProjectileResolutionKind expected, string name, List<object> checks)
    {
        var entity = em.CreateEntity(typeof(EnemyProjectileResolution), typeof(EnemyProjectilePreviousPosition),
            typeof(EnemyProjectileStaticData), typeof(LocalTransform));
        em.SetComponentData(entity, new EnemyProjectilePreviousPosition { Value = new float2(from.x, from.y) });
        em.SetComponentData(entity, new EnemyProjectileStaticData { Radius = radius });
        em.SetComponentData(entity, LocalTransform.FromPosition(new float3(to.x, to.y, 0f)));
        system.Update(world.Unmanaged);
        var actual = em.GetComponentData<EnemyProjectileResolution>(entity).Kind;
        Assert(actual == expected, name + ": expected " + expected + ", got " + actual);
        checks.Add(new { name, result = actual.ToString() });
        em.DestroyEntity(entity);
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
