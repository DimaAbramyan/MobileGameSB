using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VerifyCoordinatorEventNavigation
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static int checks;
    private static string Json(object value)=>(string)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new[]{value});
    private static Type fixture, fullStateType;
    private static readonly List<object> scenarios=new();
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static Type Loaded(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).LastOrDefault(t=>t!=null);
    private static object Fixture(string name,params object[] args)=>fixture.GetMethod(name,S).Invoke(null,args);
    private static void Set(object target,string name,object value)
    {
        for(var type=target.GetType();type!=null;type=type.BaseType)
        {var f=type.GetField(name,F);if(f!=null){f.SetValue(target,value);return;}}
        throw new Exception("Missing field "+name);
    }
    private static string State(WavePreviewPlayer p)=>(string)fullStateType.GetMethod("FullState",S).Invoke(null,new object[]{p.Simulation,p.Events});
    private static async Task Finish(WavePreviewPlayer p)
    {
        for(int guard=0;p.IsFindingCoordinatorEvent||p.IsSeeking;guard++)
        {
            if(guard>10000)throw new Exception("Navigation failed to finish");
            p.Update(0,5);if(guard%5==0)await Task.Yield();
        }
    }
    private static async Task Seek(WavePreviewPlayer p,float time){p.Seek(time);await Finish(p);}
    private static async Task<WavePreviewCoordinatorEvent> Next(WavePreviewPlayer p)
    {
        Check(p.NextCoordinatorEvent(),"Next request accepted");await Finish(p);
        Check(p.SelectedCoordinatorEvent!=null,"Event selected");
        Check(!p.IsPlaying&&!p.IsSeeking,"Navigation ends paused");
        Check(p.CurrentFrame==p.SelectedCoordinatorEvent.Frame,"Event lands on exact post-event frame");
        return p.SelectedCoordinatorEvent;
    }
    private static void Configure(WaveAttackCoordinator coordinator,float delay=0,float overlap=0)
    {
        var settings=coordinator.Settings;settings.enabled=true;settings.groups.Clear();settings.loopSequence=true;
        settings.switchMode=WaveAttackSwitchMode.Interrupt;settings.globalCooldown=0;settings.startDelay=0;
        for(int i=0;i<2;i++)settings.groups.Add(new WaveAttackCoordinatorGroup{groupName=i==0?"Group A":"Group B",
            waitUntilReady=false,completionMode=WaveAttackGroupCompletion.Timed,duration=1f,maxWaitDuration=0,
            delayAfterGroup=delay,overlapDuration=overlap,controlledSubWaves=new(){new WaveAttackSubWaveReference{scheduleIndex=i}}});
        coordinator.InvalidateConfiguration();
    }
    public static async Task<object> Run()
    {
        fixture=Loaded("WavePreviewSnapshotFixtures");fullStateType=Loaded("VerifyWavePreviewFastSeek");
        if(fixture==null||fullStateType==null)throw new Exception("Run VerifyWavePreviewFastSeek.Run first to load shared fixtures.");
        checks=0;scenarios.Clear();int worlds=World.All.Count,seed=NumericRandomPreviewControls.SeedOffset;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Coordinator navigation fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=active.isDirty;
        NumericRandomPreviewControls.SetSeedOffset(53);
        try
        {
            var wave=root.AddComponent<Wave>();
            var a=(DirectedEnemySubWave)Fixture("Subwave",root.transform,"A",-1f,false);
            var b=(DirectedEnemySubWave)Fixture("Subwave",root.transform,"B",1f,true);
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new(a.gameObject,0),new(b.gameObject,0)});
            var coordinator=root.AddComponent<WaveAttackCoordinator>();Configure(coordinator);
            string authored=EditorJsonUtility.ToJson(wave)+EditorJsonUtility.ToJson(a)+EditorJsonUtility.ToJson(b)+EditorJsonUtility.ToJson(coordinator);
            using(var p=new WavePreviewPlayer(wave,12,new Vector3(0,-3)){SnapshotInterval=.25f})
            {
                for(int i=0;i<6;i++)
                {
                    var e=await Next(p);Check(e.ActiveGroups.Count==1&&e.ActiveGroups[0]==i%2,"A → B → A repeats in order");
                    Check(e.ActivatedGroups.Count==1&&e.DeactivatedGroups.Count==(i==0?0:1),"Same-frame handoff coalesces");
                    using var ordinary=new WavePreviewPlayer(wave,12,new Vector3(0,-3)){SnapshotInterval=.25f};
                    await Seek(ordinary,e.StateTime);Check(State(p)==State(ordinary),"Navigation matches full ordinary Fast Seek state at "+e.Time);
                }
                int frame=p.CurrentFrame,count=p.CoordinatorEvents.Count;string expected=State(p);
                Check(p.PreviousCoordinatorEvent(),"Previous accepted");await Finish(p);Check(p.CurrentFrame<frame,"Previous skips current event");
                await Next(p);Check(p.CurrentFrame==frame&&State(p)==expected,"Previous then Next restores identical state");
                Check(p.CoordinatorEvents.Count==count,"Rewind/replay does not duplicate markers");
                Check(p.RestoredSnapshots>0,"Navigation uses existing snapshots");
                p.ClearSnapshotCache();await Seek(p,0);await Seek(p,frame*WavePreviewPlayer.StepInterval);
                Check(State(p)==expected&&p.CoordinatorEvents.Count==count,"Replay from zero agrees with snapshot navigation");
                scenarios.Add(new{name="loop_handoff_rewind_full_state",events=count,frame});
            }
            Check(authored==EditorJsonUtility.ToJson(wave)+EditorJsonUtility.ToJson(a)+EditorJsonUtility.ToJson(b)+EditorJsonUtility.ToJson(coordinator),"Navigation preserves authored data");
            Configure(coordinator,.2f);coordinator.Settings.globalCooldown=.3f;coordinator.Settings.groups[1].startDelay=.2f;coordinator.InvalidateConfiguration();
            using(var p=new WavePreviewPlayer(wave,8,new Vector3(0,-3)))
            {
                var first=await Next(p);var off=await Next(p);var on=await Next(p);
                Check(off.ActiveGroups.Count==0&&off.DeactivatedGroups.Count==1,"Deactivation is an event with no immediate successor");
                Check(on.ActiveGroups.Count==1&&on.ActiveGroups[0]==1&&on.Time-off.Time>=.69f,"Delay after group, global cooldown and group start delay respected");
                Check(p.CoordinatorEvents.Count==3,"Start-delay/status updates create no navigation events");
                scenarios.Add(new{name="delays_and_empty_set",first=first.Time,deactivate=off.Time,activate=on.Time});
            }
            Configure(coordinator,0,.35f);
            using(var p=new WavePreviewPlayer(wave,5,new Vector3(0,-3)))
            {
                await Next(p);var overlap=await Next(p);
                Check(overlap.ActiveGroups.Count==2&&overlap.ActivatedGroups.Count==1&&overlap.DeactivatedGroups.Count==0,"Overlap keeps A active when B starts");
                var ended=await Next(p);Check(ended.ActiveGroups.Count==1&&ended.ActiveGroups[0]==1&&ended.DeactivatedGroups[0]==0,"Overlap end deactivates only outgoing group");
                scenarios.Add(new{name="overlap",time=overlap.Time,summary=overlap.Summary});
            }
            Configure(coordinator);coordinator.Settings.switchMode=WaveAttackSwitchMode.FinishCurrentAttack;coordinator.InvalidateConfiguration();
            using(var p=new WavePreviewPlayer(wave,6,new Vector3(0,-3)))
            {
                await Seek(p,6);
                foreach(var e in p.CoordinatorEvents)Check(e.ActivatedGroups.Count+e.DeactivatedGroups.Count>0,"Drain/status changes never generate empty events");
                for(int i=1;i<p.CoordinatorEvents.Count;i++)Check(p.CoordinatorEvents[i].Frame>p.CoordinatorEvents[i-1].Frame,"At most one event per frame");
                scenarios.Add(new{name="draining",events=p.CoordinatorEvents.Count});
            }
            Configure(coordinator);
            coordinator.Settings.groups.RemoveAt(1);coordinator.Settings.groups[0].duration=.1f;coordinator.InvalidateConfiguration();
            using(var p=new WavePreviewPlayer(wave,2,new Vector3(0,-3)))
            {
                await Seek(p,2);Check(p.CoordinatorEvents.Count==1,"Restart of the same active set does not add an event");
                Check(p.CoordinatorEvents[0].Time==0&&p.CoordinatorEvents[0].StateTime==WavePreviewPlayer.StepInterval,"Event time is the coordinator tick, destination is post-step state");
                scenarios.Add(new{name="same_group_restart_no_duplicate"});
            }
            Configure(coordinator);coordinator.Settings.groups[1].duration=0;
            coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup{groupName="Group C",completionMode=WaveAttackGroupCompletion.Timed,
                duration=1,maxWaitDuration=0,waitUntilReady=false,controlledSubWaves=new(){new WaveAttackSubWaveReference{scheduleIndex=1}}});
            coordinator.InvalidateConfiguration();
            using(var p=new WavePreviewPlayer(wave,4,new Vector3(0,-3)))
            {
                await Next(p);var handoff=await Next(p);
                Check(handoff.ActiveGroups.Count==1&&handoff.ActiveGroups[0]==2,"Multiple same-tick switches coalesce into final active set");
                Check(p.CoordinatorEvents.Count==2,"Zero-duration intermediate group has no duplicate marker");
                scenarios.Add(new{name="multiple_switches_same_tick",time=handoff.Time});
            }
            foreach(var mode in new[]{WaveAttackGroupCompletion.AttackCycles,WaveAttackGroupCompletion.AllCompleted,WaveAttackGroupCompletion.FirstCompleted})
            {
                Configure(coordinator);foreach(var g in coordinator.Settings.groups){g.completionMode=mode;g.attackCyclesCount=2;}
                coordinator.Settings.reactivation=WaveAttackReactivation.Resume;coordinator.InvalidateConfiguration();
                using var p=new WavePreviewPlayer(wave,15,new Vector3(0,-3));var first=await Next(p);var next=await Next(p);
                Check(next.Frame>first.Frame,"Completion mode advances "+mode);
                using var ordinary=new WavePreviewPlayer(wave,15,new Vector3(0,-3));await Seek(ordinary,next.StateTime);
                Check(State(p)==State(ordinary),"Completion/Resume matches normal seek "+mode);
                scenarios.Add(new{name=mode.ToString(),time=next.Time});
            }
            Configure(coordinator);foreach(var g in coordinator.Settings.groups)g.duration=1000;coordinator.InvalidateConfiguration();
            using(var p=new WavePreviewPlayer(wave,3,new Vector3(0,-3)){MaxStepsPerUpdate=1})
            {
                await Seek(p,.2f);string state=State(p);int frame=p.CurrentFrame,version=p.PresentationVersion,snapshots=p.SnapshotCount,eventCount=p.Events.Count;
                var simulation=p.Simulation;var requested=p.RequestedTime;
                Check(p.NextCoordinatorEvent(),"Unreachable future search begins");
                for(int guard=0;p.IsFindingCoordinatorEvent;guard++)
                {
                    Check(guard<500,"Unreachable next event has finite work");p.Update(0,5);
                    Check(p.CurrentFrame==frame&&p.PresentationVersion==version&&ReferenceEquals(simulation,p.Simulation),"Search never mutates current frame/world/presentation");
                    Check(p.SnapshotCount==snapshots&&p.Events.Count==eventCount&&p.RequestedTime==requested,"Search does not touch current transport, events or snapshots");
                    if(guard%10==0){Check(State(p)==state,"Search preserves full visible state");await Task.Yield();}
                }
                Check(State(p)==state&&!p.IsPlaying&&!string.IsNullOrEmpty(p.CoordinatorNavigationMessage),"No-event search ends paused with message and unchanged state");
                p.NextCoordinatorEvent();p.Update(0,5);p.CancelCoordinatorEventNavigation();Check(!p.IsFindingCoordinatorEvent&&State(p)==state,"Cancel preserves current state and releases search");
                scenarios.Add(new{name="unreachable_and_isolated_search",frame});
            }
            Configure(coordinator);
            using(var p=new WavePreviewPlayer(wave,8,new Vector3(0,-3)))
            {
                await Seek(p,5);var old=p.CoordinatorEvents[0];
                p.TargetPosition=new Vector3(1,-3);Check(p.SelectedCoordinatorEvent==null,"Target change clears selection");
                p.SetTargetPositionAtTime(.2f,new Vector3(-1,-3));await Finish(p);
                Check(p.CoordinatorEvents.All(e=>e.Frame<=p.CurrentFrame),"History edit removes stale future events");
                NumericRandomPreviewControls.SetSeedOffset(54);p.Update(0,5);await Finish(p);
                Check(!p.CoordinatorEvents.Any(e=>ReferenceEquals(old,e)),"Seed change rebuilds coordinator index");
                old=p.CoordinatorEvents[0];coordinator.Settings.groups[0].duration=.5f;coordinator.InvalidateConfiguration();p.Update(0,5);await Finish(p);
                Check(!p.CoordinatorEvents.Any(e=>ReferenceEquals(old,e)),"Coordinator config change rebuilds index");
                coordinator.enabled=false;p.Update(0,5);await Finish(p);Check(!p.HasCoordinator&&!p.NextCoordinatorEvent()&&!p.PreviousCoordinatorEvent(),"Disabled coordinator has no navigation");
                coordinator.enabled=true;coordinator.Settings.enabled=false;p.Update(0,5);await Finish(p);Check(!p.HasCoordinator&&!p.NextCoordinatorEvent(),"Disabled settings have no navigation");
                scenarios.Add(new{name="input_seed_config_invalidation_and_disabled"});
            }
            Object.DestroyImmediate(coordinator);
            using(var p=new WavePreviewPlayer(wave,2,Vector3.zero))Check(!p.HasCoordinator&&!p.NextCoordinatorEvent()&&!p.PreviousCoordinatorEvent(),"Absent coordinator has no navigation");
            Check(World.All.Count==worlds,"All visible, search and snapshot worlds disposed");
            Check(active.isDirty==dirty,"User scene dirty state preserved");
            var result=new{passed=true,checks,scenarios,cleanup=true};System.IO.File.WriteAllText("AgentArtifacts/coordinator-event-navigation-checks.json",Json(result));return result;
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);NumericRandomPreviewControls.SetSeedOffset(seed);}
    }
    public static async Task<object> RealWave()
    {
        fullStateType=Loaded("VerifyWavePreviewFastSeek");if(fullStateType==null)throw new Exception("Load Fast Seek regression first.");
        checks=0;int worlds=World.All.Count;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        string authored=EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<WaveAttackCoordinator>());
        var events=new List<object>();int seed=NumericRandomPreviewControls.SeedOffset;NumericRandomPreviewControls.SetSeedOffset(53);
        try
        {
            using(var p=new WavePreviewPlayer(source,90,new Vector3(0,-3)){SnapshotInterval=2})
            {
                for(int i=0;i<4;i++)
                {
                    Check(p.NextCoordinatorEvent(),"Real wave next request accepted");await Finish(p);
                    var e=p.SelectedCoordinatorEvent;
                    Check(e!=null,"Real wave next event found: "+p.CoordinatorNavigationMessage);
                    using var ordinary=new WavePreviewPlayer(source,90,new Vector3(0,-3)){SnapshotInterval=2};
                    await Seek(ordinary,e.StateTime);Check(State(p)==State(ordinary),"Real wave navigation equals ordinary full managed/native state");
                    events.Add(new{time=e.Time,stateTime=e.StateTime,summary=e.Summary,active=e.Active});
                }
                int frame=p.CurrentFrame;string state=State(p);Check(p.PreviousCoordinatorEvent(),"Real wave previous accepted");await Finish(p);
                await Next(p);Check(p.CurrentFrame==frame&&State(p)==state,"Real wave previous/next exact replay");
            }
            Check(World.All.Count==worlds,"Real wave worlds cleaned up");Check(scene.isDirty==dirty,"Real wave leaves user scene unchanged");
            Check(authored==EditorJsonUtility.ToJson(source)+EditorJsonUtility.ToJson(source.GetComponent<WaveAttackCoordinator>()),"Real wave and coordinator assets unchanged");
            var result=new{passed=true,checks,wave=AssetDatabase.GetAssetPath(source),events,cleanup=true};
            System.IO.File.WriteAllText("AgentArtifacts/coordinator-event-navigation-real-wave.json",Json(result));return result;
        }
        finally{NumericRandomPreviewControls.SetSeedOffset(seed);}
    }

}
