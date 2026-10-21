using System;
using System.Collections;
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

public static class VerifyEnemyLaser
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, scenarios;
    private static EnemyBullet bullet;
    private static readonly Vector3 Far = new(100f, 100f);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .002f, message + ": " + actual + "/" + expected);
    private static void Near(Vector3 actual, Vector3 expected, string message) => Check(Vector3.Distance(actual, expected) < .002f, message + ": " + actual + "/" + expected);
    private static EnemyShootingSettings Shooting(bool telegraph = false)
    {
        var s = new EnemyShootingSettings(); Set(s, "projectileType", EnemyProjectileType.Laser);
        Set(s.Laser, "enableTelegraph", telegraph); Set(s.Laser, "laserDuration", 1f);
        Set(s.Laser, "fadeOutDuration", .2f); Set(s.Laser.Geometry, "lengthMode", EnemyLaserLengthMode.Custom);
        Set(s.Laser.Geometry, "laserLength", 10f); Set(s.Laser.Geometry, "laserWidth", .2f);
        return s;
    }
    private static Entity Spawn(EnemyProjectilePreviewWorld world, EnemyShootingSettings shooting, Vector3? origin = null,
        Vector3? direction = null, EnemyLaserOrigin source = default, float multiplier = 1f)
    {
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, origin ?? Vector3.zero, origin ?? Vector3.zero,
            direction ?? Vector3.right, multiplier, 3f, 0f, new EnemyProjectilePrefabSet(bullet), laserOrigin: source);
        world.Spawn(requests);
        using var q = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyLaserState));
        using var entities = q.ToEntityArray(Allocator.Temp); return entities[entities.Length - 1];
    }
    private static EnemyLaserState State(EnemyProjectilePreviewWorld world, Entity entity)
        => world.SimulationWorld.EntityManager.GetComponentData<EnemyLaserState>(entity);
    private static int Events(EnemyProjectilePreviewWorld world, out float damage)
    {
        var m = world.SimulationWorld.EntityManager;
        using var q = m.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
        var buffer = m.GetBuffer<EnemyProjectileResolutionEvent>(q.GetSingletonEntity());
        damage = buffer.Length > 0 ? buffer[0].Damage : 0f;
        foreach (var e in buffer) Check(e.UseConfiguredDamage == 1, "Authored damage opts into existing protection pipeline");
        return buffer.Length;
    }
    public static object Run()
    {
        checks = scenarios = 0;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        Geometry(); Timing(); CurvesAndFollow(); Damage(); Protection(); Children(); AdvancedLifecycle(); RuntimeAndModifiers(); Copies();
        return new { passed = true, checks, scenarios, tested = "ray viewport geometry, telegraph/delay/fades, frozen/animated dimensions, source lifetime and locked direction, contact/periodic damage, ECS runtime, Fan/Stack/Sector, child triggers and nesting, serialization/copies" };
    }
    private static void Geometry()
    {
        var bounds = Rect.MinMaxRect(-3f, -5f, 3f, 5f);
        foreach (var start in new[] { Vector2.zero, new Vector2(0, 9), new Vector2(-7, 0), new Vector2(3, 5) })
        for (int i = 0; i < 32; i++)
        {
            scenarios++; Vector2 direction = Quaternion.Euler(0, 0, i * 360f / 32) * Vector3.up;
            float length = EnemyLaserGeometryUtility.ToScreenEdge(start, direction, bounds);
            Check(!float.IsNaN(length) && length >= 0f, "Valid length for inside/outside ray");
            if (length <= .0001f) continue;
            Vector2 end = start + direction * length;
            Check(Mathf.Abs(end.x - bounds.xMin) < .002f || Mathf.Abs(end.x - bounds.xMax) < .002f
                || Mathf.Abs(end.y - bounds.yMin) < .002f || Mathf.Abs(end.y - bounds.yMax) < .002f, "Ray ends on boundary");
            Check(end.x >= bounds.xMin - .002f && end.x <= bounds.xMax + .002f && end.y >= bounds.yMin - .002f && end.y <= bounds.yMax + .002f, "Endpoint lies within viewport");
        }
        Near(EnemyLaserGeometryUtility.ToScreenEdge(new Vector2(0, 9), Vector2.down, bounds), 14, "Outside ray reaches far edge");
        Near(EnemyLaserGeometryUtility.ToScreenEdge(new Vector2(0, 9), Vector2.up, bounds), 0, "Outside ray away from viewport is empty");
        Near(EnemyLaserGeometryUtility.ToScreenEdge(Vector2.zero, Vector2.zero, bounds), 0, "Zero direction safe");
        var beam = new EnemyLaserState { Start = float2.zero, Direction = new float2(1, 0), Length = 10, Width = .2f };
        foreach (float x in new[] { -.2f, 0f, 5f, 10f, 10.2f }) foreach (float y in new[] { -.2f, -.1f, 0f, .1f, .2f })
        {
            scenarios++; bool hit = EnemyLaserGeometryUtility.Hits(beam, new EnemyProjectileCollisionTarget
            { IsCircle = 1, Center = new float2(x, y), PreviousCenter = new float2(x, y), Radius = .025f });
            Check(hit == (x >= 0f && x <= 10f && Mathf.Abs(y) <= .1f), "Rectangle geometry has correct ends and width");
        }
        Check(EnemyLaserGeometryUtility.Hits(beam, new EnemyProjectileCollisionTarget { IsCircle = 1,
            PreviousCenter = new float2(5, -3), Center = new float2(5, 3), Radius = .05f }), "Swept player cannot skip beam");
    }
    private static void Timing()
    {
        scenarios++; using var w = new EnemyProjectilePreviewWorld(); var s = Shooting(true);
        Set(s.Laser, "activationDelay", .2f); Set(s.Laser, "telegraphDuration", .5f);
        var e = Spawn(w, s); w.Step(.1f, .1f, new Vector3(5, 0));
        Check(State(w,e).Active == 0 && State(w,e).Telegraph == 0, "Silent activation delay");
        w.Step(.3f, .2f, new Vector3(5,0)); Check(State(w,e).Telegraph == 1 && State(w,e).Active == 0, "Warning stage");
        Check(Events(w,out _) == 0, "Warning never damages");
        w.Step(.701f, .401f, new Vector3(5,0)); Check(State(w,e).Active == 1 && State(w,e).Telegraph == 0, "Activation after warning");
        Check(Events(w,out _) == 1, "Damage starts at activation");
        w.Step(1.71f, 1.009f, new Vector3(5,0)); Check(State(w,e).Active == 0, "Fade tail is harmless");
        Check(Events(w,out _) == 0, "No damage during visual tail");
        w.Step(1.901f, .191f, Far); Check(!w.SimulationWorld.EntityManager.Exists(e), "Beam cleaned after tail");
    }
    private static void CurvesAndFollow()
    {
        var source = new GameObject("Laser source verification") { hideFlags = HideFlags.HideAndDontSave };
        var player = new GameObject("Laser player verification") { hideFlags = HideFlags.HideAndDontSave };
        var cameraObject = new GameObject("Laser bounds verification") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0,0,-10); camera.aspect = .6f; camera.rect = new Rect(0,.2f,1,.8f);
        try
        {
            foreach (bool constant in new[] { true, false })
            {
                scenarios++; using var w = new EnemyProjectilePreviewWorld(); var s = Shooting();
                Set(s.Laser, "followOrigin", true); Set(s.Laser.Geometry,"lengthMode",EnemyLaserLengthMode.ToScreenEdge);
                Set(s.Laser.Geometry,"constantLength",constant); Set(s.Laser.Geometry,"constantWidth",false);
                Set(s.Laser.Geometry,"widthCurve",AnimationCurve.Linear(0,0,1,2));
                source.transform.position = Vector3.zero;
                var e = Spawn(w,s,source: new EnemyLaserOrigin(source.transform,Vector3.zero,s.EmissionOrigin,camera));
                w.Step(0,0,Far,camera); Near(State(w,e).Length,3,"Initial automatic length");
                source.transform.position = Vector3.right; source.transform.rotation = Quaternion.Euler(0,0,90);
                w.Step(.5f,.5f,Far,camera); var state = State(w,e);
                Near(new Vector3(state.Start.x,state.Start.y),Vector3.right,"Follow translation");
                Near(new Vector3(state.Direction.x,state.Direction.y),Vector3.right,"Released direction never rotates");
                Near(state.Length,constant?3:2,"Constant freezes length; animated recalculates viewport"); Near(state.Width,.2f,"Normalized width curve");
            }
            foreach (bool survive in new[] { true, false })
            {
                scenarios++; using var w = new EnemyProjectilePreviewWorld(); var s = Shooting(true); Set(s.Laser,"surviveSourceDestruction",survive);
                source.SetActive(true); var e = Spawn(w,s,source:new EnemyLaserOrigin(source.transform,source.transform.position,s.EmissionOrigin,camera));
                source.SetActive(false); w.Step(.1f,.1f,Far,camera);
                Check(w.SimulationWorld.EntityManager.Exists(e)==survive,"Source removal policy during Telegraph");
            }
            source.SetActive(true); source.transform.position=Vector3.zero;
            scenarios++; using (var w = new EnemyProjectilePreviewWorld())
            {
                var s=Shooting(); Set(s.Laser,"followOrigin",true);
                s.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.Player); s.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,0,EnemyEmissionOriginReferenceMode.Boundary,true);
                player.transform.position=Vector3.zero;
                var e=Spawn(w,s,new Vector3(0,5),source:new EnemyLaserOrigin(source.transform,Vector3.zero,s.EmissionOrigin,camera,player:player.transform));
                source.transform.position=new Vector3(100,100); player.transform.position=new Vector3(2,3); camera.orthographicSize=6;
                w.Step(.2f,.2f,Far,camera); var state=State(w,e);
                Near(state.Start.x,2,"Independent player X follows player"); Near(state.Start.y,6,"World Boundary Y follows viewport");
            }
            scenarios++; using (var w = new EnemyProjectilePreviewWorld())
            {
                var s=Shooting(); Set(s.Laser.Geometry,"constantLength",false); Set(s.Laser.Geometry,"lengthCurve",AnimationCurve.Linear(0,.5f,1,1.5f));
                var e=Spawn(w,s); w.Step(.5f,.5f,Far); Near(State(w,e).Length,10,"Length curve multiplies base dimension");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
    private static void Damage()
    {
        foreach (var mode in new[] {EnemyLaserDamageMode.OnContact,EnemyLaserDamageMode.Periodic})
        {
            scenarios++; using var w=new EnemyProjectilePreviewWorld(); var s=Shooting(); Set(s.Laser,"damageMode",mode);
            Set(s.Laser,"damage",2f); Set(s.Laser,"damageInterval",.25f); Spawn(w,s,multiplier:1.5f);
            w.Step(.01f,.01f,new Vector3(5,0)); Check(Events(w,out float damage)==1,"First contact damages"); Near(damage,3,"Authored damage and wave multiplier");
            w.Step(.1f,.09f,new Vector3(5,0)); Check(Events(w,out _)==0,"No per-frame damage");
            w.Step(.27f,.17f,new Vector3(5,0)); Check(Events(w,out _)==(mode==EnemyLaserDamageMode.Periodic?1:0),"Periodic interval / contact latch");
            w.Step(.3f,.03f,Far); w.Step(.6f,.3f,new Vector3(5,0)); Check(Events(w,out _)==1,"Reentry damages once / periodic interval");
        }
    }
    private static void Children()
    {
        foreach (var evt in new[] {EnemyChildAttackEvent.OnSpawn,EnemyChildAttackEvent.HitPlayer,EnemyChildAttackEvent.Expired})
        {
            scenarios++; using var w=new EnemyProjectilePreviewWorld(); var s=Shooting();
            var child=new EnemyChildAttackSettings(); Set(child,"triggerMode",EnemyChildAttackTriggerMode.OnEvent); Set(child,"eventType",evt);
            Set(child.Pattern.Shooting,"overrideProjectileLifetime",true); Set(child.Pattern.Shooting,"projectileLifetime",20f);
            s.SetChildAttack(child); var e=Spawn(w,s); w.Step(.01f,.01f,evt==EnemyChildAttackEvent.HitPlayer?new Vector3(5,0):Far);
            if(evt==EnemyChildAttackEvent.Expired) w.Step(1.21f,1.2f,Far);
            using var q=w.SimulationWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc {All=new[]{ComponentType.ReadOnly<EnemyProjectileStaticData>()},None=new[]{ComponentType.ReadOnly<EnemyLaserState>()}});
            Check(q.CalculateEntityCount()==1,"Applicable child event: "+evt);
            if(evt==EnemyChildAttackEvent.HitPlayer) Check(w.SimulationWorld.EntityManager.Exists(e),"Child hit does not consume beam");
        }
        scenarios++; using(var w=new EnemyProjectilePreviewWorld())
        {
            var root=new EnemyShootingSettings(); var child=new EnemyChildAttackSettings(); Set(child,"triggerValue",0f);
            Set(child.Pattern.Shooting,"projectileType",EnemyProjectileType.Laser); Set(child.Pattern.Shooting.Laser,"enableTelegraph",false);
            root.SetChildAttack(child); var requests=new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests,bullet,root,Vector3.zero,Vector3.zero,Vector3.right,1,1,0,new EnemyProjectilePrefabSet(bullet));
            w.Spawn(requests); w.Step(.1f,.1f,Far);
            using var q=w.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyLaserState)); Check(q.CalculateEntityCount()==1,"Ordinary projectile creates nested laser through existing builder");
        }
    }
    private static void Protection()
    {
        scenarios++; var root=new GameObject("Laser damage protection verification"){hideFlags=HideFlags.HideAndDontSave};
        try
        {
            root.SetActive(false);var ship=root.AddComponent<ParentShip>();
            Set(ship,"<MaximumHealthPoints>k__BackingField",20f);Set(ship,"<MaximumShieldPoints>k__BackingField",20f);
            Set(ship,"currentHealthPoints",20f);Set(ship,"currentShieldPoints",10f);Set(ship,"damageInvulnerableUntil",0f);
            Set(ship,"healthDamageProjectileClearRadius",0f);
            Check(ship.TryTakeConfiguredDamage(3f),"Configured Laser damage accepted by ship");Near(ship.CurrentShieldPoints,7,"Laser respects shields");Near(ship.CurrentHealthPoints,20,"Shields protect hull");
            Check(!ship.TryTakeConfiguredDamage(3f),"Existing invulnerability rejects next beam hit");
            Set(ship,"damageInvulnerableUntil",0f);Set(ship,"currentShieldPoints",0f);
            Check(ship.TryTakeConfiguredDamage(3f),"Laser damage reaches hull");Near(ship.CurrentHealthPoints,17,"Configured hull damage retained");
            Set(ship,"damageInvulnerableUntil",0f);Check(ship.TryTakeDamage(100f),"Legacy hit accepted");Near(ship.CurrentHealthPoints,16,"Legacy enemy hits remain one unit");
            Set(ship,"damageInvulnerableUntil",0f);ship.OnDamagePipeline+=damage=>damage*.5f;
            Check(ship.TryTakeConfiguredDamage(2f),"Existing defensive modifiers run");Near(ship.CurrentHealthPoints,15,"Existing damage pipeline transforms configured amount");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void AdvancedLifecycle()
    {
        foreach(bool overwrite in new[]{false,true})
        {
            scenarios++;using var w=new EnemyProjectilePreviewWorld();var s=Shooting(true);
            var own=(EnemyLaserGeometry)Get(s.Laser,"telegraphGeometry");Set(own,"lengthMode",EnemyLaserLengthMode.Custom);
            Set(own,"laserWidth",1.2f);Set(own,"laserLength",3f);Set(s.Laser,"overrideTelegraphGeometry",overwrite);
            Set(s.Laser,"telegraphDuration",.3f);var e=Spawn(w,s);w.Step(.1f,.1f,Far);
            Near(State(w,e).Width,overwrite?1.2f:.2f,"Warning reuses main geometry unless explicitly overridden");
            Near(State(w,e).Length,overwrite?3:10,"Warning length override");
            var direction=State(w,e).Direction;w.Step(.31f,.21f,Far);
            Near(State(w,e).Width,.2f,"Main geometry restored at activation");Near(State(w,e).Length,10,"Main length restored at activation");
            Check(math.distance(direction,State(w,e).Direction)<.0001f,"Telegraph transition preserves released direction");
        }
        scenarios++;using(var w=new EnemyProjectilePreviewWorld())
        {
            var s=Shooting();Set(s.Laser,"laserDuration",.1f);Set(s.Laser,"fadeOutDuration",0f);
            var child=new EnemyChildAttackSettings();Set(child,"triggerValue",0f);Set(child,"destroyParent",true);
            Set(child.Pattern,"attackShotCount",2);Set(child.Pattern,"attackShotInterval",2f);child.Pattern.Validate();s.SetChildAttack(child);
            var e=Spawn(w,s);w.Step(.01f,.01f,Far);w.Step(.2f,.19f,Far);
            Check(w.SimulationWorld.EntityManager.Exists(e),"Destroy Parent holds lifetime until scheduled child tail");
            Check(State(w,e).Active==0,"Held visual parent cannot damage after active duration");
            w.Step(2.1f,1.9f,Far);Check(!w.SimulationWorld.EntityManager.Exists(e),"Destroy Parent completes after delayed child volley");
        }
        scenarios++;using(var w=new EnemyProjectilePreviewWorld())
        {
            var s=Shooting();Set(s.Laser.Geometry,"laserOffset",new Vector2(4,0));var child=new EnemyChildAttackSettings();
            Set(child,"triggerMode",EnemyChildAttackTriggerMode.AfterDistance);Set(child,"triggerValue",1f);s.SetChildAttack(child);
            Spawn(w,s);w.Step(.1f,.1f,Far);Check(w.Count==1,"Spawn offset is not counted as travelled distance");
        }
        foreach(bool normal in new[]{false,true})foreach(bool destroysNormal in new[]{false,true})
        {
            scenarios++;using var w=new EnemyProjectilePreviewWorld();var s=Shooting();Set(s,"overwriteProjectileCharacteristic",true);
            Set(s,"projectileCharacteristic",normal?EnemyProjectileCharacteristic.Normal:EnemyProjectileCharacteristic.Fragile);
            var e=Spawn(w,s);var m=w.SimulationWorld.EntityManager;using var q=m.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
            var targets=m.GetBuffer<EnemyProjectileCollisionTarget>(q.GetSingletonEntity());
            targets.Add(new EnemyProjectileCollisionTarget{Kind=EnemyProjectileCollisionTargetKind.Interceptor,TargetId=77,IsCircle=1,
                Center=new float2(5,0),PreviousCenter=new float2(5,0),Radius=.05f,DestroysNormal=destroysNormal?(byte)1:(byte)0});
            w.SimulationWorld.SetTime(new Unity.Core.TimeData(.1,.1f));w.SimulationWorld.GetOrCreateSystemManaged<EnemyLaserSystem>().Update();
            var resolution=m.GetComponentData<EnemyProjectileResolution>(e);
            Check((resolution.Kind==EnemyProjectileResolutionKind.Intercepted)==(!normal||destroysNormal),"Beam intercepts along its full length using existing characteristic contract");
        }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly Scene Scene=EditorSceneManager.NewPreviewScene();
        public readonly World World=new("Enemy Laser runtime verification");
        public readonly EnemyProjectileEcsSpawner Spawner;
        public readonly GameObject Root;
        public readonly SimpleEnemyAttackExecutor Executor;
        public Fixture()
        {
            Root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"),Scene);
            Root.transform.rotation=Quaternion.identity; Executor=Root.GetComponent<SimpleEnemyAttackExecutor>();
            Spawner=EnemyProjectileEcsSpawner.CreateEditorPreview(World); Set(Executor,"projectileSpawner",Spawner); Call(Executor,"Awake");
            object clock=new EnemyAttackClock(); Set(clock,"initialized",true); Set(clock,"previousTime",Time.time); Set(clock,"attackTime",0f);
            Set(Root.GetComponent<Enemy>(),"attackClock",clock);
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(Root);Spawner.Dispose();World.Dispose();EditorSceneManager.ClosePreviewScene(Scene);}
    }
    private static void RuntimeAndModifiers()
    {
        foreach(var layout in new[]{EnemyEmissionLayoutType.Point,EnemyEmissionLayoutType.Radial,EnemyEmissionLayoutType.Circle,EnemyEmissionLayoutType.Sector})
        {
            scenarios++; using var f=new Fixture(); var p=new EnemyBurstAttackSettings(); p.Shooting.CopyFrom(Shooting());
            p.Shooting.EmissionLayout.Configure(layout,4); p.Shooting.ConfigureLegacyFan(3,-20,20);
            Set(p.Shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun); Set(p.Shooting,"shotGunProjectileCount",2);
            f.Executor.SetWaveAttackSettings(p); f.Executor.BeginAttackSequence(p); f.Executor.BeginEmissionOriginAttack(p);
            Check(f.Executor.TryFireInDirection(Vector3.up,p),"Native executor accepts laser "+layout);
            using var q=f.World.EntityManager.CreateEntityQuery(typeof(EnemyLaserState)); Check(q.CalculateEntityCount()==p.Shooting.ProjectilesPerVolley,"Native layout/Fan/Stack counts "+layout);
            f.Executor.EndAttackSequence(p); Check(q.CalculateEntityCount()>0,"Stopping attack preserves released beams");
            f.World.SetTime(new Unity.Core.TimeData(.5,.5f)); f.World.GetOrCreateSystemManaged<EnemyLaserSystem>().Update();
            using var states=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp);
            foreach(var state in states) Check(state.Active==1 && math.lengthsq(state.Direction)>.9f,"Native runtime system ticks all beams");
        }
        scenarios++; using(var f=new Fixture())
        {
            var p=new EnemyBurstAttackSettings(); p.Shooting.CopyFrom(Shooting());
            p.Shooting.EmissionLayout.ConfigureSector(-30,30,3,EnemySectorLaunchMode.Sequential,.1f,.2f);
            f.Executor.SetWaveAttackSettings(p); f.Executor.BeginAttackSequence(p);f.Executor.BeginEmissionOriginAttack(p);
            Check(f.Executor.TryFireInDirection(Vector3.up,p),"Sequential Sector queued");
            using var q=f.World.EntityManager.CreateEntityQuery(typeof(EnemyLaserState)); Check(q.CalculateEntityCount()==0,"Sector start delay retained");
            object clock=new EnemyAttackClock(); Set(clock,"initialized",true);Set(clock,"previousTime",Time.time);Set(clock,"attackTime",0f);Set(f.Root.GetComponent<Enemy>(),"attackClock",clock);
            Call(f.Executor,"TickPendingSectors",.11f); Check(q.CalculateEntityCount()==1,"First Sector beam released");
            Call(f.Executor,"TickPendingSectors",.51f); Check(q.CalculateEntityCount()==3,"Sequential interval retained");
        }
        scenarios++;using(var f=new Fixture())
        {
            Set(f.Executor,"defaultProjectilePrefab",null);var p=new EnemyBurstAttackSettings();p.Shooting.CopyFrom(Shooting());
            f.Executor.SetWaveAttackSettings(p);f.Executor.BeginAttackSequence(p);f.Executor.BeginEmissionOriginAttack(p);
            Check(f.Executor.CanPerformWaveAttack&&f.Executor.TryFireInDirection(Vector3.up,p),"Laser needs no authored projectile prefab");
            using var q=f.World.EntityManager.CreateEntityQuery(typeof(EnemyLaserState));Check(q.CalculateEntityCount()==1,"Shared quad laser spawned without prefab");
        }
    }
    private static void Copies()
    {
        scenarios++; var s=Shooting(true); Set(s.Laser,"damage",3.5f); Set(s.Laser.Geometry,"laserWidth",.8f);
        var copy=new EnemyShootingSettings();copy.CopyFrom(s);Near(copy.Laser.Damage,3.5f,"Pattern copy damage");Near(copy.Laser.Geometry.LaserWidth,.8f,"Pattern copy geometry");
        Set(s.Laser.Geometry,"laserWidth",1.2f); Near(copy.Laser.Geometry.LaserWidth,.8f,"Live beams retain independent resolved profile snapshot");
        var json=JsonUtility.ToJson(copy);var restored=JsonUtility.FromJson<EnemyShootingSettings>(json);Near(restored.Laser.Damage,3.5f,"JSON roundtrip");
        Check(JsonUtility.FromJson<EnemyShootingSettings>("{\"projectileType\":0}").ProjectileType==EnemyProjectileType.Default,"Old projectile enum unchanged");
        Check(new EnemyLaserSettings().Geometry.LengthMode==EnemyLaserLengthMode.ToScreenEdge && new EnemyLaserSettings().Geometry.ConstantLength && new EnemyLaserSettings().Geometry.ConstantWidth,"Requested defaults");
        var preset=ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();var restoredPreset=ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        var root=new GameObject("Laser Local Override verification"){hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var settings=new DirectedWaveAttackSettings();settings.WaveBurstSettings.Shooting.CopyFrom(copy);preset.SetAttackSettings(settings);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(preset),restoredPreset);
            Near(restoredPreset.AttackSettings.WaveBurstSettings.Shooting.Laser.Damage,3.5f,"Preset native Unity serialization retains Laser");
            var local=root.AddComponent<DirectedWaveEnemyOverride>();local.InitializeAttackPatternOverride(settings,settings.WaveBurstSettings);
            var overridden=new EnemyBurstAttackSettings();local.ApplyAttackPatternOverride(overridden,9f);
            Check(overridden.Shooting.ProjectileType==EnemyProjectileType.Laser,"Local Override selects Laser");
            Near(overridden.Shooting.Laser.Geometry.LaserWidth,.8f,"Local Override copies Laser geometry through existing priority path");
            Near(overridden.Shooting.Laser.Damage,3.5f,"Shared projectile speed does not alter Laser damage");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(preset);UnityEngine.Object.DestroyImmediate(restoredPreset);}
    }
}
