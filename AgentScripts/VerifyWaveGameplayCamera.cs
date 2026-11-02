using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class VerifyWaveGameplayCamera
{
    const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    public static object Run()
    {
        checks=0;var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var menu=Camera.main;
        var menuProjection=menu.projectionMatrix;var menuRect=menu.rect;
        var type=typeof(WaveEditor).Assembly.GetType("WavePreviewGameplayCamera");
        var camera=(Camera)type.GetProperty("GameplayCamera",Static).GetValue(null);
        Check(camera!=null&&camera!=menu,"Uses battle camera instead of menu camera");
        Check(!camera.enabled&&EditorSceneManager.IsPreviewScene(camera.gameObject.scene),"Isolated non-rendering battle scene");
        Check(active==UnityEngine.SceneManagement.SceneManager.GetActiveScene()&&Camera.main==menu,"Active scene and main camera preserved");
        Check(menuProjection==menu.projectionMatrix&&menuRect==menu.rect,"Menu projection unchanged");
        string path=(string)type.GetProperty("ScenePath",Static).GetValue(null);string before=File.ReadAllText(path);
        Check(camera.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).All(c=>!c.enabled),"No additional rendering cameras");
        Check(camera.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).All(c=>!c.enabled),"No extra HUD rendering");
        var viewport=camera.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BattleCameraViewport>(true)).Single();
        using var serialized=new SerializedObject(viewport);
        var bottom=(RectTransform)serialized.FindProperty("bottomHudBoundary").objectReferenceValue;
        var corners=new Vector3[4];bottom.GetWorldCorners(corners);float top=corners.Max(c=>c.y);
        Check(Mathf.Abs(camera.pixelRect.yMin-top)<.1f&&top>Screen.safeArea.yMin,"Real lower HUD layout is applied");
        Check(Mathf.Abs(camera.pixelRect.width-Screen.safeArea.width)<.1f,"Adaptive fill uses all safe width");
        Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,0,out var bounds),"Battle world bounds available");
        Check(camera==(Camera)type.GetProperty("GameplayCamera",Static).GetValue(null),"Preview scene cached");
        int revision=(int)type.GetProperty("Revision",Static).GetValue(null);
        Check(revision==(int)type.GetProperty("Revision",Static).GetValue(null),"Stable camera does not invalidate snapshots");
        if(Screen.width==1170&&Screen.height==2532)
        {
            Check(Mathf.Abs(camera.pixelRect.height-2073.4f)<1f&&Mathf.Abs(camera.orthographicSize-5.288888f)<.001f,"Matches recorded runtime viewport and lens");
            Check(Mathf.Abs(bounds.width-5.9689f)<.02f&&Mathf.Abs(bounds.height-10.5778f)<.02f,"Matches recorded runtime world area");
        }
        EnemyEmissionOriginSettings.TryGetGameplayBounds(menu,0,out var oldBounds);
        using(var world=new EnemyProjectilePreviewWorld())
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var requests=new List<EnemyProjectileSpawnRequest>();
            float x=(oldBounds.xMax+bounds.xMax)*.5f;
            EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,new EnemyShootingSettings(),new Vector3(x,0),Vector3.zero,Vector3.up,1,1,0,default,maximum:1);
            world.Spawn(requests);var manager=world.SimulationWorld.EntityManager;
            using var query=manager.CreateEntityQuery(typeof(EnemyProjectileScreenState));using var entities=query.ToEntityArray(Allocator.Temp);
            var entity=entities[0];world.Step(1,0,new Vector3(1000,1000));
            Check(manager.Exists(entity),"Default ECS Preview preserves projectile beyond menu edge but inside battle");
            var pose=manager.GetComponentData<LocalTransform>(entity);pose.Position=new float3(bounds.xMax+1,0,0);manager.SetComponentData(entity,pose);
            world.Step(2,0,new Vector3(1000,1000));Check(!manager.Exists(entity),"Same Preview cleanup uses actual battle edge");
        }
        var windows=Resources.FindObjectsOfTypeAll<WavePreviewPlayerWindow>();
        var field=typeof(WavePreviewPlayerWindow).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic);
        var players=windows.Select(w=>field.GetValue(w)).ToArray();
        var temporary=EditorSceneManager.NewPreviewScene();EditorSceneManager.ClosePreviewScene(temporary);
        Check(windows.Select((w,i)=>ReferenceEquals(field.GetValue(w),players[i])).All(v=>v),"Closing auxiliary preview scene preserves player");
        Check(File.ReadAllText(path)==before,"Battle scene asset not changed");
        string result=$"Passed {checks} checks; camera={camera.name}; world={bounds}; viewport={camera.pixelRect}; size={camera.orthographicSize}; menu={oldBounds}";
        File.WriteAllText("AgentArtifacts/wave-gameplay-camera-checks.txt",result);
        return new{passed=true,checks,details=result};
    }
}
