using System;
using System.IO;
using Game.Performance;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyPerformanceFinalState
{
    [Serializable]
    private sealed class Result
    {
        public bool passed, compiling, importing, playing, sceneDirty, suiteWorldPresent;
        public bool testDefinesPersisted, suitePackagePersisted, startSceneRestored, remoteDeviceRestored;
        public bool configReady, fixturesReady;
        public string scene, applicationId, backend, analyticsState, utc;
        public int scenarios, projectContexts;
    }

    public static object Run()
    {
        var scene = SceneManager.GetActiveScene();
        var target = NamedBuildTarget.Android;
        var config = AssetDatabase.LoadAssetAtPath<PerformanceSuiteConfig>(PerformanceSuiteEditor.ConfigPath);
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PerformanceSuite/Fixtures/Enemy.prefab");
        var ship = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PerformanceSuite/Fixtures/Ship.prefab");
        bool suiteWorld = false;
        foreach (var world in World.All)
            if (world.IsCreated && world.Name == "Android Performance Suite") suiteWorld = true;
        var result = new Result
        {
            scene = scene.path, sceneDirty = scene.isDirty,
            compiling = EditorApplication.isCompiling, importing = EditorApplication.isUpdating,
            playing = EditorApplication.isPlaying, suiteWorldPresent = suiteWorld,
            applicationId = PlayerSettings.GetApplicationIdentifier(target),
            backend = PlayerSettings.GetScriptingBackend(target).ToString(),
            testDefinesPersisted = PlayerSettings.GetScriptingDefineSymbols(target).Contains("ANDROID_PERFORMANCE_SUITE"),
            startSceneRestored = !SessionState.GetBool("PerformanceSuite.OwnsStartScene", false)
                && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == SessionState.GetString("PerformanceSuite.OldStartScene", ""),
            remoteDeviceRestored = EditorSettings.unityRemoteDevice == SessionState.GetString("PerformanceSuite.RemoteDevice", EditorSettings.unityRemoteDevice),
            scenarios = config == null ? 0 : config.scenarios.Count,
            configReady = config != null && config.maximumSamples == 200000 && !config.captureProfilerTrace
                && !config.forceCollectionAfterCleanup && !config.showPlayerOverlay,
            fixturesReady = enemy != null && enemy.GetComponent<Enemy>() != null
                && enemy.GetComponent<SimpleEnemyAttackExecutor>() != null && ship != null && ship.GetComponent<ParentShip>() != null,
            projectContexts = UnityEngine.Object.FindObjectsByType<Zenject.ProjectContext>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            analyticsState = Unity.Services.Core.UnityServices.State.ToString(), utc = DateTime.UtcNow.ToString("O")
        };
        result.suitePackagePersisted = result.applicationId.EndsWith(".performancesuite", StringComparison.Ordinal);
        result.passed = !result.compiling && !result.importing && !result.playing && !result.sceneDirty
            && !result.suiteWorldPresent && !result.testDefinesPersisted && !result.suitePackagePersisted
            && result.startSceneRestored && result.remoteDeviceRestored && result.configReady && result.fixturesReady
            && result.scenarios == 18 && result.scene == "Assets/Scenes/NewMainMenu.unity";
        File.WriteAllText(Path.GetFullPath("AgentArtifacts/Performance/final-editor-state.json"), JsonUtility.ToJson(result, true));
        return result;
    }
}
