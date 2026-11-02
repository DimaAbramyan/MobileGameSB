using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyBehaviourComponentCache
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static readonly Type[] Types = { typeof(DirectedWaveFormationBehaviour), typeof(DirectedWavePhaseEntryBehaviour), typeof(DirectedWavePostBehaviour), typeof(DirectedWaveCompletionBehaviour) };
    private static readonly string[] Names = { "FormationBehaviour", "PhaseEntryBehaviour", "PostBehaviourComponent", "CompletionBehaviour" };
    private static readonly string[] Flags = { "formationBehaviourResolved", "phaseEntryBehaviourResolved", "postBehaviourResolved", "completionBehaviourResolved" };
    private static readonly string[] CacheFields = { "cachedFormationBehaviour", "cachedPhaseEntryBehaviour", "cachedPostBehaviour", "cachedCompletionBehaviour" };
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static object Get(object target, string name)
    { var t=target.GetType();return t.GetField(name,F)?.GetValue(target) ?? t.GetProperty(name,F)?.GetValue(target); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name,F).SetValue(target,value);
    private static void Invalidate(DirectedEnemySubWave wave) => typeof(DirectedEnemySubWave).GetMethod("InvalidateBehaviourComponentCache",F).Invoke(wave,null);
    private static Component Resolve(DirectedEnemySubWave wave, int index) => (Component)Get(wave,Names[index]);
    private static long Lookups(WavePreviewProfiler.Recording record)
    { long n=0;foreach(var key in new[]{WavePreviewProfiler.Counter.FormationLookupMiss,WavePreviewProfiler.Counter.PhaseLookupMiss,WavePreviewProfiler.Counter.PostLookupMiss,WavePreviewProfiler.Counter.CompletionLookupMiss})n+=record.Counters[(int)key];return n; }
    private static async Task Pump()
    {
        var done=new TaskCompletionSource<bool>();int ticks=0;
        void Tick(){if(++ticks>=3)done.TrySetResult(true);EditorApplication.QueuePlayerLoopUpdate();}
        EditorApplication.update+=Tick;
        try{await done.Task;}finally{EditorApplication.update-=Tick;}
    }
    public static async Task<object> Run()
    {
        checks=0;var details=new List<object>();var original=EditorSceneManager.GetActiveScene();bool dirty=original.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Behaviour cache checks");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        var wave=root.AddComponent<DirectedEnemySubWave>();
        try
        {
            Set(wave,"structuredConfigurationVersion",1);await Pump();Invalidate(wave);
            var record=new WavePreviewProfiler.Recording();WavePreviewProfiler.Current=record;
            for(int n=0;n<1000;n++)for(int i=0;i<4;i++)Check(Resolve(wave,i)==null,"Absent behaviour stays absent: "+Names[i]);
            Check(Lookups(record)==4,"Exactly four component queries for 4000 absent reads");
            details.Add(new{scenario="negative",reads=4000,lookups=Lookups(record)});WavePreviewProfiler.Reset();
            for(int i=0;i<4;i++)
            {
                var component=root.AddComponent(Types[i]);
                Check(ReferenceEquals(Resolve(wave,i),component),"Add after cached miss is visible immediately: "+Names[i]);
                var script=MonoScript.FromMonoBehaviour((MonoBehaviour)component);
                details.Add(new{scenario="scriptIdentity",type=Types[i].Name,script=script!=null?AssetDatabase.GetAssetPath(script):null,scriptClass=script!=null?script.GetClass()?.FullName:null});
                for(int repeat=0;repeat<25;repeat++)Check(ReferenceEquals(Resolve(wave,i),component),"Positive reference reused");
                ((Behaviour)component).enabled=false;
                Check(ReferenceEquals(Resolve(wave,i),component),"Disabled behaviour retains configuration priority");
                ((Behaviour)component).enabled=true;
                Object.DestroyImmediate(component);
                Check(ReferenceEquals(Resolve(wave,i),null),"Removed component returns actual null");
                // Exercise Unity fake-null independently of lifecycle invalidation.
                Set(wave,CacheFields[i],component);Set(wave,Flags[i],true);
                Check(!ReferenceEquals(component,null) && component==null,"Destroyed Unity reference is fake-null");
                record=new WavePreviewProfiler.Recording();WavePreviewProfiler.Current=record;
                for(int repeat=0;repeat<25;repeat++)Check(ReferenceEquals(Resolve(wave,i),null),"Fake-null cannot escape cache");
                Check(Lookups(record)==1,"Destroyed cache is re-queried only once");WavePreviewProfiler.Reset();
                // Existing explicit setters keep first priority and Set(null) falls back to the attached component.
                component=root.AddComponent(Types[i]);
                string setter=new[]{"SetFormationBehaviour","SetPhaseEntryBehaviour","SetPostBehaviourComponent","SetCompletionBehaviour"}[i];
                typeof(DirectedEnemySubWave).GetMethod(setter).Invoke(wave,new object[]{component});
                Check(ReferenceEquals(Resolve(wave,i),component),"Explicit reference priority");
                typeof(DirectedEnemySubWave).GetMethod(setter).Invoke(wave,new object[]{null});
                Check(ReferenceEquals(Resolve(wave,i),component),"Set(null) retains attached-component fallback");
                Object.DestroyImmediate(component);
            }
            // Changes to inactive objects arrive through Editor structural events.
            root.SetActive(false);for(int i=0;i<4;i++)Resolve(wave,i);
            var inactive=root.AddComponent<DirectedWaveFormationBehaviour>();await Pump();
            Check(ReferenceEquals(wave.FormationBehaviour,inactive),"Inactive object discovers added component after Editor event");
            Object.DestroyImmediate(inactive);await Pump();Check(wave.FormationBehaviour==null,"Inactive removal");root.SetActive(true);
            for(int i=0;i<4;i++)
            {
                Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Behaviour cache verification");
                try
                {
                    var component=Undo.AddComponent(root,Types[i]);Undo.FlushUndoRecordObjects();
                    Check(ReferenceEquals(Resolve(wave,i),component),"Undo add initial");
                    Undo.PerformUndo();await Pump();Check(Resolve(wave,i)==null,"Undo add removed");
                    Undo.PerformRedo();await Pump();component=root.GetComponent(Types[i]);Check(component!=null&&ReferenceEquals(Resolve(wave,i),component),"Redo add restored");
                    Undo.IncrementCurrentGroup();Undo.DestroyObjectImmediate(component);Undo.FlushUndoRecordObjects();
                    Check(Resolve(wave,i)==null,"Undo delete initial");
                    Undo.PerformUndo();await Pump();component=root.GetComponent(Types[i]);Check(component!=null&&ReferenceEquals(Resolve(wave,i),component),"Undo delete restored");
                    Undo.PerformRedo();await Pump();Check(Resolve(wave,i)==null,"Redo delete removed");
                }
                finally{Undo.RevertAllDownToGroup(group);var component=root.GetComponent(Types[i]);if(component!=null)Object.DestroyImmediate(component);}
            }
            Set(wave,"structuredConfigurationVersion",0);Set(wave,"legacyEnemyCount",7);
            typeof(DirectedEnemySubWave).GetMethod("InvalidateLegacyCompatibilityConfiguration",F).Invoke(wave,null);Invalidate(wave);
            Check(wave.UsesLegacyConfigurationCompatibility,"Legacy without separate components");
            object legacy=wave.Formation;Check((int)Get(legacy,"enemyCount")==7,"Legacy values preserved");
            var formation=root.AddComponent<DirectedWaveFormationBehaviour>();
            Check(!wave.UsesLegacyConfigurationCompatibility,"Separate component disables legacy globally");
            Check(ReferenceEquals(wave.Formation,formation.Configuration),"Separate formation priority");
            Check(ReferenceEquals(wave.PhaseEntry,Get(wave,"phaseEntry")),"Other blocks retain modern fallback when any separate component exists");
            Object.DestroyImmediate(formation);Check(wave.UsesLegacyConfigurationCompatibility,"Removal restores legacy");
            Check((int)Get(wave.Formation,"enemyCount")==7,"Legacy values survive add/remove");
            Set(wave,"structuredConfigurationVersion",1);Invalidate(wave);
            record=new WavePreviewProfiler.Recording();WavePreviewProfiler.Current=record;
            for(int n=0;n<1000;n++)Check(!wave.UsesLegacyConfigurationCompatibility,"Modern excludes legacy");
            Check(Lookups(record)==0,"Modern version short-circuits compatibility without component probes");WavePreviewProfiler.Reset();
            // The related attack resolver must also invalidate an earlier miss on addition.
            Check(wave.AttackPatternBehaviour==null,"Attack initially absent");
            var attack=root.AddComponent<DirectedWaveAttackBehaviour>();await Pump();Check(ReferenceEquals(wave.AttackPatternBehaviour,attack),"Attack added after miss");
            Object.DestroyImmediate(attack);Check(wave.AttackPatternBehaviour==null,"Destroyed attack is not returned");
            var simultaneous=new Component[4];
            var configurationNames=new[]{"FormationConfig","PhaseEntryConfig","PostBehaviourConfig","CompletionConfig"};
            for(int i=0;i<4;i++)simultaneous[i]=root.AddComponent(Types[i]);
            for(int i=0;i<4;i++)
            {
                Check(ReferenceEquals(Resolve(wave,i),simultaneous[i]),"Four behaviours coexist without source conflict");
                var configurationField=Types[i].GetField("configuration",F);
                var updated=Activator.CreateInstance(configurationField.FieldType);
                configurationField.SetValue(simultaneous[i],updated);
                Check(ReferenceEquals(Get(wave,configurationNames[i]),updated),"Configuration replacement is visible through cached component");
            }
            for(int i=0;i<4;i++)Object.DestroyImmediate(simultaneous[i]);
            details.Add(new{scenario="lifecycleAndPriority",passed=true});
        }
        finally{WavePreviewProfiler.Reset();Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        Check(original.isDirty==dirty,"User scene dirty state unchanged");
        var result=new{passed=true,checks,details};System.IO.File.WriteAllText("AgentArtifacts/behaviour-cache-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
