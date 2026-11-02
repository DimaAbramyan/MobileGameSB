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

public static class VerifyCompositeProjectiles
{
    static int checks;
    static void Check(bool test,string message){checks++;if(!test)throw new Exception(message);}
    static void Near(float a,float b,string message)=>Check(Mathf.Abs(a-b)<.003f,message+": "+a+" / "+b);
    static void Set(object obj,string field,object value)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        for(var type=obj.GetType();type!=null;type=type.BaseType)
        {
            var f=type.GetField(field,flags);if(f!=null){f.SetValue(obj,value);return;}
            var p=type.GetProperty(field,flags);if(p!=null){p.SetValue(obj,value);return;}
        }
        throw new Exception("Missing configuration member: "+field);
    }
    static T Get<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static EnemyBullet Bullet=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
    static EnemyShootingSettings Settings(EnemyProjectileType type)
    {
        var s=new EnemyShootingSettings();Set(s,"projectileType",type);s.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        Set(s,"overrideProjectileLifetime",true);Set(s,"projectileLifetime",10f); s.SetProjectileBaseSpeedOverride(true,2);
        Set(s.Composite,"radiusMode",EnemyCompositeRadiusMode.Custom);Set(s.Composite,"radius",1f);Set(s.Composite,"componentCount",8);
        return s;
    }
    sealed class Context:IDisposable
    {
        public readonly EnemyProjectilePreviewWorld World=new();
        public EntityManager Manager=>World.SimulationWorld.EntityManager;
        readonly Scene scene=EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public Context(){var go=new GameObject("Composite test camera"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(go,scene);Camera=go.AddComponent<Camera>();Camera.enabled=false;Camera.orthographic=true;Camera.orthographicSize=5;Camera.aspect=1;Camera.transform.position=new Vector3(0,0,-10);}
        public Entity Spawn(EnemyShootingSettings s,Vector3 position=default)
        {
            var requests=new List<EnemyProjectileSpawnRequest>();EnemyAttackProjectileBuilder.AppendEmitter(requests,Bullet,s,position,position,Vector3.down,1,2,0,new EnemyProjectilePrefabSet(Bullet));
            World.Spawn(requests);
            using var q=Manager.CreateEntityQuery(typeof(EnemyCompositeCenter));using var all=q.ToEntityArray(Allocator.Temp);Check(all.Length==1,"Exactly one invisible center");return all[0];
        }
        public void Tick(float time,float delta,Vector3? target=null)=>World.Step(time,delta,target??new Vector3(1000,1000),Camera);
        public void Dispose(){World.Dispose();UnityEngine.Object.DestroyImmediate(Camera.gameObject);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static object RunPlayProbe()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");checks=0;
        using var c=new Context();var world=c.World.SimulationWorld;var manager=c.Manager;
        using(var q=manager.CreateEntityQuery(typeof(EnemyProjectilePreviewCollisionPolicy)))manager.RemoveComponent<EnemyProjectilePreviewCollisionPolicy>(q);
        var group=world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileHomingSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileCustomSpeedSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileCustomScaleSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileMovementSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<EnemyCompositePoseSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileInterceptorSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectilePlayerCollisionSystem>());
        group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileLifetimeSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<EnemyLaserSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<EnemyChildAttackSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystemManaged<EnemyProjectileScreenBoundarySystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<EnemyCompositeCleanupSystem>());group.AddSystemToUpdateList(world.GetOrCreateSystem<ProjectileCleanupSystem>());
        var end=world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();group.SortSystems();
        var root=c.Spawn(Settings(EnemyProjectileType.CompositePolygon));world.SetTime(new Unity.Core.TimeData(.5,.5f));group.Update();end.Update();
        Near(manager.GetComponentData<LocalTransform>(root).Position.y,-1,"Production group moves center once");Check(c.World.Count==13,"Production group preserves 12 components and one center");
        var part=manager.GetBuffer<EnemyCompositeMember>(root)[0].Entity;manager.SetComponentData(part,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Intercepted});world.SetTime(new Unity.Core.TimeData(.6,0));group.Update();end.Update();Check(!manager.Exists(part)&&manager.Exists(root),"Production ECB removes one part only");
        manager.SetComponentData(root,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Expired});world.SetTime(new Unity.Core.TimeData(.7,0));group.Update();end.Update();Check(c.World.Count==0,"Production ECB removes the complete expired figure");
        return new{passed=true,checks,playMode=true};
    }
    public static object Run()
    {
        checks=0;int worlds=World.All.Count;
        Geometry();Lifecycle();HomingAndCurves();Screen();ChildBoundary();Serialization();Rewind();
        Check(World.All.Count==worlds,"All temporary ECS worlds disposed");return new{passed=true,checks};
    }
    static void Geometry()
    {
        var s=Settings(EnemyProjectileType.CompositeCircle);Set(s.Composite,"radiusMode",EnemyCompositeRadiusMode.Automatic);
        var c=s.Composite.Resolve(s.ProjectileType);float r=c.ResolveRadius(.2f);Near(math.distance(c.Offset(0,r),c.Offset(1,r)),.4f,"Circle automatic touching");
        Set(s.Composite,"spacingGap",.1f);c=s.Composite.Resolve(s.ProjectileType);r=c.ResolveRadius(.2f);Near(math.distance(c.Offset(0,r),c.Offset(1,r)),.5f,"Circle spacing");
        s=Settings(EnemyProjectileType.CompositePolygon);Set(s.Composite,"radiusMode",EnemyCompositeRadiusMode.Automatic);c=s.Composite.Resolve(s.ProjectileType);Check(c.ComponentCount==12,"Square with 4 per side has 12 shared vertices");r=c.ResolveRadius(.2f);
        for(int i=0;i<c.ComponentCount;i++){Near(math.distance(c.Offset(i,r),c.Offset((i+1)%c.ComponentCount,r)),.4f,"Polygon touching");for(int j=0;j<i;j++)Check(math.distance(c.Offset(i,r),c.Offset(j,r))>.001f,"No duplicated vertices");}
        s=Settings(EnemyProjectileType.CompositeSegmentedRing);Set(s.Composite,"radiusMode",EnemyCompositeRadiusMode.Automatic);c=s.Composite.Resolve(s.ProjectileType);Check(c.ComponentCount==12,"Ring component count");r=c.ResolveRadius(.2f);
        Near(math.distance(c.Offset(0,r),c.Offset(1,r)),.4f,"Segment automatic touching");Check(math.distance(c.Offset(3,r),c.Offset(4,r))>.4f,"Ring gap preserved");
        Set(s.Composite,"gapAngle",1000f);c=s.Composite.Resolve(s.ProjectileType);Check(float.IsFinite(c.ResolveRadius(.2f))&&c.EffectiveGapAngle<120,"Invalid gaps bounded");
        s=Settings(EnemyProjectileType.CompositeCircle);c=s.Composite.Resolve(s.ProjectileType);Near(c.ResolveRadius(.8f),1,"Custom radius ignores component size");
        Set(s.Composite,"rotationMode",EnemyCompositeRotationMode.Continuous);Set(s.Composite,"angularSpeed",90f);c=s.Composite.Resolve(s.ProjectileType);Near(c.Rotation(1,Vector2.up),90,"Continuous rotation");
        Set(s.Composite,"rotationMode",EnemyCompositeRotationMode.PingPong);c=s.Composite.Resolve(s.ProjectileType);Near(c.Rotation(.5f,Vector2.up),45,"PingPong reaches max");Near(c.Rotation(1.5f,Vector2.up),-45,"PingPong reverses");
        Set(s.Composite,"rotationMode",EnemyCompositeRotationMode.AlignToMovement);c=s.Composite.Resolve(s.ProjectileType);Near(c.Rotation(1,Vector2.right),90,"Align to travel");
    }
    static void Lifecycle()
    {
        foreach(var type in new[]{EnemyProjectileType.CompositeCircle,EnemyProjectileType.CompositePolygon,EnemyProjectileType.CompositeSegmentedRing})
        using(var c=new Context())
        {
            var s=Settings(type);Set(s.Composite,"rotationMode",EnemyCompositeRotationMode.Continuous);Set(s.Composite,"damage",3f);Set(s.Composite,"damageType",EnemyDamageType.Chemical);
            var root=c.Spawn(s);int count=s.Composite.Resolve(type).ComponentCount;Check(c.World.Count==count+1,"All parts instantiated");
            var parts=c.Manager.GetBuffer<EnemyCompositeMember>(root).ToNativeArray(Allocator.Temp);
            try
            {
                foreach(var part in parts){Check(!c.Manager.HasComponent<EnemyProjectileVelocity>(part.Entity)&&!c.Manager.HasComponent<EnemyProjectileHoming>(part.Entity),"No per-part trajectory");var d=c.Manager.GetComponentData<EnemyProjectileDamage>(part.Entity);Check(d.Value==3&&d.Type==EnemyDamageType.Chemical&&d.UseConfiguredDamage==2,"Configured damage/type on each part");}
                c.Tick(.5f,.5f);Near(c.Manager.GetComponentData<LocalTransform>(root).Position.y,-1,"Center follows ordinary movement");
                Near(c.Manager.GetComponentData<EnemyCompositeCenter>(root).Angle,30,"Independent spin");
                c.Manager.SetComponentData(parts[0].Entity,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Intercepted});c.Tick(.6f,0);Check(c.World.Count==count,"One part removed independently");Check(c.Manager.Exists(root),"Center survives partial destruction");
                for(int i=1;i<parts.Length;i++)c.Manager.SetComponentData(parts[i].Entity,new EnemyProjectileResolution{Kind=EnemyProjectileResolutionKind.Expired});c.Tick(.7f,0);Check(c.World.Count==0,"Empty figure removed");
            }finally{parts.Dispose();}
        }
        using(var c=new Context())
        {
            var s=Settings(EnemyProjectileType.CompositeCircle);Set(s,"projectileLifetime",.1f);c.Spawn(s);c.Tick(.2f,.2f);Check(c.World.Count==0,"Parent lifetime removes all parts");
        }
        using(var c=new Context())
        {
            var root=c.Spawn(Settings(EnemyProjectileType.CompositeCircle));c.Tick(0,0,Vector3.zero);Check(c.World.Count==9,"Invisible center does not hit player");
            var part=c.Manager.GetBuffer<EnemyCompositeMember>(root)[0].Entity;var point=c.Manager.GetComponentData<LocalTransform>(part).Position;c.Tick(.1f,0,new Vector3(point.x,point.y));Check(c.World.Count==8&&c.Manager.Exists(root),"Part player collision uses ordinary ECS resolution");
        }
    }
    static void HomingAndCurves()
    {
        using(var c=new Context())
        {
            var clone=UnityEngine.Object.Instantiate(Bullet.gameObject);clone.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var source=clone.GetComponent<EnemyProjectileAuthoring>();Set(source,"flightMode",EnemyProjectileFlightMode.Homing);Set(source,"homingTurnSpeedDegreesPerSecond",360f);
                var s=Settings(EnemyProjectileType.CompositeCircle);Set(s,"projectilePrefab",clone.GetComponent<EnemyBullet>());var root=c.Spawn(s);
                using var q=c.Manager.CreateEntityQuery(typeof(EnemyProjectileHoming));Check(q.CalculateEntityCount()==1,"Homing runs once per center");
                c.Tick(.25f,.25f,Vector3.right*3);Check(c.Manager.GetComponentData<LocalTransform>(root).Position.x>0,"Center homes using ordinary ECS flight");
            }
            finally{UnityEngine.Object.DestroyImmediate(clone);}
        }
        using(var c=new Context())
        {
            var s=Settings(EnemyProjectileType.CompositeCircle);Set(s,"projectileSpeedBehavior",EnemyProjectileSpeedBehavior.Customized);Set(s,"projectileSpeedChangeY",100f);Set(s,"projectileSpeedByDistance",AnimationCurve.Linear(0,.5f,10,.5f));
            Set(s,"projectileSizeBehavior",EnemyProjectileSizeBehavior.Customized);Set(s,"projectileSizeChangeY",100f);Set(s,"projectileSizeChangeSpeed",1f);Set(s,"projectileFinalSize",2f);Set(s,"projectileSizeChangeCurve",AnimationCurve.Linear(0,0,1,1));
            var root=c.Spawn(s);c.Tick(.25f,.25f);Near(c.Manager.GetComponentData<LocalTransform>(root).Position.y,-.25f,"Speed curve applies once to common trajectory");
            c.Tick(.75f,.5f);float scale=c.Manager.GetComponentData<LocalTransform>(root).Scale;Near(scale,1.5f,"Size curve scales center");
            var part=c.Manager.GetBuffer<EnemyCompositeMember>(root)[0].Entity;Near(c.Manager.GetComponentData<LocalTransform>(part).Scale,scale,"Parts inherit current size curve");
        }
    }
    static void Screen()
    {
        using var c=new Context();var s=Settings(EnemyProjectileType.CompositeCircle);s.SetScreenBoundary(default);
        var root=c.Spawn(s,new Vector3(0,7));c.Tick(0,0);Check(c.Manager.Exists(root),"Outside-born figure survives before entry");
        var pose=c.Manager.GetComponentData<LocalTransform>(root);pose.Position.y=4.8f;c.Manager.SetComponentData(root,pose);c.Tick(.1f,0);Check(c.Manager.GetComponentData<EnemyProjectileScreenState>(root).Entered==1,"Shape entry arms cleanup");
        pose.Position.y=5.5f;c.Manager.SetComponentData(root,pose);c.Tick(.2f,0);Check(c.Manager.Exists(root),"Center outside but shape intersects viewport");
        pose.Position.y=7;c.Manager.SetComponentData(root,pose);c.Tick(.3f,0);Check(c.World.Count==0,"Entire outside figure is removed");
    }
    static void ChildBoundary()
    {
        using var c=new Context();var s=Settings(EnemyProjectileType.CompositeCircle);s.SetScreenBoundary(default);
        var child=new EnemyChildAttackSettings();Set(child,"triggerMode",EnemyChildAttackTriggerMode.OnEvent);Set(child,"eventType",EnemyChildAttackEvent.OnScreenBoundaryReached);Set(child,"boundary",EnemyChildAttackBoundary.Top);Set(child,"timeOffset",.2f);Set(child,"destroyParent",true);
        child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));s.SetChildAttack(child);
        var root=c.Spawn(s,new Vector3(0,4));var pose=c.Manager.GetComponentData<LocalTransform>(root);pose.Position.y=7;c.Manager.SetComponentData(root,pose);c.Tick(.1f,0);
        Check(c.Manager.Exists(root)&&c.World.Count==9,"Boundary child delay preserves whole outside composite");c.Tick(.4f,0);
        using var q=c.Manager.CreateEntityQuery(typeof(EnemyCompositePart));Check(!c.Manager.Exists(root)&&q.CalculateEntityCount()==0,"Child Destroy Parent deletes entire composite");Check(c.World.Count>0,"Boundary child volley emitted before cleanup");
    }
    static void Serialization()
    {
        Check((int)EnemyProjectileType.Default==0&&(int)EnemyProjectileType.Laser==3,"Old enum values preserved");
        var s=Settings(EnemyProjectileType.CompositePolygon);Set(s.Composite,"damage",3f);var json=JsonUtility.ToJson(s);var copy=JsonUtility.FromJson<EnemyShootingSettings>(json);
        Check(copy.IsComposite&&copy.Composite.Resolve(copy.ProjectileType).ComponentCount==12,"Settings JSON roundtrip");
        var linked=new EnemyShootingSettings();linked.CopyProjectileConfigurationFrom(s);Check(linked.IsComposite&&linked.Composite.Resolve(linked.ProjectileType).Damage==3,"Projectile preset copy includes composition");
        var old=JsonUtility.FromJson<EnemyShootingSettings>("{\"projectileType\":0}");Check(!old.IsComposite,"Old settings remain ordinary");
    }
    static string Capture(WavePreviewPlayer player)
    {
        var manager=player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using var q=manager.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));using var es=q.ToEntityArray(Allocator.Temp);var values=new List<string>();
        foreach(var e in es){string text=JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(e));if(manager.HasComponent<EnemyCompositeCenter>(e))text+=JsonUtility.ToJson(manager.GetComponentData<EnemyCompositeCenter>(e));values.Add(text);}values.Sort(StringComparer.Ordinal);return string.Join("|",values);
    }
    static void Seek(WavePreviewPlayer p,float time){p.Seek(time);for(int i=0;p.IsSeeking;i++){if(i>1000)throw new Exception("Seek stalled");p.Update(0,1000);}}
    static void Rewind()
    {
        var scene=EditorSceneManager.NewPreviewScene();var go=new GameObject("Composite rewind test");SceneManager.MoveGameObjectToScene(go,scene);
        try
        {
            var wave=go.AddComponent<Wave>();var subObject=new GameObject("Composite subwave");subObject.transform.SetParent(go.transform);var sub=subObject.AddComponent<DirectedEnemySubWave>();
            Set(sub,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());Set(sub,"enemyCount",1);Set(sub,"settleDuration",0f);Set(sub,"spawnInterval",0f);
            var attack=subObject.GetComponent<DirectedWaveAttackBehaviour>()??subObject.AddComponent<DirectedWaveAttackBehaviour>();var settings=Get<DirectedWaveAttackSettings>(attack,"attackSettings");Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1,.3f,.3f);settings.WaveBurstSettings.Shooting.CopyFrom(Settings(EnemyProjectileType.CompositeCircle));Set(settings.WaveBurstSettings.Shooting.Composite,"rotationMode",EnemyCompositeRotationMode.Continuous);
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new WaveSubWaveCue(subObject,0)});
            using var p=new WavePreviewPlayer(wave,5,new Vector3(1000,1000)){SnapshotInterval=.5f};Seek(p,3);Check(p.Simulation.ProjectileCount>0,"Composite rendered/simulated by Wave Player");string state=Capture(p);Seek(p,1);Seek(p,3);Check(Capture(p)==state&&p.RestoredSnapshots>0,"Fast Seek restores shape, transforms and spin exactly");
            var manager=p.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using var query=manager.CreateEntityQuery(typeof(EnemyCompositePart));using var parts=query.ToEntityArray(Allocator.Temp);var victim=parts[0];var owner=manager.GetComponentData<EnemyCompositePart>(victim).Center;var position=manager.GetComponentData<LocalTransform>(victim).Position;
            p.TargetPosition=new Vector3(position.x,position.y,position.z);Seek(p,3.1f);Check(!manager.Exists(victim)&&manager.Exists(owner),"Recorded player contact removes an individual part");
            string damaged=Capture(p);Seek(p,1);Seek(p,3.1f);Check(Capture(p)==damaged,"Snapshots replay the same missing components");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
