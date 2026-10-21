using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePreviewSnapshotPlayer
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var f = type.GetField(name, Flags); if (f != null) { f.SetValue(target, value); return; }
            var p = type.GetProperty(name, Flags); if (p != null) { p.SetValue(target, value); return; }
        }
        throw new Exception("Missing member " + name);
    }
    private static object Fixture(string name, params object[] args) => typeof(WavePreviewSnapshotFixtures).GetMethod(name, Static).Invoke(null, args);
    private static async Task Seek(WavePreviewPlayer player, float time, double budget = 1000)
    {
        player.Seek(time);
        for (int guard = 0; player.IsSeeking; guard++)
        { if (guard > 5000) throw new Exception("Seek stalled"); player.Update(0, budget); await Task.Yield(); }
        Check(Mathf.Abs(player.CurrentTime - time) < WavePreviewPlayer.StepInterval * .51f, "Exact requested frame " + time);
    }
    private static string Capture(WavePreviewPlayer player) => (string)Fixture("Capture", player, true);
    private static string NativeState(WavePreviewPlayer player, bool identities = false)
    {
        var values = new List<string>(); var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            string value = (identities ? entity.ToString() : "") + JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity));
            if (manager.HasComponent<EnemyLaserState>(entity)) value += JsonUtility.ToJson(manager.GetComponentData<EnemyLaserState>(entity));
            values.Add(value);
        }
        values.Sort(StringComparer.Ordinal); return string.Join("\n", values);
    }
    public static async Task<object> Run()
    {
        checks = 0; int worlds = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Snapshot player verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var real = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); bool dirty = real.isDirty;
        int seed = NumericRandomPreviewControls.SeedOffset; NumericRandomPreviewControls.SetSeedOffset(0);
        try
        {
            var wave = root.AddComponent<Wave>();
            var first = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Sector child", -1f, false);
            var second = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Laser", 1f, true);
            var aimed = Get(first.AttackPatternBehaviour, "attackSettings");
            Set(aimed, "fireMode", DirectedWaveAttackFireMode.Aimed); Set(aimed, "aimMode", DirectedWaveAttackAimMode.AimOnBurst);
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(first.gameObject, 0), new(second.gameObject, .1f) });
            string authored = EditorJsonUtility.ToJson(wave) + EditorJsonUtility.ToJson(first) + EditorJsonUtility.ToJson(first.AttackPatternBehaviour);
            using (var player = new WavePreviewPlayer(wave, 35, new Vector3(100, 100)))
            {
                player.SnapshotInterval = 1;
                await Seek(player, 20); var simulation = player.Simulation; var sequence = simulation.GetSession(0);
                var world = simulation.ProjectileWorld.SimulationWorld; string beforeMove = NativeState(player, true);
                int eventCount = player.Events.Count, snapshots = player.SnapshotCount;
                player.TargetPosition = new Vector3(-100, 100);
                Check(player.CurrentFrame == 600 && !player.IsSeeking && player.RecordPlayerMovement, "Current-time drag records automatically without seeking");
                Check(ReferenceEquals(player.Simulation, simulation) && ReferenceEquals(sequence, simulation.GetSession(0))
                    && ReferenceEquals(world, simulation.ProjectileWorld.SimulationWorld), "Target drag retains world and sessions");
                player.Update(5, 1000);
                Check(NativeState(player, true) == beforeMove && player.CurrentFrame == 600 && player.Events.Count == eventCount,
                    "Dragging on pause does not advance or recreate projectiles/lasers/attacks");
                Check(player.SnapshotCount == snapshots, "Current input retains valid prefix snapshots");
                Check(player.TargetHistory.Samples.Count == 2, "One movement keyframe added");
                for (int i = 0; i < 20; i++) player.TargetPosition = new Vector3(-100 - i * .01f, 100);
                Check(player.TargetHistory.Samples.Count == 2, "Mouse changes within a frame coalesce");
                await Seek(player, 30); string thirty = Capture(player);
                await Seek(player, 10, .2); Check(player.LastRestoreFrame == 300, "Rewind restores the nearest exact snapshot");
                Check(player.TargetPosition == new Vector3(100, 100), "Target history restores past position");
                await Seek(player, 30); Check(Capture(player) == thirty, "30 to 10 to 30 restores native data and event history exactly");
                Check(player.LastRestoreFrame == 900, "Forward seek also reuses the nearest cached state");
                player.ClearSnapshotCache(); Check(player.SnapshotCount == 1, "Clear cache keeps only the initial state");
                await Seek(player, 0); await Seek(player, 30, .2); Check(Capture(player) == thirty, "Continuous replay with recorded input matches cached restoration");
                await Seek(player, 10); int oldCount = player.SnapshotCount;
                player.TargetPosition = new Vector3(15, -20);
                Check(player.TargetHistory.Samples.Count == 2, "Editing the past replaces the previous future track");
                Check(player.SnapshotCount < oldCount && player.SnapshotCount > 1, "Past edit only invalidates downstream snapshots");
                await Seek(player, 30); string branch = Capture(player);
                Check(branch != thirty, "Edited target history changes future behavior");
                player.ClearSnapshotCache(); await Seek(player, 0); await Seek(player, 30);
                Check(Capture(player) == branch, "Branched history reproduces without future snapshots");
                player.SetTargetPositionAtTime(5, new Vector3(-10, 10)); player.Update(0, 1000);
                while (player.IsSeeking) { player.Update(0, 1000); await Task.Yield(); }
                Check(player.CurrentFrame == 900 && player.LastRestoreFrame <= 150, "Editing an older keyframe rebuilds only its dependent future");
                player.SnapshotMemoryLimitMB = 16;
                Check(player.SnapshotMemoryBytes <= 16L * 1024 * 1024 && player.SnapshotCount <= 32, "Cache memory/count budget enforced");
                player.ClearPlayerMovementHistory();
                while (player.IsSeeking) { player.Update(0, 1000); await Task.Yield(); }
                Check(player.TargetHistory.Samples.Count == 1 && !player.RecordPlayerMovement && player.CurrentFrame == 900,
                    "Clear history returns to a fixed target at the selected time");
                player.Stop(); Check(World.All.Count == worlds, "Stop disposes live world and every snapshot world");

                Fixture("Sequence", first);
                var coordinator = root.AddComponent<WaveAttackCoordinator>();
                for (int i = 0; i < 2; i++) coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup { groupName = "Group " + i,
                    completionMode = WaveAttackGroupCompletion.Timed, duration = .8f, delayAfterGroup = .2f, waitUntilReady = false,
                    maxWaitDuration = 0, controlledSubWaves = new() { new WaveAttackSubWaveReference { scheduleIndex = i, subWavePrefab = i == 0 ? first.gameObject : second.gameObject } } });
                player.SnapshotMemoryLimitMB = 128; player.Restart(); player.Pause(); await Seek(player, 10);
                string coordinated = Capture(player); var coordinatorPlayer = player.Simulation.CoordinatorPlayer;
                player.TargetPosition = new Vector3(20, -20);
                Check(ReferenceEquals(player.Simulation.CoordinatorPlayer, coordinatorPlayer), "Dragging retains the active coordinator");
                await Seek(player, 4); await Seek(player, 10);
                Check(Capture(player) == coordinated, "Sequence and coordinator restore exact shared state");
                player.ClearSnapshotCache(); await Seek(player, 0); await Seek(player, 10);
                Check(Capture(player) == coordinated, "Sequence/coordinator/child continuation agrees with replay from zero");
                Check(player.RestoredSnapshots > 0, "Snapshots actually used by transport");
            }
            Check(real.isDirty == dirty, "Real scene remains unmodified");
            Check(EditorJsonUtility.ToJson(wave).Contains("scheduledSubWaves"), "Authoring references remain available");
            Check(World.All.Count == worlds, "Dispose releases all snapshot worlds");
            return new { passed = true, checks, movementNoReset = true, history = true, snapshots = true, exactReplay = true,
                sequenceCoordinator = true, memoryLimit = true, cleanup = true };
        }
        finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); NumericRandomPreviewControls.SetSeedOffset(seed); }
    }
}


