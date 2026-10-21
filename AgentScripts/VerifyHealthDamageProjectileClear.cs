using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyHealthDamageProjectileClear
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Set(object target,string field,object value) => target.GetType().GetField(field,Flags).SetValue(target,value);
    static void Check(bool condition,string label) { if(!condition) throw new Exception(label); checks++; }
    static Entity Shot(EntityManager manager,float x,EnemyProjectileCharacteristic type)
    {
        var e=manager.CreateEntity(typeof(EnemyProjectileStaticData),typeof(LocalTransform),typeof(EnemyProjectileResolution));
        manager.SetComponentData(e,new EnemyProjectileStaticData { Characteristic=type });
        manager.SetComponentData(e,LocalTransform.FromPosition(new float3(x,0,0)));
        return e;
    }
    static bool Cleared(EntityManager m,Entity e) => m.GetComponentData<EnemyProjectileResolution>(e).Kind==EnemyProjectileResolutionKind.Expired;
    public static object Run()
    {
        checks=0;
        var previousWorld=World.DefaultGameObjectInjectionWorld;
        var world=new World("Health damage clear verification");
        World.DefaultGameObjectInjectionWorld=world;
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Health clear fixture"); SceneManager.MoveGameObjectToScene(root,scene); root.SetActive(false);
        try
        {
            var registry=root.AddComponent<EnemyProjectileCollisionRegistry>();
            var ship=root.AddComponent<ParentShip>();
            Set(ship,"enemyProjectileCollisionRegistry",registry);
            Set(ship,"<MaximumHealthPoints>k__BackingField",10f); Set(ship,"<MaximumShieldPoints>k__BackingField",10f);
            ship.SetHealthPoints(10); ship.SetShieldPoints(2);
            Check(Mathf.Approximately(ship.HealthDamageProjectileClearRadius,.75f),"Default radius .75");
            var m=world.EntityManager;
            var normal=Shot(m,.5f,EnemyProjectileCharacteristic.Normal);
            var fragile=Shot(m,-.5f,EnemyProjectileCharacteristic.Fragile);
            var boundary=Shot(m,.75f,EnemyProjectileCharacteristic.Normal);
            var outside=Shot(m,.751f,EnemyProjectileCharacteristic.Fragile);
            Check(ship.TryTakeDamage(1) && ship.CurrentHealthPoints==10,"Shield damage does not reduce health");
            Check(!Cleared(m,normal) && !Cleared(m,fragile),"Shield damage does not clear bullets");
            ship.SetShieldPoints(0); Set(ship,"damageInvulnerableUntil",-1f);
            Check(ship.TryTakeDamage(1) && ship.CurrentHealthPoints==9,"Health damage accepted");
            Check(Cleared(m,normal) && Cleared(m,fragile),"Both Normal and Fragile cleared");
            Check(Cleared(m,boundary) && !Cleared(m,outside),"Inclusive radius boundary and outside exclusion");
            var protectedShot=Shot(m,.1f,EnemyProjectileCharacteristic.Normal);
            Check(!ship.TryTakeDamage(1) && !Cleared(m,protectedShot),"Invulnerability prevents clear");
            Set(ship,"damageInvulnerableUntil",-1f);
            Func<float,float> reject=value=>0; ship.OnDamagePipeline+=reject;
            Check(!ship.TryTakeDamage(1) && !Cleared(m,protectedShot),"Blocked damage does not clear");
            ship.OnDamagePipeline-=reject;
            Set(ship,"healthDamageProjectileClearRadius",0f);
            Check(ship.TryTakeDamage(1) && !Cleared(m,protectedShot),"Zero radius disables clear");
            Set(ship,"damageInvulnerableUntil",-1f); Set(ship,"healthDamageProjectileClearRadius",.2f);
            var near=Shot(m,.15f,EnemyProjectileCharacteristic.Normal);
            var far=Shot(m,.25f,EnemyProjectileCharacteristic.Normal);
            Check(ship.TryTakeDamage(1) && Cleared(m,near) && !Cleared(m,far),"Configured radius respected");
            ship.HealHealth(1);
            Check(!Cleared(m,far),"Healing does not clear bullets");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); World.DefaultGameObjectInjectionWorld=previousWorld; world.Dispose(); }
        return new { passed=checks };
    }
}
