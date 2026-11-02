using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ProfileSceneAttackCacheAlloc {
 public class Row {public string name;public int calls;public double ms;public long bytes,allocations;}
 public class Capture {public string name;public int frames;public List<Row> rows;}
 public static async Task<object> Run(){
  if(ProfilerDriver.enabled)throw new Exception("Profiler recording already active; leave user recording intact");
  bool profileEditor=ProfilerDriver.profileEditor;var original=Selection.activeObject;var stage=PrefabStageUtility.GetCurrentPrefabStage();var captures=new List<Capture>();
  try {
   foreach(int id in new[]{3,4,5}){
    var go=stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SubWave_1 ("+id+")").gameObject;Selection.activeGameObject=go;ActiveEditorTracker.sharedTracker.ForceRebuild();
    for(int i=0;i<3;i++){InternalEditorUtility.RepaintAllViews();await Task.Delay(100);}
    ProfilerDriver.ClearAllFrames();ProfilerDriver.profileEditor=true;ProfilerDriver.enabled=true;
    try{for(int i=0;i<2;i++){InternalEditorUtility.RepaintAllViews();await Task.Delay(120);}}finally{ProfilerDriver.enabled=false;}
    await Task.Delay(150);var rows=new Dictionary<string,Row>();int frames=0;
    for(int f=ProfilerDriver.firstFrameIndex;f<=ProfilerDriver.lastFrameIndex;f++){
     using var v=ProfilerDriver.GetRawFrameDataView(f,0);if(!v.valid)continue;frames++;
     for(int i=0;i<v.sampleCount;i++){
      string n=v.GetSampleName(i);if(!n.StartsWith("WaveEditor.")&&n!="GC.Collect")continue;
      if(!rows.TryGetValue(n,out var row))rows[n]=row=new Row{name=n};row.calls++;row.ms+=v.GetSampleTimeMs(i);int end=i+v.GetSampleChildrenCountRecursive(i);
      for(int j=i+1;j<=end;j++)if(v.GetSampleName(j)=="GC.Alloc"&&v.GetSampleMetadataCount(j)>0){row.bytes+=v.GetSampleMetadataAsLong(j,0);row.allocations++;}
     }
    }
    captures.Add(new Capture{name="Warm "+id,frames=frames,rows=rows.Values.OrderByDescending(r=>r.bytes).ToList()});ProfilerDriver.ClearAllFrames();
   }
   string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{captures});File.WriteAllText("AgentArtifacts/SceneAttackCache-after-allocations.json",json);return captures;
  }finally{ProfilerDriver.enabled=false;ProfilerDriver.profileEditor=profileEditor;ProfilerDriver.ClearAllFrames();Selection.activeObject=original;}
 }
}
