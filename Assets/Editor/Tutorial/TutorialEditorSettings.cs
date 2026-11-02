using System;
using System.Collections.Generic;
using Game.Tutorial;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using Zenject;

[InitializeOnLoad]
public static class TutorialEditorSettings
{
    public const string Symbol = "INCLUDE_TUTORIAL";
    static TutorialEditorSettings() => Undo.undoRedoPerformed += SynchronizeAfterUndo;
    private static void SynchronizeAfterUndo()
    {
        var config = Configuration;
        if (config == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool android = Array.IndexOf(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';'), Symbol) >= 0;
        if (android != config.includeTutorial) Synchronize(config);
    }
    public static TutorialConfiguration Configuration
    {
        get { EditorBuildSettings.TryGetConfigObject(TutorialConfiguration.EditorConfigKey, out TutorialConfiguration config); return config; }
    }

    [SettingsProvider]
    public static SettingsProvider Provider() => new("Project/Game/Tutorial", SettingsScope.Project)
    {
        label = "Tutorial",
        keywords = new HashSet<string> { "Tutorial", "Onboarding", "Include Tutorial" },
        guiHandler = _ =>
        {
            var config = Configuration;
            var selected = (TutorialConfiguration)EditorGUILayout.ObjectField("Configuration", config, typeof(TutorialConfiguration), false);
            if (selected != config && selected != null)
            {
                EditorBuildSettings.AddConfigObject(TutorialConfiguration.EditorConfigKey, selected, true);
                Synchronize(selected); config = selected;
            }
            if (config == null)
            {
                EditorGUILayout.HelpBox("Create the editable demo, then use Include Tutorial to control Editor and Android builds.", MessageType.Info);
                if (GUILayout.Button("Create Tutorial Demo")) CreateDemo();
                return;
            }
            DrawConfiguration(config);
        }
    };

    public static void DrawConfiguration(TutorialConfiguration config)
    {
        var serialized = new SerializedObject(config);
        serialized.Update();
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
        EditorGUI.BeginChangeCheck();
        var iterator = serialized.GetIterator(); iterator.NextVisible(true);
        while (iterator.NextVisible(false)) EditorGUILayout.PropertyField(iterator, true);
        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Synchronize(config);
        }
        EditorGUI.EndDisabledGroup();
        if (config.definition == null) EditorGUILayout.HelpBox("Assign a Tutorial Definition.", MessageType.Warning);
        else if (!config.definition.TryValidate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        EditorGUILayout.HelpBox("OFF removes the Tutorial define and preloaded asset root for Android and the active build target. Development force/replay never grants release permission. No waves or scenes are modified.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Synchronize Build Configuration")) Synchronize(config);
        DrawDevelopmentControls();
    }

    public static void Synchronize(TutorialConfiguration config)
    {
        // Preserve all unrelated symbols and preloaded assets.
        SetSymbol(NamedBuildTarget.Android, config.includeTutorial);
        var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        if (group != BuildTargetGroup.Unknown) SetSymbol(NamedBuildTarget.FromBuildTargetGroup(group), config.includeTutorial);
        // Profile defines are additive. Remove stale per-profile copies so one OFF switch really wins.
        if (!config.includeTutorial)
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null || Array.IndexOf(profile.scriptingDefines, Symbol) < 0) continue;
                Undo.RecordObject(profile, "Disable Tutorial in Build Profile");
                var profileSymbols = new List<string>(profile.scriptingDefines); profileSymbols.RemoveAll(value => value == Symbol);
                profile.scriptingDefines = profileSymbols.ToArray(); EditorUtility.SetDirty(profile);
            }
        var roots = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets());
        roots.RemoveAll(asset => asset is TutorialConfiguration);
        if (config.includeTutorial) roots.Add(config);
        PlayerSettings.SetPreloadedAssets(roots.ToArray());
        AssetDatabase.SaveAssets();
    }
    private static void SetSymbol(NamedBuildTarget target, bool enabled)
    {
        var symbols = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';'));
        symbols.RemoveAll(value => value == Symbol || string.IsNullOrEmpty(value));
        if (enabled) symbols.Add(Symbol);
        string result = string.Join(";", symbols);
        if (result != PlayerSettings.GetScriptingDefineSymbols(target)) PlayerSettings.SetScriptingDefineSymbols(target, result);
    }

    public static void DrawDevelopmentControls()
    {
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Development Testing", EditorStyles.boldLabel);
        ITutorialService service = null;
        if (EditorApplication.isPlaying && ProjectContext.HasInstance) service = ProjectContext.Instance.Container.TryResolve<ITutorialService>();
        using (new EditorGUI.DisabledScope(service == null))
        {
            if (GUILayout.Button("Replay Tutorial (does not change saved progress)")) { service.Stop(); service.TryStart(true); }
            if (GUILayout.Button("Stop Tutorial")) service.Stop();
            if (GUILayout.Button("Reset Saved Tutorial Progress") && EditorUtility.DisplayDialog("Reset Tutorial Progress",
                "Only onboarding status and completed tutorial steps will be reset. Other saves are preserved.", "Reset", "Cancel")) service.ResetProgressForDevelopment();
            if (GUILayout.Button("Launch Configured Tutorial Level")) Launch(service);
        }
        if (service == null) EditorGUILayout.HelpBox("Play Mode with Include Tutorial enabled is required for testing. Replay respects Allow Development Launch.", MessageType.None);
    }
    private static async void Launch(ITutorialService service)
    {
        try { if (!await service.LaunchConfiguredLevelAsync()) Debug.LogWarning("Tutorial Level was not launched. Assign a LevelConfig and select a complete team first."); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static TutorialConfiguration CreateDemo()
    {
        if (Configuration != null) return Configuration;
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial")) AssetDatabase.CreateFolder("Assets", "Tutorial");
        var definition = ScriptableObject.CreateInstance<TutorialDefinition>();
        definition.steps = new List<TutorialStep>
        {
            Step("welcome", "tutorial.welcome"),
            Step("movement_explanation", "tutorial.movement", TutorialConditionType.GameplayReady),
            Step("move", "tutorial.move", TutorialConditionType.GameplayReady, TutorialConditionType.PlayerMoved, .5f),
            Step("switch_explanation", "tutorial.switch_explanation", TutorialConditionType.TwoShipsReady),
            Step("switch", "tutorial.switch", TutorialConditionType.TwoShipsReady, TutorialConditionType.ShipSwitched),
            Step("finish", "tutorial.finish")
        };
        definition.steps[2].target = TutorialTarget.ActiveShip; definition.steps[2].highlightTarget = true;
        definition.steps[4].target = TutorialTarget.ActiveShip; definition.steps[4].showPointer = true;
        definition.steps[2].canSkip = definition.steps[4].canSkip = false;
        string definitionPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Tutorial/FirstLaunchTutorial.asset");
        AssetDatabase.CreateAsset(definition, definitionPath);
        var config = ScriptableObject.CreateInstance<TutorialConfiguration>(); config.definition = definition;
        AssetDatabase.CreateAsset(config, AssetDatabase.GenerateUniqueAssetPath("Assets/Tutorial/TutorialConfiguration.asset"));
        AddDemoStrings();
        EditorBuildSettings.AddConfigObject(TutorialConfiguration.EditorConfigKey, config, true);
        EditorUtility.SetDirty(definition); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        Synchronize(config); return config;
    }
    private static TutorialStep Step(string id, string key, TutorialConditionType start = TutorialConditionType.Always,
        TutorialConditionType completion = TutorialConditionType.ManualConfirmation, float value = 1f) => new()
    {
        id = id, hint = new LocalizedString("UI", key),
        start = new() { conditions = new() { new() { type = start } } },
        completion = new() { conditions = new() { new() { type = completion, value = value } } }
    };
    private static void AddDemoStrings()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
        if (collection == null) throw new InvalidOperationException("Existing UI String Table Collection is required.");
        var english = collection.GetTable("en") as StringTable;
        if (english == null) throw new InvalidOperationException("Existing English UI String Table is required.");
        var strings = new Dictionary<string, string>
        {
            ["tutorial.welcome"] = "Welcome! This short guide uses your real ships. Continue, then launch Classic Mode when you are ready.",
            ["tutorial.movement"] = "Use your selected movement controls to move the ship around the battlefield.",
            ["tutorial.move"] = "Move your ship to continue.",
            ["tutorial.switch_explanation"] = "Switch to your second ship using your selected ship-switch controls.",
            ["tutorial.switch"] = "Switch ships once to continue.",
            ["tutorial.finish"] = "You are ready! Continue to complete the guide and save your progress.",
            ["tutorial.continue"] = "Continue", ["tutorial.skip"] = "Skip Tutorial", ["tutorial.skip_step"] = "Skip Step"
        };
        Undo.RecordObject(english, "Add Tutorial Demo Strings"); Undo.RecordObject(collection.SharedData, "Add Tutorial Demo Keys");
        foreach (var pair in strings) if (english.GetEntry(pair.Key) == null) english.AddEntry(pair.Key, pair.Value);
        EditorUtility.SetDirty(english); EditorUtility.SetDirty(collection.SharedData);
    }
}

