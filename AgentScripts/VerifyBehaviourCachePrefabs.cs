using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyBehaviourCachePrefabs
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    private static async Task Pump(){await Task.Delay(150);}
    public static async Task<object> Run()
    {
        checks=0;string directory="Assets/AgentArtifacts/BehaviourCacheTests_"+Guid.NewGuid().ToString("N");
        string basePath=directory+"/Base.prefab",variantPath=directory+"/Variant.prefab";
        bool mainDirty=EditorSceneManager.GetActiveScene().isDirty;
        var scene=EditorSceneManager.NewPreviewScene();GameObject source=null,instance=null,loaded=null;
        try
        {
            System.IO.Directory.CreateDirectory(directory);AssetDatabase.Refresh();
            source=new GameObject("Cache prefab base");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source,scene);
            var wave=source.AddComponent<DirectedEnemySubWave>();
            typeof(DirectedEnemySubWave).GetField("structuredConfigurationVersion",F).SetValue(wave,1);
            PrefabUtility.SaveAsPrefabAsset(source,basePath);Object.DestroyImmediate(source);source=null;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
            wave=instance.GetComponent<DirectedEnemySubWave>();Check(wave.FormationBehaviour==null,"Instance initial cached miss");
            PrefabUtility.SaveAsPrefabAsset(instance,variantPath);
            Check(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath))==PrefabAssetType.Variant,"Variant created");
            loaded=PrefabUtility.LoadPrefabContents(basePath);
            var added=loaded.AddComponent<DirectedWaveFormationBehaviour>();
            Check(ReferenceEquals(loaded.GetComponent<DirectedEnemySubWave>().FormationBehaviour,added),"Prefab contents add visible");
            PrefabUtility.SaveAsPrefabAsset(loaded,basePath);PrefabUtility.UnloadPrefabContents(loaded);loaded=null;
            AssetDatabase.ImportAsset(basePath,ImportAssetOptions.ForceUpdate);await Pump();
            var baseAsset=AssetDatabase.LoadAssetAtPath<GameObject>(basePath).GetComponent<DirectedEnemySubWave>();
            Check(baseAsset.FormationBehaviour!=null,"Base prefab component persisted");
            Check(instance.GetComponent<DirectedWaveFormationBehaviour>()!=null,"Live instance receives imported component");
            Check(ReferenceEquals(wave.FormationBehaviour,instance.GetComponent<DirectedWaveFormationBehaviour>()),"Live instance negative cache invalidated");
            var variant=AssetDatabase.LoadAssetAtPath<GameObject>(variantPath).GetComponent<DirectedEnemySubWave>();
            Check(variant.FormationBehaviour!=null,"Variant inherits added component");
            loaded=PrefabUtility.LoadPrefabContents(variantPath);var variantWave=loaded.GetComponent<DirectedEnemySubWave>();
            Check(variantWave.FormationBehaviour!=null,"Variant loaded contents inherited component");
            Object.DestroyImmediate(loaded.GetComponent<DirectedWaveFormationBehaviour>());
            Check(variantWave.FormationBehaviour==null,"Variant contents removed component cache");
            PrefabUtility.SaveAsPrefabAsset(loaded,variantPath);PrefabUtility.UnloadPrefabContents(loaded);loaded=null;
            AssetDatabase.ImportAsset(variantPath,ImportAssetOptions.ForceUpdate);await Pump();
            variant=AssetDatabase.LoadAssetAtPath<GameObject>(variantPath).GetComponent<DirectedEnemySubWave>();
            Check(variant.FormationBehaviour==null,"Variant removed override preserved");
            Check(baseAsset.FormationBehaviour!=null,"Variant override does not remove base component");
            // Exercise an actual Prefab Stage, then restore the user's former stage.
            var previous=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();string previousPath=previous!=null?previous.assetPath:null;
            try
            {
                var stage=UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(variantPath);
                await Pump(); // Let Unity finish delayed framing before the stage is closed.
                var stageWave=stage.prefabContentsRoot.GetComponent<DirectedEnemySubWave>();
                Check(stageWave.PhaseEntryBehaviour==null,"Prefab Stage initial miss");
                var phase=stage.prefabContentsRoot.AddComponent<DirectedWavePhaseEntryBehaviour>();
                Check(ReferenceEquals(stageWave.PhaseEntryBehaviour,phase),"Prefab Stage addition visible");
                Object.DestroyImmediate(phase);Check(stageWave.PhaseEntryBehaviour==null,"Prefab Stage removal visible");
                // The stage's contents return to their original serialized shape before closing.
                typeof(UnityEditor.SceneManagement.PrefabStage).GetMethod("ClearDirtiness", F).Invoke(stage, null);
            }
            finally{if(previousPath!=null)UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(previousPath);else UnityEditor.SceneManagement.StageUtility.GoToMainStage();}
        }
        finally
        {
            if(loaded!=null)PrefabUtility.UnloadPrefabContents(loaded);if(instance!=null)Object.DestroyImmediate(instance);if(source!=null)Object.DestroyImmediate(source);
            EditorSceneManager.ClosePreviewScene(scene);
            // Delete only the unique fixture directory created by this invocation.
            AssetDatabase.DeleteAsset(directory);
        }
        Check(EditorSceneManager.GetActiveScene().isDirty==mainDirty,"Main scene unchanged");
        var result=new{passed=true,checks,prefab=true,variant=true,prefabStage=true};System.IO.File.WriteAllText("AgentArtifacts/behaviour-cache-prefab-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));return result;
    }
    public static object PrepareReload()
    {
        var root=new GameObject("Behaviour cache reload fixture"){hideFlags=HideFlags.HideAndDontSave};
        var wave=root.AddComponent<DirectedEnemySubWave>();
        _=wave.PhaseEntryBehaviour;_=wave.FormationBehaviour;
        root.AddComponent<DirectedWaveFormationBehaviour>();_=wave.FormationBehaviour;
        SessionState.SetInt("BehaviourCache.ReloadObject",root.GetInstanceID());
        return new{prepared=true,id=root.GetInstanceID()};
    }
    public static object VerifyReload()
    {
        var root=EditorUtility.EntityIdToObject(SessionState.GetInt("BehaviourCache.ReloadObject",0)) as GameObject;
        if(root==null)throw new Exception("Reload fixture disappeared.");
        try
        {
            var wave=root.GetComponent<DirectedEnemySubWave>();
            Check(ReferenceEquals(wave.FormationBehaviour,root.GetComponent<DirectedWaveFormationBehaviour>()),"Reload positive resolution");
            Check(wave.PhaseEntryBehaviour==null,"Reload negative resolution");
            var phase=root.AddComponent<DirectedWavePhaseEntryBehaviour>();Check(ReferenceEquals(wave.PhaseEntryBehaviour,phase),"Reload add after miss");
            return new{passed=true,domainReload=true};
        }
        finally{Object.DestroyImmediate(root);SessionState.EraseInt("BehaviourCache.ReloadObject");}
    }
}
