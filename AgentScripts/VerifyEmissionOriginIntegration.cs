using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyEmissionOriginIntegration
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Set(object obj,string field,object value){for(var type=obj.GetType();type!=null;type=type.BaseType){var info=type.GetField(field,Fields|BindingFlags.Public);if(info!=null){info.SetValue(obj,value);return;}var property=type.GetProperty(field,Fields|BindingFlags.Public);if(property!=null){property.SetValue(obj,value);return;}}throw new Exception(field);}
    static object Get(object obj,string field)=>obj.GetType().GetField(field,Fields|BindingFlags.Public).GetValue(obj);
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Seek(WaveShootingPreviewSession session,float time){int n=0;while(!session.AdvanceTo(time,10))if(++n>1000)throw new Exception("Seek stalled");}
    static float[] XPositions(WaveShootingPreviewSession session){using var query=session.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement),typeof(LocalTransform));using var poses=query.ToComponentDataArray<LocalTransform>(Allocator.Temp);var values=new float[poses.Length];for(int i=0;i<values.Length;i++)values[i]=poses[i].Position.x;return values;}
    static void Near(float x,float expected,string message)=>Check(Mathf.Abs(x-expected)<.01f,message+" "+x+" != "+expected);
    public static object Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new Exception("Requires idle Editor");checks=0;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Origin preview integration");SceneManager.MoveGameObjectToScene(root,scene);
        try{
            var wave=root.AddComponent<DirectedEnemySubWave>();var attack=root.AddComponent<DirectedWaveAttackBehaviour>();Set(wave,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());Set(wave,"enemyCount",1);Set(wave,"formationLayout",DirectedWaveFormationLayout.HorizontalLine);
            var settings=(DirectedWaveAttackSettings)Get(attack,"attackSettings");Set(settings,"fireMode",DirectedWaveAttackFireMode.Stationary);settings.StationaryFireSettings.ConfigureAngle(180);Set(settings,"burstSettingsSource",DirectedWaveBurstSettingsSource.WaveOverride);Set(settings,"waitForPreviousAttack",true);Set(settings,"delayAfterAttack",1000f);
            var profile=settings.WaveBurstSettings;profile.ConfigureLegacyRepeatedBurst(2,1000,.15f);Set(profile,"attackShotCount",2);Set(profile,"attackShotInterval",.25f);profile.Shooting.SetProjectileBaseSpeedOverride(true,1);Set(profile.Shooting,"overrideProjectileLifetime",true);Set(profile.Shooting,"projectileLifetime",10f);profile.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.Player);profile.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World,3);profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
            foreach(EnemyEmissionOriginCaptureMode mode in Enum.GetValues(typeof(EnemyEmissionOriginCaptureMode)))
            {
                profile.Shooting.EmissionOrigin.SetCaptureMode(mode);settings.Validate();using var session=new WaveShootingPreviewSession(wave,true,false,0,new Vector3(1,-5));Seek(session,1f/30);Check(session.ProjectileCount==1,"First preview volley");float first=XPositions(session)[0];session.TargetPosition=new Vector3(3,-5);Seek(session,.25f);var firstBurst=XPositions(session);Check(firstBurst.Length==2,"Second shot in burst");Near(firstBurst[1]-first,mode==EnemyEmissionOriginCaptureMode.OnShot?2:0,"Preview capture within burst");session.TargetPosition=new Vector3(5,-5);Seek(session,.7f);var shots=XPositions(session);Check(shots.Length==4,"Two complete preview bursts");Near(shots[2]-first,mode==EnemyEmissionOriginCaptureMode.OnAttackStart?0:4,"Preview capture on next burst");
            }
            // Independent captures for overlapping entrance rules using one shared profile.
            profile.Shooting.EmissionOrigin.SetCaptureMode(EnemyEmissionOriginCaptureMode.OnAttackStart);var earlier=new EnemyEmissionOriginAttackCapture(new Vector3(1,-5));var later=new EnemyEmissionOriginAttackCapture(new Vector3(6,-5));Near(earlier.Prepare(profile,0,new Vector3(9,-5),false).x,1,"Earlier entrance rule retains capture");Near(later.Prepare(profile,0,new Vector3(9,-5),false).x,6,"Later entrance rule owns capture");Near(earlier.Prepare(profile,0,new Vector3(12,-5),true).x,1,"Other rule cannot replace earlier capture");
            var preset=ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();try{preset.SetAttackSettings(settings,false,1);Set(attack,"attackPreset",preset);var resolved=new DirectedWaveAttackSettings();Check(attack.TryCopyResolvedAttackSettingsTo(0,false,resolved),"Wave resolves preset");Check(resolved.WaveBurstSettings.Shooting.EmissionOrigin.UsesPlayer,"Origin resolves through actual behaviour preset");attack.UsePresetAsLocalSettings();Check(attack.TryCopyResolvedAttackSettingsTo(0,false,resolved)&&resolved.WaveBurstSettings.Shooting.EmissionOrigin.UsesPlayer,"Preset converted to local keeps origin");}finally{UnityEngine.Object.DestroyImmediate(preset);}
            int prefabs=0,patterns=0;foreach(string guid in AssetDatabase.FindAssets("t:GameObject",new[]{"Assets/Waves"})){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));foreach(var behaviour in asset.GetComponentsInChildren<DirectedWaveAttackBehaviour>(true)){prefabs++;using var serialized=new SerializedObject(behaviour);var origin=serialized.FindProperty("attackSettings.waveBurstSettings.shooting.emissionOrigin");Check(origin!=null,"Prefab has new serialized origin");Check(origin.FindPropertyRelative("x.source").intValue==0&&origin.FindPropertyRelative("y.source").intValue==0,"Existing prefab defaults Enemy/Enemy");var result=new DirectedWaveAttackSettings();behaviour.TryCopyResolvedAttackSettingsTo(0,false,result);for(int i=0;i<result.WaveBurstSettings.PatternCount;i++){patterns++;Check(result.WaveBurstSettings.GetPattern(i).Shooting.EmissionOrigin.IsDefault,"Existing configured patterns unchanged");}}}
            return new {passed=true,checks,prefabs,patterns};
        }finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
}