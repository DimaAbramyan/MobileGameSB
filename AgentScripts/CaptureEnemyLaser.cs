using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CaptureEnemyLaser
{
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    public static object Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Laser visual verification");SceneManager.MoveGameObjectToScene(root,scene);
        var camera=root.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=4;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.065f);
        var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;var image=new Texture2D(768,768,TextureFormat.RGB24,false);
        var activeBefore=RenderTexture.active;int[] lit=new int[2];
        try
        {
            for(int warning=0;warning<2;warning++)
            {
                using var preview=new EnemyProjectilePreviewWorld();var shooting=new EnemyShootingSettings();Set(shooting,"projectileType",EnemyProjectileType.Laser);
                Set(shooting,"overwriteProjectileCharacteristic",true);Set(shooting,"projectileCharacteristic",EnemyProjectileCharacteristic.Normal);
                Set(shooting,"overwriteBaseColour",true);Set(shooting,"projectileColour",new Color(.1f,.9f,1));
                Set(shooting.Laser,"enableTelegraph",warning==1);Set(shooting.Laser.Geometry,"lengthMode",EnemyLaserLengthMode.Custom);
                Set(shooting.Laser.Geometry,"laserLength",5f);Set(shooting.Laser.Geometry,"laserWidth",.15f);Set(shooting.Laser,"telegraphColor",Color.red);
                shooting.ConfigureLegacyFan(3,-25,25);var requests=new List<EnemyProjectileSpawnRequest>();
                EnemyAttackProjectileBuilder.AppendEmitter(requests,null,shooting,new Vector3(0,2.5f),Vector3.zero,Vector3.down,1,1,0,default,
                    laserOrigin:new EnemyLaserOrigin(null,Vector3.zero,shooting.EmissionOrigin,camera));
                preview.Spawn(requests);preview.Step(.3f,.3f,Vector3.up*100,camera);preview.CaptureRenderState();
                Action<ScriptableRenderContext,Camera> draw=(context,c)=>{if(c==camera)preview.Draw(c);};
                RenderPipelineManager.beginCameraRendering+=draw;
                try{camera.Render();}finally{RenderPipelineManager.beginCameraRendering-=draw;}
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
                var pixels=image.GetPixels32();foreach(var p in pixels)if(warning==0?p.g>80:p.r>60)lit[warning]++;
                File.WriteAllBytes(warning==0?"AgentArtifacts/laser-active.png":"AgentArtifacts/laser-telegraph.png",image.EncodeToPNG());
                if(lit[warning]<1000)throw new Exception("Beam rendering missing: "+warning+" pixels "+lit[warning]);
            }
            return new{passed=true,activePixels=lit[0],telegraphPixels=lit[1],images=new[]{"AgentArtifacts/laser-active.png","AgentArtifacts/laser-telegraph.png"}};
        }
        finally{RenderTexture.active=activeBefore;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
