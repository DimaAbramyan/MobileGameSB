using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class WaveSceneVisualization
{
    private const string BoundsKey = "WaveSceneVisualization.CameraBounds";
    private const string EnemiesKey = "WaveSceneVisualization.AllEnemies";
    private const string WaveKey = "WaveSceneVisualization.Source";
    private static Wave source;
    private static bool dirty = true;
    private static double nextCheck;
    private static Rect lastBounds;
    private static Camera lastCamera;
    private static readonly Vector3[] corners = new Vector3[4];
    private static readonly List<Pose> formation = new();
    private static readonly Dictionary<Enemy, SpriteRenderer[]> renderers = new();
    private static readonly List<(Object Value,int Dirty)> dependencies = new();
    private struct Pose { public DirectedEnemySubWave Wave; public Enemy Prefab; public Matrix4x4 Matrix; }
    internal static Wave Source => source;
    internal static bool ShowCameraBounds
    {
        get => SessionState.GetBool(BoundsKey,false);
        set { SessionState.SetBool(BoundsKey,value); SceneView.RepaintAll(); }
    }
    internal static bool ShowAllEnemySilhouettes
    {
        get => SessionState.GetBool(EnemiesKey,false);
        set { SessionState.SetBool(EnemiesKey,value); SceneView.RepaintAll(); }
    }
    static WaveSceneVisualization()
    {
        SceneView.duringSceneGui += Draw;
        Selection.selectionChanged += SelectionChanged;
        EditorApplication.update += Tick;
        EditorApplication.hierarchyChanged += Invalidate;
        EditorApplication.projectChanged += InvalidateSprites;
        Undo.undoRedoPerformed += Invalidate;
        AssemblyReloadEvents.beforeAssemblyReload += Release;
        EditorApplication.quitting += Release;
        EditorApplication.delayCall += SelectionChanged;
    }
    internal static void Bind(Wave wave)
    {
        if (wave == null || source == wave) return;
        source = wave;
        SessionState.SetString(WaveKey, GlobalObjectId.GetGlobalObjectIdSlow(wave).ToString());
        Invalidate();
    }
    private static void SelectionChanged()
    {
        var selected = Selection.activeGameObject;
        var wave = selected != null ? selected.GetComponentInParent<Wave>() : null;
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (wave == null && stage != null) wave = stage.prefabContentsRoot.GetComponent<Wave>();
        if (wave != null) Bind(wave);
        else if (source == null && GlobalObjectId.TryParse(SessionState.GetString(WaveKey,""),out var id))
            Bind(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Wave);
    }
    internal static void DrawControls(Wave wave)
    {
        var selected = Selection.activeGameObject;
        if (source == null || selected != null && selected.GetComponentInParent<Wave>() == wave) Bind(wave);
        bool horizontal = EditorGUIUtility.currentViewWidth >= 480f;
        if (horizontal) EditorGUILayout.BeginHorizontal();
        try
        {
            bool bounds = GUILayout.Toggle(ShowCameraBounds,
                "Show Camera Bounds: " + (ShowCameraBounds ? "ON" : "OFF"), horizontal ? EditorStyles.miniButtonLeft : EditorStyles.miniButton);
            if (bounds != ShowCameraBounds) { Bind(wave); ShowCameraBounds = bounds; }
            bool enemies = GUILayout.Toggle(ShowAllEnemySilhouettes,
                "Show All Enemy Silhouettes: " + (ShowAllEnemySilhouettes ? "ON" : "OFF"), horizontal ? EditorStyles.miniButtonRight : EditorStyles.miniButton);
            if (enemies != ShowAllEnemySilhouettes) { Bind(wave); ShowAllEnemySilhouettes = enemies; }
        }
        finally { if (horizontal) EditorGUILayout.EndHorizontal(); }
    }
    internal static void Invalidate() { dirty = true; SceneView.RepaintAll(); }
    private static void InvalidateSprites() { WaveEnemySilhouette.Clear(); Invalidate(); }
    private static void Tick()
    {
        if ((!ShowCameraBounds && !ShowAllEnemySilhouettes) || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + .1d;
        if (!dirty)
            foreach (var entry in dependencies)
                if (entry.Value == null || EditorUtility.GetDirtyCount(entry.Value) != entry.Dirty) { dirty = true; break; }
        Camera camera = WavePreviewGameplayCamera.GameplayCamera;
        if (ShowCameraBounds && TryGetCameraBounds(camera,out var bounds) && (camera != lastCamera || bounds != lastBounds))
        { lastCamera = camera; lastBounds = bounds; SceneView.RepaintAll(); }
        if (dirty && ShowAllEnemySilhouettes) SceneView.RepaintAll();
    }
    internal static bool TryGetCameraBounds(Camera camera,out Rect bounds)
        => EnemyEmissionOriginSettings.TryGetGameplayBounds(camera,0f,out bounds);

    private static void Draw(SceneView view)
    {
        if (source == null || Event.current.type != EventType.Repaint) return;
        var previousZ = Handles.zTest;
        try
        {
            using var scope = new Handles.DrawingScope(Handles.color, Handles.matrix);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            if (ShowCameraBounds) DrawBounds(WavePreviewGameplayCamera.GameplayCamera);
            if (Application.isPlaying || !ShowAllEnemySilhouettes) return;
            WaveEnemySilhouette.BeginScene(view.camera);
            var player = WavePreviewPlayerWindow.ActivePlayer;
            if (player != null && player.Source == source && player.Simulation != null)
            { player.Simulation.DrawEnemySilhouettes(); return; }
            var legacy = WaveEditor.ActivePreview;
            if (legacy != null && legacy.PreviewWave == source)
            {
                if (legacy.ScenePreview != null) legacy.ScenePreview.DrawEnemySilhouettes();
                else legacy.DrawSceneEnemySilhouettes();
                return;
            }
            if (dirty) RebuildFormation();
            var selectedPreview = DirectedEnemySubWaveEditor.ScenePreviewWave;
            if (selectedPreview != null) DirectedEnemySubWaveEditor.DrawSceneEnemySilhouettes();
            foreach (var pose in formation) if (pose.Wave != selectedPreview) DrawPrefab(pose.Prefab,pose.Matrix);
        }
        finally { Handles.zTest = previousZ; }
    }
    // Existing Wave camera rectangle/crosshair, now driven by the actual gameplay viewport.
    private static void DrawBounds(Camera camera)
    {
        if (!TryGetCameraBounds(camera,out var bounds))
        { Handles.Label(source.transform.position,"Battle camera unavailable: " + WavePreviewGameplayCamera.Error); return; }
        corners[0] = new Vector3(bounds.xMin,bounds.yMax); corners[1] = new Vector3(bounds.xMax,bounds.yMax);
        corners[2] = new Vector3(bounds.xMax,bounds.yMin); corners[3] = new Vector3(bounds.xMin,bounds.yMin);
        Handles.DrawSolidRectangleWithOutline(corners,new Color(.15f,.8f,1f,.08f),new Color(.15f,.8f,1f,.95f));
        Handles.color = new Color(.15f,.8f,1f,.45f);
        Handles.DrawDottedLine(new Vector3(bounds.xMin,bounds.center.y),new Vector3(bounds.xMax,bounds.center.y),4);
        Handles.DrawDottedLine(new Vector3(bounds.center.x,bounds.yMin),new Vector3(bounds.center.x,bounds.yMax),4);
        Handles.Label(corners[1]+new Vector3(.15f,.15f),$"Camera View · {camera.name}\n{bounds.width:0.00} x {bounds.height:0.00}");
    }
    private static void Remember(GameObject root)
    {
        if (root == null) return;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component != null) dependencies.Add((component,EditorUtility.GetDirtyCount(component)));
    }
    private static void RebuildFormation()
    {
        formation.Clear(); renderers.Clear(); dependencies.Clear();
        if (source == null) return;
        Remember(source.gameObject);
        var schedule = source.ScheduledSubWaves;
        if (schedule != null && schedule.Count > 0)
        { foreach (var cue in schedule) if (cue != null) AddSubWave(cue.SubWavePrefab); }
        else if (source.SubWavesToCreate != null)
        { foreach (var prefab in source.SubWavesToCreate) AddSubWave(prefab); }
        dirty = false;
    }
    private static void AddSubWave(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent<DirectedEnemySubWave>(out var wave)) return;
        Remember(prefab);
        for (int i = 0, count = wave.GetSimulationEnemyCount(); i < count; i++)
        {
            var enemy = wave.GetConfiguredEnemyPrefabForSlot(i);
            if (enemy == null) continue;
            if (!renderers.ContainsKey(enemy))
            { renderers.Add(enemy,enemy.GetComponentsInChildren<SpriteRenderer>(true)); Remember(enemy.gameObject); }
            formation.Add(new Pose { Wave=wave, Prefab=enemy, Matrix=Matrix4x4.TRS(wave.GetSimulationFormationPosition(i,source.transform),enemy.transform.rotation,enemy.transform.lossyScale) });
        }
    }
    internal static void DrawEnemy(Enemy enemy,Vector3 position)
    {
        if (enemy == null) return;
        if (!renderers.ContainsKey(enemy)) renderers.Add(enemy,enemy.GetComponentsInChildren<SpriteRenderer>(true));
        DrawPrefab(enemy,Matrix4x4.TRS(position,enemy.transform.rotation,enemy.transform.lossyScale));
    }
    private static void DrawPrefab(Enemy enemy,Matrix4x4 pose)
    {
        if (enemy == null || !renderers.TryGetValue(enemy,out var sprites)) return;
        foreach (var renderer in sprites)
            if (renderer != null && renderer.enabled && ActiveRelativeTo(renderer.transform,enemy.transform))
                WaveEnemySilhouette.Draw(renderer,pose * enemy.transform.worldToLocalMatrix * renderer.localToWorldMatrix);
    }
    private static bool ActiveRelativeTo(Transform item,Transform root)
    {
        while (item != null)
        { if (!item.gameObject.activeSelf) return false; if (item == root) return true; item = item.parent; }
        return false;
    }
    private static void Release()
    {
        SceneView.duringSceneGui -= Draw;
        Selection.selectionChanged -= SelectionChanged;
        EditorApplication.update -= Tick;
        EditorApplication.hierarchyChanged -= Invalidate;
        EditorApplication.projectChanged -= InvalidateSprites;
        Undo.undoRedoPerformed -= Invalidate;
        WaveEnemySilhouette.Clear(); formation.Clear(); renderers.Clear(); dependencies.Clear();
    }
}
