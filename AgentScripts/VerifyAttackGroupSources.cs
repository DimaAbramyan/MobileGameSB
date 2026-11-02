using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyAttackGroupSources
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
    static void Dirty(DirectedWaveAttackBehaviour b) { b.InvalidateSequenceGuides(); EditorUtility.SetDirty(b); }
    static DirectedWaveAttackSettings Settings(float angle)
    {
        var s = new DirectedWaveAttackSettings(); Set(s,"fireMode",DirectedWaveAttackFireMode.Stationary);
        s.StationaryFireSettings.ConfigureAngle(angle); Set(s,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);
        s.WaveBurstSettings.ConfigureLegacySingleAttack(1,.2f,10f);
        s.WaveBurstSettings.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        s.WaveBurstSettings.Shooting.SetProjectileBaseSpeedOverride(true,1);
        return s;
    }
    static void Configure(DirectedWaveAttackPattern p, float angle) => p.Configure(Settings(angle),null,false,1,false,1,null,EnemySpeedRandomizeOn.PerProjectile);
    static void Advance(WaveShootingPreviewSession s,float time) { while(!s.AdvanceTo(time,1000)) {} }
    public static object Run()
    {
        checks=0; var root=PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab"); Editor editor=null;
        try
        {
            var wave=root.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var b=wave.AttackPatternBehaviour;
            Set(wave.Formation,"customFormationPoints",new[]{Vector3.left*3,Vector3.left*2,Vector3.left,Vector3.zero,Vector3.right,Vector3.right*2});
            Set(b,"executionMode",DirectedWaveAttackExecutionMode.Patterns); Set(b,"attackPatterns",new List<DirectedWaveAttackPattern>());
            Set(b,"attackPreset",null); Set(b,"useSelectedEnemySlots",false); Set(b,"attackGroups",new List<DirectedWaveAttackGroup>());
            Set(b,"attackGroupSlots",new List<DirectedWaveAttackGroupSlot>()); Set(b,"useAttackGroups",false);
            var common=Settings(0); common.AttackOrder.enabled=true; common.AttackOrder.mode=DirectedWaveAttackOrderMode.Simultaneous;
            common.AttackOrder.loopOrder=false; common.AttackOrder.waitForPreviousEnemy=true; Set(b,"attackSettings",common);
            b.SetUseAttackGroups(true); var a=b.AttackGroups[0]; Configure(a.Single,0); b.AssignAttackGroup(5,null); Dirty(b);
            editor=Editor.CreateEditor(b); var type=editor.GetType();
            void Select(string id) { SessionState.SetString("WaveAttack.ActiveGroup."+b.GetInstanceID(),id); editor.serializedObject.Update(); type.GetMethod("BindAttackGroupProperties",F).Invoke(editor,null); }
            string Path(int slot) => (string)type.GetMethod("GetGroupSceneSourcePath",F).Invoke(editor,new object[]{b,slot});
            Select(a.Id);
            for(int i=0;i<5;i++) Check(Path(i)!=null,"Single guide includes member "+i);
            Check(Path(5)==null,"Unassigned enemy has no group source");
            using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
            { Advance(session,.05f); Check(session.ProjectileCount==5,"Simultaneous scheduler emits from all five members only"); }
            var other=b.CreateAttackGroup(null); Configure(other.Single,90); b.AssignAttackGroup(3,other.Id); b.AssignAttackGroup(4,other.Id); Dirty(b);
            Select(other.Id); Check(Path(3)!=null&&Path(4)!=null&&Path(0)==null,"Inspector switch selects both members of second group");
            Select(a.Id); Check(Path(0)!=null&&Path(3)==null,"Switch back invalidates source paths");
            common.AttackOrder.mode=DirectedWaveAttackOrderMode.Sequential; common.AttackOrder.attackIntervalBetweenEnemies=.5f; Dirty(b);
            using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
            { Advance(session,.05f); Check(session.ProjectileCount==1,"Sequential dispatch does not fire all members at once"); Advance(session,3); Check(session.ProjectileCount>=5,"Sequential dispatch eventually includes every assigned member"); }
            var pattern=new DirectedWaveAttackPattern(); Configure(pattern,180); ((List<DirectedWaveAttackPattern>)other.Patterns).Add(pattern);
            Set(pattern,"selectedEnemySlots",new List<int>{4}); Dirty(b); Select(other.Id);
            Check(Path(4)?.Contains("patterns.Array.data[0]")==true&&Path(3)==null,"Multiple guides obey per-pattern slot eligibility");
            Set(other,"executionMode",DirectedWaveAttackExecutionMode.Sequence);
            var step=new DirectedWaveAttackSequenceStep(); Configure(step.pattern,90); other.Sequence.steps.Add(step); Dirty(b); Select(other.Id);
            Check(Path(3)?.EndsWith(".sequence")==true&&Path(4)?.EndsWith(".sequence")==true,"Sequence guide visits both assigned sources");
            using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
            { Advance(session,3); Check(session.ProjectileCount>=5,"Sequence programs keep existing dispatch timing"); }
            step.overrideTargetSlots=true; step.targetSlots.Add(4); editor.serializedObject.Update();
            var eligibility=typeof(EnemyLaserScenePreview).GetMethod("AllowsSequenceSource",BindingFlags.Static|BindingFlags.NonPublic);
            string shootingPath=Path(4)+".steps.Array.data[0].pattern.attackSettings.waveBurstSettings.shooting";
            bool Eligible(int slot)=>(bool)eligibility.Invoke(null,new object[]{editor.serializedObject,shootingPath,slot});
            Check(!Eligible(3)&&Eligible(4),"Sequence authoring guides respect overridden target slots");
            step.type=DirectedWaveAttackSequenceStepType.Pause; editor.serializedObject.Update();
            Check(!Eligible(4),"Pause step does not expose an inactive attack guide");
            b.SetUseAttackGroups(false); Dirty(b);
            using(var session=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-4)))
            { Advance(session,3.5f); Check(session.ProjectileCount>=6,"Groups disabled preserves legacy participation"); }
            return new {passed=true,checks,assignedMembers=5,groups=2,singleMultipleSequence=true,authoredAssetsChanged=false};
        }
        finally { if(editor!=null) UnityEngine.Object.DestroyImmediate(editor); PrefabUtility.UnloadPrefabContents(root); }
    }
}
