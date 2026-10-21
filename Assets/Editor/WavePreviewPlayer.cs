using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

// Transport, input history and checkpoints; movement/shooting stay in the existing conductor.
public sealed class WavePreviewPlayer : IDisposable
{
    public const int MaximumEvents = 12000;
    public const float StepInterval = WaveShootingPreviewSession.StepInterval;
    // QueuePlayerLoopUpdate alone still throttles Editor.update in an unfocused
    // Editor. Pipeline uses this same wake-up API; bind it once, only while busy.
    private static readonly Action signalEditorTick = typeof(UnityEditor.EditorApplication)
        .GetMethod("SignalTick", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?.CreateDelegate(typeof(Action)) as Action;
    private readonly List<WavePreviewEvent> events = new();
    private WaveConductorShootingPreview simulation;
    private readonly WavePreviewSnapshotCache snapshots = new();
    private readonly WavePreviewTargetHistory history;
    private readonly Action<int> beforeStep, afterStep;
    private readonly Wave source;
    private int destinationFrame;
    private double playhead;
    private bool resetRequested, disposed;
    private bool restoreRequested, manualSeek, recordPlayerMovement;
    private int validThroughFrame = int.MaxValue;
    private float snapshotInterval = 2f;
    private Vector3 targetPosition;
    private float duration;
    private double simulationTimeBudget = 24d;
    private int maxStepsPerUpdate = 2048;
    private long seekStarted, seekFinished;
    private int seekStartFrame, presentedFrame;
    private bool seekMeasured;
    public double SimulationTimeBudget
    {
        get => simulationTimeBudget;
        set => simulationTimeBudget = double.IsFinite(value) ? Math.Clamp(value, .1d, 100d) : 24d;
    }
    public int MaxStepsPerUpdate { get => maxStepsPerUpdate; set => maxStepsPerUpdate = Math.Clamp(value, 1, 10000); }
    public bool MaxSpeed { get; set; }
    public int StepsProcessed { get; private set; }
    public float SeekTargetTime { get; private set; }
    public float SimulatedDuration => StepsProcessed * StepInterval;
    public double SeekWallTime => !seekMeasured ? 0d
        : ((seekFinished != 0 ? seekFinished : Stopwatch.GetTimestamp()) - seekStarted) / (double)Stopwatch.Frequency;
    public double SimulationSpeed => SeekWallTime > 0d ? SimulatedDuration / SeekWallTime : 0d;
    public float SeekProgress => !IsLoading ? 1f : destinationFrame <= seekStartFrame ? 0f
        : Mathf.Clamp01((float)(CurrentFrame - seekStartFrame) / (destinationFrame - seekStartFrame));
    public int PresentationVersion { get; private set; }
    public Wave Source => source;
    public WaveConductorShootingPreview Simulation => simulation;
    public IReadOnlyList<WavePreviewEvent> Events => events;
    public int OmittedEvents { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsSeeking => resetRequested || restoreRequested || CurrentFrame != destinationFrame;
    public bool IsLoading => IsSeeking && manualSeek;
    public int CurrentFrame => simulation != null ? simulation.StepCount : 0;
    public float CurrentTime => CurrentFrame * StepInterval;
    public float PresentedTime => presentedFrame * StepInterval;
    public int SnapshotCount => snapshots.Count;
    public long SnapshotMemoryBytes => snapshots.Bytes;
    public int LastRestoreFrame { get; private set; } = -1;
    public int RestoredSnapshots { get; private set; }
    public WavePreviewTargetHistory TargetHistory => history;
    public float SnapshotInterval
    {
        get => snapshotInterval;
        set => snapshotInterval = float.IsFinite(value) ? Mathf.Clamp(value, .25f, 30f) : 2f;
    }
    public int SnapshotMemoryLimitMB
    {
        get => (int)(snapshots.MaximumBytes / (1024L * 1024L));
        set { snapshots.MaximumBytes = Mathf.Clamp(value, 16, 512) * 1024L * 1024L; snapshots.Trim(); }
    }
    public bool RecordPlayerMovement
    {
        get => recordPlayerMovement;
        set
        {
            if (recordPlayerMovement == value) return;
            recordPlayerMovement = value;
            if (!value) { history.FreezeAfter(CurrentFrame); snapshots.InvalidateAfter(CurrentFrame); }
        }
    }
    public float RequestedTime => destinationFrame * StepInterval;
    public float PlaybackSpeed { get; set; } = 1f;
    public float Duration
    {
        get => duration;
        set
        {
            duration = float.IsFinite(value) ? Mathf.Max(StepInterval, value) : 30f;
            if (RequestedTime > duration) Seek(duration);
        }
    }
    public Vector3 TargetPosition
    {
        get => targetPosition;
        set
        {
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z)) return;
            if (!history.Set(CurrentFrame, value)) return;
            if (CurrentFrame > 0) recordPlayerMovement = true;
            targetPosition = value; snapshots.InvalidateAfter(CurrentFrame);
            if (simulation != null) simulation.TargetPosition = value;
        }
    }
    public WavePreviewPlayer(Wave source, float duration, Vector3 targetPosition)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        this.source = source; this.targetPosition = targetPosition; history = new WavePreviewTargetHistory(targetPosition);
        beforeStep = ApplyTarget; afterStep = CompletedStep; Duration = duration;
    }
    public void Play()
    {
        if (disposed || source == null) return;
        if (CurrentFrame >= LastFrame && !IsSeeking) Seek(0f);
        EnsureSimulation(); IsPlaying = true;
    }
    public void Pause()
    {
        bool pending = IsSeeking;
        bool invalidState = resetRequested || CurrentFrame > validThroughFrame;
        IsPlaying = false; destinationFrame = CurrentFrame;
        playhead = CurrentTime;
        // Cancel transport work, but keep the rebuild required by edited inputs/settings.
        restoreRequested = invalidState && !resetRequested;
        manualSeek = invalidState;
        FinishSeekMeasurement();
        if (pending && !invalidState && simulation != null)
        {
            PublishRenderState();
            simulation.SetVisible(EnemyShootingPreview.ShowFormationVolley);
        }
    }
    public void Stop()
    {
        IsPlaying = false; destinationFrame = 0; playhead = 0; resetRequested = restoreRequested = manualSeek = false;
        validThroughFrame = int.MaxValue; LastRestoreFrame = -1; RestoredSnapshots = 0;
        seekMeasured = false; seekStarted = seekFinished = 0; StepsProcessed = presentedFrame = 0;
        ReleaseSimulation(); events.Clear(); OmittedEvents = 0; targetPosition = history.Evaluate(0);
    }
    public void Restart() { Stop(); Play(); }
    public void Seek(float time)
    {
        if (disposed || source == null || !float.IsFinite(time)) return;
        int next = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(time, 0f, Duration) / StepInterval), 0, LastFrame);
        bool changed = next != destinationFrame;
        destinationFrame = next;
        playhead = destinationFrame * (double)StepInterval;
        // Only one pending destination. Slider changes never start competing work.
        restoreRequested = destinationFrame != CurrentFrame || CurrentFrame > validThroughFrame;
        manualSeek = IsSeeking;
        if (manualSeek)
        {
            if (changed || !seekMeasured || seekFinished != 0) BeginSeekMeasurement();
            simulation?.SetVisible(false);
        }
    }
    public void Step(int direction)
    { Pause(); Seek((CurrentFrame + Math.Sign(direction)) * StepInterval); }
    public void Invalidate()
    {
        resetRequested = manualSeek = true; playhead = RequestedTime; snapshots.Clear(false);
        BeginSeekMeasurement(); simulation?.SetVisible(false);
    }
    public void ClearSnapshotCache() => snapshots.Clear(); // Keep the initial state for a correct rollback to frame zero.
    public void ClearPlayerMovementHistory()
    { history.Clear(targetPosition); recordPlayerMovement = false; Invalidate(); }
    public void SetTargetPositionAtTime(float time, Vector3 position)
    {
        if (!float.IsFinite(time) || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return;
        int frame = Mathf.Clamp(Mathf.RoundToInt(time / StepInterval), 0, LastFrame);
        if (frame == CurrentFrame) { TargetPosition = position; return; }
        if (!history.Set(frame, position)) return;
        recordPlayerMovement = true; snapshots.InvalidateAfter(frame);
        if (frame < CurrentFrame)
        {
            validThroughFrame = Mathf.Min(validThroughFrame, frame); restoreRequested = manualSeek = true;
            BeginSeekMeasurement(); simulation?.SetVisible(false);
        }
    }
    private int LastFrame => Mathf.Max(1, Mathf.RoundToInt(Duration / StepInterval));

    public bool Update(double elapsedSeconds, double budgetMilliseconds = double.NaN)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.PlayerUpdate);
        if (disposed || source == null) { Stop(); return false; }
        long batchStarted = Stopwatch.GetTimestamp();
        double budget = double.IsFinite(budgetMilliseconds) ? Math.Max(.1d, budgetMilliseconds) : SimulationTimeBudget;
        if (simulation != null && !simulation.IsConfigurationCurrent) Invalidate();
        bool catchingUp = IsSeeking;
        int previous = CurrentFrame;
        if (resetRequested) ResetSimulation();
        if (IsPlaying && !manualSeek)
        {
            if (MaxSpeed) destinationFrame = LastFrame;
            else
            {
                float speed = float.IsFinite(PlaybackSpeed) ? Mathf.Clamp(PlaybackSpeed, .25f, 16f) : 1f;
                playhead = Math.Min(LastFrame * (double)StepInterval, playhead + Math.Max(0d, elapsedSeconds) * speed);
                destinationFrame = Math.Min(LastFrame, (int)Math.Floor(playhead / StepInterval + .000001d));
            }
        }
        if (simulation == null && (IsPlaying || destinationFrame > 0 || catchingUp)) EnsureSimulation();
        if (simulation != null && restoreRequested)
        {
            var checkpoint = snapshots.Find(Math.Min(destinationFrame, validThroughFrame));
            if (checkpoint != null && (CurrentFrame > destinationFrame || CurrentFrame > validThroughFrame || checkpoint.Frame > CurrentFrame))
            {
                checkpoint.Restore(); events.Clear(); events.AddRange(checkpoint.Events); OmittedEvents = checkpoint.OmittedEvents;
                LastRestoreFrame = checkpoint.Frame; RestoredSnapshots++; ApplyTarget(CurrentFrame);
            }
            restoreRequested = false; validThroughFrame = int.MaxValue;
            seekStartFrame = CurrentFrame;
        }
        int beforeAdvance = CurrentFrame;
        double remainingBudget = budget - (Stopwatch.GetTimestamp() - batchStarted) * 1000d / Stopwatch.Frequency;
        if (simulation != null && destinationFrame > CurrentFrame && remainingBudget > 0d)
            simulation.AdvanceTo(RequestedTime, remainingBudget, false, beforeStep, afterStep,
                MaxStepsPerUpdate, deferPresentation: !WavePreviewProfiler.IntermediateVisuals);
        if (manualSeek) StepsProcessed += CurrentFrame - beforeAdvance;
        bool completedSeek = manualSeek && !IsSeeking;
        if (simulation != null)
        {
            if (!IsSeeking)
            {
                if (manualSeek) playhead = CurrentTime;
                manualSeek = false;
            }
            simulation.SetVisible(!IsLoading && EnemyShootingPreview.ShowFormationVolley);
            if (!IsLoading && (previous != CurrentFrame || catchingUp)) PublishRenderState();
        }
        if (completedSeek) FinishSeekMeasurement();
        if (MaxSpeed && IsPlaying && !manualSeek) playhead = CurrentTime;
        if (IsPlaying && CurrentFrame >= LastFrame) Pause();
        if (IsLoading || IsPlaying && (MaxSpeed || IsSeeking))
        {
            if (signalEditorTick != null) signalEditorTick();
            else UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
        return previous != CurrentFrame || catchingUp;
    }
    private void EnsureSimulation()
    {
        if (simulation != null) return;
        simulation = new WaveConductorShootingPreview(source, history.Evaluate(0), eventSink: Record,
            spawnSeed: NumericRandomPreviewControls.SeedOffset);
        simulation.DeferRenderCapture = true;
        simulation.EnableSnapshots();
        simulation.PrepareInitialState();
        snapshots.Capture(simulation, events, OmittedEvents); ApplyTarget(0);
        simulation.SetVisible(!IsLoading && EnemyShootingPreview.ShowFormationVolley);
    }
    private void ResetSimulation()
    {
        ReleaseSimulation();
        events.Clear(); OmittedEvents = 0; resetRequested = false;
        EnsureSimulation();
        restoreRequested = false; validThroughFrame = int.MaxValue;
    }
    private void ReleaseSimulation() { snapshots.Clear(false); simulation?.Dispose(); simulation = null; }
    private void BeginSeekMeasurement()
    {
        seekMeasured = true; seekStarted = Stopwatch.GetTimestamp(); seekFinished = 0;
        StepsProcessed = 0; SeekTargetTime = RequestedTime; seekStartFrame = CurrentFrame;
    }
    private void FinishSeekMeasurement()
    { if (seekMeasured && seekFinished == 0) seekFinished = Stopwatch.GetTimestamp(); }
    private void PublishRenderState()
    {
        simulation.PublishRenderState(); presentedFrame = CurrentFrame; PresentationVersion++;
    }
    private void ApplyTarget(int frame)
    { targetPosition = history.Evaluate(frame); if (simulation != null) simulation.TargetPosition = targetPosition; }
    private void CompletedStep(int frame)
    {
        ApplyTarget(frame);
        int interval = Mathf.Max(1, Mathf.RoundToInt(snapshotInterval / StepInterval));
        if (frame % interval == 0 && !WavePreviewProfiler.SkipNewSnapshots) snapshots.Capture(simulation, events, OmittedEvents);
    }
    private void Record(WavePreviewEvent observation)
    {
        if (events.Count < MaximumEvents) events.Add(observation); else OmittedEvents++;
    }
    public void Draw(UnityEditor.SceneView view) { if (!IsLoading) simulation?.Draw(view, false); }
    public void Dispose() { if (disposed) return; Stop(); history.Clear(targetPosition); disposed = true; }

    public static float BaseRouteDuration(Wave wave)
    {
        if (wave == null) return 0f;
        var warning = wave.GetComponent<WaveDangerWarningController>();
        float warningTime = warning != null && warning.ShouldPlayWarning ? warning.WarningDuration : 0f;
        float result = 0f;
        var cues = wave.ScheduledSubWaves;
        if (cues != null && cues.Count > 0)
        {
            for (int i = 0; i < cues.Count; i++)
                if (cues[i] != null) Include(cues[i].SubWavePrefab, cues[i].StartDelay);
        }
        else if (wave.SubWavesToCreate != null)
            for (int i = 0; i < wave.SubWavesToCreate.Count; i++) Include(wave.SubWavesToCreate[i], 0f);
        return result;
        void Include(GameObject prefab, float delay)
        {
            if (prefab == null || !prefab.TryGetComponent(out DirectedEnemySubWave subwave)) return;
            float route = subwave.GetSimulationBaseRouteDuration();
            if (float.IsFinite(route)) result = Mathf.Max(result, warningTime + delay + route);
        }
    }
}
