using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class VerifyWaveSceneVisualization
{
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static Type Visuals=>typeof(WaveEditor).Assembly.GetType("WaveSceneVisualization");
    static object Call(string name,params object[] args)=>Visuals.GetMethod(name,Static).Invoke(null,args);
    static bool Get(string name)=>(bool)Visuals.GetProperty(name,Static).GetValue(null);
    static void Set(string name,bool value)=>Visuals.GetProperty(name,Static).SetValue(null,value);
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static async Task Capture(SceneView view,string name)
    {
        var gameplay=(Camera)typeof(WaveEditor).Assembly.GetType("WavePreviewGameplayCamera").GetProperty("GameplayCamera",Static).GetValue(null);
        EnemyEmissionOriginSettings.TryGetGameplayBounds(gameplay,0,out var expected);
        bool matched=false;string mismatch=null;
        Action<SceneView> observe=current=>
        {
            if(current!=view||Event.current.type!=EventType.Repaint)return;
            EnemyEmissionOriginSettings.TryGetGameplayBounds(gameplay,0,out var actual);
            matched=true;
            if(Mathf.Abs(actual.width-expected.width)>.01f||Mathf.Abs(actual.height-expected.height)>.01f)mismatch=$"{expected} != {actual}";
        };
        SceneView.duringSceneGui+=observe;
        try{for(int i=0;i<5;i++){view.Repaint();await Task.Delay(70);}}
        finally{SceneView.duringSceneGui-=observe;}
        Check(matched&&mismatch==null,"Scene GUI projection matches Game View: "+mismatch);
        var rt=view.camera.targetTexture;if(rt==null)throw new Exception("No Scene View render texture");
        var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("AgentArtifacts/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;Object.DestroyImmediate(image);}
    }
    public static async Task<object> Run()
    {
        checks=0;
        var selection=Selection.objects;
        var current=Selection.activeGameObject;
        var wave=current!=null?current.GetComponentInParent<Wave>():null;
        if(wave==null)wave=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        var oldSource=Visuals.GetProperty("Source",Static).GetValue(null) as Wave;
        bool bounds=Get("ShowCameraBounds"),enemies=Get("ShowAllEnemySilhouettes");
        var view=SceneView.lastActiveSceneView;
        var pivot=view.pivot;var rotation=view.rotation;float size=view.size;bool ortho=view.orthographic;
        string before=EditorJsonUtility.ToJson(wave);
        int formations=0;
        try
        {
            Call("Bind",wave);
            Set("ShowCameraBounds",false);Set("ShowAllEnemySilhouettes",false);
            Check(!Get("ShowCameraBounds")&&!Get("ShowAllEnemySilhouettes"),"Both OFF");
            Set("ShowCameraBounds",true);Set("ShowAllEnemySilhouettes",true);
            Check(Get("ShowCameraBounds")&&Get("ShowAllEnemySilhouettes"),"Both ON");
            var child=wave.GetComponentsInChildren<Transform>(true).Last().gameObject;
            Selection.activeGameObject=child;
            Selection.objects=Array.Empty<Object>();
            Check(Get("ShowCameraBounds")&&Get("ShowAllEnemySilhouettes"),"Toggles survive selection change and no selection");
            Check((Wave)Visuals.GetProperty("Source",Static).GetValue(null)==wave,"Wave context retained without selection");
            int objects=Resources.FindObjectsOfTypeAll<GameObject>().Length;
            Call("RebuildFormation");
            var poses=(IList)Visuals.GetField("formation",Static).GetValue(null);formations=poses.Count;
            Check(formations>0,"All formation silhouettes available");
            int expectedCount=0;
            var prefabs=wave.ScheduledSubWaves.Count>0?wave.ScheduledSubWaves.Select(c=>c?.SubWavePrefab):wave.SubWavesToCreate;
            foreach(var prefab in prefabs)
                if(prefab!=null&&prefab.TryGetComponent<DirectedEnemySubWave>(out var subwave))
                    for(int slot=0;slot<subwave.GetSimulationEnemyCount();slot++)
                        if(subwave.GetConfiguredEnemyPrefabForSlot(slot)!=null)expectedCount++;
            Check(formations==expectedCount,"All configured enemies included without sampling");
            Check(objects==Resources.FindObjectsOfTypeAll<GameObject>().Length,"No GameObjects created for silhouettes");
            var first=poses[0];var enemy=(Enemy)first.GetType().GetField("Prefab").GetValue(first);
            var renderer=enemy.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite!=null);
            var meshType=typeof(WaveEditor).Assembly.GetType("WaveEnemySilhouette");
            var method=meshType.GetMethod("MeshFor",Static);
            var mesh=(Mesh)method.Invoke(null,new object[]{renderer.sprite});
            Check(mesh.vertexCount==renderer.sprite.vertices.Length,"Actual sprite geometry");
            Check(ReferenceEquals(mesh,method.Invoke(null,new object[]{renderer.sprite})),"Sprite mesh cache reused");
            var originalMatrix=(Matrix4x4)first.GetType().GetField("Matrix").GetValue(first);
            Check(originalMatrix.lossyScale==enemy.transform.lossyScale,"Authored scale retained");
            var gameplay=(Camera)typeof(WaveEditor).Assembly.GetType("WavePreviewGameplayCamera").GetProperty("GameplayCamera",Static).GetValue(null);
            var args=new object[]{gameplay,null};bool valid=(bool)Call("TryGetCameraBounds",args);
            Check(valid==EnemyEmissionOriginSettings.TryGetGameplayBounds(gameplay,0,out var expected),"Common camera bounds availability");
            if(valid)Check(((Rect)args[1])==expected,"Camera bounds match viewport shared with Runtime/Preview");
            view.orthographic=true;view.LookAtDirect(Vector3.zero,Quaternion.identity,7);
            Set("ShowAllEnemySilhouettes",false);await Capture(view,"wave-visualization-off");
            Set("ShowAllEnemySilhouettes",true);await Capture(view,"wave-visualization-all");
            var activeProperty=typeof(WavePreviewPlayerWindow).GetProperty("ActivePlayer",Static);
            var oldPlayer=activeProperty.GetValue(null);
            bool shooting=EnemyShootingPreview.ShowFormationVolley;
            using(var player=new WavePreviewPlayer(wave,10,new Vector3(0,-3,0)))
            {
                try
                {
                    player.Seek(2);
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    while(player.IsSeeking&&clock.ElapsedMilliseconds<10000)player.Update(0);
                    Check(!player.IsSeeking&&player.Simulation!=null,"Existing Preview seeks to current time");
                    activeProperty.SetValue(null,player);
                    EnemyShootingPreview.ShowFormationVolley=false;player.Simulation.SetVisible(false);
                    float time=player.CurrentTime;int snapshots=player.SnapshotCount;
                    int existing=Resources.FindObjectsOfTypeAll<GameObject>().Length;
                    Set("ShowAllEnemySilhouettes",true);await Capture(view,"wave-visualization-timeline");
                    Check(time==player.CurrentTime&&snapshots==player.SnapshotCount,"Overlay does not advance time or reset snapshots");
                    Check(existing==Resources.FindObjectsOfTypeAll<GameObject>().Length,"Live overlay reuses existing Preview objects");
                    var session=player.Simulation.GetSession(0);
                    if(session!=null)
                    {
                        var renderers=(System.Collections.Generic.List<Renderer>)typeof(WaveShootingPreviewSession).GetField("visualRenderers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(session);
                        var live=renderers.FirstOrDefault(r=>r is SpriteRenderer&&r.gameObject.activeInHierarchy&&r.enabled);
                        if(live!=null)
                        {
                            var needs=meshType.GetMethod("NeedsOverlay",Static);
                            Check((bool)needs.Invoke(null,new object[]{live}),"Hidden preview gets silhouette overlay");
                            live.forceRenderingOff=false;
                            Check(!(bool)needs.Invoke(null,new object[]{live}),"Already rendered enemy is not duplicated");
                            live.forceRenderingOff=true;
                        }
                    }
                }
                finally{activeProperty.SetValue(null,oldPlayer);EnemyShootingPreview.ShowFormationVolley=shooting;}
            }
            Check(EditorJsonUtility.ToJson(wave)==before,"Wave settings unchanged");
            Call("Invalidate");Check((bool)Visuals.GetField("dirty",Static).GetValue(null),"Configuration invalidation");
            Call("RebuildFormation");Check(!((bool)Visuals.GetField("dirty",Static).GetValue(null)),"Cache rebuilt");
            Set("ShowCameraBounds",false);Set("ShowAllEnemySilhouettes",false);
            Check(!Get("ShowCameraBounds")&&!Get("ShowAllEnemySilhouettes"),"Second toggle OFF");
            string result=$"Passed {checks} checks; {formations} formation enemies; silhouettes persist with no selection; no new GameObjects.";
            File.WriteAllText("AgentArtifacts/wave-visualization-checks.txt",result);
            return new{passed=true,checks,formations};
        }
        finally
        {
            Set("ShowCameraBounds",bounds);Set("ShowAllEnemySilhouettes",enemies);
            if(oldSource!=null)Call("Bind",oldSource);
            view.orthographic=ortho;view.LookAtDirect(pivot,rotation,size);Selection.objects=selection;SceneView.RepaintAll();
        }
    }
}
