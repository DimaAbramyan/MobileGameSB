using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
public static class VerifyBattleScaling
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    public static string Run()
    {
        checks=0;
        var root=new GameObject("Isolated viewport calculation checks"){hideFlags=HideFlags.HideAndDontSave};
        root.SetActive(false);
        var viewport=root.AddComponent<BattleCameraViewport>();
        var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
        typeof(BattleCameraViewport).GetField("targetCamera",Private).SetValue(viewport,camera);
        var rectMethod=typeof(BattleCameraViewport).GetMethod("GetGameplayRect",Private);
        var adaptive=typeof(BattleCameraViewport).GetField("fillAvailableGameplayArea",Private);
        typeof(BattleCameraViewport).GetField("fixedGameplayAspect",Private).SetValue(viewport,.52f);
        typeof(BattleCameraViewport).GetField("lensOrthographicSize",Private).SetValue(viewport,5.288888f);
        var rows=new List<string>();
        try
        {
            foreach(int height in new[]{1920,2340,2400,3000})
            {
                const int width=1080;
                // Same HUD size per screen width and top safe inset observed in the user's battle.
                var available=new Rect(0,317.6f*width/1170, width,height-(317.6f+141)*width/1170);
                adaptive.SetValue(viewport,false);
                var old=(Rect)rectMethod.Invoke(viewport,new object[]{(float)width,(float)height,available});
                adaptive.SetValue(viewport,true);
                var next=(Rect)rectMethod.Invoke(viewport,new object[]{(float)width,(float)height,available});
                Check(Mathf.Abs(next.width*width-available.width)<.01f,"All available width "+height);
                Check(Mathf.Abs(next.height*height-available.height)<.01f&&Mathf.Abs(next.y*height-available.y)<.01f,"HUD exclusion "+height);
                var texture=new RenderTexture(width,height,0);
                try
                {
                    camera.targetTexture=texture;camera.rect=next;camera.ResetAspect();
                    typeof(BattleCameraViewport).GetMethod("ApplyCinemachineLens",Private).Invoke(viewport,new object[]{available.width/available.height});
                    Check(EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,0,out var bounds),"Camera bounds "+height);
                    Check(bounds.width>=5.5f-.01f&&bounds.height>=10.577776f-.01f,"No authored reference cropping "+height+" bounds="+bounds+" precise="+bounds.height.ToString("F8")+" aspect="+camera.aspect+" pixels="+camera.pixelRect);
                    var center=camera.WorldToScreenPoint(Vector3.zero);
                    float px=(camera.WorldToScreenPoint(Vector3.right)-center).magnitude;
                    float py=(camera.WorldToScreenPoint(Vector3.up)-center).magnitude;
                    Check(Mathf.Abs(px-py)/Mathf.Max(px,py)<.001f,"Isotropic projection "+height);
                    var origin=new EnemyEmissionOriginSettings();
                    origin.X.Configure(EnemyEmissionOriginSource.World,.2f,EnemyEmissionOriginReferenceMode.Boundary,true);
                    origin.Y.Configure(EnemyEmissionOriginSource.World,.3f,EnemyEmissionOriginReferenceMode.Boundary,true);
                    var point=origin.Resolve(Vector3.zero,Vector3.zero,bounds);
                    Check(Mathf.Abs(point.x-(bounds.xMax-.2f))<.001f&&Mathf.Abs(point.y-(bounds.yMax-.3f))<.001f,"Emission Origin boundary "+height);
                    Check(EnemyChildAttackBoundaryIntersection.TryCross(Vector3.zero,new Vector3(bounds.xMax+1,0),bounds,EnemyChildAttackBoundaries.Right,0,out _,out var side)&&side==EnemyChildAttackBoundary.Right,"Boundary crossing "+height);
                    var clamped=camera.WorldToViewportPoint(new Vector3(100,100,0));clamped.x=Mathf.Clamp01(clamped.x);clamped.y=Mathf.Clamp01(clamped.y);
                    var world=camera.ViewportToWorldPoint(clamped);
                    Check(Mathf.Abs(world.x-bounds.xMax)<.01f&&Mathf.Abs(world.y-bounds.yMax)<.01f,"Player viewport clamp "+height);
                    // Actual shared ECS screen system used by Runtime and Preview, at the adaptive camera bounds.
                    using(var simulation=new EnemyProjectilePreviewWorld())
                    {
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
                        var requests=new List<EnemyProjectileSpawnRequest>();
                        EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,new EnemyShootingSettings(),Vector3.zero,Vector3.zero,Vector3.up,1,1,0,default,maximum:1);
                        simulation.Spawn(requests);
                        var manager=simulation.SimulationWorld.EntityManager;
                        using var query=manager.CreateEntityQuery(typeof(EnemyProjectileScreenState));
                        using var entities=query.ToEntityArray(Allocator.Temp);
                        var entity=entities[0];
                        var system=simulation.SimulationWorld.GetOrCreateSystemManaged<EnemyProjectileScreenBoundarySystem>();system.UpdatePreview(camera);
                        Check(manager.GetComponentData<EnemyProjectileResolution>(entity).Kind==EnemyProjectileResolutionKind.None,"ECS inside survives "+height);
                        var pose=manager.GetComponentData<LocalTransform>(entity);pose.Position=new float3(bounds.xMax+1,0,0);manager.SetComponentData(entity,pose);system.UpdatePreview(camera);
                        Check(manager.GetComponentData<EnemyProjectileResolution>(entity).Kind==EnemyProjectileResolutionKind.Expired,"ECS outside expires "+height);
                    }
                    rows.Add($"1080x{height}: old width {old.width*width:F2}px; new width {next.width*width:F2}px; world {bounds.width:F4}x{bounds.height:F4}; size {camera.orthographicSize:F4}");
                }
                finally{camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);}
            }
            adaptive.SetValue(viewport,false);
            Check(viewport.GetOrthographicSize(.3f)==5.288888f,"Legacy lens unchanged");

            string result=$"passed: true; checks: {checks}\n"+string.Join("\n",rows);
            System.IO.File.WriteAllText("AgentArtifacts/battle-scaling-checks.txt",result);return result;
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

}




