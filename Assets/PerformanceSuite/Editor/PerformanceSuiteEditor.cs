using System;
using System.IO;
using Game.Performance;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class PerformanceSuiteWindow : EditorWindow
{
    private PerformanceSuiteConfig config;
    private int selected;
    private float seconds;
    private int repetitions;
    private bool soak;
    private Vector2 scroll;
    [MenuItem("Tools/Performance/Android Performance Suite")]
    public static void Open() => GetWindow<PerformanceSuiteWindow>("Performance Suite");
    private void OnEnable() => config = AssetDatabase.LoadAssetAtPath<PerformanceSuiteConfig>(PerformanceSuiteEditor.ConfigPath);
    private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Isolated real runtime systems. Editor results are not Android benchmarks. Use the dedicated suite scene; no player profile or Analytics is initialized.", MessageType.Info);
        if (GUILayout.Button("Create / locate isolated fixtures")) { PerformanceSuiteEditor.CreateFixtures(); OnEnable(); }
        config = (PerformanceSuiteConfig)EditorGUILayout.ObjectField("Configuration", config, typeof(PerformanceSuiteConfig), false);
        if (config != null)
        {
            if (GUILayout.Button("Select configuration in Inspector")) Selection.activeObject = config;
            var choices = new string[config.scenarios.Count + 1]; choices[0] = "All enabled (soak optional)";
            for (int i = 0; i < config.scenarios.Count; i++) choices[i + 1] = config.scenarios[i].id;
            selected = EditorGUILayout.Popup("Scenario", Mathf.Clamp(selected, 0, choices.Length - 1), choices);
            seconds = EditorGUILayout.FloatField("Duration override (0 = config)", seconds);
            repetitions = EditorGUILayout.IntField("Repeats override (0 = config)", repetitions);
            soak = EditorGUILayout.Toggle("Include long soak in Run All", soak);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Run selected / all")) PerformanceSuiteEditor.Run(selected == 0 ? "" : choices[selected], seconds, repetitions, soak);
        }
        if (EditorApplication.isPlaying)
        {
            var runner = Object.FindFirstObjectByType<PerformanceSuiteRunner>();
            if (runner != null)
            {
                EditorGUILayout.LabelField("Phase", runner.Phase);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), runner.Progress, "Suite progress");
                if (GUILayout.Button("Cancel and clean up")) runner.Cancel();
                EditorGUILayout.SelectableLabel(runner.LastOutput ?? "", GUILayout.Height(35));
            }
        }
        if (GUILayout.Button("Open result directory")) { Directory.CreateDirectory(PerformanceReport.OutputRoot); EditorUtility.RevealInFinder(PerformanceReport.OutputRoot); }
        if (GUILayout.Button("Compare two results.json files"))
        {
            string before = EditorUtility.OpenFilePanel("Baseline results.json", PerformanceReport.OutputRoot, "json");
            string after = before.Length == 0 ? "" : EditorUtility.OpenFilePanel("Candidate results.json", PerformanceReport.OutputRoot, "json");
            if (after.Length > 0) { string output = PerformanceSuiteEditor.Compare(before, after); EditorUtility.RevealInFinder(output); }
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Build isolated Android IL2CPP APK")) PerformanceSuiteEditor.BuildAndroid();
        EditorGUILayout.EndScrollView();
    }
}

[InitializeOnLoad]
public static class PerformanceSuiteEditor
{
    public const string Root = "Assets/PerformanceSuite/Fixtures";
    public const string ConfigPath = Root + "/AndroidPerformanceSuite.asset";
    public const string ScenePath = Root + "/AndroidPerformanceSuite.unity";
    private const string Pending = "PerformanceSuite.Pending";
    static PerformanceSuiteEditor() => EditorApplication.playModeStateChanged += OnPlayState;

