using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyTargetPosition
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Set(object o,string field,object value){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null){f.SetValue(o,value);return;}var p=t.GetProperty(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(p!=null){p.SetValue(o,value);return;}}throw new Exception(field);}
    static EnemyChildAttackSettings Child(float delay=0f)
    {
        var c=new EnemyChildAttackSettings();Set(c,"triggerMode",EnemyChildAttackTriggerMode.OnEvent);Set(c,"eventType",EnemyChildAttackEvent.OnTargetReached);Set(c,"timeOffset",delay);
        Set(c,"repeatMode",EnemyChildAttackRepeatMode.FixedCount);Set(c,"repeatCount",1);
        c.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));Set(c.Pattern.Shooting,"overrideProjectileLifetime",true);Set(c.Pattern.Shooting,"projectileLifetime",20f);c.Pattern.Shooting.SetProjectileBaseSpeedOverride(true,1f);
        return c;
    }
    static EnemyShootingSettings Settings(bool hide=true,bool boundary=false,EnemyChildAttackSettings child=null)
    {
        var s=new EnemyShootingSettings();Set(s,"projectileMovementPattern",boundary?EnemyProjectileMovementPattern.Default:EnemyProjectileMovementPattern.TargetPoint);
        Set(s,"targetPointSpeedByRemainingDistance",AnimationCurve.Constant(0,1,1));Set(s,"targetScreenBoundary",boundary);Set(s,"hideParentOnTargetReached",hide);
        Set(s,"targetBoundaries",EnemyChildAttackBoundaries.Right);Set(s,"overrideProjectileLifetime",true);Set(s,"projectileLifetime",.05f);
        s.SetScreenBoundary(new EnemyProjectileScreenSettings(true,EnemyProjectileBoundaryOffsetMode.Custom,0));s.SetProjectileBaseSpeedOverride(true,4);if(child!=null)s.SetChildAttack(child);return s;
    }
    sealed class Context:IDisposable
    {
        public readonly EnemyProjectilePreviewWorld P=new();public EntityManager M=>P.SimulationWorld.EntityManager;
        public readonly Scene Scene;public readonly GameObject CameraObject;public readonly Camera Camera;public readonly Rect Bounds;
        public Entity Parent,Registry;public float Time;
        public Context(EnemyShootingSettings settings,Vector3 target,Vector3? spawn=null,Vector3? direction=null,float speed=4)
        {
            Scene=EditorSceneManager.NewPreviewScene();CameraObject=new GameObject("TargetPosition test camera");SceneManager.MoveGameObjectToScene(CameraObject,Scene);Camera=CameraObject.AddComponent<Camera>();Camera.enabled=false;Camera.orthographic=true;Camera.orthographicSize=5;Camera.aspect=1;Camera.transform.position=new Vector3(0,0,-10);
            EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera,0,out var bounds);Bounds=bounds;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();var requests=new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,settings,spawn??Vector3.zero,spawn??Vector3.zero,direction??Vector3.up,1,speed,0,new EnemyProjectilePrefabSet(prefab),targetPoint:target);P.Spawn(requests);
            using var q=M.CreateEntityQuery(typeof(EnemyProjectileMovement));using var es=q.ToEntityArray(Allocator.Temp);foreach(var e in es)if(!M.HasComponent<EnemyCompositePart>(e)){Parent=e;break;}
            using var r=M.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));Registry=r.GetSingletonEntity();
        }
        public void Step(float delta=.02f){Time+=delta;P.Step(Time,delta,new Vector3(1000,1000),Camera);}
        public bool Hidden=>M.Exists(Parent)&&EnemyProjectileTargetPositionUtility.IsHidden(M,Parent);
        public void Dispose(){P.Dispose();UnityEngine.Object.DestroyImmediate(CameraObject);EditorSceneManager.ClosePreviewScene(Scene);}
    }
    public static object Run()
    {
        checks=0;Check((int)EnemyChildAttackEvent.OnScreenBoundaryReached==5,"Old event ID preserved");
        foreach(bool boundary in new[]{false,true})
        {
            var child=Child();var s=Settings(false,boundary,child);Set(s,"projectileLifetime",10f);s.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            using var c=new Context(s,new Vector3(0,.04f),direction:Vector3.right,speed:100);
            c.Step(.2f);Check(c.M.Exists(c.Parent),"Visible parent survives reaching target");
            var reached=c.M.GetComponentData<EnemyProjectileTargetPosition>(c.Parent);Check(reached.Reached==1,"Both target sources latch reaching");
            Vector3 point=boundary?new Vector3(c.Bounds.xMax,0,0):new Vector3(0,.04f);
            Check(math.distance(reached.Position,(float3)point)<.0001f,"Correct event position");
            using(var q=c.M.CreateEntityQuery(typeof(EnemyProjectileMovement)))using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es)if(e!=c.Parent)Check(math.distance(c.M.GetComponentData<LocalTransform>(e).Position,(float3)point)<.0001f,"Children start at the reached point, not overshot parent");
            int count=c.P.Count;for(int i=0;i<5;i++)c.Step();Check(c.P.Count==count,"Target event emits only once");
            if(boundary)Check(c.M.GetComponentData<LocalTransform>(c.Parent).Position.x>point.x,"Boundary keeps visible parent's movement");
        }
        using(var c=new Context(Settings(),new Vector3(0,.04f))){c.Step();Check(!c.M.Exists(c.Parent)&&c.P.Count==0,"Hidden parent without Child Attack is deleted immediately");}
        var repeated=Child(.2f);Set(repeated,"repeatCount",2);Set(repeated,"repeatInterval",.1f);Set(repeated.Pattern,"attackShotCount",2);Set(repeated.Pattern,"attackShotInterval",.15f);
        var layout=repeated.Pattern.Shooting.EmissionLayout;layout.Configure(EnemyEmissionLayoutType.Sector,3);Set(layout,"launchMode",EnemySectorLaunchMode.Sequential);Set(layout,"startDelay",.1f);Set(layout,"launchInterval",.05f);repeated.Pattern.Validate();
        using(var c=new Context(Settings(child:repeated),new Vector3(0,.04f)))
        {
            c.Step();Check(c.Hidden,"Parent hides on reaching point");Check(c.M.HasComponent<DisableRendering>(c.Parent)&&c.M.GetComponentData<EnemyProjectileCollisionState>(c.Parent).Disabled==1,"Rendering and collision disabled");
            var point=c.M.GetComponentData<LocalTransform>(c.Parent).Position;for(int i=0;i<6;i++)c.Step();Check(c.Hidden&&c.P.Count==1,"Hidden parent survives lifetime while time offset is pending");
            var fields=c.M.AddBuffer<EnemyProjectilePurgeField>(c.Registry);fields.Add(new EnemyProjectilePurgeField{Radius=3,DestructionPolicy=EnemyProjectileDestructionPolicy.NormalAndFragile});c.P.SimulationWorld.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(c.P.SimulationWorld.Unmanaged);
            Check(c.M.GetComponentData<EnemyProjectileResolution>(c.Parent).Kind==EnemyProjectileResolutionKind.None,"Ability purge cannot destroy anchor");c.M.GetBuffer<EnemyProjectilePurgeField>(c.Registry).Clear();
            for(int i=0;i<150&&c.M.Exists(c.Parent);i++)c.Step();Check(!c.M.Exists(c.Parent)&&c.P.Count==12,"Automatic deletion waits for repeats and all delayed Sector launches");
            c.Step();Check(c.P.Count==12,"Created children survive parent deletion");
        }
        using(var c=new Context(Settings(child:Child(.5f)),new Vector3(0,.04f)))
        {
            c.Step();for(int i=0;i<5;i++)c.Step();float clock=c.Time;
            var type=typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot",BindingFlags.NonPublic);
            // Native state: hidden flag, target latch and DisableRendering survive snapshots.
            using(var snap=(IDisposable)Activator.CreateInstance(type,new object[]{c.P}))
            {
                var map=(Dictionary<Entity,Entity>)type.GetMethod("Restore").Invoke(snap,new object[]{c.P});c.Parent=map[c.Parent];c.Registry=map[c.Registry];
                Check(c.Hidden&&c.M.HasComponent<DisableRendering>(c.Parent)&&c.M.GetComponentData<EnemyProjectileTargetPosition>(c.Parent).Reached==1,"Snapshot restores hidden target state");
            }
        }
        var composite=Settings(child:Child(.3f));composite.SetProjectileType(EnemyProjectileType.Composite);
        using(var c=new Context(composite,new Vector3(0,.04f)))
        {
            c.Step();Check(c.Hidden,"Composite controller becomes anchor");var members=c.M.GetBuffer<EnemyCompositeMember>(c.Parent);Check(members.Length>0,"Composite damage components exist");foreach(var member in members)Check(EnemyProjectileTargetPositionUtility.IsHidden(c.M,member.Entity)&&c.M.HasComponent<DisableRendering>(member.Entity)&&c.M.GetComponentData<EnemyProjectileCollisionState>(member.Entity).Disabled==1,"All composite parts hidden and harmless");
            for(int i=0;i<80&&c.M.Exists(c.Parent);i++)c.Step();Check(!c.M.Exists(c.Parent)&&c.P.Count==1,"Composite and members cleaned, child survives");
        }
        foreach(float offset in new[]{-.5f,.5f})
        {
            var s=Settings(true,true,Child(.5f));Set(s,"targetBoundaryOffset",offset);
            using var c=new Context(s,Vector3.zero,direction:Vector3.right,speed:100);c.Step(.2f);
            Check(c.Hidden&&math.abs(c.M.GetComponentData<LocalTransform>(c.Parent).Position.x-(c.Bounds.xMax-offset))<.0001f,"Boundary offset and crossing clamp");
            c.Step();Check(c.Hidden,"Destroy Outside Screen does not cancel hidden delayed attack");
        }
        var infinite=Child();Set(infinite,"repeatMode",EnemyChildAttackRepeatMode.UntilParentDestroyed);Set(infinite,"repeatInterval",.1f);
        using(var c=new Context(Settings(child:infinite),new Vector3(0,.04f)))
        {
            for(int i=0;i<20;i++)c.Step();Check(c.Hidden&&c.P.Count>2,"Infinite repeat is active rather than a dead anchor");
            c.M.AddBuffer<EnemyProjectilePurgeField>(c.Registry).Add(new EnemyProjectilePurgeField{Radius=3,SystemCleanup=1});c.P.SimulationWorld.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(c.P.SimulationWorld.Unmanaged);c.Step();Check(!c.M.Exists(c.Parent),"System wave cleanup overrides hidden state");
            int n=c.P.Count;for(int i=0;i<10;i++)c.Step();Check(c.P.Count<=n,"No delayed children after forced cleanup");
        }
        var original=Settings(child:Child(.2f));var copy=new EnemyShootingSettings();copy.CopyFrom(original);var preset=new EnemyShootingSettings();EnemyPresetFields.Copy(preset,original,EnemyPresetFields.Projectile);
        Check(copy.TargetPositionSettings.HideParent&&preset.TargetPositionSettings.HideParent&&copy.ChildAttack.UsesTargetReached,"CopyFrom and presets preserve opt-in settings");
        return new{passed=true,checks};
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
            a.WaveBurstSettings.ConfigureLegacySingleAttack(1,1f,100f); var shooting=Settings(child:Child(2f));shooting.SetProjectileBaseSpeedOverride(true,100f);Set(shooting,"projectileLifetime",.5f);a.WaveBurstSettings.Shooting.CopyFrom(shooting); a.Validate();
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new(go,0)});
            using var p=new WavePreviewPlayer(wave,77,new Vector3(1,-2));p.SnapshotInterval=.25f;
            void Seek(float time){p.Seek(time);for(int i=0;p.IsSeeking;i++){if(i>500)throw new Exception("Seek stalled");p.Update(0,100);}}
            string Capture(bool hidden){var m=p.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using var q=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));using var es=q.ToEntityArray(Allocator.Temp);var rows=new List<string>();foreach(var e in es){var data=m.GetComponentData<EnemyProjectileMovement>(e);Check(EnemyProjectileTargetPositionUtility.IsHidden(m,e)==hidden,"Preview restores hidden flag");if(hidden)Check(m.GetComponentData<EnemyProjectileTargetPosition>(e).Reached!=0 && m.HasComponent<DisableRendering>(e),"Hidden target and renderer state restored");rows.Add(JsonUtility.ToJson(data)+JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e))+JsonUtility.ToJson(m.GetComponentData<EnemyProjectileTargetPosition>(e)));}Check(rows.Count>0,"Preview has projectiles");rows.Sort(StringComparer.Ordinal);return string.Join("|",rows);}
            Seek(1f);string hidden=Capture(true);Seek(3.5f);string expected=Capture(false);Seek(1f);Check(Capture(true)==hidden,"Hidden anchor restored before delayed child");Seek(3.5f);Check(Capture(false)==expected,"Fast Seek restores pending child schedule without duplicate events");Check(p.RestoredSnapshots>0,"Fast Seek used snapshots");return new{passed=true,checks,snapshots=p.RestoredSnapshots};
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
