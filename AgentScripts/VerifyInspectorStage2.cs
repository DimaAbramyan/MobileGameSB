using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class VerifyInspectorStage2 {
 const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
 static readonly List<string> checks=new();
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
 static void Clear()=>typeof(EnemyPresetInspector).GetMethod("ClearBuffers",Private).Invoke(null,null);
 static IDictionary Entries=>(IDictionary)typeof(EnemyPresetInspector).GetField("buffers",Private).GetValue(null);
 static int DataCount()=>Entries.Values.Cast<object>().Count(v=>v.GetType().GetField("Data").GetValue(v) is Object data&&data!=null);
 static void Change(SerializedObject so,string path,float value){so.FindProperty(path).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
 public static object Run(){
  checks.Clear();Clear();var objects=new List<Object>();int undo=-1;
  try {
   var go=new GameObject("Inspector stage 2 test"){hideFlags=HideFlags.HideAndDontSave};objects.Add(go);var attack=go.AddComponent<DirectedWaveAttackBehaviour>();
   using var so=new SerializedObject(attack);const string root="attackSettings.waveBurstSettings";var timing=so.FindProperty(root);var shooting=timing.FindPropertyRelative("shooting");
   timing.FindPropertyRelative("attackShotCount").intValue=11;so.ApplyModifiedPropertiesWithoutUndo();
   var withTiming=EnemyPresetInspector.DisplayProfile(shooting,timing);var noTiming=EnemyPresetInspector.DisplayProfile(shooting,null);
   Check(withTiming.AttackShotCount==11&&noTiming.AttackShotCount!=11,"timing/null contexts stay independent");
   Check(ReferenceEquals(withTiming,EnemyPresetInspector.DisplayProfile(shooting,timing)),"context switch preserves cached instance");
   Check(DataCount()==0,"read-only contexts allocate no ScriptableObject buffers");
   using(var shown=EnemyPresetInspector.DisplayShooting(shooting,timing))Check(shown!=null,"serialized view created lazily");
   Check(DataCount()==1,"one serialized buffer for Inspector");
   using(var shown=EnemyPresetInspector.DisplayShooting(shooting,timing))Check(shown.FindPropertyRelative("projectileType").intValue==shooting.FindPropertyRelative("projectileType").intValue,"disposing borrowed properties leaves cache usable");
   var nativeView=EnemyPresetInspector.DisplayTiming(shooting,timing);int dataId=nativeView.serializedObject.targetObject.GetInstanceID();nativeView.Dispose();
   timing.FindPropertyRelative("attackShotCount").intValue=12;
   Check(EnemyPresetInspector.DisplayProfile(shooting,timing).AttackShotCount==12,"unapplied serialized change visible");
   timing.FindPropertyRelative("attackShotCount").intValue=13;
   Check(EnemyPresetInspector.DisplayProfile(shooting,timing).AttackShotCount==13,"second edit in same event visible");
   so.ApplyModifiedPropertiesWithoutUndo();
   var typed=(DirectedWaveAttackSettings)typeof(DirectedWaveAttackBehaviour).GetField("attackSettings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(attack);
   typed.WaveBurstSettings.ConfigureLegacySingleAttack(17,1,.2f);so.Update();
   Check(EnemyPresetInspector.DisplayProfile(shooting,timing).AttackShotCount==17,"native contentHash detects direct update without dirty notification");
   using(var shown=EnemyPresetInspector.DisplayTiming(shooting,timing))Check(shown.serializedObject.targetObject.GetInstanceID()==dataId,"data revision reuses SerializedObject and ScriptableObject");
   var one=ScriptableObject.CreateInstance<EnemyProjectilePreset>();var two=ScriptableObject.CreateInstance<EnemyProjectilePreset>();objects.Add(one);objects.Add(two);
   var data=new EnemyShootingSettings();JsonUtility.FromJsonOverwrite("{\"overrideBaseSize\":true,\"baseSize\":2}",data);one.SetSettings(data);JsonUtility.FromJsonOverwrite("{\"baseSize\":4}",data);two.SetSettings(data);
   shooting.FindPropertyRelative("projectilePreset").objectReferenceValue=one;so.ApplyModifiedPropertiesWithoutUndo();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==2,"linked projectile preset resolved");
   shooting.FindPropertyRelative("projectilePreset").objectReferenceValue=two;so.ApplyModifiedPropertiesWithoutUndo();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==4,"switching preset refreshes immediately");
   JsonUtility.FromJsonOverwrite("{\"baseSize\":6}",data);two.SetSettings(data);
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==6,"preset revision refreshes without owner edit");
   var list=shooting.FindPropertyRelative("projectilePresetOverrides");list.arraySize=1;list.GetArrayElementAtIndex(0).stringValue="baseSize";shooting.FindPropertyRelative("baseSize").floatValue=9;so.ApplyModifiedPropertiesWithoutUndo();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==9,"Local Override wins over inherited value");
   var child=EnemyChildAttackSettings.CreateDefault();JsonUtility.FromJsonOverwrite("{\"triggerValue\":2}",child);
   shooting.FindPropertyRelative("childAttack").managedReferenceValue=child;shooting.FindPropertyRelative("useChildAttackPattern").boolValue=true;
   // Keep child local rather than inheriting a tree from the projectile preset.
   list.arraySize=3;list.GetArrayElementAtIndex(1).stringValue="childAttack";list.GetArrayElementAtIndex(2).stringValue="useChildAttackPattern";so.ApplyModifiedPropertiesWithoutUndo();
   var cp=shooting.FindPropertyRelative("childAttack.pattern");var cs=cp.FindPropertyRelative("shooting");
   using(var displayChild=EnemyPresetInspector.DisplayShooting(cs,cp))Check(EnemyChildAttackSettingsDrawer.IsChildPattern(displayChild),"nested child retains child-specific UI path");
   shooting.FindPropertyRelative("childAttack.triggerValue").floatValue=7;so.ApplyModifiedPropertiesWithoutUndo();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).ChildAttack.TriggerValue==7,"nested child edit invalidates parent view");
   Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.RegisterCompleteObjectUndo(attack,"Inspector stage2 test");
   shooting.FindPropertyRelative("baseSize").floatValue=10;so.ApplyModifiedPropertiesWithoutUndo();Undo.FlushUndoRecordObjects();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==10,"new override before Undo");Undo.PerformUndo();so.Update();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==9,"Undo refreshes view");Undo.PerformRedo();so.Update();
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==10,"Redo refreshes view");
   EnemyPresetInspector.Apply(shooting,timing,true);so.Update();Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==6,"Apply keeps original reset-override semantics");
   var unlink=typeof(EnemyPresetInspector).GetMethod("Unlink",Private);unlink.Invoke(null,new object[]{shooting,timing,true});so.Update();
   Check(shooting.FindPropertyRelative("projectilePreset").objectReferenceValue==null,"Unlink clears binding");
   Check(EnemyPresetInspector.DisplaySettings(shooting,timing).BaseSize==6,"Unlink materializes effective values");
   var other=new GameObject("Mixed Inspector test"){hideFlags=HideFlags.HideAndDontSave};objects.Add(other);var second=other.AddComponent<DirectedWaveAttackBehaviour>();
   using(var multi=new SerializedObject(new Object[]{attack,second})){
    var mt=multi.FindProperty(root);var ms=mt.FindPropertyRelative("shooting");Check(ReferenceEquals(ms,EnemyPresetInspector.DisplayShooting(ms,mt)),"multi-object retains original SerializedProperty");
   }
   foreach(string kind in new[]{"Behaviour","Projectile"}){
    var all=(IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod(kind+"Sections",Private).Invoke(null,new object[]{shooting});var allFields=all.ToArray();
    foreach(string field in allFields.Where(x=>x.StartsWith("#")))SessionState.SetBool("WaveInspector.Sections."+attack.GetInstanceID()+"."+shooting.propertyPath+"."+field.Substring(1),false);
    var visible=(IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod("Visible"+kind+"Sections",Private).Invoke(null,new object[]{shooting,shooting});
    bool section=false;var expected=allFields.Where(x=>{if(x.StartsWith("#")){section=true;return true;}return !section;}).ToArray();Check(visible.SequenceEqual(expected),"collapsed "+kind+" sections emit headers without hidden conditional fields");
   }
   // Repeated queries must leave all materialized buffers stable.
   var current=EnemyPresetInspector.DisplayProfile(shooting,timing);int count=DataCount();for(int i=0;i<50;i++){EnemyPresetInspector.DisplaySettings(shooting,null);Check(ReferenceEquals(current,EnemyPresetInspector.DisplayProfile(shooting,timing)),"cache hit "+i);}Check(DataCount()==count,"repeated reads create no additional buffers");
   var stress=new List<GameObject>();
   for(int i=0;i<36;i++){
    var item=new GameObject("Bounded Inspector cache "+i){hideFlags=HideFlags.HideAndDontSave};objects.Add(item);stress.Add(item);var component=item.AddComponent<DirectedWaveAttackBehaviour>();
    using var tmp=new SerializedObject(component);var t=tmp.FindProperty(root);using var shown=EnemyPresetInspector.DisplayShooting(t.FindPropertyRelative("shooting"),t);
   }
   var trim=typeof(EnemyPresetInspector).GetMethod("Trim",Private);trim.Invoke(null,null);
   Check(Entries.Count<=32,"inactive materialized contexts stay bounded");
   foreach(var item in stress)Object.DestroyImmediate(item);trim.Invoke(null,null);
   Check(Entries.Values.Cast<object>().All(v=>(Object)v.GetType().GetField("Owner").GetValue(v)!=null),"destroyed owners release cached views");
   var result=new{success=true,checks=checks.ToArray()};string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{result});File.WriteAllText("AgentArtifacts/InspectorStage2-regression.json",json);return new{success=true,checks=checks.Count};
  }finally{if(undo>=0)Undo.RevertAllDownToGroup(undo);Clear();foreach(var o in objects)if(o!=null)Object.DestroyImmediate(o);}
 }
}
