using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class VerifyAttackGroups
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static int checks;
 static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v){var f=o.GetType().GetField(n,F);if(f!=null)f.SetValue(o,v);else o.GetType().GetProperty(n,F).SetValue(o,v);}
 static DirectedWaveAttackSettings Settings(DirectedWaveAttackPattern p)=>(DirectedWaveAttackSettings)Get(p,"attackSettings");
 static DirectedWaveAttackSettings Shooting(float angle,int rays=1){var s=new DirectedWaveAttackSettings();Set(s,"fireMode",DirectedWaveAttackFireMode.Stationary);s.StationaryFireSettings.ConfigureAngle(angle);Set(s,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);s.WaveBurstSettings.ConfigureLegacySingleAttack(1,.2f,10f);var p=s.WaveBurstSettings.Shooting;p.SetScreenBoundary(new EnemyProjectileScreenSettings(false));p.SetProjectileBaseSpeedOverride(true,1);Set(p,"overrideProjectileLifetime",true);Set(p,"projectileLifetime",20f);if(rays>1)p.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial,rays);return s;}
 static void Configure(DirectedWaveAttackPattern p,float angle,int rays=1)=>p.Configure(Shooting(angle,rays),null,false,1,false,1,null,EnemySpeedRandomizeOn.PerProjectile);
 static void Dirty(DirectedWaveAttackBehaviour b){b.InvalidateSequenceGuides();EditorUtility.SetDirty(b);}
 static void Advance(WaveShootingPreviewSession s,float t){int guard=0;while(!s.AdvanceTo(t,1000)){if(++guard>100)throw new Exception("Advance stalled");}}
 static string Capture(WavePreviewPlayer p){var m=p.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using var q=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));using var es=q.ToEntityArray(Allocator.Temp);var rows=new List<string>();foreach(var e in es)rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e))+JsonUtility.ToJson(m.GetComponentData<EnemyProjectileVelocity>(e)));rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows);}
 static void Seek(WavePreviewPlayer p,float t){p.Seek(t);int n=0;while(p.IsSeeking){p.Update(0,100);if(++n>500)throw new Exception("Seek stalled");}}
 public static object Run(){checks=0;var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");int undo=-1;try{
  var wave=root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();var b=wave.AttackPatternBehaviour;
  Set(wave.Formation,"customFormationPoints",new[]{Vector3.left,Vector3.zero,Vector3.right});
  Set(b,"executionMode",DirectedWaveAttackExecutionMode.Patterns);Set(b,"attackPatterns",new List<DirectedWaveAttackPattern>());Set(b,"attackPreset",null);
  var common=Shooting(200);common.AttackOrder.enabled=true;common.AttackOrder.mode=DirectedWaveAttackOrderMode.Simultaneous;common.AttackOrder.loopOrder=false;common.AttackOrder.waitForPreviousEnemy=true;
  Set(b,"attackSettings",common);Set(b,"useSelectedEnemySlots",false);Set(b,"attackGroups",new List<DirectedWaveAttackGroup>());Set(b,"attackGroupSlots",new List<DirectedWaveAttackGroupSlot>());Set(b,"useAttackGroups",false);
  Check(!b.UseAttackGroups,"Old default groups off");string timing=JsonUtility.ToJson(common.AttackOrder);
  b.SetUseAttackGroups(true);Check(b.AttackGroups.Count==1,"First enable creates Group 1");var a=b.AttackGroups[0];string aid=a.Id;
  Check(b.GetAttackGroupForSlot(0)==a&&b.GetAttackGroupForSlot(2)==a,"First group inherits eligible slots");
  var child=new EnemyChildAttackSettings();Settings(a.Single).WaveBurstSettings.AuthoredShooting.SetChildAttack(child);
  var second=b.CreateAttackGroup(aid);string bid=second.Id;Check(aid!=bid,"Stable unique IDs");
  var childCopy=Settings(second.Single).WaveBurstSettings.AuthoredShooting.ChildAttack;
  Check(childCopy!=null&&!ReferenceEquals(child,childCopy),"Nested Child deep copied");
  JsonUtility.FromJsonOverwrite("{\"triggerValue\":7}",childCopy);Check(child.TriggerValue!=7,"Child edits isolated");
  Configure(a.Single,200);Configure(second.Single,60);Dirty(b);b.AssignAttackGroup(1,bid);b.AssignAttackGroup(2,null);
  Check(b.GetAttackGroupForSlot(2)==null,"Unassigned slot");
  b.TryGetGroupAttackSettings(0,out var sa);b.TryGetGroupAttackSettings(1,out var sb);
  Check((float)Get(sa.StationaryFireSettings,"angle")==200&&(float)Get(sb.StationaryFireSettings,"angle")==60,"Distinct stationary angles");
  Check(!ReferenceEquals(sa,sb),"Runtime materializations independent");
  for(int i=0;i<20;i++){b.TryGetGroupAttackSettings(0,out var same);Check(ReferenceEquals(sa,same),"Hot settings reused");}
  b.SetUseAttackGroups(false);Check(!b.UseAttackGroups&&b.AttackGroups.Count==2,"Disable preserves groups");b.SetUseAttackGroups(true);Check(b.GetAttackGroupForSlot(1)?.Id==bid,"Reenable restores assignments");
  string secondId=second.Id;b.CopyAttackGroup(bid,aid);Check(second.Id==secondId&&b.GetAttackGroupForSlot(1)?.Id==bid,"Copy preserves identity and assignment");Configure(second.Single,60);Dirty(b);
  Check(JsonUtility.ToJson(common.AttackOrder)==timing,"Group operations preserve shared order");
  var resolved=new DirectedWaveAttackSettings();b.TryCopyResolvedAttackSettingsTo(1,false,resolved);Check(resolved.AttackOrder.mode==DirectedWaveAttackOrderMode.Simultaneous,"Static preview uses shared timing/order");
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){Advance(session,.2f);Check(session.ProjectileCount==2,"Only assigned slots emit");var m=session.ProjectileWorld.SimulationWorld.EntityManager;using var q=m.CreateEntityQuery(typeof(EnemyProjectileVelocity));using var vs=q.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp);bool left=false,right=false;foreach(var v in vs){left|=v.Value.x<-.1f;right|=v.Value.x>.1f;}Check(left&&right,"Preview uses both group directions");}
  var delayedChild=new EnemyChildAttackSettings();Set(delayedChild,"triggerValue",0f);Set(delayedChild.Pattern,"attackShotCount",1);delayedChild.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
  Settings(a.Single).WaveBurstSettings.AuthoredShooting.SetChildAttack(delayedChild);Dirty(b);
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){Advance(session,.2f);Check(session.ProjectileCount==3,"Group-specific Child emits without affecting other group");}
  Settings(a.Single).WaveBurstSettings.AuthoredShooting.SetChildAttack(null);Dirty(b);
  var projectileSettings=Settings(second.Single).WaveBurstSettings.AuthoredShooting;var oldType=projectileSettings.ProjectileType;
  projectileSettings.SetProjectileType(EnemyProjectileType.Homing);Set(projectileSettings,"projectilePrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/HomingEnemyProjectile.prefab").GetComponent<EnemyBullet>());Dirty(b);
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){Advance(session,.2f);using var homing=session.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileHoming));Check(session.ProjectileCount==2&&homing.CalculateEntityCount()==1,"Groups spawn different actual projectile prefabs/movement");}
  projectileSettings.SetProjectileType(oldType);Set(projectileSettings,"projectilePrefab",null);Dirty(b);
  // Multiple Patterns stays scoped to its group, with legacy per-pattern slot selection.
  var multi=new DirectedWaveAttackPattern();Configure(multi,60);Set(multi,"selectedEnemySlots",new List<int>{1});Set(second,"patterns",new List<DirectedWaveAttackPattern>{multi});
  var third=b.CreateAttackGroup(aid);Set(third,"executionMode",DirectedWaveAttackExecutionMode.Sequence);third.Sequence.steps.Clear();
  var one=new DirectedWaveAttackSequenceStep{name="Up"};Configure(one.pattern,0);Settings(one.pattern).WaveBurstSettings.ConfigureLegacySingleAttack(1,.2f,.1f);
  var two=new DirectedWaveAttackSequenceStep{name="Down"};Configure(two.pattern,180);Settings(two.pattern).WaveBurstSettings.ConfigureLegacySingleAttack(1,.2f,.1f);
  third.Sequence.steps.Add(new DirectedWaveAttackSequenceStep{type=DirectedWaveAttackSequenceStepType.RepeatBlock,repeatCount=2,steps=new List<DirectedWaveAttackSequenceStep>{one}});third.Sequence.steps.Add(new DirectedWaveAttackSequenceStep{type=DirectedWaveAttackSequenceStepType.Pause,pauseDuration=.15f});third.Sequence.steps.Add(two);b.AssignAttackGroup(2,third.Id);Dirty(b);
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){Advance(session,2);Check(session.ProjectileCount==5,"Single + Multiple + Sequence use shared dispatch and distinct programs");}
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){var owner=new object();Advance(session,.05f);session.SetPermission(owner,WaveAttackPermission.BlockNewAttacks);Advance(session,2);Check(session.ProjectileCount==5,"FinishCurrent lets already granted group Sequence finish");}
  using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4))){var owner=new object();Advance(session,.05f);int count=session.ProjectileCount;session.SetPermission(owner,WaveAttackPermission.InterruptAttacks);Advance(session,.8f);Check(session.ProjectileCount==count,"Interrupt freezes group Sequence");session.ReleasePermission(owner);Advance(session,3);Check(session.ProjectileCount==5,"Resume continues group Sequence");}
  // Shared deterministic seek captures the per-slot Sequence Player and its inner runs.
  var conductor=root.GetComponent<Wave>();Set(conductor,"scheduledSubWaves",new List<WaveSubWaveCue>{new(wave.gameObject,0)});Set(wave,"spawnInterval",0f);Set(wave,"settleDuration",0f);
  using(var player=new WavePreviewPlayer(conductor,123,new Vector3(0,-4))){player.SnapshotInterval=.1f;Seek(player,2);string expected=Capture(player);Check(expected.Length>0,"Seek has projectiles");Seek(player,.15f);Seek(player,2);Check(Capture(player)==expected,"Snapshots/rewind restore group sequence deterministically");Check(player.RestoredSnapshots>0,"Actual snapshots restored");}
  RuntimeProbe(wave,b,common,aid,bid);
  // Authoring Undo/Redo and stable IDs.
  Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.RegisterCompleteObjectUndo(b,"Group regression");b.AssignAttackGroup(1,aid);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Check(b.GetAttackGroupForSlot(1)?.Id==bid,"Undo assignment");Undo.PerformRedo();Check(b.GetAttackGroupForSlot(1)?.Id==aid,"Redo assignment");
  TransformSlots(wave,b,aid,bid);
  Check(b.RemoveAttackGroup(bid)&&b.AttackGroups.Count==2,"Remove group");b.RemoveAttackGroup(third.Id);Check(!b.RemoveAttackGroup(aid),"Last group cannot be removed");
  Check(JsonUtility.ToJson(common.AttackOrder)==timing,"Shared order unchanged after delete");
  File.WriteAllText("AgentArtifacts/AttackGroups-functional.json", "{\"passed\":true,\"checks\":"+checks+"}");
  return new{passed=true,checks,groups=true,singleMultipleSequence=true,seek=true};
 }finally{if(undo>=0)Undo.RevertAllDownToGroup(undo);PrefabUtility.UnloadPrefabContents(root);}}

 static void RuntimeProbe(DirectedEnemySubWave wave,DirectedWaveAttackBehaviour b,DirectedWaveAttackSettings common,string a,string second){
  var bodies=new List<GameObject>();var alive=Get(wave,"aliveEnemies");var indices=(Dictionary<Enemy,int>)Get(wave,"formationIndices");object controller=null;
  try{
   b.AssignAttackGroup(0,a);b.AssignAttackGroup(1,second);b.AssignAttackGroup(2,null);Dirty(b);
   var type=typeof(DirectedWaveAttackBehaviour).Assembly.GetType("DirectedWaveAttackController");
   controller=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public,null,new object[]{wave,common,null,true,false},null);
   for(int i=0;i<3;i++){
    var go=new GameObject("Runtime group resolver probe");go.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,wave.gameObject.scene);bodies.Add(go);
    var enemy=go.AddComponent<Enemy>();var executor=go.AddComponent<AttackGroupExecutorProbe>();indices[enemy]=i;alive.GetType().GetMethod("Add").Invoke(alive,new object[]{enemy});
    b.GetType().GetMethod("RegisterAttackGroupSpawn",F).Invoke(b,new object[]{enemy,i});
    Check((bool)b.GetType().GetMethod("AllowsAttackGroupEnemy",F).Invoke(b,new object[]{enemy})==(i<2),"Entrance ownership filter "+i);
    if(i==2)Check(executor.Controlled,"Unassigned executor is blocked immediately at spawn");
    bool eligible=(bool)b.GetType().GetMethod("CanEnemyAttack",F,null,new Type[]{typeof(Enemy)},null).Invoke(b,new object[]{enemy});
    Check(eligible==(i<2),"Runtime shared dispatcher eligibility "+i);
    if(i<2){var args=new object[]{enemy,executor,false};var profile=(EnemyBurstAttackSettings)type.GetMethod("ResolveAttackSettings",F).Invoke(controller,args);
     bool fired=(bool)type.GetMethod("TryFireShot",F).Invoke(controller,new object[]{enemy,executor,profile});
     Check(fired&&executor.Shots==1,"Production Runtime firing path "+i);
     Check(i==0?executor.Direction.x>.1f:executor.Direction.x<-.1f,"Production Runtime group direction "+i);
    }
   }
   b.GetType().GetMethod("ReserveAttackGroupControl",F).Invoke(b,null);
   foreach(var go in bodies)Check(go.GetComponent<AttackGroupExecutorProbe>().Controlled,"Runtime reserves unassigned autonomous executor");
  }finally{
   b.GetType().GetMethod("ReleaseAttackGroupControl",F).Invoke(b,null);
   if(controller!=null)controller.GetType().GetMethod("Stop",F).Invoke(controller,null);
   foreach(var go in bodies){var e=go.GetComponent<Enemy>();alive.GetType().GetMethod("Remove").Invoke(alive,new object[]{e});indices.Remove(e);Object.DestroyImmediate(go);}
  }
 }
 static void TransformSlots(DirectedEnemySubWave wave,DirectedWaveAttackBehaviour b,string a,string second){
  var points=new GameObject("Group stable slot probe");points.transform.SetParent(wave.transform,false);
  Set(wave.Formation,"formationLayout",DirectedWaveFormationLayout.TransformPoints);Set(wave.Formation,"formationPointsRoot",points.transform);
  var p0=new GameObject("A").transform;p0.SetParent(points.transform);var p1=new GameObject("B").transform;p1.SetParent(points.transform);var p2=new GameObject("C").transform;p2.SetParent(points.transform);
  b.AssignAttackGroup(0,a);b.AssignAttackGroup(1,second);p0.SetSiblingIndex(2);Dirty(b);
  Check(b.GetAttackGroupForSlot(2)?.Id==a&&b.GetAttackGroupForSlot(0)?.Id==second,"Transform slot references survive reorder");
  Object.DestroyImmediate(p1.gameObject);Dirty(b);Check(b.GetAttackGroupForSlot(0)?.Id!=second,"Removed Transform slot does not transfer group to replacement");
 }
}

public sealed class AttackGroupExecutorProbe:MonoBehaviour,IEnemyBurstAttackExecutor,IEnemyStationaryAttackExecutor{
 public bool Controlled;public int Shots;public Vector3 Direction;
 public bool CanPerformWaveAttack=>true;public EnemyBurstAttackSettings BurstAttackSettings{get;}=new();
 public void SetWaveAttackControl(bool v)=>Controlled=v;
 public bool TryFireAt(Vector3 v,EnemyBurstAttackSettings p){Shots++;Direction=v;return true;}
 public bool TryFireInDirection(Vector3 v,EnemyBurstAttackSettings p){Shots++;Direction=v;return true;}
 public bool TryFireStationary(Vector3 v,bool worldPoint,EnemyBurstAttackSettings p){Shots++;Direction=v;return true;}
}

