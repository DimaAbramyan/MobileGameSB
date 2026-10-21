using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class InspectWavePreviewTiming
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static object Field(object value,string name)=>value?.GetType().GetField(name,Flags)?.GetValue(value);
    public static object Watch()
    {
        double end=EditorApplication.timeSinceStartup+90,last=0;var samples=new List<object>();
        EditorApplication.CallbackFunction tick=null;
        tick=()=>
        {
            double now=EditorApplication.timeSinceStartup;
            if(now>=end){EditorApplication.update-=tick;return;}
            if(now-last<.5)return;last=now;
            foreach(var window in Resources.FindObjectsOfTypeAll<WavePreviewPlayerWindow>())
            {
                var player=Field(window,"player") as WavePreviewPlayer;var sim=player?.Simulation;
                if(sim==null)continue;var cues=new List<object>();
                for(int i=0;i<sim.CueCount;i++)
                {
                    var session=sim.GetSession(i);if(session==null)continue;
                    var positions=new Dictionary<int,Vector3>();session.CopyPreviewPositionsTo(positions);
                    var positionValues=new List<object>();foreach(var position in positions)positionValues.Add(new{slot=position.Key,x=position.Value.x,y=position.Value.y,z=position.Value.z});
                    cues.Add(new{cue=i,time=session.Time,phase=session.PhaseName,state=session.State.ToString(),idle=session.IsIdle,status=session.CoordinatorStatus,positions=positionValues});
                }
                var groups=new List<object>();if(sim.CoordinatorPlayer!=null)foreach(var group in sim.CoordinatorPlayer.ActiveGroups)
                    groups.Add(new{index=group.Index,status=group.Status,start=group.StartedAt,remaining=group.Remaining});
                samples.Add(new{time=player.CurrentTime,playing=player.IsPlaying,seeking=player.IsSeeking,cues,groups});
                System.IO.File.WriteAllText("AgentArtifacts/wave-player-timing-watch.json",Newtonsoft.Json.JsonConvert.SerializeObject(samples,Newtonsoft.Json.Formatting.Indented));
            }
        };
        EditorApplication.update+=tick;return new{watching=true,seconds=90};
    }
    public static object Read()
    {
        var windows=new List<object>();
        foreach(var window in Resources.FindObjectsOfTypeAll<WavePreviewPlayerWindow>())
        {
            var player=Field(window,"player") as WavePreviewPlayer;var sim=player?.Simulation;var cues=new List<object>();
            if(sim!=null)
                for(int i=0;i<sim.CueCount;i++)
                {
                    var session=sim.GetSession(i);var groups=new List<object>();
                    if(session!=null)
                        foreach(var group in (IEnumerable)Field(session,"groups"))
                        {
                            var slots=new List<object>();foreach(var slot in (IEnumerable)Field(group,"Slots"))
                                slots.Add(new{index=Field(slot,"Index"),active=Field(slot,"Active"),ready=Field(slot,"ReadyTime"),completion=Field(slot,"CompletionTime"),position=Field(slot,"Position"),mode=Field(slot,"FireMode"),remaining=Field(slot,"RemainingSequenceAttacks")});
                            groups.Add(new{next=Field(group,"NextTime"),active=Field(group,"Active"),cycles=Field(group,"CoordinatorCycles"),counted=Field(group,"CoordinatorCounted"),round=Field(group,"Round"),settings=JsonUtility.ToJson(Field(group,"Settings")),slots});
                        }
                    cues.Add(new{index=i,time=session?.Time,phase=session?.PhaseName,status=session?.CoordinatorStatus,post=Field(session,"postStart"),worldTime=Field(session,"positionsTime"),attackOffset=Field(session,"coordinatorOffset"),groups});
                }
            var states=new List<object>();if(sim?.CoordinatorPlayer!=null)foreach(var group in sim.CoordinatorPlayer.ActiveGroups)
                states.Add(new{index=group.Index,name=group.Settings.groupName,status=group.Status,remaining=group.Remaining,started=group.StartedAt});
            windows.Add(new{source=player?.Source?.name??(Field(window,"source") as Wave)?.name,playing=player?.IsPlaying,seeking=player?.IsSeeking,time=player?.CurrentTime,requested=player?.RequestedTime,warning=sim?.Warning,cues,states});
        }
        var selected=Selection.activeGameObject;
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab");var wave=asset.GetComponent<Wave>();
        var settings=new List<object>();int index=0;
        foreach(var cue in wave.ScheduledSubWaves)
        {
            var sub=cue.SubWavePrefab.GetComponent<DirectedEnemySubWave>();
            settings.Add(new{index=index++,name=sub.name,delay=cue.StartDelay,hasEntry=sub.HasPhaseEntry,hasPost=sub.HasPostBehaviour,postStart=sub.GetSimulationPreviewPostStartTime(),routeDuration=sub.GetSimulationBaseRouteDuration(),attack=EditorJsonUtility.ToJson(sub.AttackPatternBehaviour),subwave=EditorJsonUtility.ToJson(sub)});
        }
        var inspectors=new List<object>();foreach(var editor in Resources.FindObjectsOfTypeAll<WaveEditor>())inspectors.Add(new{wave=editor.target?.name,playing=Field(editor,"previewPlaying"),time=Field(editor,"previewSampleElapsed"),shooting=Field(editor,"showAttackPatterns")});
        return new{playMode=Application.isPlaying,selected=selected?.name,windows,inspectors,coordinator=JsonUtility.ToJson(wave.GetComponent<WaveAttackCoordinator>()?.Settings),settings};
    }
}
