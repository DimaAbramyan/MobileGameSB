using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyEnemyLaserEditor
{
    private const BindingFlags Flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static int checks;
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Flags).SetValue(obj,value);
    private static object Get(object obj,string field)=>obj.GetType().GetField(field,Flags).GetValue(obj);
    private static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
    private static int Worlds(){int n=0;foreach(var w in World.All)if(w.Name=="Scene shooting preview")n++;return n;}
    public static async Task<object> Run()
    {
        checks=0; var scene=EditorSceneManager.NewPreviewScene();
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"),scene);
        Editor editor=null; Action<SceneView> draw=null;
        try
        {
            var executor=root.GetComponent<SimpleEnemyAttackExecutor>(); using var so=new SerializedObject(executor);
            var shooting=so.FindProperty("burstAttackSettings.shooting");
            shooting.FindPropertyRelative("projectileType").intValue=(int)EnemyProjectileType.Laser;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.IncrementCurrentGroup();
            shooting.FindPropertyRelative("laser.geometry.laserWidth").floatValue=.7f;
            so.ApplyModifiedProperties();Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();so.Update();Check(Mathf.Abs(executor.BurstAttackSettings.Shooting.Laser.Geometry.LaserWidth-.15f)<.001f,"Laser Undo");
            Undo.PerformRedo();so.Update();Check(Mathf.Abs(executor.BurstAttackSettings.Shooting.Laser.Geometry.LaserWidth-.7f)<.001f,"Laser Redo");
            bool overridden=false;foreach(var change in PrefabUtility.GetPropertyModifications(root))overridden|=change.propertyPath.EndsWith("laser.geometry.laserWidth");
            Check(overridden,"Laser prefab override");
            shooting=so.FindProperty("burstAttackSettings.shooting");var laser=shooting.FindPropertyRelative("laser");var geometry=laser.FindPropertyRelative("geometry");
            var visible=new HashSet<string>(EnemyLaserGeometryDrawer.Fields(geometry));
            Check(visible.Contains("lengthOffset")&&!visible.Contains("laserLength")&&!visible.Contains("widthCurve")&&!visible.Contains("lengthCurve"),"Geometry conditional defaults");
            geometry.FindPropertyRelative("lengthMode").intValue=1;geometry.FindPropertyRelative("constantWidth").boolValue=false;geometry.FindPropertyRelative("constantLength").boolValue=false;
            visible=new HashSet<string>(EnemyLaserGeometryDrawer.Fields(geometry));
            Check(visible.Contains("laserLength")&&!visible.Contains("lengthOffset")&&visible.Contains("widthCurve")&&visible.Contains("lengthCurve"),"Geometry conditional overrides");
            laser.FindPropertyRelative("damageMode").intValue=1;laser.FindPropertyRelative("overrideTelegraphGeometry").boolValue=true;
            visible=new HashSet<string>(EnemyLaserSettingsDrawer.Fields(laser));Check(visible.Contains("damageInterval")&&visible.Contains("telegraphGeometry"),"Conditional damage/warning overrides");
            laser.FindPropertyRelative("enableTelegraph").boolValue=false;visible=new HashSet<string>(EnemyLaserSettingsDrawer.Fields(laser));
            Check(!visible.Contains("telegraphDuration")&&!visible.Contains("telegraphGeometry"),"Telegraph fields hidden when disabled");
            laser.FindPropertyRelative("enableTelegraph").boolValue=true;
            var projectileFields=typeof(EnemyShootingSettingsDrawer).GetMethod("ProjectileFields",BindingFlags.Static|BindingFlags.NonPublic);
            visible=new HashSet<string>((IEnumerable<string>)projectileFields.Invoke(null,new object[]{shooting}));
            Check(visible.Contains("laser")&&visible.Contains("useChildAttackPattern")&&!visible.Contains("projectileMovementPattern")&&!visible.Contains("initialSpeedMode"),"Beam Inspector hides inapplicable motion fields");
            so.ApplyModifiedPropertiesWithoutUndo();
            editor=Editor.CreateEditor(executor);var view=SceneView.lastActiveSceneView;Check(view!=null,"Live Scene View available");
            var done=new TaskCompletionSource<bool>();int rendered=0;
            draw=current=>
            {
                if(current!=view||Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint)return;
                try
                {
                    EnemyLaserScenePreview.HandlesFor(so,root.transform.position,executor);
                    Handles.BeginGUI();GUILayout.BeginArea(new Rect(10,20,850,12000));editor.OnInspectorGUI();GUILayout.EndArea();Handles.EndGUI();
                    if(Event.current.type==EventType.Repaint&&++rendered>=2)done.TrySetResult(true);
                }
                catch(Exception ex){done.TrySetException(ex);}
            };
            SceneView.duringSceneGui+=draw;var timeout=Task.Delay(10000);
            while(!done.Task.IsCompleted&&!timeout.IsCompleted){view.Repaint();await Task.Delay(50);}
            Check(done.Task.IsCompleted,"Inspector render completed");await done.Task; SceneView.duringSceneGui-=draw;draw=null;
            int before=Worlds();
            for(int i=0;i<3;i++)
            {
                using(var preview=new EnemyChildAttackScenePreview())
                {preview.Simulate(executor,.3f,Vector3.up*100);Check(preview.Count>0,"Static executor preview includes warning");preview.Simulate(executor,0,Vector3.up*100);}
                Check(Worlds()==before,"Preview restart disposes private world and beam resources");
            }
            var compatibility=CheckWaves();var wavePreview=WavePreview();
            return new{passed=true,checks,inspectorRendered=true,sceneHandlesRendered=true,undoRedo=true,prefabOverrides=true,compatibility,wavePreview};
        }
        finally
        {
            SceneView.duringSceneGui-=draw;if(editor!=null)UnityEngine.Object.DestroyImmediate(editor);
            Undo.ClearUndo(root.GetComponent<SimpleEnemyAttackExecutor>());UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static object WavePreview()
    {
        int warnings=0,active=0,beams=0;
        foreach(bool sequence in new[]{false,true})
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
            try
            {
                var wave=root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();var b=wave.AttackPatternBehaviour;
                Set(b,"attackPreset",null);((IList)Get(b,"attackPatterns")).Clear();
                Set(b,"executionMode",sequence?DirectedWaveAttackExecutionMode.Sequence:DirectedWaveAttackExecutionMode.Patterns);
                var authored=(DirectedWaveAttackSettings)Get(b,"attackSettings");
                if(sequence)
                {
                    var step=new DirectedWaveAttackSequenceStep();var pattern=new DirectedWaveAttackPattern();Set(step,"pattern",pattern);
                    authored=(DirectedWaveAttackSettings)Get(pattern,"attackSettings");b.AttackSequence.steps=new(){step};
                    b.AttackSequence.sequenceMode=DirectedWaveAttackSequenceMode.Loop;b.AttackSequence.loopCount=0;
                }
                Set(authored,"fireMode",DirectedWaveAttackFireMode.Default);Set(authored,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);
                Set(authored,"attackStartDelay",0f);Set(authored,"attacksPerSecond",10f);
                var p=authored.WaveBurstSettings;p.ConfigureLegacySingleAttack(2,1,.2f);Set(p.Shooting,"projectileType",EnemyProjectileType.Laser);
                p.Shooting.EmissionLayout.ConfigureSector(-30,30,3,EnemySectorLaunchMode.Sequential,.05f,.05f);
                p.Shooting.ConfigureLegacyFan(2,-5,5);Set(p.Shooting,"shotGunMode",EnemyProjectileShotGunMode.ShotGun);Set(p.Shooting,"shotGunProjectileCount",2);
                Set(p.Shooting.Laser,"laserDuration",.4f);Set(p.Shooting.Laser,"telegraphDuration",.2f);Set(p.Shooting.Laser,"followOrigin",true);
                using var preview=new WaveShootingPreviewSession(wave,true,false,0,Vector3.up*100);
                using var q=preview.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyLaserState));
                var seen=new HashSet<Entity>();bool sawWarning=false,sawActive=false;
                for(int i=1;i<=100;i++)
                {
                    preview.AdvanceTo(i*WaveShootingPreviewSession.StepInterval,1000);
                    using var entities=q.ToEntityArray(Allocator.Temp);foreach(var e in entities)seen.Add(e);
                    using var states=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp);
                    foreach(var s in states){sawWarning|=s.Telegraph!=0;sawActive|=s.Active!=0;}
                }
                Check(sawWarning&&sawActive,"Wave/Sequence preview simulates full Laser lifecycle: "+sequence);
                Check(seen.Count>=24,"Wave/Sequence retains multiple volleys, Fan/Stack/Sector: "+sequence);
                warnings+=sawWarning?1:0;active+=sawActive?1:0;beams+=seen.Count;
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        return new{modes=2,warnings,active,beams};
    }
    private static object CheckWaves()
    {
        string[] paths={"Assets/Waves/1/1_Extreme/1_1_extrime.prefab","Assets/Waves/1/1_Extreme/1_2_extrime.prefab",
            "Assets/Waves/1/1_Extreme/1_3_extrime.prefab","Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
            "Assets/Waves/2/2_1_extrime.prefab","Assets/Waves/2/2_2_extrime.prefab","Assets/Waves/2/2_3_extrime.prefab"};
        int subwaves=0,slots=0,patterns=0;
        foreach(var path in paths)
        {
            using var sha=SHA256.Create();string hash=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"No missing script "+path);
                foreach(var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                {
                    subwaves++;int count=wave.GetConfiguredEnemySlotCount();slots+=count;
                    for(int i=0;i<count;i++)
                    {
                        var settings=new DirectedWaveAttackSettings();if(wave.AttackPatternBehaviour==null||!wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i,false,settings))continue;
                        var p=settings.WaveBurstSettings;
                        for(int j=0;j<p.PatternCount;j++){patterns++;Check(p.GetPattern(j).Shooting.ProjectileType!=EnemyProjectileType.Laser,"Old projectile type retained");}
                    }
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Check(hash==Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))),"Prefab unchanged "+path);
        }
        return new{prefabs=paths.Length,subwaves,slots,patterns,missingScripts=0,noPrefabWrites=true};
    }
}
