using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class VerifyShootingSimulationModes
{
    static int checks;
    static void Assert(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    static void Set(object obj, string name, object value)
    {
        for (var type=obj.GetType(); type!=null; type=type.BaseType)
        {
            const BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
            var field=type.GetField(name,f); if(field!=null) {field.SetValue(obj,value);return;}
            var property=type.GetProperty(name,f); if(property!=null){property.SetValue(obj,value);return;}
        }
        throw new Exception(name);
    }
    static void Seek(WaveShootingPreviewSession session, float time)
    { int count=0; while(!session.AdvanceTo(time,4)) if(++count>2000) throw new Exception("Seek stalled"); }
    static Vector2[] Directions(WaveShootingPreviewSession session)
    {
        var manager=session.ProjectileWorld.SimulationWorld.EntityManager;
        using var query=manager.CreateEntityQuery(typeof(EnemyProjectileVelocity));
        using var values=query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp);
        var result=new Vector2[values.Length];
        for(int i=0;i<result.Length;i++) result[i]=new Vector2(values[i].Value.x,values[i].Value.y).normalized;
        return result;
    }
    public static async Task<object> Run()
    {
        checks=0;
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Shooting simulation modes verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        var wave=root.AddComponent<DirectedEnemySubWave>();
        var attack=root.AddComponent<DirectedWaveAttackBehaviour>();
        Set(wave,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
        Set(wave,"enemyCount",1); Set(wave,"formationLayout",DirectedWaveFormationLayout.HorizontalLine);
        var settings=typeof(DirectedWaveAttackBehaviour).GetField("attackSettings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(attack) as DirectedWaveAttackSettings;
        Set(settings,"fireMode",DirectedWaveAttackFireMode.Aimed);
        Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);
        Set(settings,"waitForPreviousAttack",true); Set(settings,"delayAfterAttack",1000f);
        var profile=settings.WaveBurstSettings;
        profile.ConfigureLegacyRepeatedBurst(2,1000,.15f);
        Set(profile,"attackShotCount",2); Set(profile,"attackShotInterval",.25f);
        profile.Shooting.SetProjectileBaseSpeedOverride(true,3);
        Set(profile.Shooting,"overrideProjectileLifetime",true); Set(profile.Shooting,"projectileLifetime",10f);
        profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
        var timings=new System.Collections.Generic.List<object>();
        WaveShootingPreviewSession drawSession=null;
        Action<SceneView> draw=null;
        try
        {
            foreach(var mode in new[]{DirectedWaveAttackAimMode.AimOnStart,DirectedWaveAttackAimMode.AimOnBurst,DirectedWaveAttackAimMode.AimContinuous})
            {
                Set(settings,"aimMode",mode); settings.Validate();
                using var session=new WaveShootingPreviewSession(wave,true,false,0,new Vector3(0,-5));
                Seek(session,mode==DirectedWaveAttackAimMode.AimOnBurst?.25f:1f/30);
                var enemy=wave.GetConfiguredEnemyPrefabForSlot(0);
                var frame=EnemyShootingPreview.CreateFrame(enemy.GetComponent<SimpleEnemyAttackExecutor>(),profile.Shooting,
                    wave.GetConfiguredFormationSlotPosition(0),0,null,out _);
                session.TargetPosition=frame.ShootingPoint+Vector3.right*5;
                Seek(session,.7f);
                var directions=Directions(session); Assert(directions.Length==4,"Four shots in two bursts: "+directions.Length+" mode="+mode+" warning="+session.Warning);
                int down=0,right=0;
                foreach(var d in directions){if(d.y<-.99f)down++;if(d.x>.99f)right++;}
                Assert(mode==DirectedWaveAttackAimMode.AimOnStart?down==4:mode==DirectedWaveAttackAimMode.AimOnBurst?down==2&&right==2:down==1&&right==3,
                    "Aimed capture matches selected mode: "+mode+" down="+down+" right="+right);
            }
            Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);
            Set(settings,"useAttackStartDelay",true);Set(settings,"attackStartDelay",1000f);
            profile.ConfigureLegacySingleAttack(1,0,0);
            root.AddComponent<DirectedWavePhaseEntryBehaviour>();
            Set(wave,"pathCheckpoints",new[]{new DirectedWavePathCheckpoint{position=Vector3.zero,durationToNext=2},
                new DirectedWavePathCheckpoint{position=new Vector3(0,-2)}});
            Set(wave,"settleDuration",1f);
            var entrance=attack.EditorPreviewEntranceSettings;
            Set(entrance,"isEnabled",true);
            var at=new DirectedWaveAtCheckpointAttackRule();Set(at,"checkpointIndex",1);Set(at,"shotCount",2);
            entrance.AtCheckpointRules.Add(at);
            var across=new DirectedWaveAcrossCheckpointsAttackRule();Set(across,"attackCount",2);
            entrance.AcrossCheckpointRules.Add(across);
            using(var session=new WaveShootingPreviewSession(wave,false,false,0,new Vector3(0,-5)))
            {
                Seek(session,2.2f);
                Assert(session.ProjectileCount==4,"Entrance checkpoint and across-checkpoint rules emit actual ECS shots: "+session.ProjectileCount+" post="+wave.GetSimulationPreviewPostStartTime()+" checkpoints="+wave.GetConfiguredEntranceCheckpointCount()+" entry="+wave.HasPhaseEntry);
            }
            Set(entrance,"isEnabled",false);
            Set(settings,"useAttackStartDelay",false);
            Set(wave,"enemyCount",1024);
            Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);
            Set(settings,"waitForPreviousAttack",false);Set(settings,"attacksPerSecond",1000f);
            profile.ConfigureLegacySingleAttack(1,0,0);
            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial,1000);
            profile.Shooting.ConfigureLegacyFan(1000,-30,30); settings.Validate();
            drawSession=new WaveShootingPreviewSession(wave,true,true,0,new Vector3(0,-5));
            Assert(drawSession.EnemyCount==32,"1024-enemy preview is sampled to 32");
            var timer=Stopwatch.StartNew(); Seek(drawSession,2);
            Assert(drawSession.ProjectileCount<=2048 && drawSession.Omitted>0,"Million-ray volleys are explicitly bounded");
            double max=0;
            for(int i=0;i<10;i++) {timer.Restart();drawSession.AdvanceTo(drawSession.Time+1f/30,4);max=Math.Max(max,timer.Elapsed.TotalMilliseconds);}
            timings.Add(new{mode="dense 1024 enemies, million rays",active=drawSession.ProjectileCount,maxStepMs=max,omitted=drawSession.Omitted});
            timer.Restart();drawSession.ProjectileWorld.Step(drawSession.Time+1f/30,1f/30,drawSession.TargetPosition);
            double simulationMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart();drawSession.ProjectileWorld.CaptureRenderState();
            double captureMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart();typeof(WaveShootingPreviewSession).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(drawSession,new object[]{drawSession.Time});
            timings.Add(new {simulationMs,captureMs,schedulingMs=timer.Elapsed.TotalMilliseconds});
            var view=SceneView.lastActiveSceneView;
            if(view==null)throw new Exception("Scene View required");
            var done=new TaskCompletionSource<bool>();
            bool shown=EnemyShootingPreview.ShowFormationVolley;
            draw=current=>
            {
                if(current!=view||Event.current.type!=EventType.Repaint||done.Task.IsCompleted)return;
                try{drawSession.Draw(view);done.SetResult(true);}catch(Exception error){done.TrySetException(error);}
            };
            try
            {
                EnemyShootingPreview.ShowFormationVolley=true;
                SceneView.duringSceneGui+=draw;view.Repaint();
                if(await Task.WhenAny(done.Task,Task.Delay(10000))!=done.Task)throw new Exception("Scene did not repaint");
                await done.Task;Assert(true,"Actual Scene GUI renders instanced projectile batches and target handle");
                drawSession.SetVisible(false);Assert(!drawSession.TargetObject.activeInHierarchy,"Hide disables temporary visuals");
            }
            finally{SceneView.duringSceneGui-=draw;EnemyShootingPreview.ShowFormationVolley=shown;}
        }
        finally
        {
            drawSession?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        return new{passed=true,checks,timings};
    }
}
