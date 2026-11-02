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
public static class ProfileSceneAttackCache {
 static readonly string[] Names={"WaveEditor.ShootingSceneGUI.Repaint","WaveEditor.AttackSceneGUI.Repaint","WaveEditor.SceneAttackSettings.Resolve","WaveEditor.SceneAttackSettings.Lookup"};
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
   for(int i=0;i<rec.Length;i++){rec[i].Stop();var m=new Metric{name=Names[i],frames=rec[i].Count};for(int j=0;j<rec[i].Count;j++){var s=rec[i].GetSample(j);if(s.Count>0)m.samples.Add(s.Value/1000000.0/s.Count);m.calls+=s.Count;m.totalMs+=s.Value/1000000.0;m.maxFrameMs=Math.Max(m.maxFrameMs,s.Value/1000000.0);}phase.metrics.Add(m);}
   phases.Add(phase);
  }finally{foreach(var r in rec)r.Dispose();}
 }
 public static async Task<object> Run(){
  var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||!stage.assetPath.EndsWith("2_5_extrime.prefab"))throw new Exception("Expected 2_5_extrime stage");
  var original=Selection.activeObject;var phases=new List<Phase>();
  try {
   Clear();
   foreach(int id in new[]{3,4,5}) {
    var go=stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SubWave_1 ("+id+")").gameObject;
    Selection.activeGameObject=stage.prefabContentsRoot;ActiveEditorTracker.sharedTracker.ForceRebuild();await Task.Delay(180);
    await PhaseFor(phases,go,"First "+id,3);
    await PhaseFor(phases,null,"Steady "+id,20);
    Selection.activeGameObject=stage.prefabContentsRoot;ActiveEditorTracker.sharedTracker.ForceRebuild();await Task.Delay(180);
    await PhaseFor(phases,go,"Reopen "+id,3);
   }
   string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{phases});File.WriteAllText("AgentArtifacts/SceneAttackCache-after-timings.json",json);
   return new{file="AgentArtifacts/SceneAttackCache-after-timings.json",phases};
  }finally{Selection.activeObject=original;Clear();}
 }
}
