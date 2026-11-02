using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Disposable production ECS worlds; no game scene, prefab, preset or save is edited.
public static class VerifyTargetPoint
{
    static int checks;
    const float Delta = .02f;
    static readonly Vector3 Elsewhere = new(1000,1000);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(object o, string field, object value)
    {
        for (var type=o.GetType();type!=null;type=type.BaseType)
        {
            var f=type.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            if(f!=null){f.SetValue(o,value);return;}
            var p=type.GetProperty(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            if(p!=null){p.SetValue(o,value);return;}
        }
        throw new Exception("Missing field: "+field);
    }
    static EnemyShootingSettings Settings(AnimationCurve curve)
    {
        var s = new EnemyShootingSettings(); Set(s,"projectileMovementPattern",EnemyProjectileMovementPattern.TargetPoint);
        Set(s,"targetPointSpeedByRemainingDistance",curve); Set(s,"overrideProjectileLifetime",true); Set(s,"projectileLifetime",120f);
        Set(s,"collisionSettings",new EnemyProjectileCollisionSettings(false));
        s.SetScreenBoundary(new EnemyProjectileScreenSettings(false)); s.SetProjectileBaseSpeedOverride(true,4f);
        return s;
    }
    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public EntityManager M => Preview.SimulationWorld.EntityManager;
        public Entity E, Registry;
        public float Time;
        public Context(EnemyShootingSettings s, Vector3 target, Vector3 spawn = default, EnemyBullet prefab = null)
        {
            prefab ??= AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,s,spawn,spawn,Vector3.down,1f,4f,0f,new EnemyProjectilePrefabSet(prefab),targetPoint:target);
            Check(requests.Count==1 && requests[0].TargetPoint==target,"Builder captures release-time point");
            Preview.Spawn(requests);
            using var q=M.CreateEntityQuery(typeof(EnemyProjectileMovement)); using var entities=q.ToEntityArray(Allocator.Temp); E=entities[0];
            using var r=M.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag)); Registry=r.GetSingletonEntity();
        }
        public Vector3 Position => M.GetComponentData<LocalTransform>(E).Position;
        public EnemyProjectileMovement Motion => M.GetComponentData<EnemyProjectileMovement>(E);
        public void Step() { Time+=Delta; Preview.Step(Time,Delta,Elsewhere); }
        public void Field(float multiplier)
        {
            if (!M.HasBuffer<EnemyProjectileRegionSpeedField>(Registry)) M.AddBuffer<EnemyProjectileRegionSpeedField>(Registry);
            var b=M.GetBuffer<EnemyProjectileRegionSpeedField>(Registry); b.Clear();
            b.Add(new EnemyProjectileRegionSpeedField { Minimum=new float2(-100),Maximum=new float2(100),DividerY=0,FrontMultiplier=multiplier,RearMultiplier=multiplier });
        }
        public float Finish(Vector3 target)
        {
            bool overshot=false;
            for(int i=0; Motion.HasReachedTarget==0; i++)
            {
                if(i>10000) throw new Exception("TargetPoint did not arrive");
                Vector3 before=Position; Step();
                if(Vector3.Dot(target-Position,target-before)<-.000001f) overshot=true;
            }
            Check(!overshot && (Position-target).sqrMagnitude<1e-10f,"Exact arrival without overshoot");
            float arrived=Time;
            for(int i=0;i<10;i++) Step();
            Check(M.Exists(E) && Position==target && math.lengthsq(M.GetComponentData<EnemyProjectileVelocity>(E).Value)==0,"Remains stopped and alive");
            return arrived;
        }
        public void Dispose()=>Preview.Dispose();
    }
    public static object Run()
    {
        checks=0;
        Check((int)EnemyProjectileMovementPattern.Default==0 && (int)EnemyProjectileMovementPattern.Orbit==3,"Old enum values retained");
        Check(!JsonUtility.FromJson<EnemyShootingSettings>("{}").UsesTargetPoint,"Old settings unchanged");
        var constant=AnimationCurve.Linear(0,1,1,1);
        foreach(var goal in new[] { Vector3.zero,new Vector3(.0001f,0,0),new Vector3(2,3,0) })
        { using var c=new Context(Settings(constant),goal); c.Finish(goal); }
        var slow=AnimationCurve.Linear(0,0,1,1);
        foreach(var curve in new[] { slow,AnimationCurve.Linear(0,3,1,.5f),AnimationCurve.Linear(0,0,1,0) })
        { using var c=new Context(Settings(curve),Vector3.up); c.Finish(Vector3.up); }
        float baseline,slowed,boosted;
        using(var c=new Context(Settings(slow),Vector3.up)) baseline=c.Finish(Vector3.up);
        using(var c=new Context(Settings(slow),Vector3.up)) { c.Field(.2f); slowed=c.Finish(Vector3.up); }
        using(var c=new Context(Settings(slow),Vector3.up)) { c.Field(2.5f); boosted=c.Finish(Vector3.up); }
        Check(slowed>baseline*4.8f && boosted<baseline*.45f,"External effects change time, not destination");
        using(var c=new Context(Settings(slow),Vector3.up))
        {
            c.Field(0); for(int i=0;i<10;i++)c.Step();
            Check(c.Position==Vector3.zero && c.Motion.HasReachedTarget==0,"Zero external speed pauses without false arrival");
            c.Field(.2f); c.Step();
            float remaining=math.distance(c.Motion.TargetPoint,new float2(c.Position.x,c.Position.y));
            var samples=c.M.GetBuffer<EnemyProjectileSpeedCurveSample>(c.E);
            float expected=4f*math.max(.01f,ProjectileCustomSpeedSystem.EvaluateSpeedMultiplier(samples,remaining/c.Motion.TargetInitialDistance));
            c.Step();
            Check(math.abs(math.length(c.M.GetComponentData<EnemyProjectileVelocity>(c.E).Value)-expected)<.0001f,"Curve uses actual remaining distance after slow");
            Check(math.distance(c.Motion.TargetPoint,new float2(0,1))==0,"Moving preview player does not retarget");
        }
        using(var c=new Context(Settings(slow),Vector3.up))
        {
            for(int i=0;i<15;i++)c.Step(); float clock=c.Time;
            var t=typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot",BindingFlags.NonPublic);
            using(var snap=(IDisposable)Activator.CreateInstance(t,new object[]{c.Preview}))
            {
                for(int i=0;i<10;i++)c.Step(); Vector3 expected=c.Position;
                var map=(Dictionary<Entity,Entity>)t.GetMethod("Restore").Invoke(snap,new object[]{c.Preview}); c.E=map[c.E]; c.Registry=map[c.Registry]; c.Time=clock;
                for(int i=0;i<10;i++)c.Step();
                Check(c.Position==expected,"Snapshot replay restores exact target and progress");
            }
        }
        var parentSettings=Settings(constant);
        var child=new EnemyChildAttackSettings();Set(child,"triggerValue",.1f); child.Pattern.Shooting.CopyFrom(Settings(constant));parentSettings.SetChildAttack(child);
        using(var c=new Context(parentSettings,new Vector3(.1f,0)))
        {
            for(int i=0;i<10;i++)c.Step();
            Check(c.Motion.HasReachedTarget!=0 && c.Preview.Count>1,"Stopped parent continues Child Attack");
            using var q=c.M.CreateEntityQuery(typeof(EnemyProjectileMovement));using var es=q.ToEntityArray(Allocator.Temp);bool captured=false;
            foreach(var e in es)if(e!=c.E){var movement=c.M.GetComponentData<EnemyProjectileMovement>(e);captured|=movement.Pattern==EnemyProjectileMovementPattern.TargetPoint && math.distance(movement.TargetPoint,new float2(1000))<.0001f;}
            Check(captured,"Child TargetPoint captures player at its own release");
        }
        var expiring=Settings(constant);Set(expiring,"projectileLifetime",.1f);
        using(var c=new Context(expiring,Vector3.zero)){for(int i=0;i<8;i++)c.Step();Check(!c.M.Exists(c.E),"Lifetime still cleans up stopped projectile");}
        var source=Settings(slow); var copy=new EnemyShootingSettings(); copy.CopyFrom(source);
        var preset=new EnemyShootingSettings(); EnemyPresetFields.Copy(preset,source,EnemyPresetFields.Projectile);
        Check(copy.UsesTargetPoint && preset.UsesTargetPoint && copy.SpeedByDistance.Evaluate(.5f)==.5f && preset.SpeedByDistance.Evaluate(.5f)==.5f,"Copy and presets preserve curve");
        var enemy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<SimpleEnemyAttackExecutor>();
        foreach(string field in new[]{"homingProjectilePrefab","burstAtPointProjectilePrefab"})
        {
            var bullet=(EnemyBullet)new SerializedObject(enemy).FindProperty(field).objectReferenceValue;
            using var c=new Context(Settings(constant),Vector3.up,prefab:bullet);
            Check(!c.M.HasComponent<EnemyProjectileHoming>(c.E) && !c.M.HasComponent<EnemyProjectileBurstAtPoint>(c.E),"TargetPoint overrides prefab flight mode"); c.Finish(Vector3.up);
        }
        return new{passed=true,checks,baseline,slowed,boosted};
    }
    public static object RunSeek()
    {
        checks=0; var scene=EditorSceneManager.NewPreviewScene(); var root=new GameObject("TargetPoint seek verification"); SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var wave=root.AddComponent<Wave>(); var go=new GameObject("One source");go.transform.SetParent(root.transform,false);
            var sub=go.AddComponent<DirectedEnemySubWave>(); Set(sub,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(sub,"enemyCount",1); Set(sub,"spawnInterval",0f); Set(sub,"settleDuration",0f);
            var attack=go.GetComponent<DirectedWaveAttackBehaviour>()??go.AddComponent<DirectedWaveAttackBehaviour>(); Set(attack,"allowAutonomousAttackDuringEntrance",false);
            var a=(DirectedWaveAttackSettings)typeof(DirectedWaveAttackBehaviour).GetField("attackSettings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(attack);
            Set(a,"fireMode",DirectedWaveAttackFireMode.Stationary); Set(a,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride); Set(a,"waitForPreviousAttack",true);Set(a,"delayAfterAttack",100f);
            a.WaveBurstSettings.ConfigureLegacySingleAttack(1,1f,100f); a.WaveBurstSettings.Shooting.CopyFrom(Settings(AnimationCurve.Linear(0,0,1,1))); a.Validate();
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new(go,0)});
            using var p=new WavePreviewPlayer(wave,77,new Vector3(1,-2));p.SnapshotInterval=.25f;
            void Seek(float time){p.Seek(time);for(int i=0;p.IsSeeking;i++){if(i>500)throw new Exception("Seek stalled");p.Update(0,100);}}
            string Capture(){var m=p.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using var q=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));using var es=q.ToEntityArray(Allocator.Temp);var rows=new List<string>();foreach(var e in es){var data=m.GetComponentData<EnemyProjectileMovement>(e);Check(data.Pattern==EnemyProjectileMovementPattern.TargetPoint && math.distance(data.TargetPoint,new float2(1,-2))<.0001f,"Preview captured actual player point");rows.Add(JsonUtility.ToJson(data)+JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e)));}Check(rows.Count>0,"Preview has projectiles");rows.Sort(StringComparer.Ordinal);return string.Join("|",rows);}
            Seek(3f);string expected=Capture();Seek(.2f);Seek(3f);Check(Capture()==expected,"Fast Seek and replay are deterministic");Check(p.RestoredSnapshots>0,"Fast Seek used snapshots");return new{passed=true,checks,snapshots=p.RestoredSnapshots};
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
