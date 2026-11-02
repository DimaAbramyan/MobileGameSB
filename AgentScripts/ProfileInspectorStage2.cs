using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Unity.Profiling;
public static class ProfileInspectorStage2 {
 static readonly string[] Names={"WaveEditor.Inspector.Layout","WaveEditor.Inspector.Repaint","WaveEditor.Presets.Display","WaveEditor.Presets.ReadProfile","WaveEditor.Presets.CreateBuffer","WaveEditor.Presets.Rebuild","WaveEditor.Presets.Signature","WaveEditor.Inspector.CategoryHeight","WaveEditor.Inspector.DrawGroup","WaveEditor.Inspector.SectionHeight","WaveEditor.Inspector.SectionDraw","WaveEditor.Inspector.ChildHeight","WaveEditor.Inspector.DrawTiming"};
 static Action Clear= (Action)typeof(DirectedWaveAttackBehaviourEditor).Assembly.GetType("SceneAttackSettingsCache").GetMethod("Clear",BindingFlags.Static|BindingFlags.NonPublic).CreateDelegate(typeof(Action));
 public class Metric {public string name;public long calls;public double totalMs,maxFrameMs;public int frames;public List<double> samples=new();}
 public class Phase {public string name;public double selectionMs;public object[] editors;public List<Metric> metrics=new();}
 static async Task PhaseFor(List<Phase> phases,GameObject go,string name,int repaintCount){
  var rec=Names.Select(n=>ProfilerRecorder.StartNew(ProfilerCategory.Scripts,n,1024)).ToArray();var phase=new Phase{name=name};
  try {
   if(go!=null){var clock=System.Diagnostics.Stopwatch.StartNew();Selection.activeGameObject=go;ActiveEditorTracker.sharedTracker.ForceRebuild();clock.Stop();phase.selectionMs=clock.Elapsed.TotalMilliseconds;}
   for(int i=0;i<repaintCount;i++){InternalEditorUtility.RepaintAllViews();await Task.Delay(120);}
   await Task.Delay(60);
   phase.editors=ActiveEditorTracker.sharedTracker.activeEditors.Select(e=>(object)e.GetType().Name).ToArray();
   for(int i=0;i<rec.Length;i++){if(rec[i].Valid)rec[i].Stop();var m=new Metric{name=Names[i],frames=rec[i].Valid?rec[i].Count:0};for(int j=0;j<m.frames;j++){var s=rec[i].GetSample(j);if(s.Count>0)m.samples.Add(s.Value/1000000.0/s.Count);m.calls+=s.Count;m.totalMs+=s.Value/1000000.0;m.maxFrameMs=Math.Max(m.maxFrameMs,s.Value/1000000.0);}phase.metrics.Add(m);}
   if(phase.metrics[0].calls < (go==null?Math.Min(10,repaintCount):1)
      || phase.metrics[1].calls < (go==null?Math.Min(10,repaintCount):1))
       throw new Exception("Inspector did not issue enough Layout/Repaint callbacks for "+name);
   phases.Add(phase);
  }finally{foreach(var r in rec)r.Dispose();}
 }
 public static Task<object> Before()=>Run("before");
 public static Task<object> After()=>Run("after");
 public static async Task<object> Run(string label){
  var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||!stage.assetPath.EndsWith("2_5_extrime.prefab"))throw new Exception("Expected 2_5_extrime stage");
  bool savedShooting=EnemyShootingPreview.ShowFormationVolley;var original=Selection.activeObject;var phases=new List<Phase>();
  var expanded=stage.prefabContentsRoot.GetComponentsInChildren<DirectedWaveAttackBehaviour>(true).Select(a=>(target:(UnityEngine.Object)a,value:InternalEditorUtility.GetIsInspectorExpanded(a))).ToArray();
  try {
   foreach(var pair in expanded)InternalEditorUtility.SetIsInspectorExpanded(pair.target,true);
   foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())if(window.GetType().Name=="InspectorWindow"){window.Show();window.Focus();window.Repaint();}
   EnemyShootingPreview.ShowFormationVolley=true;Clear();
   var warm=stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SubWave_1 (3)").gameObject;
   Selection.activeGameObject=warm;ActiveEditorTracker.sharedTracker.ForceRebuild();
   for(int i=0;i<3;i++){InternalEditorUtility.RepaintAllViews();await Task.Delay(120);}
   typeof(EnemyPresetInspector).GetMethod("ClearBuffers",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);

   foreach(int id in new[]{3,4,5}) {
    var go=stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SubWave_1 ("+id+")").gameObject;
    Selection.activeGameObject=stage.prefabContentsRoot;ActiveEditorTracker.sharedTracker.ForceRebuild();await Task.Delay(180);
    await PhaseFor(phases,go,"First "+id,3);
    await PhaseFor(phases,null,"Steady "+id,20);
    Selection.activeGameObject=stage.prefabContentsRoot;ActiveEditorTracker.sharedTracker.ForceRebuild();await Task.Delay(180);
    await PhaseFor(phases,go,"Reopen "+id,3);
   }
   string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{phases});File.WriteAllText("AgentArtifacts/InspectorStage2-"+label+"-timings.json",json);
   return new{file="AgentArtifacts/InspectorStage2-"+label+"-timings.json",phases=phases.Count};
  }finally{foreach(var pair in expanded)if(pair.target!=null)InternalEditorUtility.SetIsInspectorExpanded(pair.target,pair.value);Selection.activeObject=original;EnemyShootingPreview.ShowFormationVolley=savedShooting;Clear();}
 }
}
