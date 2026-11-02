using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

public static class InvestigatePPtr
{
    const string Root = "AgentArtifacts/PPtrDiagnostics";
    static void Write(string name, object value) { Directory.CreateDirectory(Root); File.WriteAllText(Root + "/" + name + ".json", (string)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[]{typeof(object)}).Invoke(null, new[]{value})); }
    static object Describe(Object o) { if (!o) return null; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id); return new { name=o.name, type=o.GetType().FullName, path=AssetDatabase.GetAssetPath(o), guid, fileID=id, instanceID=o.GetInstanceID() }; }
    public static object Capture()
    {
        var plugins = new List<object>();
        foreach(var p in PluginImporter.GetAllImporters()) plugins.Add(new { info=Describe(p), android=p.GetCompatibleWithPlatform(BuildTarget.Android), any=p.GetCompatibleWithAnyPlatform(), editor=p.GetCompatibleWithEditor(), cpu=p.GetPlatformData(BuildTarget.Android,"CPU") });
        var scripts = MonoImporter.GetAllRuntimeMonoScripts();
        var scriptIssues = new List<object>();
        foreach(var s in scripts) if (!s || (s.GetClass()!=null && !s.GetClass().Assembly.FullName.Contains("Editor") && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(s)))) scriptIssues.Add(Describe(s));
        var sceneObjects = new List<object>();
        for(int i=0;i<SceneManager.sceneCount;i++) foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(!c) {sceneObjects.Add(new {missing=true,root=root.name});continue;}
                if(c is MonoBehaviour mb) { using(var so=new SerializedObject(c)) {var prop=so.FindProperty("m_Script");if(prop!=null && !(prop.objectReferenceValue is MonoScript)) sceneObjects.Add(new {component=Describe(c),script=Describe(prop.objectReferenceValue)});}}
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(c,out string g,out long id);
                if(id==227821869 || id==215614217) sceneObjects.Add(new {component=Describe(c),go=c.gameObject.name});
            }
        Write("baseline",new {utc=DateTime.UtcNow,version=Application.unityVersion,plugins,scripts=scripts.Length,scriptIssues,sceneObjects,settings=Settings(),report=Report(BuildReport.GetLatestReport())});
        return new {plugins=plugins.Count,scripts=scripts.Length,scriptIssues=scriptIssues.Count,sceneObjects};
    }
    static object Settings() => new {id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),arch=PlayerSettings.Android.targetArchitectures.ToString(),development=EditorUserBuildSettings.development,bundle=EditorUserBuildSettings.buildAppBundle,frameTiming=PlayerSettings.enableFrameTimingStats,defines=PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android),scenes=EditorBuildSettings.scenes.Select(s=>new{s.path,s.enabled}).ToArray(),open=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>new {path=SceneManager.GetSceneAt(i).path,dirty=SceneManager.GetSceneAt(i).isDirty}).ToArray()};
    static object Report(BuildReport r) => !r ? null : new {summary=new {result=r.summary.result.ToString(),r.summary.totalErrors,r.summary.totalWarnings,seconds=r.summary.totalTime.TotalSeconds,r.summary.outputPath},steps=r.steps.Select(s=> new {s.name,s.depth,seconds=s.duration.TotalSeconds,messages=s.messages.Select(m=>new{type=m.type.ToString(),m.content}).ToArray()}).ToArray()};
    [Serializable] sealed class ScriptRef { public long fileID = 0; public string guid = null; }
    [Serializable] sealed class ScriptRefs { public ScriptRef[] items = null; }
    public static object AuditReferences()
    {
        var input=(ScriptRefs)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("DeserializeObject",new[]{typeof(string),typeof(Type)}).Invoke(null,new object[]{"{\"items\":"+File.ReadAllText(Root+"/unique-script-refs.json")+"}",typeof(ScriptRefs)});
        var issues=new List<object>();
        int resolved=0, prefabs=0, behaviours=0;
        foreach(var item in input.items)
        {
            string path=AssetDatabase.GUIDToAssetPath(item.guid);
            Object found=null;
            if(!string.IsNullOrEmpty(path)) foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            { if(obj && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string g,out long id) && id==item.fileID) { found=obj;break; } }
            if(!(found is MonoScript script))issues.Add(new {item.guid,item.fileID,path,target=Describe(found)});
            else {resolved++;if(script.GetClass()==null)issues.Add(new {item.guid,item.fileID,path,issue="Script class unresolved"});}
        }
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!go)continue;
            prefabs++;
            foreach(var t in go.GetComponentsInChildren<Transform>(true))
            {
                int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if(missing>0)issues.Add(new {path,go=t.name,missing});
                foreach(var mb in t.GetComponents<MonoBehaviour>())
                {if(!mb)continue;behaviours++;using(var so=new SerializedObject(mb)){var s=so.FindProperty("m_Script");if(s==null || !(s.objectReferenceValue is MonoScript))issues.Add(new {path,go=t.name,component=mb.GetType().FullName,issue="Invalid m_Script"});}}
            }
        }
        var sceneObjects=new List<object>();
        for(int i=0;i<SceneManager.sceneCount;i++)foreach(var go in SceneManager.GetSceneAt(i).GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Component>(true))
        {
            if(!c){issues.Add(new{scene=SceneManager.GetSceneAt(i).path,missing=true});continue;}
            var id=GlobalObjectId.GetGlobalObjectIdSlow(c);
            if(id.targetObjectId==227821869 || id.targetObjectId==215614217)sceneObjects.Add(new {id=id.ToString(),component=Describe(c),hierarchy=AnimationUtility.CalculateTransformPath(c.transform,null)});
        }
        Write("reference-audit",new{resolved,unique=input.items.Length,prefabs,behaviours,issues,sceneObjects});
        return new {resolved,unique=input.items.Length,prefabs,behaviours,issues,sceneObjects};
    }
    public static object AuditBuildDependencies(string label)
    {
        var report=BuildReport.GetLatestReport();
        var assets=report.packedAssets.SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var callbacks=TypeCache.GetTypesDerivedFrom<UnityEditor.Build.IPreprocessBuildWithReport>().Select(t=>t.FullName).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var sceneCallbacks=TypeCache.GetTypesDerivedFrom<UnityEditor.Build.IProcessSceneWithReport>().Select(t=>t.FullName).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Where(p=>p.name.Contains("entities")||p.name.Contains("addressables")||p.name.Contains("animation")||p.name.Contains("buildpipeline")).Select(p=>new{p.name,p.version}).ToArray();
        Write(label+"-dependencies",new{assets,callbacks,sceneCallbacks,packages,settings=Settings()});
        return new {assets=assets.Length,callbacks=callbacks.Length,sceneCallbacks=sceneCallbacks.Length,packages};
    }
    public static object VerifyBuildGate()
    {
        var method=typeof(PerformanceSuiteEditor).GetMethod("ValidateBuildResult",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        if(method==null)throw new InvalidOperationException("Build result validation method is unavailable");
        var checks=new List<object>();
        foreach(var test in new[]{(BuildResult.Succeeded,0),(BuildResult.Succeeded,45),(BuildResult.Succeeded,53),(BuildResult.Failed,0),(BuildResult.Cancelled,0),(BuildResult.Unknown,0)})
        {
            bool rejected=false;
            try {method.Invoke(null,new object[]{test.Item1,test.Item2});}
            catch(System.Reflection.TargetInvocationException e) when(e.InnerException is InvalidOperationException){rejected=true;}
            bool expected=test.Item1!=BuildResult.Succeeded||test.Item2!=0;
            checks.Add(new{result=test.Item1.ToString(),errors=test.Item2,rejected,passed=rejected==expected});
            if(rejected!=expected)throw new InvalidOperationException("Incorrect gate for "+test);
        }
        Write("build-gate-checks",new{passed=true,checks});return new{passed=true,checks=checks.Count};
    }
    public static object InspectBattleButtons()
    {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Fighting/Fighting.unity");
        var buttons=new List<object>();
        try
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name!="Button")continue;
                var components=new List<object>();
                foreach(var c in t.GetComponents<Component>())
                {
                    if(!c){components.Add(new{missing=true});continue;}
                    if(c is MonoBehaviour mb)
                    {
                        var script=MonoScript.FromMonoBehaviour(mb);
                        components.Add(new{type=c.GetType().FullName,assembly=c.GetType().Assembly.GetName().Name,script=Describe(script),source=script?script.text:null});
                    }
                }
                buttons.Add(new{hierarchy=AnimationUtility.CalculateTransformPath(t,null),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),components});
            }
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
        Write("battle-button-audit",buttons);return new{buttons=buttons.Count};
    }
    public static object FinalState()
    {
        var plugins=PluginImporter.GetAllImporters().Select(p=>new{info=Describe(p),android=p.GetCompatibleWithPlatform(BuildTarget.Android),any=p.GetCompatibleWithAnyPlatform(),editor=p.GetCompatibleWithEditor(),cpu=p.GetPlatformData(BuildTarget.Android,"CPU")}).ToArray();
        var result=new {settings=Settings(),plugins,compiling=EditorApplication.isCompiling,importing=EditorApplication.isUpdating,playing=EditorApplication.isPlaying,errorStack=Application.GetStackTraceLogType(LogType.Error).ToString()};
        Write("final-state",result);return new {plugins=plugins.Length,result.compiling,result.importing,result.playing,result.errorStack};
    }
    public static object Queue(string mode)
    {
        if(mode!="minimal" && mode!="suite" && mode!="normal")throw new ArgumentException("Expected minimal, suite or normal",nameof(mode));
        if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Idle Editor required");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Dirty scene; no changes made");
        if(new DriveInfo(Path.GetPathRoot(Application.dataPath)).AvailableFreeSpace<6L*1024*1024*1024)throw new IOException("Need 6 GiB free");
        Write("status",new{status="queued",mode});
        EditorApplication.CallbackFunction callback = null; callback = () => { EditorApplication.update -= callback; Build(mode); }; EditorApplication.update += callback;
        return new {queued=true,mode};
    }
    static void Build(string mode)
    {
        object before=Settings();
        var flags=PluginImporter.GetAllImporters().Where(p=>p.assetPath.StartsWith("Assets/",StringComparison.Ordinal)).Select(p=>(path:p.assetPath,android:p.GetCompatibleWithPlatform(BuildTarget.Android))).ToArray();
        var logs=new List<object>();
        Application.LogCallback handler=(message,stack,type)=> {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)logs.Add(new{utc=DateTime.UtcNow,message,stack,type=type.ToString()});};
        var oldStack=Application.GetStackTraceLogType(LogType.Error);
        var arch=PlayerSettings.Android.targetArchitectures;bool development=EditorUserBuildSettings.development, bundle=EditorUserBuildSettings.buildAppBundle;
        string appId=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        var backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        bool frameTiming=PlayerSettings.enableFrameTimingStats;
        string outcome="failed", failure=null;
        Write("status",new{status="building",mode});
        Application.logMessageReceived += handler;
        try
        {
            Application.SetStackTraceLogType(LogType.Error,StackTraceLogType.Full);
            Debug.Log("PPTR_DIAGNOSTIC_BEGIN "+mode);
            if(mode=="suite") PerformanceSuiteEditor.BuildAndroid();
            else
            {
                string[] paths;
                if(mode=="minimal")
                {
                    string dir="Assets/AgentArtifacts/PPtrDiagnostics";
                    if(!AssetDatabase.IsValidFolder(dir)) {if(!AssetDatabase.IsValidFolder("Assets/AgentArtifacts"))AssetDatabase.CreateFolder("Assets","AgentArtifacts");AssetDatabase.CreateFolder("Assets/AgentArtifacts","PPtrDiagnostics");}
                    string path=dir+"/Minimal.unity";
                    if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                    {
                        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                        EditorSceneManager.SaveScene(scene,path);EditorSceneManager.CloseScene(scene,true);
                    }
                    paths=new[]{path};
                }
                else paths=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                EditorUserBuildSettings.buildAppBundle=false;
                var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=paths,target=BuildTarget.Android,locationPathName=Path.GetFullPath(Root+"/"+mode+".apk"),options=BuildOptions.Development|BuildOptions.DetailedBuildReport});
                if(r.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build result "+r.summary.result);
            }
            Write(mode+"-build",Report(BuildReport.GetLatestReport()));
            outcome="completed";
            Write("status",new{status="completed",mode});
        }
        catch(Exception e){failure=e.Message;Write("status",new{status="failed",mode,error=e.Message});}
        finally
        {
            var restorationErrors=new List<string>();
            try
            {
                foreach(var f in flags)
                {
                    try {var p=AssetImporter.GetAtPath(f.path) as PluginImporter;if(p && p.GetCompatibleWithPlatform(BuildTarget.Android)!=f.android){p.SetCompatibleWithPlatform(BuildTarget.Android,f.android);p.SaveAndReimport();}}
                    catch(Exception e){restorationErrors.Add(f.path+": "+e.Message);}
                }
            }
            finally
            {
                try
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,appId);
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,backend);
                    PlayerSettings.enableFrameTimingStats=frameTiming;
                    PlayerSettings.Android.targetArchitectures=arch;EditorUserBuildSettings.development=development;EditorUserBuildSettings.buildAppBundle=bundle;
                }
                finally {Application.SetStackTraceLogType(LogType.Error,oldStack);Application.logMessageReceived -= handler;}
            }
            Write(mode+"-logs",logs);Write(mode+"-settings",new{before,after=Settings(),restorationErrors,pluginsRestored=flags.All(f=>(AssetImporter.GetAtPath(f.path) as PluginImporter)?.GetCompatibleWithPlatform(BuildTarget.Android)==f.android)});
            Write("status",new{status=restorationErrors.Count==0?outcome:"restoration_failed",mode,error=failure,restorationErrors});
            Debug.Log("PPTR_DIAGNOSTIC_END "+mode);
        }
    }
}



