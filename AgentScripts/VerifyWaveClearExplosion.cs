using System;
using System.Collections;
using System.Reflection;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyWaveClearExplosion
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
    static object Get(object target,string field)=>target.GetType().GetField(field,Flags).GetValue(target);
    static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static Entity Shot(EntityManager m,float x,EnemyProjectileCharacteristic type)
    {
        var e=m.CreateEntity(typeof(EnemyProjectileStaticData),typeof(LocalTransform),typeof(EnemyProjectileResolution));
        m.SetComponentData(e,new EnemyProjectileStaticData{Characteristic=type}); m.SetComponentData(e,LocalTransform.FromPosition(new float3(x,3,0))); return e;
    }
    static bool Expired(EntityManager m,Entity e)=>m.GetComponentData<EnemyProjectileResolution>(e).Kind==EnemyProjectileResolutionKind.Expired;
    public static object Run()
    {
        checks=0;float priorScale=Time.timeScale;var priorWorld=World.DefaultGameObjectInjectionWorld;
        var world=new World("Wave clear explosion verification");World.DefaultGameObjectInjectionWorld=world;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Wave clear fixture");SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
        try
        {
            var registry=root.AddComponent<EnemyProjectileCollisionRegistry>();var wave=root.AddComponent<WaveManager>();var enemies=new EnemyManager();Set(wave,"enemyManager",enemies);Set(wave,"projectileCollisionRegistry",registry);
            Time.timeScale=1;
            Invoke(wave,"TryStartWaveClearExplosion"); Check(((IList)Get(wave,"projectileClearExplosions")).Count==0,"No kill means no explosion");
            var enemy=root.AddComponent<Enemy>();enemy.transform.position=new Vector3(2,3,0);enemy.isDead=true;enemies.AddEnemy(enemy);
            bool positionReady=false;enemies.OnEnemyDestroyed+=e=>positionReady=enemies.EnemyKillCount==1&&enemies.LastKilledEnemyPosition==e.transform.position;
            enemies.NotifyEnemyDestroyed(enemy);Check(positionReady,"Death position recorded before wave completion handlers");
            Invoke(wave,"TryStartWaveClearExplosion"); Check(Mathf.Approximately(Time.timeScale,.3f),"Default slow time scale .3");
            Invoke(wave,"TryStartWaveClearExplosion"); Check(((IList)Get(wave,"projectileClearExplosions")).Count==1,"Wave effect only starts once");
            var m=world.EntityManager;var normal=Shot(m,2.8f,EnemyProjectileCharacteristic.Normal);var fragile=Shot(m,1.2f,EnemyProjectileCharacteristic.Fragile);var far=Shot(m,3.1f,EnemyProjectileCharacteristic.Normal);
            Invoke(registry,"EnsureRegistryEntity");
            Invoke(wave,"TickWaveClearEffects",.5f);Invoke(registry,"UpdateTargetSnapshot");world.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(world.Unmanaged);
            Check(Expired(m,normal)&&Expired(m,fragile),"Expanding field destroys both types at killed enemy position");Check(!Expired(m,far),"Outside current radius remains alive");
            var effects=(IList)Get(wave,"projectileClearExplosions");var effect=effects[0];float radius=(float)effect.GetType().GetField("Radius").GetValue(effect);Check(Mathf.Approximately(radius,1),"Radius grows 2 units per real second");
            Time.timeScale=0;Invoke(wave,"TickWaveClearEffects",2f);Check(Mathf.Approximately(Time.timeScale,0)&&Mathf.Approximately((float)Get(wave,"slowMotionRemaining"),.3f),"Pause freezes slow motion and effect");
            Time.timeScale=1;Invoke(wave,"TickWaveClearEffects",.3f);Check(Mathf.Approximately(Time.timeScale,1),"Time restored after .8 real seconds");
            Invoke(registry,"UpdateTargetSnapshot");world.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(world.Unmanaged);Check(Expired(m,far),"Far projectile cleared when expanding radius reaches it");
            Invoke(wave,"TickWaveClearEffects",6.2f);Check(effects.Count==0,"Explosion expires at 7 seconds");Check(((IDictionary)Get(registry,"waveClearPurgeFields")).Count==0,"Expired ECS field removed");
            Set(wave,"waveClearEffectStarted",false);Set(wave,"waveClearTimeScale",.5f);Set(wave,"waveClearSlowMotionDuration",1f);Set(wave,"waveClearExplosionExpansionSpeed",4f);Set(wave,"waveClearExplosionLifetime",2f);
            Invoke(wave,"TryStartWaveClearExplosion");Invoke(wave,"TickWaveClearEffects",.25f);effect=effects[0];radius=(float)effect.GetType().GetField("Radius").GetValue(effect);
            Check(Mathf.Approximately(Time.timeScale,.5f)&&Mathf.Approximately(radius,1),"Inspector settings affect time and growth");
            Invoke(wave,"StopWaveClearEffects");Check(Mathf.Approximately(Time.timeScale,1)&&effects.Count==0,"Disable restores time and cleans fields");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);World.DefaultGameObjectInjectionWorld=priorWorld;world.Dispose();Time.timeScale=priorScale;}
        return new{passed=checks};
    }
}
