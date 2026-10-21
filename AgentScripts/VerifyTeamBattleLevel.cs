using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyTeamBattleLevel {
static void Check(bool value,string message){if(!value)throw new Exception(message);}
static void Call(object target,string name){target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
public static object Run(){
if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Requires idle Editor");
var scene=EditorSceneManager.NewPreviewScene(); var root=new GameObject("Team level verification"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root,scene);
try {
root.AddComponent<Rigidbody2D>();var selection=root.AddComponent<ShipSelect>();var controller=root.AddComponent<PlayerController>();Call(controller,"Awake");Call(selection,"Awake");
var ships=new ParentShip[2];int[] events=new int[2];
for(int i=0;i<2;i++){var child=new GameObject("Ship "+i);child.transform.SetParent(root.transform);ships[i]=child.AddComponent<ParentShip>();ships[i].ShipData=AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Orbit.asset");child.AddComponent<WeaponController>();int index=i;ships[i].OnLevelChanged+=level=>events[index]++;}
selection.InitializeShips();Check(controller.TeamBattleLevel==1,"Initial team level");
typeof(ParentShip).GetField("playerController",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ships[1],controller);ships[1].Dying();Check(ships[1].IsDestroyed&&!ships[1].gameObject.activeSelf,"Destroyed ship is inactive");
Check(controller.LevelUpAllShips()==2,"Both ships receive upgrade");Check(ships[0].GetLevel()==2&&ships[1].GetLevel()==2,"Destroyed ship shares level");Check(events[0]==1&&events[1]==1,"Inactive ship receives progression event");
Check(ships[1].ReviveAfterWave(),"Revival succeeds");Check(ships[1].GetLevel()==2,"Revival preserves team level");
ships[1].SetLevel(4);Check(controller.TeamBattleLevel==4&&ships[0].GetLevel()==4,"Ship API updates team authority");
ships[0].LevelUp();Check(ships[1].GetLevel()==5&&controller.TeamBattleLevel==5,"Ship level-up synchronizes team");
controller.SetTeamBattleLevel(999);Check(ships[0].IsWeaponLevelMax&&ships[1].IsWeaponLevelMax,"Maximum applies to whole team");Check(controller.LevelUpAllShips()==0,"Maximum cannot upgrade");
return new {success=true,checks=10,level=controller.TeamBattleLevel};
}finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
}}