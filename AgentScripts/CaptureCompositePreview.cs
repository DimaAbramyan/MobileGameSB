using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CaptureCompositePreview
{
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    public static object Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();var go=new GameObject("Composite render check"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(go,scene);
        using var world=new EnemyProjectilePreviewWorld();RenderTexture rt=null;Texture2D image=null;var prior=RenderTexture.active;
        try
        {
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.65f;camera.aspect=2.5f;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.035f,.07f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var requests=new List<EnemyProjectileSpawnRequest>();var types=new[]{EnemyProjectileType.CompositeCircle,EnemyProjectileType.CompositePolygon,EnemyProjectileType.CompositeSegmentedRing};
            for(int i=0;i<3;i++)
            {
                var s=new EnemyShootingSettings();s.SetProjectileType(types[i]);s.SetScreenBoundary(new EnemyProjectileScreenSettings(false));Set(s.Composite,"radiusMode",EnemyCompositeRadiusMode.Custom);Set(s.Composite,"radius",.9f);Set(s.Composite,"componentScale",3f);Set(s.Composite,"componentCount",12);Set(s.Composite,"gapAngle",45f);
                var p=new Vector3((i-1)*2.6f,0);EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,s,p,p,Vector3.up,1,1,0,new EnemyProjectilePrefabSet(prefab));
            }
            world.Spawn(requests);world.CaptureRenderState();world.RegisterCamera(camera);
            rt=new RenderTexture(1200,480,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(1200,480,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1200,480),0,0);image.Apply();
            string path="AgentArtifacts/composite-preview.png";File.WriteAllBytes(path,image.EncodeToPNG());return new{path,count=world.Count};
        }
        finally{RenderTexture.active=prior;if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
