using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game.Infrastructure.Analytics;
using Unity.Services.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UnityConsent;
using Zenject;
using ProjectAnalytics = Game.Infrastructure.Analytics.IAnalyticsService;

// Loaded through Pipeline run_script outside Assets. It preserves the Editor's existing scene and options.
public static class VerifyAnalyticsPlayMode
{
    private static EnterPlayModeOptions oldOptions;
    private static bool oldOptionsEnabled;
    private static string oldRemoteDevice;
    private static SceneAsset oldStartScene;
    private static string outputPath;
    private static bool running;

    [Serializable]
    private sealed class Result
    {
        public bool success;
        public int count;
        public List<string> checks;
        public string error;
        public string sdkVersion = "6.3.0";
        public string environment = "development";
        public bool realConsentNeverGranted = true;
        public int realRecordEventCalls = 0;
        public bool actualNetworkOfflineTest = false;
        public string sceneScope = "isolated container and runtime additive scene, not full gameplay scene";
    }

    public static object Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run while the Editor is stopped.");
        if (EndUserConsent.GetConsentState().AnalyticsIntent == ConsentStatus.Granted)
            throw new InvalidOperationException("Diagnostic refused: real user consent is already granted. Use a separate no-consent test profile.");

        outputPath = Path.GetFullPath("AgentArtifacts/analytics-playmode.json");
        const string folder = "Assets/AgentArtifacts/AnalyticsDiagnostics";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/AgentArtifacts", "AnalyticsDiagnostics");
        var path = folder + "/Empty.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(active);
        }

        oldOptions = EditorSettings.enterPlayModeOptions;
        oldOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        oldStartScene = EditorSceneManager.playModeStartScene;
        oldRemoteDevice = EditorSettings.unityRemoteDevice;
        EditorSettings.unityRemoteDevice = "None"; // This SDK check does not use Unity Remote/ADB.
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.playModeStateChanged += OnStateChanged;
        File.WriteAllText(outputPath, "{\"status\":\"queued\"}");
        return new { queued = true, outputPath, scene = path, realConsentNeverGranted = true };
    }

    private static void OnStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && !running)
        {
            running = true;
            _ = Run();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnStateChanged;
            EditorSettings.enterPlayModeOptions = oldOptions;
            EditorSettings.enterPlayModeOptionsEnabled = oldOptionsEnabled;
            EditorSceneManager.playModeStartScene = oldStartScene;
            EditorSettings.unityRemoteDevice = oldRemoteDevice;
        }
    }

    private static async Task Run()
    {
        var checks = new List<string>();
        UnityAnalyticsService service = null;
        Scene transient = default;
        try
        {
            Check(!Zenject.ProjectContext.HasInstance, "No gameplay ProjectContext or player save initialization", checks);
            var consent = EndUserConsent.GetConsentState();
            Check(consent.AnalyticsIntent != ConsentStatus.Granted, "Native consent not granted", checks);
            var container = new DiContainer();
            container.Bind<AnalyticsConfiguration>().FromInstance(new AnalyticsConfiguration());
            container.Bind<IUnityAnalyticsBackend>().To<UnityAnalyticsBackend>().AsSingle();
            container.BindInterfacesAndSelfTo<UnityAnalyticsService>().AsSingle();
            service = container.Resolve<UnityAnalyticsService>();
            var first = service.InitializeAsync();
            Check(ReferenceEquals(first, service.InitializeAsync()), "Real initialization calls share task", checks);
            if (await Task.WhenAny(first, Task.Delay(20000)) != first)
                throw new TimeoutException("Real UGS initialization exceeded 20 seconds");
            Check(await first, "Real SDK initialized: " + service.LastError, checks);
            Check(UnityServices.State == ServicesInitializationState.Initialized, "Official UnityServices reports initialized", checks);
            Check(service.EnvironmentName == "development", "SDK configured for development only", checks);
            Check(!service.CanRecordEvents && service.Consent != ConsentStatus.Granted, "Adapter remains inactive without consent", checks);
            var sdk = Unity.Services.Analytics.AnalyticsService.Instance;
            bool active = (bool)sdk.GetType().GetProperty("IsActive", BindingFlags.Instance | BindingFlags.Public).GetValue(sdk);
            Check(!active, "Official SDK IsActive=false, not just adapter gate", checks);
            Check(await service.InitializeAsync(), "Repeated real initialization succeeds without restart", checks);

            transient = SceneManager.CreateScene("Analytics isolated scene transition");
            Check(ReferenceEquals(service, container.CreateSubContainer().Resolve<ProjectAnalytics>()), "Scene scope keeps project service", checks);
            var unload = SceneManager.UnloadSceneAsync(transient);
            while (!unload.isDone) await Task.Yield();
            transient = default;
            Check(service.IsInitialized && !service.CanRecordEvents, "Scene unload preserves readiness and no-consent state", checks);

            using (var foreign = new UnityAnalyticsService(new UnityAnalyticsBackend(), new AnalyticsConfiguration()))
            {
                Check(!await foreign.InitializeAsync() && foreign.LastError.Contains("outside"), "Unknown prior UGS environment fails closed", checks);
                Check(!foreign.GrantConsent() && EndUserConsent.GetConsentState().AnalyticsIntent == consent.AnalyticsIntent,
                    "Unverified environment cannot be activated by a later consent call", checks);
            }
            Check(EndUserConsent.GetConsentState().AnalyticsIntent == consent.AnalyticsIntent &&
                  EndUserConsent.GetConsentState().AdsIntent == consent.AdsIntent, "Diagnostic never changes native consent", checks);
            service.Dispose();
            service.Dispose();
            Check(service.State == AnalyticsInitializationState.Disposed, "Real adapter teardown idempotent", checks);
            File.WriteAllText(outputPath, JsonUtility.ToJson(new Result { success = true, count = checks.Count, checks = checks }, true));
        }
        catch (Exception exception)
        {
            File.WriteAllText(outputPath, JsonUtility.ToJson(new Result { success = false, count = checks.Count, checks = checks, error = exception.ToString() }, true));
        }
        finally
        {
            service?.Dispose();
            if (transient.IsValid() && transient.isLoaded) _ = SceneManager.UnloadSceneAsync(transient);
            EditorApplication.delayCall += EditorApplication.ExitPlaymode;
        }
    }

    private static void Check(bool condition, string name, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks.Add(name);
    }
}
