using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CommandLineBuild
{
    private const string BuildPathArgument = "-buildPath";
    private const string DefaultAndroidBuildPath = "Builds/Android/My project.apk";

    public static void BuildAndroid()
    {
        string buildPath = GetArgumentValue(BuildPathArgument, DefaultAndroidBuildPath);
        string fullBuildPath = Path.GetFullPath(buildPath);
        string directory = Path.GetDirectoryName(fullBuildPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string[] scenes = GetEnabledScenes();

        if (scenes.Length == 0)
        {
            Debug.LogError("Build failed: no enabled scenes in EditorBuildSettings.");
            EditorApplication.Exit(1);
            return;
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullBuildPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {fullBuildPath}. Size: {summary.totalSize} bytes.");
            EditorApplication.Exit(0);
            return;
        }

        Debug.LogError($"Build failed: {summary.result}. Errors: {summary.totalErrors}, warnings: {summary.totalWarnings}.");
        EditorApplication.Exit(1);
    }

    private static string[] GetEnabledScenes()
    {
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        int enabledCount = 0;

        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled)
                enabledCount++;
        }

        string[] scenes = new string[enabledCount];
        int sceneIndex = 0;

        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (!buildScenes[i].enabled)
                continue;

            scenes[sceneIndex] = buildScenes[i].path;
            sceneIndex++;
        }

        return scenes;
    }

    private static string GetArgumentValue(string argumentName, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argumentName)
                return args[i + 1];
        }

        return defaultValue;
    }
}
