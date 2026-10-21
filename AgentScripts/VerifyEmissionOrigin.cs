using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyEmissionOrigin
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    sealed class Context : IEnemyEmissionOriginContext
    {
        public Vector3 Position;
        public Camera Camera;
        public Vector3 AttackTargetPosition => Position;
        public bool HasEmissionOriginPlayer => true;
        public Camera EmissionOriginCamera => Camera;
    }
    struct Shot { public Vector3 Position, Direction; }
    static void Check(bool value,string message){ checks++;if(!value)throw new Exception(message); }
    static void Near(Vector3 actual,Vector3 expected,string message)=>Check((actual-expected).sqrMagnitude<.00002f,message+" "+actual+" != "+expected);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static object Get(object target,string field)=>target.GetType().GetField(field,Private|BindingFlags.Public).GetValue(target);
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    public static object Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Requires idle Editor");
        checks=0; Coordinates(); IndependentCapture(); Copies(); Runtime(); return new {passed=true,checks};
    }
    static void Coordinates()
    {
        var settings=new EnemyEmissionOriginSettings();var enemy=new Vector3(2,3,0);var player=new Vector3(-4,-5,0);var bounds=Rect.MinMaxRect(-3,-7,3,7);
        Near(settings.Resolve(enemy,player,bounds),enemy,"Legacy defaults");
        foreach(EnemyEmissionOriginSource x in Enum.GetValues(typeof(EnemyEmissionOriginSource)))
        foreach(EnemyEmissionOriginSource y in Enum.GetValues(typeof(EnemyEmissionOriginSource)))
        {settings.X.Configure(x,1);settings.Y.Configure(y,-1);Near(settings.Resolve(enemy,player,bounds),new Vector3(x==EnemyEmissionOriginSource.Enemy?3:x==EnemyEmissionOriginSource.Player?-3:1,y==EnemyEmissionOriginSource.Enemy?2:y==EnemyEmissionOriginSource.Player?-6:-1,0),"Independent axis sources");}
        settings.X.Configure(EnemyEmissionOriginSource.Player);settings.Y.Configure(EnemyEmissionOriginSource.World,-1,EnemyEmissionOriginReferenceMode.Boundary,true);
        Near(settings.Resolve(enemy,player,bounds),new Vector3(-4,8,0),"Above player outside top boundary");
        foreach(bool upper in new[]{false,true})foreach(float offset in new[]{-1f,1f})
        {settings.X.Configure(EnemyEmissionOriginSource.World,offset,EnemyEmissionOriginReferenceMode.Boundary,upper);settings.Y.Configure(EnemyEmissionOriginSource.World,offset,EnemyEmissionOriginReferenceMode.Boundary,upper);Near(settings.Resolve(enemy,player,bounds),new Vector3(upper?3-offset:-3+offset,upper?7-offset:-7+offset,0),"Boundary offsets point inward");}
        settings.X.Configure(EnemyEmissionOriginSource.Player);settings.Y.Configure(EnemyEmissionOriginSource.Player);
        foreach(EnemyEmissionOriginCaptureMode mode in Enum.GetValues(typeof(EnemyEmissionOriginCaptureMode)))
        {
            settings.SetCaptureMode(mode);EnemyEmissionOriginCaptureState state=default;state.Begin(settings,new Vector3(1,1,0));
            Near(state.Prepare(settings,Vector3.one*2,false),mode==EnemyEmissionOriginCaptureMode.OnShot?new Vector3(2,2,0):new Vector3(1,1,0),"Capture held within burst");
            Near(state.Prepare(settings,Vector3.one*3,true),mode==EnemyEmissionOriginCaptureMode.OnAttackStart?new Vector3(1,1,0):new Vector3(3,3,0),"Burst refresh");
            Near(state.Prepare(settings,Vector3.one*4,false),mode==EnemyEmissionOriginCaptureMode.OnAttackStart?new Vector3(1,1,0):mode==EnemyEmissionOriginCaptureMode.OnShot?new Vector3(4,4,0):new Vector3(3,3,0),"Capture held after refresh");
        }
    }
    static void IndependentCapture()
    {
        var settings = new EnemyEmissionOriginSettings(); settings.X.Configure(EnemyEmissionOriginSource.Player); settings.Y.Configure(EnemyEmissionOriginSource.Player);
        settings.X.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnAttackStart); settings.Y.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnShot);
        EnemyEmissionOriginCaptureState state=default; state.Begin(settings,new Vector3(1,2,0));
        Near(state.Prepare(settings,new Vector3(3,4,0),false),new Vector3(1,4,0),"X attack capture independent of Y shot capture");
        settings.X.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnBurstStart); state.Begin(settings,new Vector3(5,6,0));
        Near(state.Prepare(settings,new Vector3(7,8,0),false),new Vector3(5,8,0),"X burst holds while Y refreshes");
        Near(state.Prepare(settings,new Vector3(9,10,0),true),new Vector3(9,10,0),"Both refresh at respective burst/shot boundaries");
    }
    static void Copies()
    {
        var profile=Profile();profile.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.Player,2);profile.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,-1,EnemyEmissionOriginReferenceMode.Boundary,true);profile.Shooting.EmissionOrigin.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnBurstStart);
        string expected=JsonUtility.ToJson(profile.Shooting.EmissionOrigin);
        var copy=new EnemyBurstAttackSettings();copy.CopyFrom(profile);Check(JsonUtility.ToJson(copy.Shooting.EmissionOrigin)==expected,"Deep copy");
        copy.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World,99);Check(profile.Shooting.EmissionOrigin.X.Source==EnemyEmissionOriginSource.Player,"Copy independence");
        var local=new EnemyBurstAttackSettings();local.ApplyShootingOverrideFrom(profile);Check(JsonUtility.ToJson(local.Shooting.EmissionOrigin)==expected,"Local shooting override");
        profile.EnableMultiplePatterns();var entry=new EnemyAttackPatternSettings();entry.CopyFrom(copy);profile.AddPattern(entry);
        var multiple=new EnemyBurstAttackSettings();multiple.CopyFrom(profile);Check(JsonUtility.ToJson(multiple.GetPattern(0).Shooting.EmissionOrigin)==expected,"Multiple first pattern");Check(multiple.GetPattern(1).Shooting.EmissionOrigin.X.Offset==99,"Multiple independent origins");
        var preset=ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();try{var wave=new DirectedWaveAttackSettings();wave.WaveBurstSettings.CopyFrom(profile);preset.SetAttackSettings(wave,false,1);var resolved=new DirectedWaveAttackSettings();resolved.CopyFrom(preset.AttackSettings);Check(JsonUtility.ToJson(resolved.WaveBurstSettings.GetPattern(0).Shooting.EmissionOrigin)==expected,"Preset copy");}finally{UnityEngine.Object.DestroyImmediate(preset);}
        var restored=JsonUtility.FromJson<EnemyBurstAttackSettings>(JsonUtility.ToJson(profile));Check(JsonUtility.ToJson(restored.GetPattern(0).Shooting.EmissionOrigin)==expected,"JSON persistence");
        var legacy=JsonUtility.FromJson<EnemyShootingSettings>("{}");Check(legacy.EmissionOrigin.IsDefault,"Missing serialized fields preserve defaults");
    }
    static EnemyBurstAttackSettings Profile(){var p=new EnemyBurstAttackSettings();p.ConfigureLegacySingleAttack(1,1,0);p.SetFacing(EnemyFacingMode.Fixed);p.Shooting.SetProjectileBaseSpeedOverride(true,3);return p;}
    static void Runtime()
    {
        var scene=EditorSceneManager.NewPreviewScene();var clone=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));SceneManager.MoveGameObjectToScene(clone,scene);
        var cameraObject=new GameObject("Origin test camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.transform.position=new Vector3(1,2,-10);camera.rect=new Rect(.1f,.2f,.8f,.6f);camera.aspect=.6f;
        var world=new World("Emission Origin verification");var spawner=new EnemyProjectileEcsSpawner();Set(spawner,"entityWorld",world);Set(spawner,"entityManager",world.EntityManager);
        try{
            Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,0,out var bounds),"Viewport bounds available");Near(new Vector3(bounds.xMin,bounds.yMax,0),new Vector3(-2,7,0),"Cropped viewport uses actual camera aspect");camera.orthographicSize=6;camera.aspect=.5f;EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,0,out bounds);Near(new Vector3(bounds.xMin,bounds.yMax,0),new Vector3(-2,8,0),"Dynamic lens and aspect");
            clone.transform.position=new Vector3(2,3,0);clone.transform.rotation=Quaternion.Euler(0,0,27);var executor=clone.GetComponent<SimpleEnemyAttackExecutor>();Set(executor,"projectileSpawner",spawner);Call(executor,"Awake");Set(executor,"autonomousAttack",false);
            var context=new Context{Position=new Vector3(-4,-5,0),Camera=camera};var profile=Profile();profile.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.Player);profile.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,-1,EnemyEmissionOriginReferenceMode.Boundary,true);
            foreach(EnemyEmissionLayoutType layout in Enum.GetValues(typeof(EnemyEmissionLayoutType)))
            {
                if(layout==EnemyEmissionLayoutType.Sector)profile.Shooting.EmissionLayout.ConfigureSector(-30,30,3);else profile.Shooting.EmissionLayout.Configure(layout,3,1);
                profile.Shooting.ConfigureLegacyFan(3,-10,10);Set(profile.Shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun);Set(profile.Shooting,"shotGunProjectileCount",2);Set(profile.Shooting,"shotGunDirection",Vector2.up);profile.Shooting.SetLayoutRotationEnabled(true);profile.Validate();
                executor.SetWaveAttackSettings(null);executor.SetWaveAttackSettings(profile);executor.SetWaveAimTarget(context);executor.BeginEmissionOriginAttack(profile);Check(executor.PrepareEmissionOriginShot(profile,true),"Prepare each layout");Check(executor.TryFireAt(context.Position,profile),"Fire relocated layout");
                var shots=Read(world);Check(shots.Length==profile.Shooting.ProjectilesPerVolley,"Fan/Stack count retained");var frame=EnemyShootingPreview.CreateFrame(executor,profile.Shooting,clone.transform.position,0,null,out var basis);frame.Target=context.Position;frame.PlayerPosition=context.Position;frame.GameplayBounds=bounds;
                for(int arm=0;arm<profile.Shooting.DirectionCount;arm++){var emitter=EnemyShootingPreviewGeometry.GetEmitter(profile.Shooting,arm,frame,basis,0,aimed:true);Near(emitter.Position,executor.GetEmissionPosition(profile,arm),"Scene/runtime relocated emitter");for(int j=0;j<profile.Shooting.ProjectilesPerDirection;j++){var projectile=EnemyShootingPreviewGeometry.GetProjectile(profile.Shooting,emitter,j);var shot=shots[arm*profile.Shooting.ProjectilesPerDirection+j];Near(shot.Position,projectile.Position,"Actual ECS origin matches preview");Near(shot.Direction,projectile.Direction,"Aimed directions originate at emitter");}}
                Clear(world);
                executor.PrepareEmissionOriginShot(profile,true);Check(executor.TryFireStationary(new Vector3(7,-8,0),true,profile),"Stationary world point");shots=Read(world);frame.Target=new Vector3(7,-8,0);for(int arm=0;arm<profile.Shooting.DirectionCount;arm++){var emitter=EnemyShootingPreviewGeometry.GetEmitter(profile.Shooting,arm,frame,basis,0,aimed:true,stationary:true);Near(shots[arm*profile.Shooting.ProjectilesPerDirection].Direction,EnemyShootingPreviewGeometry.GetProjectile(profile.Shooting,emitter,0).Direction,"Stationary points from actual spawn");}Clear(world);
            }
            profile=Profile();profile.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.Player);profile.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,10);
            foreach(var mode in new[]{EnemyEmissionOriginCaptureMode.OnAttackStart,EnemyEmissionOriginCaptureMode.OnBurstStart,EnemyEmissionOriginCaptureMode.OnShot})
            {
                profile.Shooting.EmissionOrigin.SetCaptureMode(mode);context.Position=new Vector3(1,-5,0);executor.SetWaveAttackSettings(null);executor.SetWaveAttackSettings(profile);executor.SetWaveAimTarget(context);executor.BeginEmissionOriginAttack(profile);
                context.Position=new Vector3(2,-5,0);executor.PrepareEmissionOriginShot(profile,false);executor.TryFireInDirection(Vector3.down,profile);float first=Read(world)[0].Position.x;Clear(world);
                context.Position=new Vector3(3,-5,0);executor.PrepareEmissionOriginShot(profile,true);executor.TryFireInDirection(Vector3.down,profile);float second=Read(world)[0].Position.x;Clear(world);
                context.Position=new Vector3(4,-5,0);executor.PrepareEmissionOriginShot(profile,false);executor.TryFireInDirection(Vector3.down,profile);float third=Read(world)[0].Position.x;Clear(world);
                Check(Mathf.Abs((second-first)-(mode==EnemyEmissionOriginCaptureMode.OnAttackStart?0:mode==EnemyEmissionOriginCaptureMode.OnBurstStart?2:1))<.001f,"Runtime burst capture");Check(Mathf.Abs((third-second)-(mode==EnemyEmissionOriginCaptureMode.OnShot?1:0))<.001f,"Runtime shot capture");
            }
            profile.Shooting.EmissionOrigin.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnShot);profile.Shooting.EmissionLayout.ConfigureSector(-30,30,3,EnemySectorLaunchMode.Sequential,.2f,.1f);context.Position=new Vector3(1,-5,0);executor.SetWaveAttackSettings(null);executor.SetWaveAttackSettings(profile);executor.SetWaveAimTarget(context);executor.BeginEmissionOriginAttack(profile);executor.PrepareEmissionOriginShot(profile,true);executor.TryFireAt(context.Position,profile);float start=(float)Get(((IList)Get(executor,"pendingSectors"))[0],"StartTime");
            Call(executor,"TickPendingSectors",start+.2001f);var firstShot=Read(world)[0];Clear(world);clone.transform.position+=new Vector3(2,3,0);context.Position=new Vector3(7,-5,0);executor.PrepareEmissionOriginShot(profile,false);executor.TryFireAt(context.Position,profile);Call(executor,"TickPendingSectors",start+.4001f);var remaining=Read(world);Check(remaining.Length==5,"Overlapping delayed volleys finish");Near(remaining[0].Position,firstShot.Position,"Sector origin frozen despite enemy and player moving");Near(remaining[1].Position,firstShot.Position,"Same origin through last direction");Check(remaining[2].Position.x-firstShot.Position.x>5.9f,"Next volley gets own origin");Clear(world);
            profile=Profile();profile.EnableMultiplePatterns();var relocated=new EnemyAttackPatternSettings();var relocatedSettings=Profile();relocatedSettings.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World,12);relocatedSettings.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,11);relocated.CopyFrom(relocatedSettings);profile.AddPattern(relocated);profile.Validate();
            executor.SetWaveAttackSettings(null);executor.SetWaveAttackSettings(profile);executor.SetWaveAimTarget(context);executor.BeginEmissionOriginAttack(profile);executor.PrepareEmissionOriginShot(profile,true);
            DirectedWaveAttackAimState multipleAim=default;Check(multipleAim.TryFire(clone.GetComponent<Enemy>(),executor,profile,context.Position,false),"Multiple with default first and relocated second pattern fires");var multipleShots=Read(world);Check(multipleShots.Length==2,"Both independent origins emit");for(int i=0;i<2;i++)Near(multipleShots[i].Direction,(context.Position-multipleShots[i].Position).normalized,"Each Multiple pattern acquires aim from its own actual origin");Clear(world);
            var property=new SerializedObject(executor).FindProperty("burstAttackSettings").FindPropertyRelative("shooting");Check(property.FindPropertyRelative("emissionOrigin.x.source")!=null,"Inspector serialized axis");var fields=(IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod("BehaviourFields",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{property});var ordered=new List<string>(fields);Check(ordered.IndexOf("emissionOrigin")<ordered.IndexOf("emissionLayout"),"Origin drawn before layout");
        }finally{UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(cameraObject);world.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Shot[] Read(World world){using var query=world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity),typeof(LocalTransform),typeof(EnemyProjectileMovement));using var transforms=query.ToComponentDataArray<LocalTransform>(Allocator.Temp);using var moves=query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);var shots=new Shot[transforms.Length];for(int i=0;i<shots.Length;i++)shots[i]=new Shot{Position=transforms[i].Position,Direction=new Vector3(moves[i].ForwardDirection.x,moves[i].ForwardDirection.y,0)};return shots;}
    static void Clear(World world){using var query=world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity));world.EntityManager.DestroyEntity(query);}
}