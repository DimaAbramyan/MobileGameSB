using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyCoordinatorEventNavigationEditor
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,F).Invoke(target,args);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,F).SetValue(target,value);
    public static async Task<object> Run()
    {
        int worlds=World.All.Count;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        string authored=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<WaveAttackCoordinator>());
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var window=ScriptableObject.CreateInstance<WavePreviewPlayerWindow>();
        try
        {
            Set(window,"followSelection",false);Set(window,"previewDuration",90f);Call(window,"SetSource",source);
            window.position=new Rect(100,100,690,850);window.ShowUtility();window.Focus();
            var player=(WavePreviewPlayer)Call(window,"EnsurePlayer");
            for(int i=0;i<2;i++)
            {
                if(!player.NextCoordinatorEvent())throw new Exception("Editor navigation request failed.");
                var timeout=DateTime.UtcNow.AddSeconds(25);
                while(player.IsSeeking||player.IsFindingCoordinatorEvent)
                {if(DateTime.UtcNow>timeout)throw new Exception("Editor navigation timed out.");window.Repaint();await Task.Delay(30);}
            }
            if(player.SelectedCoordinatorEvent==null||player.IsPlaying)throw new Exception("Editor event selection did not pause.");
            window.Repaint();await Task.Delay(400);
            Rect rect=window.position;float scale=EditorGUIUtility.pixelsPerPoint;
            int width=Mathf.RoundToInt(rect.width*scale),height=Mathf.RoundToInt(rect.height*scale);
            var host=typeof(EditorWindow).GetField("m_Parent",F).GetValue(window);
            var grab=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("GrabPixels",F);
            var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                grab.Invoke(host,new object[]{target,new Rect(0,0,width,height)});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);
                var pixels=texture.GetPixels();
                for(int y=0;y<height/2;y++)for(int x=0;x<width;x++)
                {int first=y*width+x,last=(height-1-y)*width+x;(pixels[first],pixels[last])=(pixels[last],pixels[first]);}
                texture.SetPixels(pixels);texture.Apply();
                System.IO.File.WriteAllBytes("AgentArtifacts/coordinator-event-navigation-window.png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
            string selected=player.SelectedCoordinatorEvent.Summary;float time=player.SelectedCoordinatorEvent.Time;
            window.Close();if(window!=null)Object.DestroyImmediate(window);window=null;
            if(World.All.Count!=worlds)throw new Exception("Closing the player window leaked search/snapshot worlds.");
            if(scene.isDirty!=dirty||authored!=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<WaveAttackCoordinator>()))throw new Exception("Editor UI modified scene or wave data.");
            return new{passed=true,editorNavigation=true,selected,time,screenshot="AgentArtifacts/coordinator-event-navigation-window.png",cleanup=true,noAssetMutation=true};
        }
        finally{if(window!=null){window.Close();if(window!=null)Object.DestroyImmediate(window);}}
    }
}
