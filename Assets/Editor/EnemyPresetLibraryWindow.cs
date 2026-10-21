using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyPresetLibraryWindow : EditorWindow
{
    [SerializeField] private int category;
    [SerializeField] private string search = "";
    [SerializeField] private string categoryFilter = "";
    [SerializeField] private EnemyReusablePreset selected;
    [SerializeField] private EnemyProjectilePreset previewProjectile;
    [SerializeField] private EnemyAttackPatternPreset previewPattern;
    [SerializeField] private EnemyBullet fallbackPrefab;
    [SerializeField] private Object applyTarget;
    [SerializeField] private string shootingPath, timingPath;
    [SerializeField] private int seed = 12345;
    private readonly List<EnemyReusablePreset> assets = new();
    private Editor assetEditor;
    private Vector2 listScroll, inspectorScroll;
    private EnemyPresetPreviewSession preview;
    private float time;
    private bool playing;
    private double lastTime;

    [MenuItem("Tools/Waves/Attack and Projectile Preset Library")]
    public static void OpenLibrary() => GetWindow<EnemyPresetLibraryWindow>("Attack Preset Library").Show();
    public static void ShowPreset(EnemyReusablePreset asset)
    {
        var window = GetWindow<EnemyPresetLibraryWindow>("Attack Preset Library");
        window.category = asset is EnemyProjectilePreset ? 0 : 1; window.Select(asset); window.Show();
    }
    public static void Open(SerializedProperty shooting, SerializedProperty timing, bool projectile)
    {
        var window = GetWindow<EnemyPresetLibraryWindow>("Attack Preset Library");
        window.applyTarget = shooting.serializedObject.targetObject; window.shootingPath = shooting.propertyPath;
        window.timingPath = timing?.propertyPath; window.category = projectile ? 0 : 1;
        window.RefreshAssets(); window.Show();
    }
    private void OnEnable()
    {
        minSize = new Vector2(680f, 420f); RefreshAssets();
        EditorApplication.projectChanged += RefreshAssets;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.playModeStateChanged += OnPlayState;
        SceneView.duringSceneGui += DrawPreview;
        if (selected != null) Select(selected);
    }
    private void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshAssets; EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop; EditorApplication.playModeStateChanged -= OnPlayState;
        SceneView.duringSceneGui -= DrawPreview; Stop();
        if (assetEditor != null) DestroyImmediate(assetEditor);
    }
    private void OnPlayState(PlayModeStateChange state) => Stop();
    private void RefreshAssets()
    {
        assets.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyProjectilePreset t:EnemyAttackPatternPreset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyReusablePreset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) assets.Add(asset);
        }
        assets.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase)); Repaint();
    }
    private void Select(EnemyReusablePreset asset)
    {
        Stop(); selected = asset;
        if (assetEditor != null) DestroyImmediate(assetEditor);
        assetEditor = asset != null ? Editor.CreateEditor(asset) : null;
    }
    private void OnGUI()
    {
        int next = GUILayout.Toolbar(category, new[] { "Projectile Presets", "Attack Pattern Presets" });
        if (next != category) { category = next; Select(null); }
        search = EditorGUILayout.TextField("Search name / tags", search);
        categoryFilter = EditorGUILayout.TextField("Category filter", categoryFilter);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                foreach (var asset in assets)
                {
                    if (asset == null || (asset is EnemyProjectilePreset) != (category == 0) || !Matches(asset)) continue;
                    if (GUILayout.Toggle(selected == asset, new GUIContent(asset.DisplayName, asset.Description), "Button"))
                        if (selected != asset) Select(asset);
                }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Create New")) Create();
            }
            using (new EditorGUILayout.VerticalScope())
            {
                if (selected == null) { EditorGUILayout.HelpBox("Select a preset. Preview runs in Scene View using the existing projectile simulation.", MessageType.Info); return; }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rename")) Rename();
                    if (GUILayout.Button("Duplicate")) Duplicate();
                    if (GUILayout.Button("Ping Asset")) EditorGUIUtility.PingObject(selected);
                }
                using (new EditorGUI.DisabledScope(applyTarget == null))
                {
                    if (GUILayout.Button("Apply to current attack")) ApplyToTarget();
                    if (applyTarget != null) EditorGUILayout.LabelField("Target: " + applyTarget.name, EditorStyles.miniLabel);
                }
                DrawPreviewControls();
                inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                if (assetEditor != null) assetEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }
    }
    private bool Matches(EnemyReusablePreset asset)
    {
        if (!string.IsNullOrWhiteSpace(categoryFilter) && (asset.Category ?? "").IndexOf(categoryFilter, StringComparison.OrdinalIgnoreCase) < 0) return false;
        string text = asset.DisplayName + " " + asset.name + " " + asset.Description + " " + asset.Category + " " + string.Join(" ", asset.Tags ?? Array.Empty<string>());
        return string.IsNullOrWhiteSpace(search) || text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    private void Create()
    {
        string path = EditorUtility.SaveFilePanelInProject("New preset", category == 0 ? "ProjectilePreset" : "AttackPatternPreset", "asset", "Choose asset location.");
        if (string.IsNullOrEmpty(path)) return;
        EnemyReusablePreset asset = category == 0 ? CreateInstance<EnemyProjectilePreset>() : CreateInstance<EnemyAttackPatternPreset>();
        AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "Create preset"); AssetDatabase.SaveAssetIfDirty(asset); RefreshAssets(); Select(asset);
    }
    private void Rename()
    {
        string old = AssetDatabase.GetAssetPath(selected);
        string path = EditorUtility.SaveFilePanelInProject("Rename preset", selected.name, "asset", "Choose new name in the same folder.", System.IO.Path.GetDirectoryName(old));
        if (string.IsNullOrEmpty(path) || path == old) return;
        // AssetDatabase preserves the GUID and all existing references.
        string error = AssetDatabase.MoveAsset(old, path);
        if (!string.IsNullOrEmpty(error)) Debug.LogWarning(error); RefreshAssets();
    }
    private void Duplicate()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(AssetDatabase.GetAssetPath(selected));
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(selected), path)) return;
        AssetDatabase.ImportAsset(path); Select(AssetDatabase.LoadAssetAtPath<EnemyReusablePreset>(path)); RefreshAssets();
    }
    private void ApplyToTarget()
    {
        var serialized = new SerializedObject(applyTarget);
        var shooting = serialized.FindProperty(shootingPath); var timing = string.IsNullOrEmpty(timingPath) ? null : serialized.FindProperty(timingPath);
        if (shooting == null) return;
        shooting.FindPropertyRelative(category == 0 ? "projectilePreset" : "attackPatternPreset").objectReferenceValue = selected;
        EnemyPresetInspector.Apply(shooting, timing, category == 0);
    }
    private void DrawPreviewControls()
    {
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Scene View Preview", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        if (category == 0) previewPattern = (EnemyAttackPatternPreset)EditorGUILayout.ObjectField("Preview Attack Pattern", previewPattern, typeof(EnemyAttackPatternPreset), false);
        else previewProjectile = (EnemyProjectilePreset)EditorGUILayout.ObjectField("Preview Projectile", previewProjectile, typeof(EnemyProjectilePreset), false);
        fallbackPrefab = (EnemyBullet)EditorGUILayout.ObjectField("Fallback Projectile Prefab", fallbackPrefab, typeof(EnemyBullet), false);
        seed = EditorGUILayout.IntField("Seed", seed);
        if (EditorGUI.EndChangeCheck()) Restart();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "Pause" : "Play")) { if (preview == null) Restart(); playing = !playing; lastTime = EditorApplication.timeSinceStartup; }
            if (GUILayout.Button("Repeat Same Seed")) Restart();
            if (GUILayout.Button("New Seed")) { seed = unchecked((int)DateTime.UtcNow.Ticks); Restart(); }
            if (GUILayout.Button("Stop / Clear")) Stop();
            if (GUILayout.Button("Focus")) { var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>(); view.LookAt(new Vector3(0f, 0f, 0f), Quaternion.identity, 10f, true); }
        }
        EditorGUILayout.LabelField($"Time: {time:0.00} s   Projectiles: {preview?.Count ?? 0}   Omitted: {preview?.Omitted ?? 0}", EditorStyles.miniLabel);
        if (preview != null && preview.Warning != null) EditorGUILayout.HelpBox(preview.Warning, MessageType.Warning);
    }
    private void Restart()
    {
        Stop(); if (selected == null || Application.isPlaying) return;
        var pattern = category == 1 ? selected as EnemyAttackPatternPreset : previewPattern;
        var projectile = category == 0 ? selected as EnemyProjectilePreset : previewProjectile ?? pattern?.SuggestedProjectile;
        if (pattern != null && EnemyPresetDependencies.HasCycle(pattern) || projectile != null && EnemyPresetDependencies.HasCycle(projectile)) return;
        preview = new EnemyPresetPreviewSession(pattern, projectile, fallbackPrefab, seed);
        preview.Advance(0f); SceneView.RepaintAll();
    }
    private void Stop() { playing = false; time = 0f; preview?.Dispose(); preview = null; SceneView.RepaintAll(); }
    private void Tick()
    {
        if (preview != null && !preview.IsCurrent) { bool resume = playing; Restart(); playing = resume; }
        if (!playing || preview == null) return;
        double now = EditorApplication.timeSinceStartup;
        time += Mathf.Min(.1f, (float)(now - lastTime)); lastTime = now;
        preview.Advance(time); Repaint(); SceneView.RepaintAll();
    }
    private void DrawPreview(SceneView view)
    {
        if (preview == null || Application.isPlaying) return;
        preview.Draw(view.camera);
        Handles.color = Color.yellow; Handles.DrawWireDisc(new Vector3(0f, -3f), Vector3.forward, .075f);
        Handles.Label(new Vector3(.2f, -3f), "Preview target");
    }
}
