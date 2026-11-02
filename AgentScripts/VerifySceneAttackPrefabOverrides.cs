using System;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class VerifySceneAttackPrefabOverrides {
 delegate bool Lookup(DirectedWaveAttackBehaviour b,int s,bool e,out DirectedWaveAttackSettings v,out int i);
 public static object Run(){
 var get=(Lookup)typeof(DirectedWaveAttackBehaviourEditor).Assembly.GetType("SceneAttackSettingsCache").GetMethod("TryGet",BindingFlags.NonPublic|BindingFlags.Static).CreateDelegate(typeof(Lookup));
 var stage=PrefabStageUtility.GetCurrentPrefabStage();var scene=EditorSceneManager.NewPreviewScene();
 try{
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);var root=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);var t=root.GetComponentsInChildren<Transform>(true).First(x=>x.name=="SubWave_1 (5)");var attack=t.GetComponent<DirectedWaveAttackBehaviour>();
  get(attack,0,false,out var original,out _);float initial=original.RuntimeProjectileBaseSpeedOverride;
  using(var so=new SerializedObject(attack)){so.FindProperty("overrideBaseProjectileSpeed").boolValue=true;so.FindProperty("baseProjectileSpeed").floatValue=5.25f;so.ApplyModifiedPropertiesWithoutUndo();}EditorUtility.SetDirty(attack);PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
  get(attack,0,false,out var changed,out _);var expected=new DirectedWaveAttackSettings();attack.TryCopyResolvedAttackSettingsTo(0,false,expected);if(!changed.HasRuntimeProjectileBaseSpeedOverride||changed.RuntimeProjectileBaseSpeedOverride!=5.25f||ReferenceEquals(original,changed))throw new Exception("Prefab override not refreshed: actual="+changed.RuntimeProjectileBaseSpeedOverride+" expected="+expected.RuntimeProjectileBaseSpeedOverride);
  using(var so=new SerializedObject(attack)){var p=so.FindProperty("baseProjectileSpeed");if(!p.prefabOverride)throw new Exception("Expected real prefab override");PrefabUtility.RevertPropertyOverride(p,InteractionMode.AutomatedAction);}
  using(var so=new SerializedObject(attack)){PrefabUtility.RevertPropertyOverride(so.FindProperty("overrideBaseProjectileSpeed"),InteractionMode.AutomatedAction);}
  get(attack,0,false,out var reverted,out _);attack.TryCopyResolvedAttackSettingsTo(0,false,expected);if(reverted.RuntimeProjectileBaseSpeedOverride!=expected.RuntimeProjectileBaseSpeedOverride||reverted.HasRuntimeProjectileBaseSpeedOverride!=expected.HasRuntimeProjectileBaseSpeedOverride)throw new Exception("Prefab revert not refreshed");
  var result=new{success=true,initial,modified=changed.RuntimeProjectileBaseSpeedOverride,reverted=reverted.RuntimeProjectileBaseSpeedOverride,originalAssetUnmodified=true};string json=(string)Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{result});File.WriteAllText("AgentArtifacts/SceneAttackCache-prefab-overrides.json",json);return result;
 }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
}
