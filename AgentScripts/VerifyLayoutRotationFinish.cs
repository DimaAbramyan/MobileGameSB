using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyLayoutRotationFinish
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v){var f=o.GetType().GetField(n,F);if(f!=null)f.SetValue(o,v);else o.GetType().GetProperty(n,F).SetValue(o,v);}
    static EnemyBurstAttackSettings Profile(EnemyLayoutRotationOnAttackFinished mode)
    {
        var p=new EnemyBurstAttackSettings();p.ConfigureLegacySingleAttack(3,10f,.2f);
        var r=new EnemyRotationSettings();Set(r,"onAttackFinished",mode);p.Shooting.ConfigureRotation(r,true);return p;
    }
    static void Advance(WaveShootingPreviewSession s,float t){while(!s.AdvanceTo(t,1000)) {}}
    public static object Run()
    {
        checks=0;var go=new GameObject("Layout lifecycle fixture");go.SetActive(false);
        var executor=go.AddComponent<SimpleEnemyAttackExecutor>();
        var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            foreach(EnemyLayoutRotationOnAttackFinished mode in Enum.GetValues(typeof(EnemyLayoutRotationOnAttackFinished)))
            {
                var p=Profile(mode);executor.ResumeRotation();Set(executor,"patternRotationTime",1.5f);Set(executor,"facingRotationTime",4f);
                executor.BeginAttackSequence(p);executor.EndAttackSequence(p);
                Check((bool)Get(executor,"patternRotationRunning")== (mode==EnemyLayoutRotationOnAttackFinished.ContinueRotation),"Finish running state "+mode);
                Check(Mathf.Approximately((float)Get(executor,"patternRotationTime"),mode==EnemyLayoutRotationOnAttackFinished.ResetRotation?0:1.5f),"Finish angle clock "+mode);
                Check((float)Get(executor,"facingRotationTime")==4f,"Facing clock untouched "+mode);
                executor.BeginAttackSequence(p);Check((bool)Get(executor,"patternRotationRunning"),"Next attack resumes "+mode);executor.EndAttackSequence(p);
            }
            var pause=Profile(EnemyLayoutRotationOnAttackFinished.PauseRotation);
            executor.BeginAttackSequence(pause);executor.BeginAttackSequence(pause);executor.EndAttackSequence(pause);
            Check((bool)Get(executor,"patternRotationRunning"),"Overlapping attack keeps rotation running");executor.EndAttackSequence(pause);
            Check(!(bool)Get(executor,"patternRotationRunning"),"Last overlapping attack pauses");
            executor.BeginLayoutRotationProgram();executor.BeginAttackSequence(pause);executor.EndAttackSequence(pause);
            Check((bool)Get(executor,"patternRotationRunning"),"Sequence pause between steps does not finish program");executor.EndLayoutRotationProgram();
            Check(!(bool)Get(executor,"patternRotationRunning"),"Sequence completion pauses");
            var copy=new EnemyRotationSettings();copy.CopyFrom(pause.Shooting.Rotation);
            Check(copy.OnAttackFinished==EnemyLayoutRotationOnAttackFinished.PauseRotation,"Rotation copy preserves policy");
            Check(new EnemyRotationSettings().OnAttackFinished==EnemyLayoutRotationOnAttackFinished.ContinueRotation,"Legacy default unchanged");
            var wave=root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();var b=wave.AttackPatternBehaviour;
            b.SetUseAttackGroups(false);Set(b,"attackPatterns",new System.Collections.Generic.List<DirectedWaveAttackPattern>());Set(b,"attackPreset",null);
            Set(b,"useSelectedEnemySlots",false);Set(b,"executionMode",DirectedWaveAttackExecutionMode.Patterns);
            foreach(var mode in new[]{EnemyLayoutRotationOnAttackFinished.PauseRotation,EnemyLayoutRotationOnAttackFinished.ResetRotation})
            {
                var settings=new DirectedWaveAttackSettings();Set(settings,"fireMode",DirectedWaveAttackFireMode.Default);
                Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);settings.WaveBurstSettings.CopyFrom(Profile(mode));Set(b,"attackSettings",settings);b.InvalidateSequenceGuides();EditorUtility.SetDirty(b);
                using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
                {
                    Advance(session,1f);var slots=(IList)Get(session,"slots");var pose=Get(slots[0],"Pose");
                    Check(!(bool)Get(pose,"LayoutRunning"),"Preview stops between attacks "+mode+"; policy="+Get(pose,"LayoutFinish")+"; count="+Get(pose,"LayoutAttacks")+"; active="+Get(slots[0],"Active"));
                    float elapsed=(float)Get(pose,"LayoutElapsed");Check(mode==EnemyLayoutRotationOnAttackFinished.ResetRotation?elapsed==0:elapsed>0,"Preview clock finish policy "+mode);
                    var graph=typeof(WaveShootingPreviewSession).Assembly.GetType("WavePreviewStateGraph");
                    var snapshot=graph.GetMethod("Capture",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{new object[]{session}});
                    Advance(session,2f);graph.GetMethod("Restore",F).Invoke(snapshot,new object[]{null});
                    Check((float)Get(pose,"LayoutElapsed")==elapsed&&!(bool)Get(pose,"LayoutRunning"),"Snapshot restores held rotation "+mode);
                }
            }
            var grouped=new DirectedWaveAttackSettings();Set(grouped,"fireMode",DirectedWaveAttackFireMode.Default);
            Set(grouped,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);grouped.WaveBurstSettings.CopyFrom(pause);
            grouped.WaveBurstSettings.ConfigureLegacySingleAttack(3,0f,.2f);
            grouped.AttackOrder.enabled=true;grouped.AttackOrder.mode=DirectedWaveAttackOrderMode.Simultaneous;grouped.AttackOrder.loopOrder=false;
            Set(b,"attackSettings",grouped);b.SetUseAttackGroups(true);var group=b.AttackGroups[0];Set(group,"executionMode",DirectedWaveAttackExecutionMode.Sequence);
            var attack=new DirectedWaveAttackSequenceStep();attack.pattern.Configure(grouped,null,false,1,false,1,null,EnemySpeedRandomizeOn.PerProjectile);
            group.Sequence.steps.Add(attack);group.Sequence.steps.Add(new DirectedWaveAttackSequenceStep{type=DirectedWaveAttackSequenceStepType.Pause,pauseDuration=1f});
            group.Sequence.steps.Add(attack);b.InvalidateSequenceGuides();EditorUtility.SetDirty(b);
            using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
            {
                Advance(session,.8f);var slot=((IList)Get(session,"slots"))[0];var pose=Get(slot,"Pose");
                Check((bool)Get(pose,"LayoutRunning"),"Attack Group sequence keeps rotating during inter-step pause");
                Advance(session,2.5f);Check(!(bool)Get(pose,"LayoutRunning"),"Attack Group sequence pauses only after whole program");
            }
            var conductor=root.GetComponent<Wave>();Set(conductor,"scheduledSubWaves",new System.Collections.Generic.List<WaveSubWaveCue>{new(wave.gameObject,0)});
            Set(wave,"spawnInterval",0f);Set(wave,"settleDuration",0f);
            using(var player=new WavePreviewPlayer(conductor,321,new Vector3(0,-4)))
            {
                player.SnapshotInterval=.1f;
                void Seek(float t){player.Seek(t);int guard=0;while(player.IsSeeking){player.Update(0,100);if(++guard>500)throw new Exception("Seek stalled");}}
                string State(){var text="";foreach(var cue in (IEnumerable)Get(player.Simulation,"cues")){var s=Get(cue,"Session");if(s==null)continue;foreach(var slot in (IEnumerable)Get(s,"slots")){var pose=Get(slot,"Pose");text+=Get(pose,"LayoutElapsed")+":"+Get(pose,"LayoutRunning")+":"+Get(pose,"LayoutAttacks")+";";}}return text;}
                Seek(2);string expected=State();Seek(.15f);Seek(2);
                Check(State()==expected,"Fast Seek and rewind restore layout lifecycle deterministically");
                Check(player.RestoredSnapshots>0,"Fast Seek actually restored snapshots");
            }
            return new{passed=true,checks};
        }
        finally{UnityEngine.Object.DestroyImmediate(go);PrefabUtility.UnloadPrefabContents(root);}
    }
}
