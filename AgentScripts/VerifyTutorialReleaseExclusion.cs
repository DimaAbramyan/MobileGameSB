using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

public static class VerifyTutorialReleaseExclusion
{
    public static object Run()
    {
        var configuration = TutorialEditorSettings.Configuration;
        if (configuration == null) throw new Exception("Tutorial configuration missing");
        var source = CompilationPipeline.GetAssemblies(AssembliesType.Player).First(a => a.name == "Assembly-CSharp");
        var defines = source.defines.Where(d => !d.StartsWith("UNITY_EDITOR") && d != "INCLUDE_TUTORIAL" && d != "DEVELOPMENT_BUILD").ToArray();
        var response = new List<string> { "/nologo", "/target:library", "/langversion:latest", "/out:AgentArtifacts/TutorialOffIntegration.dll", "/define:" + string.Join(";", defines) };
        int absent = 0;
        foreach (string path in Directory.GetFiles("Assets/Scripts/Tutorial", "*.cs"))
        {
            response.Add("\"" + Path.GetFullPath(path) + "\""); absent++;
        }
        foreach (string path in new[]
        {
            "Assets/Scripts/Infrastructure/DependencyInjection/ProjectManagerInstaller.cs",
            "Assets/Scripts/Infrastructure/DependencyInjection/FightingInstaller.cs"
        })
        {
            response.Add("\"" + Path.GetFullPath(path) + "\"");
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            try
            {
                string path = assembly.Location;
                if (!string.IsNullOrEmpty(path) && paths.Add(path)) response.Add("/reference:\"" + path + "\"");
            }
            catch (NotSupportedException) { }
        }
        File.WriteAllLines("AgentArtifacts/tutorial-off.rsp", response);
        var roots = PlayerSettings.GetPreloadedAssets();
        string before = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        try
        {
            // Build API validation only: no scene/player launch and no replacement of unrelated defines/roots.
            configuration.includeTutorial = false;
            PlayerSettings.SetPreloadedAssets(roots.Where(asset => asset is not Game.Tutorial.TutorialConfiguration).ToArray());
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", before.Split(';').Where(d => d != "INCLUDE_TUTORIAL")));
            TutorialBuildValidation.Validate(BuildTarget.Android);
            string report = "AgentArtifacts/tutorial-release-exclusion.json";
            File.WriteAllText(report, JsonUtility.ToJson(new Verification
            {
                tutorialSourceFilesExcluded = absent,
                offIntegrationCompilation = false,
                androidReleaseConfiguration = true,
                tutorialPreloadedRootsWhenOff = 0,
                fullAndroidPlayerBuilt = false
            }, true));
            return new { success = true, sources = absent, responseFile = "AgentArtifacts/tutorial-off.rsp", androidOffConfiguration = true, report };
        }
        finally
        {
            configuration.includeTutorial = true;
            PlayerSettings.SetPreloadedAssets(roots);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, before);
        }
    }
    public static object VerifyAssembly()
    {
        var assembly = System.Reflection.Assembly.Load(File.ReadAllBytes("AgentArtifacts/TutorialOffIntegration.dll"));
        var types = assembly.GetTypes();
        if (types.Length != 2 || types.Any(t => t.Namespace == "Game.Tutorial")) throw new Exception("OFF assembly retains tutorial types.");
        string path = "AgentArtifacts/tutorial-release-exclusion.json";
        var report = JsonUtility.FromJson<Verification>(File.ReadAllText(path)); report.offIntegrationCompilation = true;
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return new { offIntegrationCompilation = true, types = types.Select(t => t.FullName).ToArray(), report = path };
    }
    [Serializable] private sealed class Verification
    {
        public int tutorialSourceFilesExcluded, tutorialPreloadedRootsWhenOff;
        public bool offIntegrationCompilation, androidReleaseConfiguration, fullAndroidPlayerBuilt;
    }
}
