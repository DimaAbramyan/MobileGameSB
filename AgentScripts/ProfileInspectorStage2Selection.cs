using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
public static class ProfileInspectorStage2Selection {
 static readonly ProfilerMarker Marker=new("WaveEditor.Inspector.Selection");
 public class Row{public string name;public int calls;public double totalMs,selfMs,maxMs;public long bytes;}
 public static async Task<object> Run(){
 if(ProfilerDriver.enabled)throw new Exception("User Profiler already recording");
 var selection=Selection.activeObject;bool editor=ProfilerDriver.profileEditor;var result=new List<object>();var stage=PrefabStageUtility.GetCurrentPrefabStage();
 try{foreach(int id in new[]{3,4,5}){
  Selection.activeGameObject=stage.prefabContentsRoot;ActiveEditorTracker.sharedTracker.ForceRebuild();await Task.Delay(120);
  ProfilerDriver.ClearAllFrames();ProfilerDriver.profileEditor=true;ProfilerDriver.enabled=true;await Task.Delay(30);
  var go=stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="SubWave_1 ("+id+")").gameObject;
  using(Marker.Auto()){Selection.activeGameObject=go;ActiveEditorTracker.sharedTracker.ForceRebuild();}
  InternalEditorUtility.RepaintAllViews();await Task.Delay(200);ProfilerDriver.enabled=false;await Task.Delay(120);
  var rows=new Dictionary<string,Row>();int samples=0;
  for(int f=ProfilerDriver.firstFrameIndex;f<=ProfilerDriver.lastFrameIndex;f++){
   using var v=ProfilerDriver.GetRawFrameDataView(f,0);if(!v.valid)continue;
   for(int i=0;i<v.sampleCount;i++)if(v.GetSampleName(i)=="WaveEditor.Inspector.Selection"){
    samples++;int end=i+v.GetSampleChildrenCountRecursive(i);
    for(int j=i;j<=end;j++){
     string name=v.GetSampleName(j);if(!rows.TryGetValue(name,out var r))rows[name]=r=new Row{name=name};
     double time=v.GetSampleTimeMs(j),children=0;int last=j+v.GetSampleChildrenCountRecursive(j);
     for(int k=j+1;k<=last;k+=1+v.GetSampleChildrenCountRecursive(k))children+=v.GetSampleTimeMs(k);
     r.calls++;r.totalMs+=time;r.selfMs+=Math.Max(0,time-children);r.maxMs=Math.Max(r.maxMs,time);
     if(name=="GC.Alloc"&&v.GetSampleMetadataCount(j)>0)r.bytes+=v.GetSampleMetadataAsLong(j,0);
    }
   }
  }
  result.Add(new{subwave=id,samples,selectionMs=rows.TryGetValue("WaveEditor.Inspector.Selection",out var selected)?selected.totalMs:0,rows=rows.Values.OrderByDescending(r=>r.selfMs).Take(25).ToArray()});ProfilerDriver.ClearAllFrames();
 }}finally{ProfilerDriver.enabled=false;ProfilerDriver.profileEditor=editor;ProfilerDriver.ClearAllFrames();Selection.activeObject=selection;}
 string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{result});File.WriteAllText("AgentArtifacts/InspectorStage2-selection-native.json",json);return new{file="AgentArtifacts/InspectorStage2-selection-native.json"};
 }
}
