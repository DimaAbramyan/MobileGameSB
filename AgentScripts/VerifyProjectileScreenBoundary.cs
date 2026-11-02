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

public static class VerifyProjectileScreenBoundary
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld World = new();
        public EntityManager Manager => World.SimulationWorld.EntityManager;
        private readonly Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public Context()
        {
            var go = new GameObject("Screen cleanup test camera") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go,scene); Camera=go.AddComponent<Camera>(); Camera.enabled=false;
            Camera.orthographic=true; Camera.orthographicSize=5; Camera.aspect=1; Camera.transform.position=new Vector3(0,0,-10);
        }
        public Entity Spawn(Vector3 position, EnemyProjectileScreenSettings screen = default, EnemyChildAttackSettings child = null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var shooting=new EnemyShootingSettings(); shooting.SetScreenBoundary(screen); shooting.SetChildAttack(child);
            Set(shooting,"overrideProjectileLifetime",true); Set(shooting,"projectileLifetime",100f);
            var requests=new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,shooting,position,position,Vector3.down,1,1,0,new EnemyProjectilePrefabSet(prefab));
            World.Spawn(requests);
            using var q=Manager.CreateEntityQuery(typeof(EnemyProjectileScreenState)); using var all=q.ToEntityArray(Allocator.Temp);
            var entity=all[all.Length-1];
            var data=Manager.GetComponentData<EnemyProjectileStaticData>(entity); data.Radius=.2f; Manager.SetComponentData(entity,data);
            return entity;
        }
        public void Move(Entity entity, Vector3 position,float scale=1)
        { var pose=Manager.GetComponentData<LocalTransform>(entity); pose.Position=new float3(position.x,position.y,position.z); pose.Scale=scale; Manager.SetComponentData(entity,pose); }
        public void Tick(float time=1) => World.Step(time,0,new Vector3(1000,1000),Camera);
        public void BoundaryOnly() => World.SimulationWorld.GetOrCreateSystemManaged<EnemyProjectileScreenBoundarySystem>().UpdatePreview(Camera);
        public void Dispose() { World.Dispose(); UnityEngine.Object.DestroyImmediate(Camera.gameObject); EditorSceneManager.ClosePreviewScene(scene); }
    }
    public static object RunPlayProbe()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        checks=0;
        using var c=new Context();
        var cameraObject=new GameObject("Screen cleanup Play Mode probe"){hideFlags=HideFlags.HideAndDontSave};
        try
        {
            var camera=cameraObject.AddComponent<Camera>(); camera.CopyFrom(c.Camera); camera.enabled=true;
            camera.transform.position=new Vector3(0,0,-10); cameraObject.tag="MainCamera";
            Check(Camera.main==camera,"Runtime uses actual gameplay main camera");
            var e=c.Spawn(new Vector3(0,7));
            var system=c.World.SimulationWorld.GetOrCreateSystemManaged<EnemyProjectileScreenBoundarySystem>();
            system.Update(); Check(c.Manager.GetComponentData<EnemyProjectileResolution>(e).Kind==EnemyProjectileResolutionKind.None,"Runtime preserves outside birth");
            c.Move(e,Vector3.zero); system.Update(); Check(c.Manager.GetComponentData<EnemyProjectileScreenState>(e).Entered==1,"Runtime records entry");
            c.Move(e,new Vector3(0,7)); system.Update();
            Check(c.Manager.GetComponentData<EnemyProjectileResolution>(e).Kind==EnemyProjectileResolutionKind.Expired,"Runtime resolves exit with automatic radius");
            c.Tick(); Check(!c.Manager.Exists(e),"Shared cleanup destroys resolved entity");
            return new {passed=true,checks,playMode=true};
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
    public static object Run()
    {
        checks=0;
        using(var c=new Context())
        {
            var e=c.Spawn(Vector3.zero); c.Move(e,new Vector3(0,5.3f),2); c.Tick();
            Check(c.Manager.Exists(e),"Automatic uses current scaled radius");
            c.Move(e,new Vector3(0,5.3f),1); c.Tick(); Check(!c.Manager.Exists(e),"Automatic updates after shrinking");
        }
        using(var c=new Context())
        {
            var e=c.Spawn(Vector3.zero,new EnemyProjectileScreenSettings(true,EnemyProjectileBoundaryOffsetMode.Custom,1));
            c.Move(e,new Vector3(0,5.9f)); c.Tick(); Check(c.Manager.Exists(e),"Custom keeps center within offset");
            c.Move(e,new Vector3(0,6.1f)); c.Tick(); Check(!c.Manager.Exists(e),"Custom deletes beyond offset");
        }
        using(var c=new Context())
        {
            var e=c.Spawn(Vector3.zero,new EnemyProjectileScreenSettings(false)); c.Move(e,Vector3.one*100); c.Tick();
            Check(c.Manager.Exists(e),"Disabled keeps outside projectile");
        }
        using(var c=new Context())
        {
            var e=c.Spawn(new Vector3(0,7)); c.Move(e,new Vector3(0,6)); c.Tick();
            Check(c.Manager.Exists(e)&&c.Manager.GetComponentData<EnemyProjectileScreenState>(e).Entered==0,"Outside spawn waits for entry");
            c.Move(e,new Vector3(0,4)); c.Tick(); Check(c.Manager.GetComponentData<EnemyProjectileScreenState>(e).Entered==1,"Entry arms cleanup");
            c.Move(e,new Vector3(0,6)); c.Tick(); Check(!c.Manager.Exists(e),"Cleanup after subsequent exit");
            e=c.Spawn(new Vector3(0,7)); c.Move(e,new Vector3(0,-7)); c.Tick(); Check(!c.Manager.Exists(e),"Fast full-screen crossing is not missed");
            e=c.Spawn(new Vector3(7,7)); c.Move(e,new Vector3(7,-7)); c.Tick(); Check(c.Manager.Exists(e),"Outside segment missing viewport does not arm");
        }
        using(var c=new Context())
        {
            c.Camera.aspect=.5f;
            var e=c.Spawn(Vector3.zero); c.Move(e,new Vector3(3,0)); c.BoundaryOnly();
            Check(c.Manager.GetComponentData<EnemyProjectileResolution>(e).Kind==EnemyProjectileResolutionKind.Expired,"Runtime system marks existing resolution using camera aspect");
            c.Tick(); Check(!c.Manager.Exists(e),"Preview cleanup consumes the same runtime resolution");
        }
        using(var c=new Context())
        {
            var child=new EnemyChildAttackSettings(); Set(child,"triggerMode",EnemyChildAttackTriggerMode.OnEvent);
            Set(child,"eventType",EnemyChildAttackEvent.OnScreenBoundaryReached); Set(child,"boundary",EnemyChildAttackBoundary.Top);
            Set(child,"boundaryOffset",-1f); Set(child,"timeOffset",.5f); Set(child,"destroyParent",false);
            var e=c.Spawn(Vector3.zero,child:child); c.Move(e,new Vector3(7,0)); c.Tick(1);
            Check(c.Manager.Exists(e),"Unselected edge does not cancel pending child boundary trigger");
            c.Move(e,new Vector3(7,7)); c.Tick(2);
            Check(c.Manager.Exists(e)&&c.Manager.IsComponentEnabled<EnemyChildAttackScreenHold>(e),"Time Offset holds parent outside screen");
            c.Tick(3); c.Tick(4); Check(!c.Manager.Exists(e),"Hold releases after delayed child work completes");
            Check(c.World.Count>0,"Delayed child was emitted before parent cleanup");
        }
        var legacy=JsonUtility.FromJson<EnemyShootingSettings>("{}");
        Check(legacy.ScreenBoundary.DestroyOutsideScreen&&legacy.ScreenBoundary.OffsetMode==EnemyProjectileBoundaryOffsetMode.Automatic,"Absent legacy JSON fields default to enabled Automatic");
        var settings=new EnemyShootingSettings(); settings.SetScreenBoundary(new EnemyProjectileScreenSettings(false,EnemyProjectileBoundaryOffsetMode.Custom,1.25f));
        var copy=new EnemyShootingSettings(); copy.CopyFrom(settings);
        Check(!copy.ScreenBoundary.DestroyOutsideScreen&&copy.ScreenBoundary.BoundaryOffset==1.25f,"Copy preserves screen settings");
        copy=new EnemyShootingSettings(); copy.CopyProjectileConfigurationFrom(settings);
        Check(!copy.ScreenBoundary.DestroyOutsideScreen&&copy.ScreenBoundary.OffsetMode==EnemyProjectileBoundaryOffsetMode.Custom,"Projectile presets include screen settings");
        copy=JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(settings));
        Check(!copy.ScreenBoundary.DestroyOutsideScreen&&copy.ScreenBoundary.BoundaryOffset==1.25f,"JSON round trip");
        return new {passed=true,checks};
    }
}
