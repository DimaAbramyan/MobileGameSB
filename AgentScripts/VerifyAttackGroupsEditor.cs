using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public sealed class AttackGroupEditorProbeWindow:EditorWindow {
 public Editor Inspector;public Exception Error;public int Repaints;
 Vector2 scroll;void OnGUI(){if(Inspector==null)return;scroll=EditorGUILayout.BeginScrollView(scroll);try{Inspector.OnInspectorGUI();}catch(Exception e){Error=e;}finally{EditorGUILayout.EndScrollView();}if(Event.current.type==EventType.Repaint)Repaints++;}
}
public static class VerifyAttackGroupsEditor {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;static int checks;
 static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static DirectedWaveAttackSettings Scene(DirectedWaveAttackBehaviour b,int slot){var type=typeof(DirectedWaveAttackBehaviourEditor).Assembly.GetType("SceneAttackSettingsCache");var args=new object[]{b,slot,false,null,0};bool found=(bool)type.GetMethod("TryGet",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);Check(found,"Scene group settings found");return (DirectedWaveAttackSettings)args[3];}
 public static async Task<object> Run(){checks=0;var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");Editor editor=null;AttackGroupEditorProbeWindow window=null;var focus=EditorWindow.focusedWindow;var objects=new List<Object>();try{
  var sub=root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();var b=sub.AttackPatternBehaviour;
  Set(sub.Formation,"customFormationPoints",new[]{Vector3.left,Vector3.right});
  var settings=new DirectedWaveAttackSettings();Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(200);settings.Validate();
  Set(b,"attackPreset",null);Set(b,"attackSettings",settings);Set(b,"attackPatterns",new List<DirectedWaveAttackPattern>());Set(b,"executionMode",DirectedWaveAttackExecutionMode.Patterns);b.SetUseAttackGroups(true);
  var a=b.AttackGroups[0];var second=b.CreateAttackGroup(a.Id);b.AssignAttackGroup(1,second.Id);b.InvalidateSequenceGuides();
  var authored=(DirectedWaveAttackSettings)Get(a.Single,"attackSettings");var child=new EnemyChildAttackSettings();child.Pattern.Shooting.SetChildAttack(new EnemyChildAttackSettings());authored.WaveBurstSettings.AuthoredShooting.SetChildAttack(child);b.CopyAttackGroup(second.Id,a.Id);
  using(var data=new SerializedObject(b)){
   var ca=data.FindProperty("attackGroups.Array.data[0].single.attackSettings.waveBurstSettings.shooting.childAttack");var cb=data.FindProperty("attackGroups.Array.data[1].single.attackSettings.waveBurstSettings.shooting.childAttack");
   Check(ca.managedReferenceId!=cb.managedReferenceId,"Group copies own independent serialized Child reference IDs");
   ca.FindPropertyRelative("triggerValue").floatValue=2;data.ApplyModifiedPropertiesWithoutUndo();data.Update();
   Check(data.FindProperty("attackGroups.Array.data[1].single.attackSettings.waveBurstSettings.shooting.childAttack.triggerValue").floatValue!=2,"Serialized Child edit does not change copied group");
  }
  a=b.AttackGroups[0];second=b.AttackGroups[1];
  string selectionKey="WaveAttack.ActiveGroup."+b.GetInstanceID();editor=Editor.CreateEditor(b);window=ScriptableObject.CreateInstance<AttackGroupEditorProbeWindow>();window.position=new Rect(100,100,620,850);window.Inspector=editor;window.ShowUtility();
  var cached=Scene(b,0);
  for(int i=0;i<6;i++){
   string id=i%2==0?a.Id:second.Id;SessionState.SetString(selectionKey,id);string before=JsonUtility.ToJson(b);int n=window.Repaints;
   double until=EditorApplication.timeSinceStartup+5;while(window.Repaints<n+2&&EditorApplication.timeSinceStartup<until){window.Repaint();await Task.Delay(25);if(window.Error!=null)throw window.Error;}
   Check(window.Repaints>=n+2,"Group GUI Layout/Repaint observed");Check(JsonUtility.ToJson(b)==before,"Group switch does not mutate data or copy configuration");
   var bound=(SerializedProperty)Get(editor,"attackSettings");Check(bound.propertyPath.Contains("attackGroups.Array.data["+(i%2)+"].single"),"Existing drawers bound to selected group");
   Check(ReferenceEquals(cached,Scene(b,0)),"Group switch keeps Scene cache materialization");
  }
  window.Inspector=null;
  var preset=ScriptableObject.CreateInstance<EnemyProjectilePreset>();objects.Add(preset);var p=new EnemyShootingSettings();JsonUtility.FromJsonOverwrite("{\"overrideBaseSize\":true,\"baseSize\":2}",p);preset.SetSettings(p);
  var groupSettings=(DirectedWaveAttackSettings)Get(a.Single,"attackSettings");Set(groupSettings.WaveBurstSettings.AuthoredShooting,"projectilePreset",preset);b.InvalidateSequenceGuides();EditorUtility.SetDirty(b);
  Check(Scene(b,0).WaveBurstSettings.Shooting.BaseSize==2,"Group projectile preset resolves");
  JsonUtility.FromJsonOverwrite("{\"baseSize\":3}",p);preset.SetSettings(p);Check(Scene(b,0).WaveBurstSettings.Shooting.BaseSize==3,"Preset edit invalidates group materialization");
  var local=root.AddComponent<DirectedWaveEnemyOverride>();var localProfile=new EnemyBurstAttackSettings();JsonUtility.FromJsonOverwrite("{\"overrideBaseSize\":true,\"baseSize\":4}",localProfile.AuthoredShooting);local.SetAttackSettingsOverride(localProfile);
  var copy=EnemyAttackSettingsResolver.CreateRuntimeCopy(Scene(b,0).WaveBurstSettings,local,null);Check(copy.Shooting.BaseSize==4,"Local override keeps priority over group preset");
  Set(groupSettings.WaveBurstSettings.AuthoredShooting,"projectilePreset",null);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab"),root.scene);objects.Add(instance);
  var inst=instance.transform.Find("Main").GetComponent<DirectedEnemySubWave>().AttackPatternBehaviour;inst.SetUseAttackGroups(true);PrefabUtility.RecordPrefabInstancePropertyModifications(inst);
  Check(PrefabUtility.HasPrefabInstanceAnyOverrides(instance,false),"Groups produce normal prefab overrides");
  PrefabUtility.RevertObjectOverride(inst,InteractionMode.AutomatedAction);Check(!inst.UseAttackGroups,"Prefab Revert restores legacy source");
  return new{passed=true,checks,groupSwitch=true,presets=true,localOverride=true,prefabRevert=true};
 }finally{if(window!=null){window.Inspector=null;window.Close();}if(editor!=null)Object.DestroyImmediate(editor);foreach(var o in objects)if(o!=null)Object.DestroyImmediate(o);PrefabUtility.UnloadPrefabContents(root);if(focus!=null)focus.Focus();}}
}
