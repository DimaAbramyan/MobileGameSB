using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public sealed class AttackGroupsProfileWindow:EditorWindow {
 public Editor Inspector; public int Layouts,Repaints,Warm; public double LayoutMs,RepaintMs; public long LayoutBytes,RepaintBytes; public Exception Error;
 Vector2 scroll;
 void OnGUI(){if(Inspector==null)return;scroll=EditorGUILayout.BeginScrollView(scroll);
  bool measure=Warm>=20;var type=Event.current.type;long b=GC.GetAllocatedBytesForCurrentThread(), t=Stopwatch.GetTimestamp();
  try{Inspector.OnInspectorGUI();}catch(Exception e){Error=e;}finally{double ms=(Stopwatch.GetTimestamp()-t)*1000d/Stopwatch.Frequency;long bytes=GC.GetAllocatedBytesForCurrentThread()-b;
   if(type==EventType.Layout&&measure&&Layouts<20){Layouts++;LayoutMs+=ms;LayoutBytes+=bytes;}
   if(type==EventType.Repaint){Warm++;if(measure&&Repaints<20){Repaints++;RepaintMs+=ms;RepaintBytes+=bytes;}}EditorGUILayout.EndScrollView();}
 }
}
public static class ProfileAttackGroupsInspector {
 public static async Task<object> Run(string phase){
  GameObject root=null;Editor editor=null;AttackGroupsProfileWindow window=null;var focus=EditorWindow.focusedWindow;
  try{
   root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_5_extrime.prefab");
   DirectedWaveAttackBehaviour source=null;foreach(var a in root.GetComponentsInChildren<DirectedWaveAttackBehaviour>(true))if(a.name=="SubWave_1 (5)")source=a;
   if(source==null)throw new Exception("Fixture missing");
   editor=Editor.CreateEditor(source);window=ScriptableObject.CreateInstance<AttackGroupsProfileWindow>();window.position=new Rect(100,100,580,850);window.Inspector=editor;window.ShowUtility();
   double until=EditorApplication.timeSinceStartup+25;
   while((window.Layouts<20||window.Repaints<20)&&EditorApplication.timeSinceStartup<until){window.Repaint();await Task.Delay(25);if(window.Error!=null)throw window.Error;}
   if(window.Layouts!=20||window.Repaints!=20)throw new Exception("Invalid GUI sample counts");
   var result=new{phase,fixture="2_5_extrime/SubWave_1 (5)",width=580,layouts=window.Layouts,repaints=window.Repaints,layoutMs=window.LayoutMs/20,repaintMs=window.RepaintMs/20,layoutBytes=window.LayoutBytes/20,repaintBytes=window.RepaintBytes/20};
   File.WriteAllText("AgentArtifacts/AttackGroups-"+phase+"-inspector.json",JsonUtility.ToJson(new ProfileData{Phase=phase,LayoutMs=window.LayoutMs/20,RepaintMs=window.RepaintMs/20,LayoutBytes=window.LayoutBytes/20,RepaintBytes=window.RepaintBytes/20},true));return result;
  }finally{if(window!=null){window.Inspector=null;window.Close();}if(editor!=null)Object.DestroyImmediate(editor);if(root!=null)PrefabUtility.UnloadPrefabContents(root);if(focus!=null)focus.Focus();}
 }
 [Serializable]class ProfileData{public string Phase;public double LayoutMs,RepaintMs;public long LayoutBytes,RepaintBytes;}
}