    [MenuItem("Tools/Performance/Create isolated suite fixtures")]
    public static void CreateFixtures()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var existing = AssetDatabase.LoadAssetAtPath<PerformanceSuiteConfig>(ConfigPath);
        if (existing != null && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var config = existing != null ? existing : ScriptableObject.CreateInstance<PerformanceSuiteConfig>();
        config.bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab")?.GetComponent<EnemyBullet>();
        if (config.bullet == null) throw new InvalidOperationException("Existing configured Bullet fixture not found.");
        var sprite = config.bullet.GetComponent<EnemyProjectileAuthoring>().VisualRenderer.sprite;
        if (existing == null)
        {
            var homing = Object.Instantiate(config.bullet.gameObject); homing.name = "Performance Homing";
            try
            {
                Set(homing.GetComponent<EnemyProjectileAuthoring>(), "flightMode", 1);
                config.homingBullet = PrefabUtility.SaveAsPrefabAsset(homing, Root + "/Homing.prefab").GetComponent<EnemyBullet>();
            }
            finally { Object.DestroyImmediate(homing); }
            var enemy = new GameObject("Performance Enemy"); enemy.SetActive(false);
            try
            {
                enemy.AddComponent<SpriteRenderer>().sprite = sprite;
                enemy.GetComponent<SpriteRenderer>().color = Color.cyan; enemy.transform.localScale = Vector3.one * .3f;
                enemy.AddComponent<CircleCollider2D>().isTrigger = true;
                var unit = enemy.AddComponent<Enemy>(); Set(unit, "_maxHealth", 30f); Set(unit, "shieldPoints", 4f);
                var executor = enemy.AddComponent<SimpleEnemyAttackExecutor>();
                Set(executor, "defaultProjectilePrefab", config.bullet); Set(executor, "homingProjectilePrefab", config.homingBullet);
                Set(executor, "attackModeVersion", 2); Set(executor, "emissionBindingVersion", 1);
                JsonUtility.FromJsonOverwrite("{\"attackCooldown\":0.5,\"shooting\":{\"emissionLayoutVersion\":1,\"emissionLayout\":{\"type\":1,\"directionsCount\":8},\"overrideProjectileLifetime\":true,\"projectileLifetime\":2}}", executor.BurstAttackSettings);
                enemy.SetActive(true);
                config.enemy = PrefabUtility.SaveAsPrefabAsset(enemy, Root + "/Enemy.prefab").GetComponent<Enemy>();
            }
            finally { Object.DestroyImmediate(enemy); }
            var data = ScriptableObject.CreateInstance<ShipData>();
            data.maximumHealthPoints = 8; data.maximumShieldPoints = 4; data.mass = 1; data.speed = 5;
            AssetDatabase.CreateAsset(data, Root + "/ShipData.asset");
            var ship = new GameObject("Performance Ship"); ship.SetActive(false);
            try
            {
                ship.AddComponent<SpriteRenderer>().sprite = sprite;
                ship.AddComponent<CircleCollider2D>().radius = .05f;
                var parent = ship.AddComponent<ParentShip>(); parent.ShipData = data;
                ship.AddComponent<WeaponController>();
                var ability = ship.AddComponent<ShieldCreateAbility>(); Set(parent, "activeAbility", ability);
                Set(parent, "healthDamageProjectileClearRadius", 0f);
                ship.SetActive(true);
                config.ship = PrefabUtility.SaveAsPrefabAsset(ship, Root + "/Ship.prefab").GetComponent<ParentShip>();
            }
            finally { Object.DestroyImmediate(ship); }
            AddCases(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cameraObject = new GameObject("Performance Camera");
                var camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera";
                camera.orthographic = true; camera.orthographicSize = 6; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; cameraObject.transform.position = new Vector3(0, 0, -10);
                var host = new GameObject("Android Performance Suite"); host.AddComponent<PerformanceSuiteRunner>().configuration = config;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save suite scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
        }
        AssetDatabase.SaveAssetIfDirty(config);
    }
    private static void AddCases(PerformanceSuiteConfig c)
    {
        foreach (int count in new[] { 500, 1000, 2000, 5000 }) Add(c, "ecs_" + count, PerformanceWorkload.Projectiles, count);
        Add(c, "ecs_churn_2000", PerformanceWorkload.Churn, 2000).lifetime = .7f;
        Add(c, "ecs_collision_1000", PerformanceWorkload.Collision, 1000);
        Add(c, "homing_1000", PerformanceWorkload.Homing, 1000);
        Add(c, "angular_2000", PerformanceWorkload.Angular, 2000);
        Add(c, "orbit_2000", PerformanceWorkload.Orbit, 2000);
        Add(c, "child_single", PerformanceWorkload.Child, 300);
        Add(c, "child_three_generations", PerformanceWorkload.Child, 300).childGenerations = 3;
        Add(c, "laser_telegraph_32", PerformanceWorkload.Laser, 32);
        foreach (int count in new[] { 16, 48, 96 }) Add(c, "waves_" + count, PerformanceWorkload.Waves, 500).enemies = count;
        Add(c, "coordinator_groups", PerformanceWorkload.Waves, 500).coordinator = true;
        Add(c, "team_combat", PerformanceWorkload.Combat, 500).coordinator = true;
        var soak = Add(c, "soak_world_reuse", PerformanceWorkload.Soak, 2000);
        soak.coordinator = true; soak.measurementSeconds = 300; soak.warmupSeconds = 10;
    }
    private static PerformanceCase Add(PerformanceSuiteConfig c, string id, PerformanceWorkload type, int count)
    {
        var value = new PerformanceCase { id = id, workload = type, projectiles = count };
        c.scenarios.Add(value); return value;
    }
    private static void Set(Object target, string property, object value)
    {
        var serialized = new SerializedObject(target);
        var p = serialized.FindProperty(property) ?? throw new InvalidOperationException(target.GetType().Name + "." + property);
        if (value is Object reference) p.objectReferenceValue = reference;
        else if (value is int integer) p.intValue = integer;
        else if (value is float scalar) p.floatValue = scalar;
        else throw new ArgumentException("Unsupported fixture value.");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void Run(string scenarioId = "", float seconds = 0, int repetitions = 0, bool includeSoak = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Already in Play Mode.");
        CreateFixtures();
        SessionState.SetString("PerformanceSuite.OldStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("PerformanceSuite.OwnsStartScene", true);
        SessionState.SetString("PerformanceSuite.RemoteDevice", EditorSettings.unityRemoteDevice);
        EditorSettings.unityRemoteDevice = "None";
        SessionState.SetString("PerformanceSuite.Scenario", scenarioId);
        SessionState.SetFloat("PerformanceSuite.Seconds", seconds); SessionState.SetInt("PerformanceSuite.Repeats", repetitions);
        SessionState.SetBool("PerformanceSuite.Soak", includeSoak); SessionState.SetBool(Pending, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying = true;
    }
    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            try
            {
                var runner = Object.FindFirstObjectByType<PerformanceSuiteRunner>();
                if (runner == null) throw new InvalidOperationException("Suite scene host missing.");
                runner.StartSuite(SessionState.GetString("PerformanceSuite.Scenario", "") is { Length: > 0 } name ? name : null,
                    SessionState.GetFloat("PerformanceSuite.Seconds", 0), SessionState.GetInt("PerformanceSuite.Repeats", 0), SessionState.GetBool("PerformanceSuite.Soak", false));
            }
            catch (Exception error) { Debug.LogException(error); }
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("PerformanceSuite.OwnsStartScene", false))
        {
            SessionState.SetBool("PerformanceSuite.OwnsStartScene", false);
            EditorSettings.unityRemoteDevice = SessionState.GetString("PerformanceSuite.RemoteDevice", "None");
            string path = SessionState.GetString("PerformanceSuite.OldStartScene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
    public static string Compare(string before, string after)
    {
        string md = PerformanceReport.Compare(JsonUtility.FromJson<PerformanceRun>(File.ReadAllText(before)), JsonUtility.FromJson<PerformanceRun>(File.ReadAllText(after)));
        string output = Path.Combine(PerformanceReport.OutputRoot, "comparison-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".md");
        Directory.CreateDirectory(PerformanceReport.OutputRoot); File.WriteAllText(output, md); return output;
    }
    public static string BuildAndroid()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes manually before building the performance APK. The suite does not save unrelated scenes.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) throw new InvalidOperationException("Android Build Support is not installed.");
        // IL2CPP writes intermediate code and symbols next to the project, even when the APK is exported elsewhere.
        var projectDrive = new DriveInfo(Path.GetPathRoot(Application.dataPath));
        if (projectDrive.AvailableFreeSpace < 5L * 1024 * 1024 * 1024)
            throw new IOException("At least 5 GiB of free space on the project drive is required before building the performance APK; cold builds may need more. No build settings were changed.");
        CreateFixtures();
        var target = NamedBuildTarget.Android;
        string originalId = PlayerSettings.GetApplicationIdentifier(target);
        var backend = PlayerSettings.GetScriptingBackend(target);
        var architecture = PlayerSettings.Android.targetArchitectures;
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        bool development = EditorUserBuildSettings.development;
        bool frameTiming = PlayerSettings.enableFrameTimingStats;
        var pluginStates = new System.Collections.Generic.List<(string path, bool enabled)>();
        foreach (var importer in PluginImporter.GetAllImporters())
            if (importer.assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                && importer.assetPath.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                pluginStates.Add((importer.assetPath, importer.GetCompatibleWithPlatform(BuildTarget.Android)));
        string output = Path.GetFullPath("AgentArtifacts/Performance/Build/AndroidPerformanceSuite.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        try
        {
            PlayerSettings.SetApplicationIdentifier(target, originalId + ".performancesuite");
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false; PlayerSettings.enableFrameTimingStats = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.Android, options = BuildOptions.Development | BuildOptions.DetailedBuildReport,
                extraScriptingDefines = new[] { "ANDROID_PERFORMANCE_SUITE" } });
            ValidateBuildResult(report.summary.result, report.summary.totalErrors);
            File.WriteAllText(Path.ChangeExtension(output, ".package.txt"), originalId + ".performancesuite");
            return output;
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(target, originalId); PlayerSettings.SetScriptingBackend(target, backend);
            PlayerSettings.Android.targetArchitectures = architecture; EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.development = development;
            PlayerSettings.enableFrameTimingStats = frameTiming;
            // Some preprocessors switch native libraries, and Unity need not call their postprocessor after cancellation.
            foreach (var state in pluginStates)
            {
                var importer = AssetImporter.GetAtPath(state.path) as PluginImporter;
                if (importer == null || importer.GetCompatibleWithPlatform(BuildTarget.Android) == state.enabled) continue;
                importer.SetCompatibleWithPlatform(BuildTarget.Android, state.enabled);
                importer.SaveAndReimport();
            }
        }
    }

    internal static void ValidateBuildResult(BuildResult result, int totalErrors)
    {
        if (result != BuildResult.Succeeded || totalErrors != 0)
            throw new InvalidOperationException($"Suite APK build is not clean: {result}, {totalErrors} error(s). Inspect the BuildReport and Editor.log.");
    }
}
