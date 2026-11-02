using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class VerifyAttackGroupsPrefab {
 public static object Run(){int checks=0;void Check(bool x,string m){checks++;if(!x)throw new Exception(m);}
  string path="Assets/Editor/AttackGroupsVerification-"+Guid.NewGuid().ToString("N")+".prefab";
  var scene=EditorSceneManager.NewPreviewScene();var go=new GameObject("Attack Groups isolated serialization fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
  try{
   var b=go.AddComponent<DirectedWaveAttackBehaviour>();b.SetUseAttackGroups(true);string first=b.AttackGroups[0].Id;
   PrefabUtility.SaveAsPrefabAssetAndConnect(go,path,InteractionMode.AutomatedAction);
   var second=b.CreateAttackGroup(first);string id=second.Id;b.AssignAttackGroup(0,id);PrefabUtility.RecordPrefabInstancePropertyModifications(b);
   PrefabUtility.ApplyObjectOverride(b,path,InteractionMode.AutomatedAction);
   var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<DirectedWaveAttackBehaviour>();
   Check(saved.UseAttackGroups&&saved.AttackGroups.Count==2,"Prefab Apply persists group collection");
   Check(saved.AttackGroups[0].Id==first&&saved.AttackGroups[1].Id==id,"Prefab Apply preserves stable IDs");
   Check(saved.GetAttackGroupForSlot(0)?.Id==id,"Prefab Apply persists assignment");
   b.AssignAttackGroup(0,null);PrefabUtility.RecordPrefabInstancePropertyModifications(b);Check(b.GetAttackGroupForSlot(0)==null,"Local override removes assignment");
   PrefabUtility.RevertObjectOverride(b,InteractionMode.AutomatedAction);Check(b.GetAttackGroupForSlot(0)?.Id==id,"Prefab Revert restores applied group membership");
   return new{passed=true,checks,applyRevert=true};
  }finally{UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)AssetDatabase.DeleteAsset(path);}
 }
}
