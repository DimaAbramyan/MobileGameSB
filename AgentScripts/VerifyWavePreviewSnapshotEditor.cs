using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyWavePreviewSnapshotEditor
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Flags).Invoke(value,args);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Flags).SetValue(value,data);
    public static object Present()
    {
        var wave=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        var window=WavePreviewPlayerWindow.Open(wave);Set(window,"followSelection",false);Set(window,"showAdvanced",true);
        var player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Seek(20);while(player.IsSeeking)player.Update(0,1000);player.Pause();window.Repaint();
        return new{opened=true,wave=AssetDatabase.GetAssetPath(wave),editMode=!Application.isPlaying,currentTime=player.CurrentTime,snapshots=player.SnapshotCount,advanced=true};
    }
    public static object PrepareReload()
    {
        var wave=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        var window=WavePreviewPlayerWindow.Open(wave);Set(window,"followSelection",false);
        Call(window,"Release");int worlds=World.All.Count;
        var player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Seek(5f);while(player.IsSeeking)player.Update(0,1000);player.Pause();
        string token=Guid.NewGuid().ToString("N");
        AssemblyReloadEvents.beforeAssemblyReload+=()=>System.IO.File.WriteAllText("AgentArtifacts/wave-player-reload-event.txt",token);
        System.IO.File.WriteAllText("AgentArtifacts/wave-player-reload-baseline.json",JsonUtility.ToJson(new ReloadState{worlds=worlds,token=token,dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}));
        return new{prepared=true,ownedWorlds=World.All.Count-worlds,currentTime=player.CurrentTime};
    }
    [Serializable]private sealed class ReloadState{public int worlds;public string token;public bool dirty;}
    public static object VerifyReload()
    {
        checks=0;var expected=JsonUtility.FromJson<ReloadState>(System.IO.File.ReadAllText("AgentArtifacts/wave-player-reload-baseline.json"));
        Check(System.IO.File.Exists("AgentArtifacts/wave-player-reload-event.txt")&&System.IO.File.ReadAllText("AgentArtifacts/wave-player-reload-event.txt")==expected.token,"A real assembly reload event occurred");
        Check(World.All.Count==expected.worlds,"Assembly reload releases all preview worlds");
        int temporary=0;foreach(var obj in Resources.FindObjectsOfTypeAll<GameObject>())if(obj.name=="Wave Conductor Preview (temporary)"||obj.name=="Wave Preview (temporary)")temporary++;
        Check(temporary==0,"Assembly reload destroys temporary visual roots");
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty==expected.dirty,"Reload preserves scene dirty state");
        return new{passed=true,checks,domainReloadCleanup=true,temporaryRoots=temporary};
    }
    public static async Task<object> Run()
    {
        checks=0;int worlds=World.All.Count;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        var other=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab").GetComponent<Wave>();
        string before=EditorJsonUtility.ToJson(source);var window=ScriptableObject.CreateInstance<WavePreviewPlayerWindow>();
        Action<SceneView> draw=null;
        try
        {
            Set(window,"followSelection",false);Set(window,"showAdvanced",true);Call(window,"SetSource",source);window.position=new Rect(100,100,700,900);window.ShowUtility();
            var player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Seek(5f);while(player.IsSeeking)player.Update(0,1000);player.Pause();
            string state=player.CurrentTime.ToString("R");
            Check(World.All.Count==worlds+1+player.SnapshotCount,"Window owns one live ECS world plus data-only snapshots");
            var view=SceneView.lastActiveSceneView;Check(view!=null,"Scene View available");
            var done=new TaskCompletionSource<bool>();int rendered=0;
            draw=current=>
            {
                if(current!=view||(Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint))return;
                try{Handles.BeginGUI();GUILayout.BeginArea(new Rect(5,20,690,1400));Call(window,"OnGUI");GUILayout.EndArea();Handles.EndGUI();if(Event.current.type==EventType.Repaint&&++rendered>=2)done.TrySetResult(true);}
                catch(Exception error){done.TrySetException(error);}
            };
            SceneView.duringSceneGui+=draw;var timeout=Task.Delay(10000);
            while(!done.Task.IsCompleted&&!timeout.IsCompleted){view.Repaint();await Task.Delay(30);}
            Check(done.Task.IsCompleted,"Transport/slider/events window GUI rendered");await done.Task;SceneView.duringSceneGui-=draw;draw=null;
            Check(player.CurrentTime.ToString("R")==state,"Repainting does not simulate time");
            Call(window,"SetSource",other);Check(World.All.Count==worlds,"Changing Wave disposes previous simulation");
            player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Play();player.Update(.1,1000);Call(window,"SceneChanged",scene,scene);
            Check(World.All.Count==worlds,"Scene change clears preview");
            player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Play();player.Update(.1,1000);
            Call(window,"PlayModeChanged",PlayModeStateChange.ExitingEditMode);Check(World.All.Count==worlds,"Leaving Edit Mode handler releases preview");
            player=(WavePreviewPlayer)Call(window,"EnsurePlayer");player.Play();player.Update(.1,1000);
            window.Close();if(window!=null)Object.DestroyImmediate(window);window=null;
            Check(World.All.Count==worlds,"Closing window frees ECS world");Check(scene.isDirty==dirty,"Window never dirties real scene");
            Check(EditorJsonUtility.ToJson(source)==before,"Window never changes Wave asset");
            return new{passed=true,checks,gui=true,sourceChange=true,sceneChange=true,closeCleanup=true,noAssetMutation=true};
        }
        finally{SceneView.duringSceneGui-=draw;if(window!=null){window.Close();if(window!=null)Object.DestroyImmediate(window);}}
    }
}
