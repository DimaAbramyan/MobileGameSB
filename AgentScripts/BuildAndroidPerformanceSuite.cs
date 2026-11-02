using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildAndroidPerformanceSuite
{
    [Serializable] private sealed class State { public string status, output, error, utc; }
    [Serializable] private sealed class Settings
    {
        public string applicationId, backend, architecture, defines, scene, nativePluginFlags;
        public bool bundle, frameTiming, sceneDirty, development;
        public static Settings Capture()
        {
            var target = NamedBuildTarget.Android;
            var scene = SceneManager.GetActiveScene();
            var plugins = new List<string>();
            foreach (var importer in PluginImporter.GetAllImporters())
                if (importer.assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                    && importer.assetPath.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                    plugins.Add(importer.assetPath + "=" + importer.GetCompatibleWithPlatform(BuildTarget.Android));
            plugins.Sort(StringComparer.Ordinal);
            return new Settings { applicationId = PlayerSettings.GetApplicationIdentifier(target),
                backend = PlayerSettings.GetScriptingBackend(target).ToString(), architecture = PlayerSettings.Android.targetArchitectures.ToString(),
                defines = PlayerSettings.GetScriptingDefineSymbols(target), bundle = EditorUserBuildSettings.buildAppBundle,
                frameTiming = PlayerSettings.enableFrameTimingStats, scene = scene.path, sceneDirty = scene.isDirty,
                development = EditorUserBuildSettings.development, nativePluginFlags = string.Join("\n", plugins) };
        }
        public bool SameAs(Settings other) => applicationId == other.applicationId && backend == other.backend
            && architecture == other.architecture && defines == other.defines && scene == other.scene
            && bundle == other.bundle && frameTiming == other.frameTiming && sceneDirty == other.sceneDirty
            && development == other.development && nativePluginFlags == other.nativePluginFlags;
    }
    [Serializable] private sealed class SettingsCheck { public bool passed; public Settings before, after; }
    public static object Queue()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Idle Editor required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes manually before scheduling the suite APK build.");
        string file = Path.GetFullPath("AgentArtifacts/Performance/Build/build-status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        Write(file, "queued", null, null);
        EditorApplication.CallbackFunction callback = null;
        callback = () =>
        {
            EditorApplication.update -= callback;
            Write(file, "building", null, null);
            var before = Settings.Capture();
            try { Write(file, "completed", PerformanceSuiteEditor.BuildAndroid(), null); }
            catch (Exception error)
            {
                string safe = error.Message.Split(new[] { "Environment Variables:" }, StringSplitOptions.None)[0];
                Write(file, "failed", null, safe.Length > 2000 ? safe.Substring(0, 2000) : safe);
            }
            finally
            {
                var after = Settings.Capture();
                var check = new SettingsCheck { before = before, after = after, passed = before.SameAs(after) };
                File.WriteAllText(Path.GetFullPath("AgentArtifacts/Performance/build-settings-check.json"),
                    "{\"passed\":" + (check.passed ? "true" : "false")
                    + ",\"before\":" + JsonUtility.ToJson(before) + ",\"after\":" + JsonUtility.ToJson(after) + "}");
            }
        };
        EditorApplication.update += callback;
        return new { queued = true, statusFile = file };
    }
    private static void Write(string file, string status, string output, string error)
        => File.WriteAllText(file, JsonUtility.ToJson(new State { status = status, output = output, error = error, utc = DateTime.UtcNow.ToString("O") }, true));
}