public static class WavePreviewSnapshotFixtures
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private static void Set(object value,string field,object data)
    {for(var type=value.GetType();type!=null;type=type.BaseType){var f=type.GetField(field,Flags);if(f!=null){f.SetValue(value,data);return;}var p=type.GetProperty(field,Flags);if(p!=null){p.SetValue(value,data);return;}}throw new Exception("Missing field "+field);}
    private static object Get(object value,string field)=>value.GetType().GetField(field,Flags).GetValue(value);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Flags).Invoke(value,args);
    private static void Range(object value,string field,float min,float max)
    {var range=Get(value,field+"Random");if(range is RandomFloatRange f){f.enabled=true;f.min=min;f.max=max;}else{var n=(RandomIntRange)range;n.enabled=true;n.min=(int)min;n.max=(int)max;}}
    private static void Seek(WavePreviewPlayer player,float time,double budget=1000d)
    {
        player.Seek(time);for(int guard=0;player.IsSeeking;guard++){if(guard>2000)throw new Exception("Seek stalled");player.Update(0,budget);}
        Check(Mathf.Abs(player.CurrentTime-time)<=WavePreviewPlayer.StepInterval*.51f,"Seek reaches requested frame "+time+" / "+player.CurrentTime);
    }
    private static string Capture(WavePreviewPlayer player,bool includeEvents=true)
    {
        var state=new List<string>();var simulation=player.Simulation;
        for(int cue=0;cue<simulation.CueCount;cue++)
        {
            var session=simulation.GetSession(cue);if(session==null)continue;
            var positions=new Dictionary<int,Vector3>();session.CopyPreviewPositionsTo(positions);
            foreach(var p in positions)state.Add("E"+cue+"/"+p.Key+":"+p.Value.ToString("R"));
        }
        var m=simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query=m.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));
        using var entities=query.ToEntityArray(Allocator.Temp);
        foreach(var entity in entities)
        {
            string s=JsonUtility.ToJson(m.GetComponentData<LocalTransform>(entity))+JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(entity));
            if(m.HasComponent<EnemyLaserState>(entity))s+=JsonUtility.ToJson(m.GetComponentData<EnemyLaserState>(entity));
            state.Add(s);
        }
        state.Sort(StringComparer.Ordinal);var result=new StringBuilder(string.Join("\n",state));
        if(includeEvents)foreach(var e in player.Events)result.Append($"\n{e.Time:R}|{e.Kind}|{e.Cue}|{e.Slot}|{e.Count}|{e.Detail}");
        return result.ToString();
    }
    private static DirectedEnemySubWave Subwave(Transform parent,string name,float x,bool laser=false)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,1);
        var wave=go.AddComponent<DirectedEnemySubWave>();Set(wave,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
        Set(wave,"enemyCount",1);Set(wave,"settleDuration",0f);Set(wave,"spawnInterval",0f);Set(wave,"formationCoordinateSpace",DirectedWaveCoordinateSpace.LocalToSubWave);
        var attack=go.GetComponent<DirectedWaveAttackBehaviour>()??go.AddComponent<DirectedWaveAttackBehaviour>();
        Set(attack,"allowAutonomousAttackDuringEntrance",false);
        Configure((DirectedWaveAttackSettings)Get(attack,"attackSettings"),laser);return wave;
    }
    private static void Configure(DirectedWaveAttackSettings settings,bool laser=false)
    {
        Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(180);
        Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"waitForPreviousAttack",true);Set(settings,"delayAfterAttack",.3f);
        var profile=settings.WaveBurstSettings;profile.ConfigureLegacySingleAttack(2,.5f,.2f);profile.Shooting.ConfigureLegacyFan(3,-45,45);
        var shooting=profile.Shooting;shooting.SetProjectileBaseSpeedOverride(true,1);Set(shooting,"overrideProjectileLifetime",true);Set(shooting,"projectileLifetime",3f);
        if(laser)
        {Set(shooting,"projectileType",EnemyProjectileType.Laser);Set(shooting.Laser.Geometry,"lengthMode",EnemyLaserLengthMode.Custom);Set(shooting.Laser.Geometry,"laserLength",3f);Set(shooting.Laser,"telegraphDuration",.5f);}
        else
        {
            Range(shooting,"fanProjectileCount",2,5);Range(shooting,"fanMinAngle",-60,-20);Range(shooting,"fanMaxAngle",20,60);
            Range(shooting,"projectileAngularSpeed",-25,25);Range(profile,"attackShotInterval",.1f,.3f);Range(profile,"attackShotCount",2,4);
            shooting.EmissionLayout.ConfigureSector(-40,40,4,EnemySectorLaunchMode.Sequential,.1f,.06f);
            shooting.EmissionLayout.ConfigureLaunchOrder(EnemySectorLaunchOrder.PingPong);
            Range(shooting.EmissionLayout,"directionsCount",2,4);
            var child=new EnemyChildAttackSettings();Set(child,"triggerValue",.2f);Set(child,"repeatCount",2);
            child.Pattern.Shooting.ConfigureLegacyFan(3,-10,10);child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true,.5f);
            child.Pattern.Shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange{enabled=true,min=.5f,max=.5f});shooting.SetChildAttack(child);
        }
        settings.Validate();
    }
    public static object Run()
    {
        checks=0;int originalSeed=NumericRandomPreviewControls.SeedOffset;NumericRandomPreviewControls.SetSeedOffset(0);
        int worlds=World.All.Count;var realScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=realScene.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Wave Player verification");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var wave=root.AddComponent<Wave>();var first=Subwave(root.transform,"First",-1);var second=Subwave(root.transform,"Second",1,true);
            Set(wave,"scheduledSubWaves",new List<WaveSubWaveCue>{new(first.gameObject,0),new(second.gameObject,.1f)});
            string authored=EditorJsonUtility.ToJson(first)+EditorJsonUtility.ToJson(first.AttackPatternBehaviour)+EditorJsonUtility.ToJson(second)+EditorJsonUtility.ToJson(wave);
            var random=JsonUtility.ToJson(UnityEngine.Random.state);
            using(var player=new WavePreviewPlayer(wave,30,new Vector3(100,100)))
            {
                player.Play();player.Update(.2,1000);Check(player.CurrentFrame==6,"Play fixed clock");player.Pause();string pause=Capture(player);player.Update(5,1000);Check(Capture(player)==pause&&player.CurrentFrame==6,"Pause freezes state and event history");
                player.Play();player.Update(.1,1000);Check(player.CurrentFrame==9,"Resume continues, no reset");player.Pause();
                player.Seek(20);player.Update(0,.1);int pausedFrame=player.CurrentFrame;player.Pause();string backlog=Capture(player);player.Update(1,1000);Check(player.CurrentFrame==pausedFrame&&Capture(player)==backlog,"Pause cancels a pending catch-up without advancing state");
                Seek(player,20);string twenty=Capture(player);int eventCount=player.Events.Count;Seek(player,5);Seek(player,20,.1d);
                Check(Capture(player)==twenty&&player.Events.Count==eventCount,"20 to 5 to 20 replays identical ECS/enemies/events with a different work budget");
                player.Step(-1);player.Update(0,1000);while(player.IsSeeking)player.Update(0,1000);Check(player.CurrentFrame==599,"Step Backward exactly one tick");
                player.Step(1);player.Update(0,1000);Check(player.CurrentFrame==600&&Capture(player)==twenty,"Step Forward restores exact state");
                Check(player.Simulation.ActiveCueCount==2,"Multiple subwaves share clock");
                float parentStart=0f;foreach(var e in player.Events)if(e.Cue==0&&e.Kind==WavePreviewEventKind.ProjectileSpawn){parentStart=e.Time;break;}
                Seek(player,parentStart+.6f);
                using(var children=player.Simulation.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))using(var values=children.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
                {bool child=false;foreach(var v in values)child|=Mathf.Abs(Unity.Mathematics.math.length(v.Value)-.5f)<.0001f;Check(child,"Child Attack actually emits its configured projectiles");}
                var kinds=new HashSet<WavePreviewEventKind>();foreach(var e in player.Events)kinds.Add(e.Kind);
                Check(kinds.Contains(WavePreviewEventKind.EnemySpawn)&&kinds.Contains(WavePreviewEventKind.AttackStart)&&kinds.Contains(WavePreviewEventKind.AttackEnd)&&kinds.Contains(WavePreviewEventKind.ProjectileSpawn),"Observed spawn/attack/projectile events");
                float laserStart=-1;foreach(var e in player.Events)if(e.Cue==1&&e.Kind==WavePreviewEventKind.ProjectileSpawn){laserStart=e.Time;break;}Check(laserStart>=0,"Laser spawn observed");
                float telegraphTime=Mathf.Round((laserStart+.2f)/WavePreviewPlayer.StepInterval)*WavePreviewPlayer.StepInterval;
                Seek(player,telegraphTime);var m=player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;using(var q=m.CreateEntityQuery(typeof(EnemyLaserState)))using(var values=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp)){bool telegraph=false;foreach(var v in values)telegraph|=v.Telegraph!=0;Check(telegraph,"Actual Laser Telegraph state at selected time");}
                string telegraphState=Capture(player);Seek(player,laserStart+.7f);using(var q=m.CreateEntityQuery(typeof(EnemyLaserState)))using(var values=q.ToComponentDataArray<EnemyLaserState>(Allocator.Temp)){bool active=false;foreach(var v in values)active|=v.Active!=0;Check(active,"Actual Laser active state");}
                Seek(player,telegraphTime);Check(Capture(player)==telegraphState,"Laser/Telegraph rewind exact");
                player.Stop();Check(player.Simulation==null&&player.CurrentFrame==0&&player.Events.Count==0,"Stop frees simulation and resets clock");
                foreach(float speed in new[]{.25f,.5f,1f,2f,4f}){player.Restart();player.PlaybackSpeed=speed;player.Update(WavePreviewPlayer.StepInterval*4d,1000);Check(player.CurrentFrame==Mathf.RoundToInt(4*speed),"Playback speed "+speed);player.Stop();}
                Check(World.All.Count==worlds,"Stop frees all owned ECS worlds");
                Check(authored==EditorJsonUtility.ToJson(first)+EditorJsonUtility.ToJson(first.AttackPatternBehaviour)+EditorJsonUtility.ToJson(second)+EditorJsonUtility.ToJson(wave),"Replay never edits authoring components");
                Sequence(first);player.Restart();Seek(player,4);string sequence=Capture(player);Seek(player,1);Seek(player,4);Check(sequence==Capture(player),"Sequence and child attacks replay exactly");player.Stop();
                var coordinator=root.AddComponent<WaveAttackCoordinator>();
                for(int i=0;i<2;i++)coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup{groupName="Group "+i,completionMode=WaveAttackGroupCompletion.Timed,duration=.7f,delayAfterGroup=.2f,waitUntilReady=false,controlledSubWaves=new(){new WaveAttackSubWaveReference{scheduleIndex=i,subWavePrefab=i==0?first.gameObject:second.gameObject}}});
                player.Restart();Seek(player,5);string coordinated=Capture(player);Seek(player,1);Seek(player,5);Check(coordinated==Capture(player),"Coordinator + Sequence + child timeline replays exactly");
                bool groupEvent=false;foreach(var e in player.Events)groupEvent|=e.Kind==WavePreviewEventKind.CoordinatorGroupChange;Check(groupEvent,"Real coordinator transitions observed");
            }
            Check(JsonUtility.ToJson(UnityEngine.Random.state)==random,"Global Unity Random state restored");
            // Sequence was deliberately configured in memory above; no source assets or scenes were saved.
            Check(EditorJsonUtility.ToJson(wave).Contains("scheduledSubWaves"),"Authoring configuration remains available");
            Check(realScene.isDirty==dirty,"Real scene dirty flag unchanged");
            return new{passed=true,checks,transport=true,replay20_5_20=true,sequenceChildren=true,laserTelegraph=true,coordinator=true,globalRandomPreserved=true};
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);NumericRandomPreviewControls.SetSeedOffset(originalSeed);Check(World.All.Count==worlds,"Dispose leaves no worlds");}
    }
    private static void Sequence(DirectedEnemySubWave wave)
    {
        var behaviour=wave.AttackPatternBehaviour;Set(behaviour,"executionMode",DirectedWaveAttackExecutionMode.Sequence);var sequence=behaviour.AttackSequence;
        sequence.sequenceMode=DirectedWaveAttackSequenceMode.Loop;sequence.loopCount=0;sequence.steps.Clear();
        var first=new DirectedWaveAttackSequenceStep{name="Random Sector + Child"};Configure((DirectedWaveAttackSettings)Get(first.pattern,"attackSettings"));
        var second=new DirectedWaveAttackSequenceStep{name="Laser"};Configure((DirectedWaveAttackSettings)Get(second.pattern,"attackSettings"),true);
        sequence.steps.Add(first);sequence.steps.Add(new DirectedWaveAttackSequenceStep{type=DirectedWaveAttackSequenceStepType.Pause,pauseDuration=.1f});sequence.steps.Add(second);
    }
    public static object ExistingWaves()
        => ExistingRange(0,7);
    public static object ExistingWave(int index) => ExistingRange(index,1);
    private static object ExistingRange(int first,int count)
    {
        checks=0;var paths=new[]{"Assets/Waves/1/1_Extreme/1_1_extrime.prefab","Assets/Waves/1/1_Extreme/1_2_extrime.prefab","Assets/Waves/1/1_Extreme/1_3_extrime.prefab","Assets/Waves/1/1_Extreme/1_4_extrime.prefab","Assets/Waves/2/2_1_extrime.prefab","Assets/Waves/2/2_2_extrime.prefab","Assets/Waves/2/2_3_extrime.prefab"};
        int worlds=World.All.Count;
        for(int pathIndex=first;pathIndex<first+count;pathIndex++)
        {
            string path=paths[pathIndex];
            byte[] bytes=File.ReadAllBytes(path);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var wave=root.GetComponent<Wave>();Check(wave!=null,"Wave conductor "+path);
                var components=root.GetComponentsInChildren<Component>(true);var before=new List<string>();foreach(var c in components)before.Add(c!=null?EditorJsonUtility.ToJson(c):null);
                using var player=new WavePreviewPlayer(wave,30,new Vector3(0,-3));Seek(player,20);string state=Capture(player);Seek(player,5);Seek(player,20);Check(Capture(player)==state,"Existing wave exact replay "+path);
                for(int i=0;i<components.Length;i++)if(components[i]!=null)Check(EditorJsonUtility.ToJson(components[i])==before[i],"Serialized component unchanged "+path+" / "+components[i].GetType().Name);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Check(Convert.ToBase64String(bytes)==Convert.ToBase64String(File.ReadAllBytes(path)),"Prefab bytes unchanged "+path);
        }
        Check(World.All.Count==worlds,"Existing waves release all preview worlds");return new{passed=true,checks,waves=count,exactReplay=true,sourceUnchanged=true};
    }
}

public static class WavePreviewAimSnapshotChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Fixture(string method, params object[] args) => typeof(WavePreviewSnapshotFixtures)
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static void Seek(WavePreviewPlayer player, float time)
    { player.Seek(time); while (player.IsSeeking) player.Update(0, 1000); }
    private static string State(WavePreviewPlayer player) => (string)Fixture("Capture", player, false);
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count; var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Aim snapshot checks"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var sub = (DirectedEnemySubWave)Fixture("Subwave", root.transform, "Aimed", 0f, false);
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(sub.gameObject, 0) });
            foreach (var mode in new[] { DirectedWaveAttackAimMode.AimOnStart, DirectedWaveAttackAimMode.AimOnBurst, DirectedWaveAttackAimMode.AimContinuous })
            {
                var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed); Set(settings, "aimMode", mode);
                Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride); Set(settings, "delayAfterAttack", 100f);
                var profile = settings.WaveBurstSettings; profile.ConfigureLegacySingleAttack(2, 100, .4f);
                Set(profile, "burstAttack", EnemyBurstAttackMode.Enabled); Set(profile, "repeatBurst", true);
                Set(profile, "burstShotCount", 3); Set(profile, "burstShotInterval", .2f);
                profile.Shooting.ConfigureLegacyFan(1, 0, 0); profile.Shooting.SetProjectileBaseSpeedOverride(true, 1);
                Set(profile.Shooting, "overrideProjectileLifetime", true); Set(profile.Shooting, "projectileLifetime", 10f);
                settings.Validate(); Set(sub.AttackPatternBehaviour, "attackSettings", settings);
                using var control = new WavePreviewPlayer(wave, 10, new Vector3(100, 100));
                using var changed = new WavePreviewPlayer(wave, 10, new Vector3(100, 100));
                float first = -1;
                for (int i = 1; i <= 150 && first < 0; i++)
                {
                    Seek(control, i * WavePreviewPlayer.StepInterval);
                    foreach (var e in control.Events) if (e.Kind == WavePreviewEventKind.ProjectileSpawn) { first = e.Time; break; }
                }
                Check(first >= 0, "Aimed fixture starts " + mode);
                Seek(changed, control.CurrentTime); string initial = State(changed);
                changed.TargetPosition = new Vector3(-100, 100);
                Check(State(changed) == initial && State(control) == initial, "Moving target retains already emitted ordinary projectiles " + mode);
                float withinBurst = control.CurrentTime + .24f;
                Seek(control, withinBurst); Seek(changed, withinBurst);
                Check((State(control) != State(changed)) == (mode == DirectedWaveAttackAimMode.AimContinuous), "Within-burst capture follows aim mode " + mode);
                Seek(control, first + 1.2f); Seek(changed, first + 1.2f);
                Check((State(control) != State(changed)) == (mode != DirectedWaveAttackAimMode.AimOnStart), "Next burst refreshes only intended capture " + mode);
                string end = State(changed); changed.ClearSnapshotCache(); Seek(changed, 0); Seek(changed, first + 1.2f);
                Check(State(changed) == end, "Aiming history replay remains exact " + mode);
                changed.Seek(9); changed.Update(0, .1); changed.Seek(.1f); changed.Seek(first + 1.2f);
                while (changed.IsSeeking) changed.Update(0, .2);
                Check(State(changed) == end && !changed.IsLoading, "Rapid seek changes keep only final request " + mode);
                changed.SetTargetPositionAtTime(.1f, new Vector3(10, -100));
                int restored = changed.RestoredSnapshots;
                changed.Seek(changed.CurrentTime); changed.Pause();
                Check(changed.IsSeeking, "Pause and same-time seek retain required history invalidation " + mode);
                while (changed.IsSeeking) changed.Update(0, 1000);
                Check(changed.RestoredSnapshots > restored, "Older input edit restores its valid prefix " + mode);
                var oldSimulation = changed.Simulation; changed.ClearPlayerMovementHistory(); changed.Pause();
                Check(changed.IsSeeking, "Pause does not discard clearing recorded history " + mode);
                while (changed.IsSeeking) changed.Update(0, 1000);
                Check(!ReferenceEquals(changed.Simulation, oldSimulation), "Fixed-target rebuild completes while paused " + mode);
            }
        }
        finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        Check(World.All.Count == worlds, "Aiming tests release all worlds"); return new { passed = true, checks, aimModes = 3, recordedReplay = true, cancelSeek = true };
    }
}
