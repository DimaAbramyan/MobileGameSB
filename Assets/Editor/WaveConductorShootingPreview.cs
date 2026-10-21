using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// One projectile world and one player target for the entire conductor schedule.
public sealed partial class WaveConductorShootingPreview : IDisposable
{
    public const int MaximumEnemies = int.MaxValue;
    private sealed class Cue
    {
        public int Index;
        public DirectedEnemySubWave Wave;
        public float Delay;
        public int[] SpawnOrder;
        public int AttackRevision;
        public WaveShootingPreviewSession Session;
        public bool Started;
        public WavePreviewStateGraph InitialState;
        public WaveShootingPreviewSession.VisualState InitialVisuals;
        public List<WavePreviewEvent> InitialEvents;
    }

    private readonly Wave owner;
    private readonly List<Cue> cues = new();
    private readonly EnemyProjectilePreviewWorld projectiles = new();
    private readonly Stopwatch timer = new();
    private readonly GameObject root;
    private readonly GameObject target;
    private readonly Mesh targetMesh;
    private readonly Material targetMaterial;
    private bool disposed;
    internal bool DeferRenderCapture { get; set; }
    private readonly Action<WavePreviewEvent> eventSink;
    private readonly int? spawnSeed;
    private readonly int presetRevision = EnemyPresetRevision.Value;
    public float Time { get; private set; }
    public int StepCount { get; private set; }
    public int CueCount => cues.Count;
    public int UnsupportedCueCount { get; private set; }
    private readonly int numericPreviewRevision = NumericRandomPreviewControls.Revision;
    private readonly List<(Object Value, int Dirty)> configurationObjects = new();
    public bool IsConfigurationCurrent
    {
        get
        {
            if (numericPreviewRevision != NumericRandomPreviewControls.Revision || presetRevision != EnemyPresetRevision.Value) return false;
            if (!IsCoordinatorCurrent) return false;
            foreach (var item in configurationObjects)
                if (item.Value == null || EditorUtility.GetDirtyCount(item.Value) != item.Dirty) return false;
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].Wave == null || (cues[i].Wave.AttackPatternBehaviour != null ? cues[i].Wave.AttackPatternBehaviour.ConfigurationRevision : 0) != cues[i].AttackRevision) return false;
            return true;
        }
    }
    public int ActiveCueCount
    {
        get { int count = 0; for (int i = 0; i < cues.Count; i++) if (cues[i].Started) count++; return count; }
    }
    public int EnemyCount
    {
        get { int count = 0; for (int i = 0; i < cues.Count; i++) if (cues[i].Started) count += cues[i].Session.EnemyCount; return count; }
    }
    public int ProjectileCount => projectiles.Count;
    public long Omitted
    {
        get
        {
            long count = projectiles.Omitted;
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].Started) count += cues[i].Session.Omitted - projectiles.Omitted;
            return count;
        }
    }
    public string Warning { get; private set; }
    public Vector3 TargetPosition { get => target != null ? target.transform.position : Vector3.zero;
        set { if (target != null) target.transform.position = value; } }
    public EnemyProjectilePreviewWorld ProjectileWorld => projectiles;

    public WaveConductorShootingPreview(Wave owner, Vector3 targetPosition, Sprite targetSprite = null,
        Action<WavePreviewEvent> eventSink = null, int? spawnSeed = null)
    {
        this.owner = owner;
        this.eventSink = eventSink; this.spawnSeed = spawnSeed;
        root = new GameObject("Wave Conductor Preview (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        var scene = owner.gameObject.scene.IsValid() ? owner.gameObject.scene
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        target = new GameObject("Preview Player") { hideFlags = HideFlags.HideAndDontSave };
        target.transform.SetParent(root.transform);
        TargetPosition = targetPosition;
        WaveShootingPreviewSession.CreateTargetVisual(target, targetSprite, out targetMesh, out targetMaterial);
        AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        try
        {
            var warning = owner.GetComponent<WaveDangerWarningController>();
            float warningDelay = warning != null && warning.ShouldPlayWarning ? warning.WarningDuration : 0f;
            var schedule = owner.ScheduledSubWaves;
            if (schedule != null && schedule.Count > 0)
            {
                for (int i = 0; i < schedule.Count; i++)
                    if (schedule[i] != null) AddCue(i, schedule[i].SubWavePrefab, schedule[i].StartDelay + warningDelay);
            }
            else if (owner.SubWavesToCreate != null)
            {
                for (int i = 0; i < owner.SubWavesToCreate.Count; i++) AddCue(i, owner.SubWavesToCreate[i], warningDelay);
            }
            // Include every enemy; projectile counts and per-update work have their own budgets.
            InitializeCoordinator();
            RememberConfiguration(owner.gameObject);
            foreach (var cue in cues) RememberConfiguration(cue.Wave.gameObject);
        }
        catch { Dispose(); throw; }
    }
    private void RememberConfiguration(GameObject source)
    {
        foreach (var component in source.GetComponentsInChildren<Component>(true))
            if (component != null) configurationObjects.Add((component, EditorUtility.GetDirtyCount(component)));
    }

    private void AddCue(int index, GameObject source, float delay)
    {
        if (source == null || !source.TryGetComponent(out DirectedEnemySubWave wave))
        { UnsupportedCueCount++; return; }
        cues.Add(new Cue { Index = index, Wave = wave, Delay = delay,
            AttackRevision = wave.AttackPatternBehaviour != null ? wave.AttackPatternBehaviour.ConfigurationRevision : 0,
            SpawnOrder = WaveShootingPreviewSession.BuildSpawnOrder(wave,
                spawnSeed.HasValue ? unchecked((int)NumericRandomStream.Hash((uint)spawnSeed.Value ^ (uint)index)) : (int?)null) });
    }

    public int[] GetSpawnOrder(int cueIndex)
    {
        for (int i = 0; i < cues.Count; i++) if (cues[i].Index == cueIndex) return cues[i].SpawnOrder;
        return null;
    }

    public WaveShootingPreviewSession GetSession(int cueIndex)
    {
        for (int i = 0; i < cues.Count; i++) if (cues[i].Index == cueIndex) return cues[i].Started ? cues[i].Session : null;
        return null;
    }

    public bool AdvanceTo(float destination, double budgetMilliseconds = 4d, bool captureRenderState = true,
        Action<int> beforeStep = null, Action<int> afterStep = null, int maxSteps = 120,
        bool deferPresentation = false)
    {
        if (disposed) return true;
        timer.Restart();
        const float step = WaveShootingPreviewSession.StepInterval;
        int steps = 0;
        while ((StepCount + 1) * step <= destination + .00001f)
        {
            using var stepProfile = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.FixedStep);
            beforeStep?.Invoke(StepCount);
            int active = 0;
            for (int i = 0; i < cues.Count; i++) if (Time >= cues[i].Delay) active++;
            int emissionBudget = Mathf.Max(1, 512 / Mathf.Max(1, active));
            PrepareReadyCues();
            using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Coordinator)) coordinatorPlayer?.Tick(Time);
            ObserveCoordinator();
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].Started) cues[i].Session.PrepareSharedStep(Time - cues[i].Delay, emissionBudget, deferPresentation);
            projectiles.Step((StepCount + 1) * step, step, TargetPosition);
            Time = ++StepCount * step;
            afterStep?.Invoke(StepCount);
            if (++steps >= Mathf.Max(1, maxSteps) || timer.Elapsed.TotalMilliseconds >= budgetMilliseconds) break;
        }
        if (captureRenderState) projectiles.CaptureRenderState();
        return destination - Time < step + .00001f;
    }

    public void PrepareInitialState() { if (!disposed) PrepareReadyCues(); }

    public void SetVisible(bool visible)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Visibility);
        if (disposed) return;
        projectiles.SetVisible(visible);
        if (target.TryGetComponent<Renderer>(out var renderer)) renderer.forceRenderingOff = !visible;
        for (int i = 0; i < cues.Count; i++) cues[i].Session?.SetVisible(visible && cues[i].Started);
    }

    internal void PublishRenderState()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Publish);
        foreach (var cue in cues) if (cue.Started) cue.Session.PublishVisuals();
        projectiles.CaptureRenderState();
    }

    private void PrepareReadyCues()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.InitializeCue);
        for (int i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            if (Time < cue.Delay || cue.Started) continue;
            cue.Started = true;
            if (cue.Session != null)
            {
                if (cue.InitialEvents != null) foreach (var observation in cue.InitialEvents) eventSink?.Invoke(observation);
                if (!string.IsNullOrEmpty(cue.Session.Warning)) Warning = cue.Wave.name + ": " + cue.Session.Warning;
                continue;
            }
            bool initializing = true;
            if (snapshotsEnabled) cue.InitialEvents = new List<WavePreviewEvent>();
            Action<WavePreviewEvent> observe = observation =>
            {
                if (initializing) cue.InitialEvents?.Add(observation);
                eventSink?.Invoke(observation);
            };
            cue.Session = new WaveShootingPreviewSession(cue.Wave, false, true, 0, TargetPosition,
                suppliedSpawnOrder: cue.SpawnOrder, previewParent: owner.transform,
                sharedProjectiles: projectiles, sharedTarget: target,
                eventSink: eventSink != null ? observe : null, eventTimeOffset: cue.Delay, eventCue: cue.Index);
            initializing = false;
            if (snapshotsEnabled) CaptureInitialSession(cue);
            if (!string.IsNullOrEmpty(cue.Session.Warning)) Warning = cue.Wave.name + ": " + cue.Session.Warning;
        }
    }

    public void Draw(SceneView view, bool allowTargetEditing = true)
    {
        if (disposed || Application.isPlaying || view == null) return;
        bool visible = EnemyShootingPreview.ShowFormationVolley;
        projectiles.RegisterCamera(view.camera);
        SetVisible(visible);
        if (!visible) return;
        if (Event.current.type == EventType.Repaint)
        {
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(TargetPosition, Vector3.forward, .4f);
            Handles.Label(TargetPosition + Vector3.down * .55f, "Preview Player · shared by all subwaves");
        }
        if (!allowTargetEditing) return;
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(TargetPosition, Quaternion.identity);
        if (EditorGUI.EndChangeCheck()) { position.z = 0f; TargetPosition = position; view.Repaint(); }
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingEditMode) Dispose(); }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        coordinatorPlayer?.Dispose();
        AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        for (int i = 0; i < cues.Count; i++) cues[i].Session?.Dispose();
        projectiles.Dispose();
        if (root != null) Object.DestroyImmediate(root);
        if (targetMesh != null) Object.DestroyImmediate(targetMesh);
        if (targetMaterial != null) Object.DestroyImmediate(targetMaterial);
        cues.Clear();
    }
}
