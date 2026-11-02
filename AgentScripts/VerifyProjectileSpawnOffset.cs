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

public static class VerifyProjectileSpawnOffset
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static readonly Vector3 Target = new(1000, 1000);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object value, string name, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var f = type.GetField(name, Flags); if (f != null) { f.SetValue(value, data); return; }
            var p = type.GetProperty(name, Flags); if (p != null) { p.SetValue(value, data); return; }
        }
        throw new Exception("Missing " + name);
    }
    public static object SeekProbe()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Bounce snapshot verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var go = new GameObject("One moving projectile"); go.transform.SetParent(root.transform, false);
            var subwave = go.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "spawnInterval", 0f); Set(subwave, "settleDuration", 0f);
            var attack = go.GetComponent<DirectedWaveAttackBehaviour>() ?? go.AddComponent<DirectedWaveAttackBehaviour>();
            var settings = (DirectedWaveAttackSettings)attack.GetType().GetField("attackSettings", Flags).GetValue(attack);
            Set(attack, "allowAutonomousAttackDuringEntrance", false);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(135);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 20f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 1f, 20f);
            var shooting = settings.WaveBurstSettings.Shooting;
            Set(shooting, "enableSpawnOffset", true); Set(shooting, "baseOffsetX", .6f);
            Range(shooting, "baseOffsetY", -.25f, .25f, EnemyNumericRandomizeOn.PerProjectile);
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(true, bounce: true, edges: EnemyChildAttackBoundaries.All));
            shooting.SetProjectileBaseSpeedOverride(true, 8f);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 20f);
            Set(shooting, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
            Set(shooting, "projectileSizeChangeY", 0f); Set(shooting, "projectileFinalSize", 0f); Set(shooting, "projectileSizeChangeSpeed", .3f);
            settings.Validate(); Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(go, 0) });
            using var player = new WavePreviewPlayer(wave, 8, Target); player.SnapshotInterval = .25f;
            void Seek(float time) { player.Seek(time); for (int i = 0; player.IsSeeking; i++) { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); } }
            string Capture()
            {
                var m = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = m.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp); var rows = new List<string>();
                foreach (var e in entities) rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileVelocity>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileScreenState>(e)));
                rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
            }
            Seek(3f); string expected = Capture();
            Check(expected.Length > 0, "Live bouncing/shrinking projectile captured");
            Seek(1f); Seek(3f); Check(Capture() == expected, "Snapshots restore exact position, direction, scale and boundary state");
            Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "Fast Seek removes zero-size projectile before Lifetime");
            Seek(2f); Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "No zero-size projectile resurrected after rewind");
            Check(player.RestoredSnapshots > 0 && !player.IsPlaying, "Actual snapshot restores preserve pause");
            return new { passed = true, checks, snapshotRestores = player.RestoredSnapshots, deterministicSeek = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static object Get(object o,string name) => o.GetType().GetField(name,Flags).GetValue(o);
    static void Range(object o,string name,float min,float max,EnemyNumericRandomizeOn scope=EnemyNumericRandomizeOn.PerShot)
    { var r=(RandomFloatRange)Get(o,name+"Random"); r.enabled=true;r.min=min;r.max=max;r.initialized=true;r.randomizeOn=scope; }
    static void Near(Vector3 a,Vector3 b,string why) => Check((a-b).sqrMagnitude<.000001f,why+": "+a+" / "+b);
    static EnemyShootingSettings Settings(int fan=1,int stack=1)
    {
        var s=new EnemyShootingSettings();s.ConfigureLegacyFan(fan,0,0);s.EmissionLayout.Configure(EnemyEmissionLayoutType.Point,1);
        if(stack>1){Set(s,"shotGunMode",EnemyProjectileShotGunMode.ShotGun);Set(s,"shotGunProjectileCount",stack);}
        Set(s,"enableSpawnOffset",true);Set(s,"stepOffsetX",.5f);
        s.SetScreenBoundary(new EnemyProjectileScreenSettings(false)); s.Validate();return s;
    }
    static List<EnemyProjectileSpawnRequest> Requests(EnemyShootingSettings s,int arm,EnemyNumericSample n=default,Vector3? direction=null,Vector3? position=null)
    {
        var list=new List<EnemyProjectileSpawnRequest>();
        var bullet=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        EnemyAttackProjectileBuilder.AppendEmitter(list,bullet,s,position??Vector3.zero,Vector3.zero,direction??Vector3.up,1,2,0,new EnemyProjectilePrefabSet(bullet),
            randomSpeed:EnemyRandomSpeedSample.Identity(123).WithNumeric(n),directionIndex:arm);
        return list;
    }
    public static object Run()
    {
        checks=0;
        var s=Settings(5);var shot=Requests(s,0);
        for(int i=0;i<5;i++)Near(shot[i].Position,new Vector3(-1+i*.5f,0),"Five symmetric projectiles");
        Set(s,"baseOffsetX",.2f);Set(s,"baseOffsetY",.7f);Set(s,"stepOffsetY",.1f);
        shot=Requests(s,0,direction:Vector3.right);
        for(int i=0;i<5;i++){Near(shot[i].Position,new Vector3(.7f+(i-2)*.1f,-(.2f+(i-2)*.5f)),"Local axes rotate with flight");Near(shot[i].Direction,Vector3.right,"Offset does not turn projectile");}
        Set(s,"enableSpawnOffset",false);foreach(var p in Requests(s,0))Near(p.Position,Vector3.zero,"Disabled preserves old spawn");
        s=Settings(stack:4);shot=Requests(s,0);Near(shot[0].Position,Vector3.left*.75f,"Even stack left");Near(shot[3].Position,Vector3.right*.75f,"Even stack right");
        Check(EnemyShootingPreviewGeometry.CoincidentStackCount(s)==1,"Separated Stack rays are not collapsed");
        foreach(var layout in new[]{EnemyEmissionLayoutType.Point,EnemyEmissionLayoutType.Radial,EnemyEmissionLayoutType.Circle,EnemyEmissionLayoutType.Sector})
        {
            s=Settings(3,2);Set(s,"fanMinAngle",-30f);Set(s,"fanMaxAngle",30f);
            s.EmissionLayout.Configure(layout,3);
            if(layout==EnemyEmissionLayoutType.Sector)s.EmissionLayout.ConfigureSector(-40,40,3,EnemySectorLaunchMode.Sequential,.2f,.1f);
            s.SetLayoutRotationEnabled(true);
            var frame=new EnemyEmissionPreviewFrame{EnemyPosition=Vector3.zero,ShootingPoint=Vector3.zero,Scale=Vector3.one,EnemyBasis=Quaternion.identity,PointBasis=Quaternion.identity,Origin=Vector3.zero};
            var n=new EnemyNumericSample(71,99);Range(s,"baseOffsetX",-.3f,.3f,EnemyNumericRandomizeOn.PerProjectile);
            for(int arm=0;arm<s.DirectionCount;arm++)
            {
                var emitter=EnemyShootingPreviewGeometry.GetEmitter(s,arm,frame,Quaternion.identity,.5f);
                var req=Requests(s,arm,n,emitter.BaseDirection,emitter.Position);
                for(int i=0;i<req.Count;i++)
                {
                    var p=EnemyShootingPreviewGeometry.GetProjectile(s,emitter,i,n.ForDirection(arm).ForProjectile(i));
                    Near(req[i].Position,p.Position,"Scene/Runtime position "+layout);Near(req[i].Direction,p.Direction,"Scene/Runtime direction "+layout);
                }
            }
        }
        s=Settings();s.EmissionLayout.ConfigureSector(0,90,5,EnemySectorLaunchMode.Sequential);
        Near(Requests(s,0)[0].Position,Vector3.left,"Sequential direction keeps full-sector first index");
        Near(Requests(s,4)[0].Position,Vector3.right,"Sequential direction keeps full-sector last index");
        s=Settings(3);s.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle,4);
        Near(Requests(s,0)[0].Position,Requests(s,3)[0].Position,"Circle resets pellet index per emitter");
        s=Settings();Set(s,"stepOffsetX",0f);Range(s,"baseOffsetY",-2,3);
        var a=new EnemyNumericSample(10,20,0,0);var b=new EnemyNumericSample(10,21,1,1);
        foreach(var scope in (EnemyNumericRandomizeOn[])Enum.GetValues(typeof(EnemyNumericRandomizeOn)))
        {
            Range(s,"baseOffsetY",-2,3,scope);
            var x=s.GetProjectileSpawnPosition(Vector3.zero,Vector3.up,0,0,a);
            Near(x,s.GetProjectileSpawnPosition(Vector3.zero,Vector3.up,0,0,a),"Deterministic "+scope);
            var y=s.GetProjectileSpawnPosition(Vector3.zero,Vector3.up,0,0,b);
            Check((x==y)==(scope==EnemyNumericRandomizeOn.PerAttack),"Resample scope "+scope);
        }
        var copy=new EnemyShootingSettings();copy.CopyFrom(s);Near(Requests(copy,0,a)[0].Position,Requests(s,0,a)[0].Position,"Copy retains ranges");
        var old=JsonUtility.FromJson<EnemyShootingSettings>("{}");Check(!old.EnableSpawnOffset,"Legacy disabled by default");
        var profile=new EnemyBurstAttackSettings();profile.Shooting.CopyFrom(s);var pattern=ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();var projectile=ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            pattern.SetSettings(profile);var linked=new EnemyBurstAttackSettings();linked.ApplyPatternPreset(pattern,EnemyPresetUseMode.Linked);
            projectile.SetSettings(new EnemyShootingSettings());linked.AuthoredShooting.LinkProjectile(projectile);
            Check(linked.ResolvePresets().Shooting.EnableSpawnOffset,"Projectile preset does not erase linked Shooting offset");
            linked.AuthoredShooting.AttackPatternPresetOverrides.Add("shooting.baseOffsetX");Set(linked.AuthoredShooting,"baseOffsetX",5f);linked.Validate();
            Check((float)Get(linked.ResolvePresets().Shooting,"baseOffsetX")==5f,"Local Override priority preserved");
        }
        finally{UnityEngine.Object.DestroyImmediate(pattern);UnityEngine.Object.DestroyImmediate(projectile);}
        return new{passed=true,checks,layouts=4,fan=true,stack=true,sequential=true,randomScopes=4,presets=true};
    }
    static object Call(object o,string method,params object[] args) => o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Clock(Enemy enemy,float time)
    { object clock=new EnemyAttackClock();Set(clock,"initialized",true);Set(clock,"previousTime",Time.time);Set(clock,"attackTime",time);Set(enemy,"attackClock",clock); }
    static List<Vector3> Capture(EnemyProjectilePreviewWorld world)
    {
        var m=world.SimulationWorld.EntityManager;
        using var query=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));
        using var poses=query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var list=new List<Vector3>();foreach(var p in poses)list.Add(new Vector3(p.Position.x,p.Position.y,p.Position.z));
        list.Sort((a,b)=>{int x=a.x.CompareTo(b.x);return x!=0?x:a.y.CompareTo(b.y);});return list;
    }
    public static object NativePreviewProbe()
    {
        checks=0;
        foreach(var layout in new[]{EnemyEmissionLayoutType.Point,EnemyEmissionLayoutType.Radial,EnemyEmissionLayoutType.Circle,EnemyEmissionLayoutType.Sector})
        foreach(bool random in new[]{false,true}) NativePreview(layout,random);
        return new{passed=true,checks,layouts=4,legacyAndRandomPaths=true,nativeExecutor=true};
    }
    static void NativePreview(EnemyEmissionLayoutType layout,bool random)
    {
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Spawn offset Preview");var actor=new GameObject("Spawn offset native executor");actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
        using var runtime=new EnemyProjectilePreviewWorld();
        try
        {
            var wave=root.AddComponent<DirectedEnemySubWave>();var attack=root.GetComponent<DirectedWaveAttackBehaviour>()??root.AddComponent<DirectedWaveAttackBehaviour>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>();
            var bullet=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            Set(wave,"enemyPrefab",prefab);Set(wave,"enemyCount",1);
            var settings=(DirectedWaveAttackSettings)Get(attack,"attackSettings");Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(180);
            Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"waitForPreviousAttack",true);Set(settings,"delayAfterAttack",1000f);
            var profile=settings.WaveBurstSettings;profile.ConfigureLegacySingleAttack(1,1,1000);
            var shooting=profile.Shooting;shooting.ConfigureLegacyFan(3,-30,30);shooting.SetProjectileBaseSpeedOverride(true,2);Set(shooting,"numericRandomSeed",2468);
            Set(shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun);Set(shooting,"shotGunProjectileCount",2);
            Set(shooting,"enableSpawnOffset",true);Set(shooting,"baseOffsetY",.5f);Set(shooting,"stepOffsetX",.3f);
            if(random)
            {
                Range(shooting,"baseOffsetY",-.4f,.4f,EnemyNumericRandomizeOn.PerProjectile);
                var count=(RandomIntRange)Get(shooting,"fanProjectileCountRandom");count.enabled=true;count.initialized=true;count.min=2;count.max=4;count.randomizeOn=EnemyNumericRandomizeOn.PerDirection;
            }
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));shooting.EmissionLayout.Configure(layout,3);
            if(layout==EnemyEmissionLayoutType.Sector)shooting.EmissionLayout.ConfigureSector(-50,50,3,EnemySectorLaunchMode.Sequential,0,0);
            settings.Validate();actor.transform.position=wave.GetSimulationFormationPosition(0,root.transform);actor.transform.rotation=prefab.transform.rotation;actor.transform.localScale=prefab.transform.localScale;var enemy=actor.AddComponent<Enemy>();var executor=actor.AddComponent<SimpleEnemyAttackExecutor>();Set(executor,"defaultProjectilePrefab",bullet);Set(executor,"projectileSpawner",Get(runtime,"spawner"));
            Set(executor,"projectileSpawnOffset",Get(prefab.GetComponent<SimpleEnemyAttackExecutor>(),"projectileSpawnOffset"));
            var point=(Transform)Get(prefab.GetComponent<SimpleEnemyAttackExecutor>(),"projectileSpawnPoint");
            if(point!=null){var localPoint=new GameObject("Fixture shooting point");localPoint.transform.SetParent(actor.transform,false);localPoint.transform.localPosition=prefab.transform.InverseTransformPoint(point.position);Set(executor,"projectileSpawnPoint",localPoint.transform);}
            Call(executor,"Awake");actor.SetActive(true);executor.SetNumericOwner(0);executor.SetWaveAttackSettings(profile);
            executor.BeginEmissionOriginAttack(profile);executor.BeginAttackSequence(profile);Clock(enemy,0);
            Check(executor.PrepareEmissionOriginShot(profile,true)&&executor.TryFireStationary(Vector3.down,false,profile),"Native release");
            Call(executor,"TickPendingSectors",0f);
            using var preview=new WaveShootingPreviewSession(wave,true,false,0,Vector3.down*5);while(!preview.AdvanceTo(WaveShootingPreviewSession.StepInterval,1000d)){}
            runtime.Step(WaveShootingPreviewSession.StepInterval,WaveShootingPreviewSession.StepInterval,Vector3.down*5);
            var actual=Capture(runtime);var expected=Capture(preview.ProjectileWorld);
            Check(actual.Count==expected.Count&&actual.Count>0,"Native/Preview count "+layout+" random="+random+": "+actual.Count+"/"+expected.Count);
            foreach(var pose in actual){int found=expected.FindIndex(p=>(p-pose).sqrMagnitude<.000001f);Check(found>=0,"Native/Preview spawn "+layout+" random="+random+": "+pose);expected.RemoveAt(found);}
            executor.EndAttackSequence(profile);
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }

    public static object VariantsProbe()
    {
        checks=0;int before=World.All.Count;
        using(var world=new EnemyProjectilePreviewWorld())
        {
            var s=Settings();Set(s,"stepOffsetX",0f);Set(s,"baseOffsetX",.4f);
            var child=new EnemyChildAttackSettings();Set(child,"triggerValue",0f);
            child.Pattern.Shooting.CopyFrom(Settings());Set(child.Pattern.Shooting,"stepOffsetX",0f);Set(child.Pattern.Shooting,"baseOffsetX",.3f);
            s.SetChildAttack(child);world.Spawn(Requests(s,0));world.Step(.01f,.01f,Target);
            var poses=Capture(world);Check(poses.Count==2,"One parent and child");
            Check(poses.Exists(p=>Mathf.Abs(p.x-.4f)<.001f)&&poses.Exists(p=>Mathf.Abs(p.x-.7f)<.001f),"Child offset follows parent position and own direction");
        }
        foreach(var type in new[]{EnemyProjectileType.Composite,EnemyProjectileType.Laser})
        {
            using var world=new EnemyProjectilePreviewWorld();var s=Settings();Set(s,"baseOffsetX",.4f);Set(s,"baseOffsetY",.8f);s.SetProjectileType(type);
            var request=Requests(s,0);world.Spawn(request);var m=world.SimulationWorld.EntityManager;
            using var query=m.CreateEntityQuery(type==EnemyProjectileType.Composite?typeof(EnemyCompositeCenter):typeof(EnemyLaserState),typeof(LocalTransform));
            using var poses=query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            Check(poses.Length==1,"One "+type+" source");var point=poses[0].Position;Near(new Vector3(point.x,point.y,point.z),request[0].Position,type+" spawn offset");
        }
        Check(before==World.All.Count,"Owned worlds freed");return new{passed=true,checks,child=true,composite=true,laser=true};
    }

    public static object RunPlayProbe() { Check(Application.isPlaying,"Play Mode required");return NativePreviewProbe(); }
    public static object InspectorProbe()
    {
        checks=0;var preset=ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            using var serialized=new SerializedObject(preset);var p=serialized.FindProperty("projectile");
            var method=typeof(EnemyShootingSettingsDrawer).GetMethod("BehaviourFields",BindingFlags.Static|BindingFlags.NonPublic);
            bool Visible(){foreach(var field in (IEnumerable<string>)method.Invoke(null,new object[]{p}))if(field=="baseOffsetX")return true;return false;}
            Check(!Visible(),"Offset fields hidden by default");p.FindPropertyRelative("enableSpawnOffset").boolValue=true;
            p.FindPropertyRelative("baseOffsetX").floatValue=-.75f;serialized.ApplyModifiedPropertiesWithoutUndo();serialized.Update();
            Check(Visible(),"Offset fields visible when enabled");
            foreach(string name in new[]{"baseOffsetX","baseOffsetY","stepOffsetX","stepOffsetY"})
            {
                var f=typeof(EnemyShootingSettings).GetField(name,Flags);var attr=(RandomizableAttribute)Attribute.GetCustomAttribute(f,typeof(RandomizableAttribute));
                Check(attr!=null&&attr.Scopes==EnemyNumericScopes.All,"Shared Randomizable drawer/scope "+name);
                Check(p.FindPropertyRelative(attr.Range)!=null,"Serialized random range "+name);
            }
            p.FindPropertyRelative("enableSpawnOffset").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();serialized.Update();
            Check(!Visible()&&p.FindPropertyRelative("baseOffsetX").floatValue==-.75f,"Toggle preserves negative fractional value");
            return new{passed=true,checks,sharedNumericDrawer=true};
        }
        finally{UnityEngine.Object.DestroyImmediate(preset);}
    }

}