// Build-time guard: mismatched defines or asset roots must fail clearly rather than create a broken player.
public sealed class TutorialBuildValidation : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => 100;
    public void OnPreprocessBuild(BuildReport report)
    {
        Validate(report.summary.platform);
    }
    public static void Validate(BuildTarget target)
    {
        var config = TutorialEditorSettings.Configuration;
        bool included = config != null && config.includeTutorial;
        var named = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
        bool symbol = Array.IndexOf(PlayerSettings.GetScriptingDefineSymbols(named).Split(';'), TutorialEditorSettings.Symbol) >= 0;
        var activeProfile = BuildProfile.GetActiveBuildProfile();
        if (!included && activeProfile != null && Array.IndexOf(activeProfile.scriptingDefines, TutorialEditorSettings.Symbol) >= 0)
            throw new BuildFailedException("Active Build Profile retains INCLUDE_TUTORIAL. Synchronize Tutorial configuration.");
        bool root = false;
        foreach (var asset in PlayerSettings.GetPreloadedAssets())
            if (asset is TutorialConfiguration c)
            {
                if (!included || c != config) throw new BuildFailedException("Remove stale Tutorial roots using Project Settings > Game > Tutorial > Synchronize.");
                root = true;
            }
        if (symbol != included || included && !root)
            throw new BuildFailedException("Tutorial symbols/preloaded assets differ from Include Tutorial. Synchronize Tutorial build configuration.");
        if (included && (config.definition == null || !config.definition.TryValidate(out _)))
            throw new BuildFailedException("Tutorial is enabled but its Definition is invalid.");
        // Resources/Addressables would bypass the optional preloaded root. Keep Tutorial data outside them.
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialConfiguration t:TutorialDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Resources/")) throw new BuildFailedException($"Move optional Tutorial assets out of Resources: {path}");
            if (AddressableAssetSettingsDefaultObject.Settings != null && AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid) != null)
                throw new BuildFailedException($"Optional Tutorial assets must not be independent Addressables build roots: {path}");
        }
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || TutorialEditorSettings.Configuration?.includeTutorial == true) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var anchor in root.GetComponentsInChildren<TutorialAnchor>(true)) UnityEngine.Object.DestroyImmediate(anchor);
    }
}
