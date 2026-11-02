using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyExpandedNumericRandomization
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(float a, float b, string message) => Check(Mathf.Abs(a-b) < .0002f, message + $": {a} / {b}");
    private static object Get(object value, string field) => value.GetType().GetField(field, Flags).GetValue(value);
    private static void Set(object value, string field, object data)
    { for (var type=value.GetType(); type!=null; type=type.BaseType) { var f=type.GetField(field,Flags); if(f!=null){f.SetValue(value,data);return;} var p=type.GetProperty(field,Flags);if(p!=null){p.SetValue(value,data);return;} } throw new Exception(field); }
    private static object Call(object value,string method,params object[] args) => value.GetType().GetMethod(method,Flags).Invoke(value,args);
    private static void Range(object value, string field, float min, float max, EnemyNumericRandomizeOn mode = EnemyNumericRandomizeOn.PerShot)
    {
        if (Get(value,field+"Random") is RandomFloatRange f) { f.enabled=true; f.min=min; f.max=max; f.randomizeOn=mode; f.initialized=true; }
        else { var i=(RandomIntRange)Get(value,field+"Random"); i.enabled=true; i.min=(int)min; i.max=(int)max; i.randomizeOn=mode; i.initialized=true; }
    }
    public static object Run()
    {
        checks=0; int previousSeed=NumericRandomPreviewControls.SeedOffset;
        NumericRandomPreviewControls.SetSeedOffset(0);
        try
        {
        Scopes(); Geometry(); Timing(); Rotation(); CopyAndPresets(); ChildAimCapture();
        foreach (var layout in new[] { EnemyEmissionLayoutType.Point, EnemyEmissionLayoutType.Radial, EnemyEmissionLayoutType.Circle, EnemyEmissionLayoutType.Sector }) NativePreview(layout);
        NativePreview(EnemyEmissionLayoutType.Circle, true);NativePreview(EnemyEmissionLayoutType.Sector, true);
        RuntimeSchedule(); AutonomousSchedule();
        return new { passed=true, checks, numericScopes=4, runtimePreviewLayouts=4, multipleRuntimePreview=true, linkedCopy=true, timing=true, child=true, nativeSchedulers=2 };
        }
        finally { NumericRandomPreviewControls.SetSeedOffset(previousSeed); }
    }
    private static void Scopes()
    {
        var range=new RandomFloatRange { enabled=true,min=-3,max=7 };
        foreach (var mode in (EnemyNumericRandomizeOn[])Enum.GetValues(typeof(EnemyNumericRandomizeOn)))
        {
            range.randomizeOn=mode;
            var a=new EnemyNumericSample(5,7,2,3); var b=new EnemyNumericSample(5,9,2,3);
            Near(a.Float(range,0,99), a.Float(range,0,99), "Stable immutable sample");
            Check((a.Float(range,0,99)==b.Float(range,0,99)) == (mode==EnemyNumericRandomizeOn.PerAttack), "Shot frequency "+mode);
            Check((a.Float(range,0,99)==a.ForDirection(4).Float(range,0,99)) == (mode==EnemyNumericRandomizeOn.PerAttack||mode==EnemyNumericRandomizeOn.PerShot), "Direction frequency "+mode);
            Check((a.Float(range,0,99)==a.ForProjectile(8).Float(range,0,99)) == (mode!=EnemyNumericRandomizeOn.PerProjectile), "Projectile frequency "+mode);
        }
        var ints=new RandomIntRange {enabled=true,min=8,max=3}; var stream=new NumericRandomStream(13); var seen=new HashSet<int>();
        for(int i=0;i<300;i++){int n=ints.Sample(0,ref stream);Check(n>=3&&n<=8,"Inclusive integer bounds");seen.Add(n);}
        Check(seen.Count==6,"All integer values reachable"); ints.min=ints.max=6; Check(ints.Sample(0,ref stream)==6,"Int Min=Max");
        ints.min=int.MinValue;ints.max=int.MaxValue;ints.Sample(0,ref stream); Check(true,"Whole Int32 range does not overflow");
        range.min=float.NaN; range.max=float.PositiveInfinity; Check(float.IsFinite(range.Sample(0,ref stream)),"Invalid float bounds normalized");
    }
    private static void Geometry()
    {
        var shooting=new EnemyShootingSettings(); shooting.ConfigureLegacyFan(3,-45,45);
        Range(shooting,"fanProjectileCount",3,8);Range(shooting,"fanMinAngle",-60,-20);Range(shooting,"fanMaxAngle",20,60);
        Set(shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun); Range(shooting,"shotGunProjectileCount",2,6,EnemyNumericRandomizeOn.PerDirection);
        shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle,4); Range(shooting.EmissionLayout,"radius",.2f,1.2f);
        Range(shooting.EmissionLayout,"centerOffsetX",-2,2); Range(shooting.EmissionLayout,"centerOffsetY",-1,1);
        Range(shooting,"projectileOrbitRadius",.1f,3,EnemyNumericRandomizeOn.PerProjectile);
        Range(shooting,"projectileAngularSpeed",10,100,EnemyNumericRandomizeOn.PerProjectile);
        var original=JsonUtility.ToJson(shooting);var state=new EnemyAttackNumericState(12345,0,1);state.BeginSpeed();
        var counts=new HashSet<int>();var requests=new List<EnemyProjectileSpawnRequest>();
        for(int shot=0;shot<16;shot++)
        {
            var numeric=state.PrepareSpeed(shooting,true,0);var geometry=shooting.SampleGeometry(numeric.Numeric);
            Check(geometry.FanProjectileCount>=3&&geometry.FanProjectileCount<=8,"Fan count range"); counts.Add(geometry.FanProjectileCount);
            float radius=geometry.EmissionLayout.Radius;
            for(int arm=0;arm<4;arm++)
            {
                var direction=geometry.SampleGeometry(numeric.Numeric.ForDirection(arm),true);requests.Clear();
                EnemyAttackProjectileBuilder.AppendEmitter(requests,null,direction,Vector3.zero,Vector3.zero,Vector3.down,1,1,0,default,randomSpeed:numeric,directionIndex:arm);
                Check(requests.Count%geometry.FanProjectileCount==0 && requests.Count>=2*geometry.FanProjectileCount&&requests.Count<=6*geometry.FanProjectileCount,"Per Direction Stack count");
                Near(direction.EmissionLayout.Radius,radius,"One radius for whole shot");
                Check(requests[0].OrbitRadius!=requests[1].OrbitRadius,"Per projectile Orbit radius");
                Check(requests[0].AngularSpeed!=requests[1].AngularSpeed,"Per projectile turn speed");
            }
            state.PrepareSpeed(shooting,false,0);Near(geometry.EmissionLayout.Radius,radius,"Later shot cannot change pending geometry");
        }
        Check(counts.Count>1,"Fan count changes between shots");Check(JsonUtility.ToJson(shooting)==original,"Sampling does not mutate authored settings");
        var layout=new EnemyEmissionLayoutSettings(); layout.ConfigureSector(-60,60,4,EnemySectorLaunchMode.Sequential,.2f,.1f); layout.ConfigureLaunchOrder(EnemySectorLaunchOrder.PingPong);
        Range(layout,"directionsCount",4,8);Range(layout,"startDelay",.1f,.3f);Range(layout,"launchInterval",.05f,.15f);
        var first=layout.SampleNumeric(new EnemyNumericSample(5,8));var second=layout.SampleNumeric(new EnemyNumericSample(5,9));
        var registry=new EnemySectorLaunchOrderRegistry();registry.BeginAttack(layout);var ticket=registry.Reserve(first);Check(ticket.GetLaunchIndex(first,0)==0,"Random geometry retains forward Sector order");ticket.Complete();
        var next=registry.Reserve(second);Check(next.GetLaunchIndex(second,0)==second.EmissionCount-1,"Random geometry retains PingPong identity");next.Complete();
    }
    private static void Timing()
    {
        var profile=new EnemyBurstAttackSettings();profile.Validate();Set(profile,"useAttackStartDelay",true);
        Range(profile,"attackStartDelay",.1f,.2f);Range(profile,"attackShotCount",3,8);Range(profile,"attackShotInterval",.2f,.4f);
        var registry=new EnemyAttackNumericRegistry();var state=registry.Get(profile);Check(state!=null,"Timing-only ranges create numeric state");
        var schedule=new EnemyAttackPatternSchedule();schedule.Begin(profile,0,1,true,state);int count=0;float previous=-1;
        for(float time=0;time<10&&!schedule.IsComplete;time+=.001f)
            if(schedule.IsDue(time)){if(previous>=0)Check(time-previous>=.1999f&&time-previous<=.402f,"Sampled interval applied to scheduler");else Check(time>=.1f&&time<=.202f,"Sampled initial delay");previous=time;count++;schedule.Advance(profile,time,1,0);}
        Check(count>=3&&count<=8,"Shot count sampled once at attack begin");
        var a=new EnemyAttackNumericState(9,0,1);var b=new EnemyAttackNumericState(9,0,1);
        var interval=profile.SampleShotInterval(a);profile.SampleStartDelay(a);Near(interval,profile.SampleShotInterval(b),"Timing channels independent");
        var wave=new DirectedWaveAttackSettings();Range(wave,"attacksPerSecond",2,5);Range(wave,"delayAfterAttack",.3f,.6f);
        var x=wave.CreateSchedulingNumeric();var y=wave.CreateSchedulingNumeric();
        for(int i=0;i<20;i++){float rate=wave.SampleAttacksPerSecond(x);Near(rate,wave.SampleAttacksPerSecond(y),"Dispatch Seed reproducibility");Check(rate>=2&&rate<=5,"Dispatch rate bounds");}
    }
    private static void Rotation()
    {
        var rotation=new EnemyRotationSettings();Range(rotation,"rotationSpeedDegreesPerSecond",20,80,EnemyNumericRandomizeOn.PerAttack);
        Range(rotation,"rotationAngle",90,180,EnemyNumericRandomizeOn.PerShot);Range(rotation,"rotationFromAngle",-45,0,EnemyNumericRandomizeOn.PerAttack);
        string original=JsonUtility.ToJson(rotation);var a=rotation.SampleNumeric(new EnemyNumericSample(13,19));var b=rotation.SampleNumeric(new EnemyNumericSample(13,20));
        Near(a.RotationSpeedDegreesPerSecond,b.RotationSpeedDegreesPerSecond,"Rotation speed held across attack");
        Near(a.RotationFromAngle,b.RotationFromAngle,"Rotation start held across attack");Check(a.RotationAngle!=b.RotationAngle,"Rotation target changes per shot");
        Check(a.RotationSpeedDegreesPerSecond>=20&&a.RotationSpeedDegreesPerSecond<=80&&a.RotationAngle>=90&&a.RotationAngle<=180,"Rotation ranges");
        var copy=new EnemyRotationSettings();copy.CopyFrom(rotation);copy.ScaleAttackTiming(2);
        Near(copy.SampleNumeric(new EnemyNumericSample(13,19)).RotationSpeedDegreesPerSecond,a.RotationSpeedDegreesPerSecond*2,"Sequence speed multiplier scales random rotation speed");
        Check(JsonUtility.ToJson(rotation)==original,"Rotation sampling/scaling does not mutate source");
    }
    private static void ChildAimCapture()
    {
        using var world=new EnemyProjectilePreviewWorld();var manager=world.SimulationWorld.EntityManager;
        var runner=new EnemyChildAttackRunner(manager,(EnemyProjectileEcsSpawner)Get(world,"spawner"));
        try
        {
            var child=new EnemyChildAttackSettings();Set(child,"fireMode",DirectedWaveAttackFireMode.Aimed);Set(child,"aimMode",DirectedWaveAttackAimMode.AimOnStart);
            var shooting=child.Pattern.Shooting;shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle,3);
            Range(shooting.EmissionLayout,"radius",.2f,1,EnemyNumericRandomizeOn.PerDirection);Range(shooting,"fanProjectileCount",2,5);
            var type=typeof(EnemyChildAttackRunner).GetNestedType("ParentState",BindingFlags.NonPublic);var state=Activator.CreateInstance(type);
            Set(state,"Settings",child);Set(state,"Numeric",new EnemyAttackNumericState(19,0,1));Set(state,"Forward",Vector3.up);
            var bullet=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();Set(state,"Prefabs",new EnemyProjectilePrefabSet(bullet));
            Set(runner,"hasTarget",true);Set(runner,"targetPosition",new Vector3(2,5));
            var profile=child.Pattern.RuntimeSettings;Call(runner,"QueueVolley",state,profile,0f,true);
            Vector3 captured=(Vector3)Get(state,"LockedTarget");Set(runner,"targetPosition",new Vector3(-5,-2));
            Call(runner,"QueueVolley",state,profile,.2f,false);Near(((Vector3)Get(state,"LockedTarget")-captured).magnitude,0,"Random child geometry retains Aim On Start target");
            var volleys=(System.Collections.IList)Get(state,"Volleys");Check(volleys.Count==2,"Both randomized aimed child volleys planned");var volley=volleys[1];var sample=(EnemyRandomSpeedSample)Get(volley,"NumericSpeed");
            var geometry=(EnemyShootingSettings)Get(volley,"Shooting");var directions=(Vector3[])Get(volley,"LockedDirections");
            for(int arm=0;arm<directions.Length;arm++)
            {
                var perDirection=geometry.SampleGeometry(sample.Numeric.ForDirection(arm),true);
                var position=(Vector3)Call(runner,"Position",state,perDirection,arm,.2f);
                Near((directions[arm]-(captured-position).normalized).magnitude,0,"Child aims from sampled per-direction emitter");
            }
        }
        finally{runner.Dispose();}
    }
    private static void CopyAndPresets()
    {
        var source=new EnemyBurstAttackSettings();source.Validate();Range(source,"attackShotCount",3,8); Range(source.Shooting,"fanProjectileCount",2,5);
        Range(source.Shooting,"projectileLifetime",4,9,EnemyNumericRandomizeOn.PerProjectile);
        var child=new EnemyChildAttackSettings();Range(child,"triggerValue",1,3);Range(child,"repeatCount",1,4);Range(child,"repeatInterval",.2f,.8f);
        Range(child,"spawnOffsetX",-2,2);Range(child,"spawnOffsetY",1,3);source.Shooting.SetChildAttack(child);
        var copy=new EnemyBurstAttackSettings();copy.CopyFrom(source);
        Check(!ReferenceEquals(copy.AttackShotCountRandom,source.AttackShotCountRandom),"Timing range copied deeply");
        var sampledA=child.SampleNumeric(91);var sampledB=child.SampleNumeric(91);Near(sampledA.TriggerValue,sampledB.TriggerValue,"Child stable Seed");
        Check(sampledA.TriggerValue>=1&&sampledA.TriggerValue<=3&&sampledA.RepeatCount>=1&&sampledA.RepeatCount<=4,"Child trigger/repeat ranges");
        Check(sampledA.SpawnOffset.x>=-2&&sampledA.SpawnOffset.x<=2&&sampledA.SpawnOffset.y>=1&&sampledA.SpawnOffset.y<=3,"Independent child offset axes");
        var projectile=ScriptableObject.CreateInstance<EnemyProjectilePreset>();var pattern=ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        try
        {
            projectile.SetSettings(source.Shooting); pattern.SetSettings(source);
            var linked=new EnemyBurstAttackSettings();linked.ApplyPatternPreset(pattern,EnemyPresetUseMode.Linked);linked.AuthoredShooting.LinkProjectile(projectile);
            Check(linked.AttackShotCountRandom.enabled&&Get(linked.Shooting,"projectileLifetimeRandom") is RandomFloatRange {enabled:true},"Both preset categories retain ranges");
            linked.AuthoredShooting.AttackPatternPresetOverrides.Add("attackShotCountRandom");Range(linked,"attackShotCount",6,6);linked.Validate();
            var resolved=linked.ResolvePresets();Near(resolved.AttackShotCountRandom.min,6,"Local random timing override");
            source.AttackShotCountRandom.min=2;pattern.SetSettings(source);Near(linked.ResolvePresets().AttackShotCountRandom.min,6,"Linked asset update retains local range");
            var independent=new EnemyBurstAttackSettings();independent.ApplyPatternPreset(pattern,EnemyPresetUseMode.Copy);independent.AuthoredShooting.LinkProjectile(projectile,EnemyPresetUseMode.Copy);
            source.AttackShotCountRandom.min=1;pattern.SetSettings(source);Near(independent.AttackShotCountRandom.min,2,"Copy mode retains independent timing range");
            source.EnableMultiplePatterns(); Check(source.GetPattern(0).AttackShotCountRandom.enabled,"Multiple conversion retains ranges");
            var roundtrip=JsonUtility.FromJson<EnemyBurstAttackSettings>(JsonUtility.ToJson(copy));roundtrip.Validate();
            Check(roundtrip.AttackShotCountRandom.enabled&&roundtrip.Shooting.ChildAttack.SampleNumeric(91).RepeatCount==sampledA.RepeatCount,"Serialized ranges and Child survive round trip");
        }
        finally{Object.DestroyImmediate(projectile);Object.DestroyImmediate(pattern);}
    }
    private static void NativePreview(EnemyEmissionLayoutType layout, bool multiple=false)
    {
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Expanded numeric Preview");var actor=new GameObject("Expanded numeric runtime");actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
        using var runtime=new EnemyProjectilePreviewWorld();
        try
        {
            var wave=root.AddComponent<DirectedEnemySubWave>();var attack=root.GetComponent<DirectedWaveAttackBehaviour>()??root.AddComponent<DirectedWaveAttackBehaviour>();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>();
            var bullet=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();Set(wave,"enemyPrefab",prefab);Set(wave,"enemyCount",1);
            var settings=(DirectedWaveAttackSettings)Get(attack,"attackSettings");Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(180);
            Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"waitForPreviousAttack",true);Set(settings,"delayAfterAttack",1000f);
            var profile=settings.WaveBurstSettings;profile.ConfigureLegacyRepeatedBurst(2,1000,.15f);Set(profile,"attackShotCount",2);Set(profile,"attackShotInterval",.25f);
            var shooting=profile.Shooting;shooting.ConfigureLegacyFan(3,-45,45);shooting.SetProjectileBaseSpeedOverride(true,2);Set(shooting,"numericRandomSeed",2468);
            Range(shooting,"fanProjectileCount",2,5);Range(shooting,"fanMinAngle",-60,-20);Range(shooting,"fanMaxAngle",20,60);
            Set(shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun);Range(shooting,"shotGunProjectileCount",2,4,EnemyNumericRandomizeOn.PerDirection);
            Range(shooting,"projectileAngularSpeed",10,80,EnemyNumericRandomizeOn.PerProjectile);Range(shooting,"projectileLateralSpeed",1,4,EnemyNumericRandomizeOn.PerProjectile);
            Set(shooting,"overrideProjectileLifetime",true);Set(shooting,"projectileLifetime",40f);shooting.EmissionLayout.Configure(layout,3);
            if(layout==EnemyEmissionLayoutType.Sector)shooting.EmissionLayout.ConfigureSector(-50,50,3,EnemySectorLaunchMode.Sequential,.1f,.2f);
            if(layout!=EnemyEmissionLayoutType.Point){Range(shooting.EmissionLayout,layout==EnemyEmissionLayoutType.Circle?"emitterCount":"directionsCount",2,4);Range(shooting.EmissionLayout,"startAngle",-15,15);}
            if(layout==EnemyEmissionLayoutType.Circle){Range(shooting.EmissionLayout,"radius",.2f,.7f);Range(shooting.EmissionLayout,"centerOffsetX",-1,1);}
            if(multiple){profile.EnableMultiplePatterns();var second=new EnemyAttackPatternSettings();second.CopyFrom(profile.GetPattern(0));Set(second.Shooting,"numericRandomSeed",9517);Range(second.Shooting,"fanProjectileCount",1,3);profile.AddPattern(second);}
            settings.Validate();var enemy=actor.AddComponent<Enemy>();var executor=actor.AddComponent<SimpleEnemyAttackExecutor>();Set(executor,"defaultProjectilePrefab",bullet);Set(executor,"projectileSpawner",Get(runtime,"spawner"));Call(executor,"Awake");actor.SetActive(true);executor.SetNumericOwner(0);
            executor.SetWaveAttackSettings(profile);
            for(int p=0;p<profile.PatternCount;p++){executor.BeginEmissionOriginAttack(profile.GetPattern(p));executor.BeginAttackSequence(profile.GetPattern(p));}
            for(int i=0;i<4;i++){Clock(enemy,new[]{0f,.15f,.4f,.55f}[i]);for(int p=0;p<profile.PatternCount;p++){var firing=profile.GetPattern(p);Check(executor.PrepareEmissionOriginShot(firing,i%2==0),"Native numeric origin ready");Check(executor.TryFireStationary(Vector3.down,false,firing),"Native numeric volley");}}
            Clock(enemy,2f);Call(executor,"TickPendingSectors",2f);
            using var preview=new WaveShootingPreviewSession(wave,true,false,0,Vector3.down*5);while(!preview.AdvanceTo(2f,1000d)){}
            var actual=Capture(runtime);var expected=Capture(preview.ProjectileWorld);Check(actual.Count==expected.Count&&actual.Count>0,"Native/Preview randomized count "+layout+" multiple="+multiple+": "+actual.Count+"/"+expected.Count);
            actual.Sort((a,b)=>a.z.CompareTo(b.z));expected.Sort((a,b)=>a.z.CompareTo(b.z));
            for(int i=0;i<actual.Count;i++){Near(actual[i].x,expected[i].x,"Native/Preview base speed");Near(actual[i].y,expected[i].y,"Native/Preview angular speed");Near(actual[i].z,expected[i].z,"Native/Preview lateral speed");}
            for(int p=0;p<profile.PatternCount;p++)executor.EndAttackSequence(profile.GetPattern(p));
        }
        finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    private static List<Vector3> Capture(EnemyProjectilePreviewWorld world)
    {
        using var q=world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));using var values=q.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
        var result=new List<Vector3>();foreach(var v in values)result.Add(new Vector3(v.ReferenceSpeed,v.AngularSpeedDegreesPerSecond,v.LateralSpeed));return result;
    }
    private static void Clock(Enemy enemy,float time)
    {object clock=new EnemyAttackClock();Set(clock,"initialized",true);Set(clock,"previousTime",Time.time);Set(clock,"attackTime",time);Set(enemy,"attackClock",clock);}
    private sealed class RecordingExecutor : IEnemyBurstAttackExecutor, IEnemyAttackNumericSource
    {
        public Enemy Enemy;
        public readonly List<float> Times=new();
        public EnemyBurstAttackSettings BurstAttackSettings { get; set; }
        public EnemyAttackNumericRegistry NumericRegistry { get; } = new();
        public bool CanPerformWaveAttack => true;
        public void SetNumericOwner(int slot) => NumericRegistry.Configure(slot);
        public void SetWaveAttackControl(bool value) { }
        public bool TryFireAt(Vector3 target,EnemyBurstAttackSettings profile){Times.Add(Enemy.AttackTime);return true;}
        public bool TryFireInDirection(Vector3 direction,EnemyBurstAttackSettings profile){Times.Add(Enemy.AttackTime);return true;}
    }
    private static EnemyBurstAttackSettings TimedProfile()
    {
        var profile=new EnemyBurstAttackSettings();profile.Validate();profile.Shooting.ConfigureLegacyFan(1,0,0);profile.Shooting.SetProjectileBaseSpeedOverride(true,2);
        Set(profile.Shooting,"numericRandomSeed",4321);Set(profile.Shooting,"overrideProjectileLifetime",true);Set(profile.Shooting,"projectileLifetime",40f);
        Set(profile,"useAttackStartDelay",true);Range(profile,"attackStartDelay",.2f,.5f);Range(profile,"attackShotCount",3,7);Range(profile,"attackShotInterval",.1f,.3f);
        profile.AttackCooldownRandom.CopyFrom(new RandomFloatRange{enabled=true,min=10,max=20});return profile;
    }
    private static void RuntimeSchedule()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_2_extrime.prefab");var actor=new GameObject("Random runtime scheduler");actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,root.scene);
        try
        {
            var wave=root.GetComponentInChildren<DirectedEnemySubWave>(true);var enemy=actor.AddComponent<Enemy>();actor.SetActive(true);
            var profile=TimedProfile();var settings=new DirectedWaveAttackSettings();Set(settings,"fireMode",DirectedWaveAttackFireMode.Default);
            var controller=Activator.CreateInstance(typeof(Wave).Assembly.GetType("DirectedWaveAttackController"),new object[]{wave,settings,null});
            var executor=new RecordingExecutor{Enemy=enemy,BurstAttackSettings=profile};((System.Collections.IDictionary)Get(controller,"executors"))[enemy]=executor;
            var stack=new Stack<System.Collections.IEnumerator>();stack.Push((System.Collections.IEnumerator)Call(controller,"FireFullAttack",enemy,executor,profile));
            for(float time=0;time<10&&stack.Count>0;time+=.01f){Clock(enemy,time);for(int guard=0;guard<100&&stack.Count>0;guard++){var routine=stack.Peek();if(!routine.MoveNext()){stack.Pop();continue;}if(routine.Current is System.Collections.IEnumerator nested)stack.Push(nested);else break;}}
            Check(stack.Count==0&&executor.Times.Count>=3&&executor.Times.Count<=7,"Wave controller consumes sampled shot count");
            Check(executor.Times[0]>=.2f&&executor.Times[0]<=.52f,"Wave controller consumes sampled start delay");
            for(int i=1;i<executor.Times.Count;i++)Check(executor.Times[i]-executor.Times[i-1]>=.099f&&executor.Times[i]-executor.Times[i-1]<=.32f,"Wave controller samples each interval");
            var ready=(Dictionary<Enemy,float>)Get(controller,"nextAttackReadyTimes");Check(ready[enemy]-executor.Times[executor.Times.Count-1]>=10,"Wave controller keeps cooldown sampling");
        }
        finally{Object.DestroyImmediate(actor);PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void AutonomousSchedule()
    {
        var scene=EditorSceneManager.NewPreviewScene();var actor=new GameObject("Random autonomous scheduler");actor.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
        using var world=new EnemyProjectilePreviewWorld();
        try
        {
            var enemy=actor.AddComponent<Enemy>();var executor=actor.AddComponent<SimpleEnemyAttackExecutor>();var profile=TimedProfile();
            Set(executor,"burstAttackSettings",profile);Set(executor,"defaultProjectilePrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>());
            Set(executor,"projectileSpawner",Get(world,"spawner"));Call(executor,"Awake");actor.SetActive(true);executor.NumericRegistry.Clear();Clock(enemy,0);Call(executor,"ResetAttackSchedule");
            var state=new EnemyAttackNumericState(4321,0,1);var schedule=new EnemyAttackPatternSchedule();schedule.Begin(profile,0,1,true,state);
            int expected=0;for(float time=0;time<5;time+=.01f){Clock(enemy,time);Call(executor,"TickAutonomousAttack",time);if(schedule.IsDue(time)){expected++;schedule.Advance(profile,time,1,1000);}}
            Check(world.Count==expected&&expected>=3&&expected<=7,"Autonomous executor matches shared sampled schedule: "+world.Count+"/"+expected);
        }
        finally{Object.DestroyImmediate(actor);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
