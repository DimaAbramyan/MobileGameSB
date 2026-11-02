using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

// Production ECS systems in a disposable world; no game assets or saves are modified.
public static class VerifyIndestructibleProjectiles
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public static object Run()
    {
        checks = 0;
        Check(!JsonUtility.FromJson<EnemyShootingSettings>("{}").OverridesProjectileDamage, "Legacy default");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        foreach (var type in new[] { EnemyProjectileCharacteristic.Fragile, EnemyProjectileCharacteristic.Normal, EnemyProjectileCharacteristic.Indestructible })
        {
            var s = new EnemyShootingSettings(); Set(s, "overwriteProjectileCharacteristic", true); Set(s, "projectileCharacteristic", type);
            Set(s, "overrideProjectileDamage", true); Set(s, "projectileDamage", 2.75f);
            s.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            var copy = new EnemyShootingSettings(); copy.CopyFrom(s);
            Check(copy.ProjectileCharacteristic == type && copy.OverridesProjectileDamage && copy.ProjectileDamage == 2.75f, "Copy settings");
            var preset = new EnemyShootingSettings(); EnemyPresetFields.Copy(preset, s, EnemyPresetFields.Projectile);
            Check(preset.OverridesProjectileDamage && preset.ProjectileDamage == 2.75f, "Preset settings");
            using var p = new EnemyProjectilePreviewWorld(); var w = p.SimulationWorld; var m = w.EntityManager;
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, prefab, s, Vector3.zero, Vector3.zero, Vector3.up, 3f, 1f, 0f, new EnemyProjectilePrefabSet(prefab));
            p.Spawn(requests);
            using var q = m.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = q.ToEntityArray(Allocator.Temp); var e = entities[0];
            using var rq = m.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag)); var r = rq.GetSingletonEntity();
            m.RemoveComponent<EnemyProjectilePreviewCollisionPolicy>(r);
            Check(m.GetComponentData<EnemyProjectileDamage>(e).Value == 2.75f, "Exact override");
            void Target(float x, EnemyProjectileCollisionTargetKind kind, byte normal = 0)
            {
                var t = m.GetBuffer<EnemyProjectileCollisionTarget>(r); t.Clear();
                t.Add(new EnemyProjectileCollisionTarget { Kind=kind, TargetId=7, IsCircle=1, Center=new float2(x,0), PreviousCenter=new float2(x,0), Radius=.1f, DestroysNormal=normal });
            }
            foreach (byte normal in new byte[] { 0, 1 })
            {
                Target(0, EnemyProjectileCollisionTargetKind.Interceptor, normal);
                w.GetOrCreateSystem<ProjectileInterceptorSystem>().Update(w.Unmanaged);
                Check((m.GetComponentData<EnemyProjectileResolution>(e).Kind == EnemyProjectileResolutionKind.Intercepted) == EnemyProjectileCharacteristics.CanBeDestroyed(type, normal != 0), "Interceptor policy");
                m.SetComponentData(e, new EnemyProjectileResolution());
            }
            var fields = m.AddBuffer<EnemyProjectilePurgeField>(r);
            fields.Add(new EnemyProjectilePurgeField { Radius=1, DestructionPolicy=EnemyProjectileDestructionPolicy.NormalAndFragile });
            w.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(w.Unmanaged);
            bool persistent = type == EnemyProjectileCharacteristic.Indestructible;
            Check((m.GetComponentData<EnemyProjectileResolution>(e).Kind == EnemyProjectileResolutionKind.None) == persistent, "Black Hole policy");
            m.SetComponentData(e, new EnemyProjectileResolution()); m.GetBuffer<EnemyProjectilePurgeField>(r).Clear();
            Target(0, EnemyProjectileCollisionTargetKind.PlayerShip);
            var collision = w.GetOrCreateSystem<ProjectilePlayerCollisionSystem>(); collision.Update(w.Unmanaged);
            Check((m.GetComponentData<EnemyProjectileResolution>(e).Kind == EnemyProjectileResolutionKind.None) == persistent, "Contact survival");
            if (!persistent) continue;
            for (int i=0; i<60; i++) collision.Update(w.Unmanaged);
            Check(m.GetBuffer<EnemyProjectileResolutionEvent>(r).Length == 1, "Continuous overlap emits once");
            var st = typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot", BindingFlags.NonPublic);
            using (var snapshot = (IDisposable)Activator.CreateInstance(st, new object[] { p }))
            {
                Target(10, EnemyProjectileCollisionTargetKind.PlayerShip); collision.Update(w.Unmanaged);
                Target(0, EnemyProjectileCollisionTargetKind.PlayerShip); collision.Update(w.Unmanaged);
                Check(m.GetBuffer<EnemyProjectileResolutionEvent>(r).Length == 2, "Reentry damages again");
                var map = (Dictionary<Entity, Entity>)st.GetMethod("Restore").Invoke(snapshot, new object[] { p }); e=map[e]; r=rq.GetSingletonEntity();
                collision.Update(w.Unmanaged);
                Check(m.GetBuffer<EnemyProjectileResolutionEvent>(r).Length == 1, "Snapshot restores contact latch");
            }
            var speedFields = m.AddBuffer<EnemyProjectileRegionSpeedField>(r);
            speedFields.Add(new EnemyProjectileRegionSpeedField { Minimum=new float2(-100), Maximum=new float2(100), DividerY=0, FrontMultiplier=.2f, RearMultiplier=2.5f });
            float y = m.GetComponentData<LocalTransform>(e).Position.y;
            w.SetTime(new Unity.Core.TimeData(.1, .1f)); w.GetOrCreateSystem<ProjectileMovementSystem>().Update(w.Unmanaged);
            Check(math.abs(m.GetComponentData<LocalTransform>(e).Position.y-y-.02f)<.001f, "Indestructible slows normally");
            var field = m.GetBuffer<EnemyProjectileRegionSpeedField>(r)[0]; field.FrontMultiplier=2.5f; speedFields=m.GetBuffer<EnemyProjectileRegionSpeedField>(r); speedFields[0]=field;
            y=m.GetComponentData<LocalTransform>(e).Position.y;
            w.SetTime(new Unity.Core.TimeData(.2, .1f)); w.GetOrCreateSystem<ProjectileMovementSystem>().Update(w.Unmanaged);
            Check(math.abs(m.GetComponentData<LocalTransform>(e).Position.y-y-.25f)<.001f, "Indestructible accelerates normally");
            m.GetBuffer<EnemyProjectilePurgeField>(r).Add(new EnemyProjectilePurgeField { Radius=1, SystemCleanup=1 });
            w.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(w.Unmanaged);
            Check(m.GetComponentData<EnemyProjectileResolution>(e).Kind == EnemyProjectileResolutionKind.Expired, "System cleanup");
        }
        foreach (var type in new[] { EnemyProjectileType.Default, EnemyProjectileType.Composite, EnemyProjectileType.Laser })
        {
            var s = new EnemyShootingSettings(); s.SetProjectileType(type);
            Set(s, "overwriteProjectileCharacteristic", true); Set(s, "projectileCharacteristic", EnemyProjectileCharacteristic.Indestructible);
            Set(s, "overrideProjectileDamage", true); Set(s, "projectileDamage", 2.75f);
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, prefab, s, Vector3.zero, Vector3.zero, Vector3.up, 3f, 1f, 0f, new EnemyProjectilePrefabSet(prefab));
            using var p = new EnemyProjectilePreviewWorld(); p.Spawn(requests); var m=p.SimulationWorld.EntityManager;
            using var q=m.CreateEntityQuery(typeof(EnemyProjectileDamage), typeof(EnemyProjectileStaticData));
            using var entities=q.ToEntityArray(Allocator.Temp); int bodies=0;
            foreach (var e in entities)
            {
                if (m.HasComponent<EnemyCompositeCenter>(e)) { Check(m.GetComponentData<EnemyProjectileCollisionState>(e).Disabled != 0, "Composite center stays non-damaging"); continue; }
                bodies++;
                Check(m.GetComponentData<EnemyProjectileDamage>(e).Value == 2.75f, "Damage override reaches " + type);
                Check(m.GetComponentData<EnemyProjectileStaticData>(e).Characteristic == EnemyProjectileCharacteristic.Indestructible, "Characteristic reaches " + type);
            }
            Check(bodies>0, "Created bodies for " + type);
        }
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Configured damage verification"); root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var ship = root.AddComponent<ParentShip>(); ship.AddMaxHealthPoints(10); ship.AddMaxShieldPoints(10);
            ship.SetHealthPoints(10); ship.SetShieldPoints(10);
            Check(ship.TryTakeConfiguredDamage(2.75f) && ship.CurrentShieldPoints == 7.25f, "Configured shield damage");
            Check(!ship.TryTakeConfiguredDamage(2.75f), "Invulnerability preserved");
            Set(ship, "damageInvulnerableUntil", -1f); ship.SetShieldPoints(0);
            Check(ship.TryTakeConfiguredDamage(2.75f) && ship.CurrentHealthPoints == 7.25f, "Configured hull damage");
            Set(ship, "damageInvulnerableUntil", -1f);
            Check(!ship.TryTakeConfiguredDamage(0), "Zero override is harmless");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        return new { passed=true, checks };
    }
}
