using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WavePreviewPlayerWindow : EditorWindow
{
    private static readonly string[] SpeedLabels = { "0.25×", "0.5×", "1×", "2×", "4×", "8×", "16×", "Max Speed" };
    private static readonly float[] Speeds = { .25f, .5f, 1f, 2f, 4f, 8f, 16f, 1f };
    [SerializeField] private Wave source;
    [SerializeField] private float previewDuration = 30f;
    [SerializeField] private Vector3 targetPosition = new(0f, -3f, 0f);
    [SerializeField] private int speedIndex = 2;
    [SerializeField] private bool followSelection = true;
    private WavePreviewPlayer player;
    private double previousUpdate;
    private bool invalidated;
    private float viewStart, viewLength = 30f;
    private Vector2 scroll;
    private Vector2 contentScroll;
    private string error;
    private bool showEvents = true;
    [SerializeField] private bool showCoordinator = true;
    private Editor coordinatorEditor;
    [SerializeField] private float snapshotInterval = 2f;
    [SerializeField] private int snapshotMemoryLimitMB = 128;
    [SerializeField] private bool recordPlayerMovement;
    [SerializeField] private bool showAdvanced;
    [SerializeField] private float simulationTimeBudget = 24f;
    [SerializeField] private int maxStepsPerUpdate = 2048;
    [SerializeField] private bool showSeekDiagnostics;
    private double nextProgressRepaint;
    private bool wasLoading;

    [MenuItem("Tools/Waves/Wave Preview Player")]
    public static void ShowWindow() => Open(SelectedWave());
    public static WavePreviewPlayerWindow Open(Wave wave)
    {
        var window = GetWindow<WavePreviewPlayerWindow>("Wave Preview Player");
        window.minSize = new Vector2(520f, 440f);
        if (wave != null) window.SetSource(wave);
        window.Show(); return window;
    }
    private void OnEnable()
    {
        previousUpdate = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        SceneView.duringSceneGui += DrawScene;
        Selection.selectionChanged += SelectionChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Release;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.quitting += Release;
        EditorApplication.projectChanged += Invalidate;
        EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
        EditorSceneManager.sceneClosing += SceneClosing;
        Undo.undoRedoPerformed += Invalidate;
        Undo.postprocessModifications += Modifications;
    }
    private void OnDisable()
    {
        Release();
        EditorApplication.update -= Tick;
        SceneView.duringSceneGui -= DrawScene;
        Selection.selectionChanged -= SelectionChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= Release;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorApplication.quitting -= Release;
        EditorApplication.projectChanged -= Invalidate;
        EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
        EditorSceneManager.sceneClosing -= SceneClosing;
        Undo.undoRedoPerformed -= Invalidate;
        Undo.postprocessModifications -= Modifications;
    }
    private void SceneChanged(Scene previous, Scene current) => Release();
    private void SceneClosing(Scene scene, bool removingScene) => Release();
    private void PlayModeChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingEditMode) Release(); }
    private void Release()
    {
        player?.Dispose(); player = null;
        if (coordinatorEditor != null) DestroyImmediate(coordinatorEditor);
        SceneView.RepaintAll(); Repaint();
    }
    private void Invalidate() => invalidated = true;
    private UndoPropertyModification[] Modifications(UndoPropertyModification[] changes)
    {
        if (source == null) return changes;
        foreach (var change in changes)
        {
            var changed = change.currentValue.target;
            if (changed != null && (changed.hideFlags & HideFlags.DontSave) == 0
                && (!(changed is Component temporary) || (temporary.gameObject.hideFlags & HideFlags.DontSave) == 0)
                && (EditorUtility.IsPersistent(changed) || changed is Component component && component.gameObject.scene == source.gameObject.scene))
            { Invalidate(); break; }
        }
        return changes;
    }
    private static Wave SelectedWave()
    {
        var selected = Selection.activeGameObject;
        return selected != null ? selected.GetComponentInParent<Wave>() : null;
    }
    private void SelectionChanged()
    {
        var selected = SelectedWave();
        if (followSelection && selected != null && selected != source) SetSource(selected);
    }
    private void SetSource(Wave value)
    {
        if (source == value) return;
        Release(); source = value; error = null; invalidated = false;
        viewStart = 0f; viewLength = previewDuration;
    }
    private WavePreviewPlayer EnsurePlayer()
    {
        if (player == null && source != null)
            player = new WavePreviewPlayer(source, previewDuration, targetPosition)
            { SnapshotInterval = snapshotInterval, SnapshotMemoryLimitMB = snapshotMemoryLimitMB, RecordPlayerMovement = recordPlayerMovement,
                SimulationTimeBudget = simulationTimeBudget, MaxStepsPerUpdate = maxStepsPerUpdate };
        return player;
    }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        double delta = now - previousUpdate; previousUpdate = now;
        if (player == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || source == null) { Release(); return; }
        try
        {
            if (invalidated) { player.Invalidate(); invalidated = false; }
            player.PlaybackSpeed = Speeds[Mathf.Clamp(speedIndex, 0, Speeds.Length - 1)];
            player.MaxSpeed = speedIndex == Speeds.Length - 1;
            int previousPresentation = player.PresentationVersion;
            bool changed = player.Update(delta);
            targetPosition = player.TargetPosition;
            recordPlayerMovement = player.RecordPlayerMovement;
            bool loading = player.IsLoading;
            if (wasLoading != loading || previousPresentation != player.PresentationVersion)
                SceneView.RepaintAll();
            if ((changed || player.IsPlaying) && (!loading || now >= nextProgressRepaint || wasLoading != loading))
            {
                Repaint();
                nextProgressRepaint = now + .1d;
            }
            wasLoading = loading;
        }
        catch (Exception exception) { error = exception.Message; Release(); Debug.LogException(exception); }
    }
    private void DrawScene(SceneView view)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SceneGUI);
        if (player == null || Application.isPlaying) return;
        player.Draw(view);
        if (player.Simulation == null) return;
        if (!player.IsLoading)
        {
            EditorGUI.BeginChangeCheck();
            var next = Handles.PositionHandle(targetPosition, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            { next.z = 0f; player.TargetPosition = next; targetPosition = player.TargetPosition; Repaint(); }
        }
        Handles.BeginGUI();
        GUI.Box(new Rect(12f, 12f, 360f, 28f), player.IsLoading
            ? $"Seeking {player.RequestedTime:0.000}s · calculating {player.CurrentTime:0.000}s"
            : $"Wave Preview Player · {player.CurrentTime:0.000}s · {(player.IsPlaying ? "Playing" : "Paused")}");
        Handles.EndGUI();
    }
    private void OnGUI()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.WindowGUI);
        contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
        try { DrawContents(); }
        finally { EditorGUILayout.EndScrollView(); }
    }
    private void DrawContents()
    {
        EditorGUI.BeginChangeCheck();
        var selected = (Wave)EditorGUILayout.ObjectField("Wave", source, typeof(Wave), true);
        if (EditorGUI.EndChangeCheck()) SetSource(selected);
        followSelection = EditorGUILayout.Toggle("Follow Wave Selection", followSelection);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (Application.isPlaying) { EditorGUILayout.HelpBox("Available in Edit Mode.", MessageType.Info); return; }
        if (source == null) { EditorGUILayout.HelpBox("Select a Wave conductor or assign it above. Open its prefab to use Scene View.", MessageType.Info); return; }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Play", EditorStyles.toolbarButton)) { EnemyShootingPreview.ShowFormationVolley = true; EnsurePlayer().Play(); }
            if (GUILayout.Button("Pause", EditorStyles.toolbarButton)) player?.Pause();
            if (GUILayout.Button("Stop", EditorStyles.toolbarButton)) { player?.Stop(); SceneView.RepaintAll(); }
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton)) { EnemyShootingPreview.ShowFormationVolley = true; EnsurePlayer().Restart(); }
            if (GUILayout.Button("◀ Step", EditorStyles.toolbarButton)) EnsurePlayer().Step(-1);
            if (GUILayout.Button("Step ▶", EditorStyles.toolbarButton)) EnsurePlayer().Step(1);
            speedIndex = EditorGUILayout.Popup(speedIndex, SpeedLabels, EditorStyles.toolbarPopup, GUILayout.Width(92f));
        }
        EditorGUI.BeginChangeCheck();
        previewDuration = Mathf.Max(WavePreviewPlayer.StepInterval, EditorGUILayout.FloatField(new GUIContent("Preview Duration",
            "Preview horizon, not an enemy-death prediction. Loops and combat-dependent waves have no fixed total duration."), previewDuration));
        if (EditorGUI.EndChangeCheck()) { if (player != null) player.Duration = previewDuration; viewLength = Mathf.Min(viewLength, previewDuration); }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"{player?.CurrentTime ?? 0f:0.000}s / {previewDuration:0.000}s   ·   step {WavePreviewPlayer.StepInterval:0.00000}s");
            if (GUILayout.Button("Use Route Duration", GUILayout.Width(145f)))
            { previewDuration = Mathf.Max(WavePreviewPlayer.StepInterval, WavePreviewPlayer.BaseRouteDuration(source)); if (player != null) player.Duration = previewDuration; viewStart = 0; viewLength = previewDuration; }
        }
        DrawTimeline();
        if (player != null && player.IsLoading)
        {
            var rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, player.SeekProgress,
                $"Seeking {player.RequestedTime:0.000}s — {player.CurrentTime:0.000}s");
        }
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(player != null && player.IsLoading))
            targetPosition = EditorGUILayout.Vector3Field("Preview Player Target", targetPosition);
        if (EditorGUI.EndChangeCheck() && player != null) { player.TargetPosition = targetPosition; targetPosition = player.TargetPosition; }
        EditorGUI.BeginChangeCheck();
        recordPlayerMovement = EditorGUILayout.Toggle(new GUIContent("Record Player Movement",
            "Dragging the target after playback starts enables recording automatically. Turning this off keeps past inputs and freezes the target from the current time."),
            player != null ? player.RecordPlayerMovement : recordPlayerMovement);
        if (EditorGUI.EndChangeCheck() && player != null) player.RecordPlayerMovement = recordPlayerMovement;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Clear Snapshot Cache", "Keeps only the initial state needed to rewind to zero."))) player?.ClearSnapshotCache();
            using (new EditorGUI.DisabledScope(player != null && player.IsLoading))
                if (GUILayout.Button(new GUIContent("Clear Player Movement History", "Rebuilds the selected time using the current position as one fixed target.")))
                { player?.ClearPlayerMovementHistory(); recordPlayerMovement = false; }
        }
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings", true);
        if (showAdvanced)
        {
            snapshotInterval = Mathf.Clamp(EditorGUILayout.FloatField("Snapshot Interval", snapshotInterval), .25f, 30f);
            snapshotMemoryLimitMB = Mathf.Clamp(EditorGUILayout.IntField("Snapshot Memory Limit (MB)", snapshotMemoryLimitMB), 16, 512);
            simulationTimeBudget = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Simulation Time Budget (ms)",
                "Cooperative budget per Editor update. A fixed step or snapshot is atomic and can exceed this budget."), simulationTimeBudget), .1f, 100f);
            maxStepsPerUpdate = Mathf.Clamp(EditorGUILayout.IntField("Max Steps Per Update", maxStepsPerUpdate), 1, 10000);
            if (player != null)
            {
                player.SnapshotInterval = snapshotInterval; player.SnapshotMemoryLimitMB = snapshotMemoryLimitMB;
                player.SimulationTimeBudget = simulationTimeBudget; player.MaxStepsPerUpdate = maxStepsPerUpdate;
                EditorGUILayout.LabelField($"{player.SnapshotCount} snapshots · estimated {player.SnapshotMemoryBytes / (1024d * 1024d):0.0} MB · {player.TargetHistory.Samples.Count} target samples");
                showSeekDiagnostics = EditorGUILayout.Foldout(showSeekDiagnostics, "Fast Seek Diagnostics", true);
                if (showSeekDiagnostics)
                {
                    EditorGUILayout.LabelField("Target Time", $"{player.SeekTargetTime:0.000} s");
                    EditorGUILayout.LabelField("Simulated Duration", $"{player.SimulatedDuration:0.000} s");
                    EditorGUILayout.LabelField("Seek Wall Time", $"{player.SeekWallTime:0.000} s");
                    EditorGUILayout.LabelField("Simulation Speed", $"{player.SimulationSpeed:0.0}×");
                    EditorGUILayout.LabelField("Steps Processed", player.StepsProcessed.ToString());
                }
            }
        }
        NumericRandomPreviewControls.Draw();
        if (player?.Simulation != null)
        {
            var simulation = player.Simulation;
            if (simulation.UnsupportedCueCount > 0) EditorGUILayout.HelpBox($"{simulation.UnsupportedCueCount} schedule entries have no DirectedEnemySubWave. Use their existing legacy Preview; they are not simulated by this player.", MessageType.Warning);
            EditorGUILayout.LabelField($"{simulation.ActiveCueCount}/{simulation.CueCount} subwaves · {simulation.EnemyCount} enemy visuals · {simulation.ProjectileCount} projectiles");
            if (!string.IsNullOrEmpty(simulation.Warning)) EditorGUILayout.HelpBox(simulation.Warning, MessageType.Warning);
            if (simulation.Omitted > 0) EditorGUILayout.HelpBox($"Preview limits omitted {simulation.Omitted} projectiles. Maximum live projectiles: {EnemyProjectilePreviewWorld.MaximumProjectiles}.", MessageType.Info);
        }
        EditorGUILayout.HelpBox("Duration is a preview range. Enemy deaths, player-dependent dives and danger-warning visuals are not simulated here. Child attacks and Laser/Telegraph use the existing Preview; their individual trigger markers are not exposed yet.", MessageType.None);
        DrawEvents();
        DrawCoordinator();
    }
    private void DrawCoordinator()
    {
        EditorGUILayout.Space(8f);
        showCoordinator = EditorGUILayout.Foldout(showCoordinator, "Attack Coordinator", true);
        if (!showCoordinator) return;
        var coordinator = source.GetComponent<WaveAttackCoordinator>();
        if (coordinator == null)
        {
            if (EditorUtility.IsPersistent(source))
            {
                if (GUILayout.Button("Open Prefab To Add Attack Coordinator"))
                {
                    var stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(source));
                    SetSource(stage.prefabContentsRoot.GetComponent<Wave>());
                }
            }
            else if (GUILayout.Button("Add Attack Coordinator (optional)"))
                Undo.AddComponent<WaveAttackCoordinator>(source.gameObject);
            return;
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Editor.CreateCachedEditor(coordinator, typeof(WaveAttackCoordinatorEditor), ref coordinatorEditor);
            coordinatorEditor.OnInspectorGUI();
            player?.Simulation?.DrawCoordinatorInspectorStatus();
        }
    }
    private void DrawTimeline()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Timeline);
        viewLength = Mathf.Clamp(viewLength, WavePreviewPlayer.StepInterval, previewDuration);
        viewStart = Mathf.Clamp(viewStart, 0f, previewDuration - viewLength);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("− Zoom", GUILayout.Width(70f))) viewLength = Mathf.Min(previewDuration, viewLength * 2f);
            if (GUILayout.Button("+ Zoom", GUILayout.Width(70f))) { viewStart = Mathf.Max(0f, (player?.CurrentTime ?? 0f) - viewLength * .25f); viewLength *= .5f; }
            if (GUILayout.Button("Fit", GUILayout.Width(45f))) { viewStart = 0; viewLength = previewDuration; }
            viewStart = EditorGUILayout.Slider("View", viewStart, 0f, Mathf.Max(0f, previewDuration - viewLength));
        }
        float current = player?.RequestedTime ?? 0f;
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("Timeline", current, viewStart, Mathf.Min(previewDuration, viewStart + viewLength));
        if (EditorGUI.EndChangeCheck()) { EnemyShootingPreview.ShowFormationVolley = true; EnsurePlayer().Seek(time); }
        EditorGUI.BeginChangeCheck();
        time = EditorGUILayout.FloatField("Go To Time", current);
        if (EditorGUI.EndChangeCheck()) EnsurePlayer().Seek(time);
        Rect strip = GUILayoutUtility.GetRect(1f, 44f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(strip, new Color(.13f, .14f, .17f));
        if (player != null)
        {
            var observations = player.Events;
            // Pixel buckets keep rendering bounded even when the event history is dense.
            int previousPixel = -1;
            for (int i = 0; i < observations.Count; i++)
            {
                var observation = observations[i];
                if (observation.Time < viewStart || observation.Time > viewStart + viewLength) continue;
                int pixel = Mathf.FloorToInt((observation.Time - viewStart) / viewLength * strip.width);
                if (pixel == previousPixel) continue;
                previousPixel = pixel;
                EditorGUI.DrawRect(new Rect(strip.x + pixel, strip.y + 8f, 2f, 26f), EventColor(observation.Kind));
            }
            float cursor = (player.CurrentTime - viewStart) / viewLength;
            if (cursor >= 0f && cursor <= 1f) EditorGUI.DrawRect(new Rect(strip.x + cursor * strip.width, strip.y, 2f, strip.height), Color.white);
        }
        if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && strip.Contains(Event.current.mousePosition) && Event.current.button == 0)
        { EnsurePlayer().Seek(viewStart + Mathf.Clamp01((Event.current.mousePosition.x - strip.x) / strip.width) * viewLength); Event.current.Use(); }
    }
    private static Color EventColor(WavePreviewEventKind kind) => kind switch
    {
        WavePreviewEventKind.EnemySpawn => Color.green,
        WavePreviewEventKind.ProjectileSpawn => Color.yellow,
        WavePreviewEventKind.AttackStart => new Color(1f, .4f, .3f),
        WavePreviewEventKind.AttackEnd => new Color(.6f, .6f, .9f),
        WavePreviewEventKind.CoordinatorGroupChange => Color.cyan,
        _ => new Color(1f, .4f, 1f)
    };
    private void DrawEvents()
    {
        showEvents = EditorGUILayout.Foldout(showEvents, $"Observed Events ({player?.Events.Count ?? 0})", true);
        if (!showEvents || player == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(60f));
        var observations = player.Events;
        // Show a bounded tail; the full bounded history remains available to future Timeline tools.
        for (int i = Mathf.Max(0, observations.Count - 60); i < observations.Count; i++)
        {
            var item = observations[i];
            EditorGUILayout.LabelField($"{item.Time:0.000}s  {item.Kind}", item.Detail ?? $"Cue {item.Cue + 1} · Slot {item.Slot} · {item.Count}");
        }
        EditorGUILayout.EndScrollView();
        if (player.OmittedEvents > 0) EditorGUILayout.LabelField($"Event history capped; {player.OmittedEvents} further observations omitted.");
    }
}
