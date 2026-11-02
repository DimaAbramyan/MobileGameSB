using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class VerifyPerformanceBuildReadiness
{
    public static object Run()
    {
        long free = new DriveInfo(Path.GetPathRoot(Application.dataPath)).AvailableFreeSpace;
        if (free >= 5L * 1024 * 1024 * 1024)
            return new { skipped = true, reason = "Storage is sufficient; do not invoke an actual build in this negative test." };
        var target = NamedBuildTarget.Android;
        string id = PlayerSettings.GetApplicationIdentifier(target);
        string defines = PlayerSettings.GetScriptingDefineSymbols(target);
        var backend = PlayerSettings.GetScriptingBackend(target);
        var architecture = PlayerSettings.Android.targetArchitectures;
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        bool timing = PlayerSettings.enableFrameTimingStats;
        bool blocked = false;
        try { PerformanceSuiteEditor.BuildAndroid(); }
        catch (IOException error) { blocked = error.Message.StartsWith("At least 5 GiB", StringComparison.Ordinal); }
        bool unchanged = id == PlayerSettings.GetApplicationIdentifier(target)
            && defines == PlayerSettings.GetScriptingDefineSymbols(target)
            && backend == PlayerSettings.GetScriptingBackend(target)
            && architecture == PlayerSettings.Android.targetArchitectures
            && bundle == EditorUserBuildSettings.buildAppBundle && timing == PlayerSettings.enableFrameTimingStats;
        return new { passed = blocked && unchanged && !BuildPipeline.isBuildingPlayer, blockedBeforeBuild = blocked, settingsUnchanged = unchanged, freeBytes = free };
    }
}
