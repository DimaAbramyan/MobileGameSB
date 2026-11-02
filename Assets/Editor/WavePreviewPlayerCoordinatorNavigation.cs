using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public sealed partial class WavePreviewPlayer
{
    // A request is cooperative, cancelable and bounded even for infinite groups.
    private const int MaximumCoordinatorSearchSteps = 18000;
    private const double MaximumCoordinatorSearchSeconds = 10d;
    private readonly WavePreviewCoordinatorEventIndex coordinatorEvents = new();
    private readonly WavePreviewCoordinatorObserver coordinatorObserver = new();
    private readonly WavePreviewCoordinatorObserver searchObserver = new();
    private WaveConductorShootingPreview coordinatorSearch;
    private int searchDirection, searchAnchor, searchSteps;
    private long searchStarted;
    private Action<int> beforeSearchStep, afterSearchStep;
    private Func<bool> stopSearchStep;
    private WavePreviewCoordinatorEvent foundCoordinatorEvent;

    public IReadOnlyList<WavePreviewCoordinatorEvent> CoordinatorEvents => coordinatorEvents.Events;
    public WavePreviewCoordinatorEvent SelectedCoordinatorEvent { get; private set; }
    public string CoordinatorNavigationMessage { get; private set; }
    public bool IsFindingCoordinatorEvent => searchDirection != 0;
    public float CoordinatorSearchTime => coordinatorSearch?.Time ?? 0f;
    public bool HasCoordinator => HasEnabledCoordinator(source);
    public static bool HasEnabledCoordinator(Wave wave) => wave != null
        && wave.TryGetComponent(out WaveAttackCoordinator coordinator)
        && WaveConductorShootingPreview.IsCoordinatorEnabledForPreview(coordinator);

    public bool PreviousCoordinatorEvent() => NavigateCoordinatorEvent(-1);
    public bool NextCoordinatorEvent() => NavigateCoordinatorEvent(1);
    private bool NavigateCoordinatorEvent(int direction)
    {
        if (disposed || !HasCoordinator || IsLoading || IsFindingCoordinatorEvent) return false;
        if (simulation != null && !simulation.IsConfigurationCurrent) { Invalidate(); return false; }
        Pause();
        searchAnchor = CurrentFrame;
        CoordinatorNavigationMessage = null;
        if (TryFindCoordinatorEvent(direction, out var known)) { SeekCoordinatorEvent(known); return true; }
        if (direction < 0 && coordinatorEvents.ScannedThroughFrame >= searchAnchor - 1
            || direction > 0 && searchAnchor >= LastFrame)
        { CoordinatorNavigationMessage = direction < 0 ? "No previous Coordinator event." : "No next Coordinator event in Preview Duration."; return false; }
        searchDirection = direction; searchSteps = 0; searchStarted = Stopwatch.GetTimestamp();
        return true;
    }
    public void SeekCoordinatorEvent(WavePreviewCoordinatorEvent observation)
    {
        if (observation == null || !HasCoordinator || observation.Frame > LastFrame || !coordinatorEvents.Contains(observation)) return;
        if (simulation != null && !simulation.IsConfigurationCurrent) { Invalidate(); return; }
        Pause(); Seek(observation.StateTime); SelectedCoordinatorEvent = observation;
        CoordinatorNavigationMessage = null;
    }
    public void CancelCoordinatorEventNavigation()
    {
        CancelCoordinatorSearch(true);
        CoordinatorNavigationMessage = "Coordinator event search canceled.";
    }
    private void CancelCoordinatorSearch(bool release = false)
    {
        searchDirection = 0; foundCoordinatorEvent = null;
        if (!release) return;
        coordinatorSearch?.Dispose(); coordinatorSearch = null; searchObserver.Reset(null);
    }
    private void InvalidateCoordinatorEventsAfter(int frame)
    {
        CancelCoordinatorSearch(true); coordinatorEvents.InvalidateAfter(frame);
        SelectedCoordinatorEvent = null; CoordinatorNavigationMessage = null;
    }
    private void ClearCoordinatorEvents()
    {
        CancelCoordinatorSearch(true); coordinatorEvents.Clear(); coordinatorObserver.Reset(null);
        SelectedCoordinatorEvent = null; CoordinatorNavigationMessage = null;
    }
    private bool TryFindCoordinatorEvent(int direction, out WavePreviewCoordinatorEvent observation)
    {
        observation = coordinatorEvents.Find(searchAnchor, direction);
        return observation != null && observation.Frame <= LastFrame
            && coordinatorEvents.ScannedThroughFrame >= (direction < 0 ? searchAnchor - 1 : observation.Frame);
    }
    private void ObserveSearchStep(int frame)
    {
        searchSteps++;
        searchObserver.Observe(coordinatorSearch, source, frame, coordinatorEvents);
        if (TryFindCoordinatorEvent(searchDirection, out var next)) foundCoordinatorEvent = next;
    }
    private bool SearchStepComplete() => foundCoordinatorEvent != null || coordinatorEvents.LimitReached
        || searchSteps >= MaximumCoordinatorSearchSteps || coordinatorSearch.CoordinatorPlayer?.IsComplete == true
        || searchDirection < 0 && coordinatorEvents.ScannedThroughFrame >= searchAnchor - 1;

    private bool UpdateCoordinatorSearch(double budget)
    {
        if (!HasCoordinator) { CancelCoordinatorSearch(true); return true; }
        if (coordinatorSearch != null && !coordinatorSearch.IsConfigurationCurrent) { Invalidate(); return true; }
        if (coordinatorSearch == null)
        {
            // Mementos are bound to their original world. Search in an independent
            // instance of the same conductor, never restore/mutate the visible one.
            coordinatorSearch = new WaveConductorShootingPreview(source, history.Evaluate(0),
                spawnSeed: NumericRandomPreviewControls.SeedOffset);
            coordinatorSearch.DeferRenderCapture = true;
            coordinatorSearch.SetVisible(false); coordinatorSearch.PrepareInitialState(); coordinatorSearch.SetVisible(false);
            searchObserver.Reset(coordinatorSearch);
            beforeSearchStep ??= frame => coordinatorSearch.TargetPosition = history.Evaluate(frame);
            afterSearchStep ??= ObserveSearchStep;
            stopSearchStep ??= SearchStepComplete;
        }
        coordinatorSearch.AdvanceTo(LastFrame * StepInterval, budget, false, beforeSearchStep, afterSearchStep,
            Math.Min(MaxStepsPerUpdate, Math.Max(1, MaximumCoordinatorSearchSteps - searchSteps)),
            deferPresentation: true, stopAfterStep: stopSearchStep);
        coordinatorSearch.SetVisible(false);
        if (foundCoordinatorEvent != null)
        {
            var selected = foundCoordinatorEvent; CancelCoordinatorSearch(); SeekCoordinatorEvent(selected);
            return true;
        }
        bool finished = coordinatorSearch.StepCount >= LastFrame || coordinatorSearch.CoordinatorPlayer?.IsComplete == true
            || searchDirection < 0 && coordinatorEvents.ScannedThroughFrame >= searchAnchor - 1;
        bool limited = searchSteps >= MaximumCoordinatorSearchSteps
            || (Stopwatch.GetTimestamp() - searchStarted) / (double)Stopwatch.Frequency >= MaximumCoordinatorSearchSeconds;
        if (finished || limited || coordinatorEvents.LimitReached)
        {
            CoordinatorNavigationMessage = coordinatorEvents.LimitReached ? "Coordinator event history limit reached."
                : limited ? $"Search limit reached at {coordinatorSearch.Time:0.00}s. Press Next Event to continue."
                : $"No {(searchDirection > 0 ? "next" : "previous")} Coordinator event through {coordinatorSearch.Time:0.00}s (Preview Duration / completed sequence).";
            CancelCoordinatorSearch();
        }
        else if (signalEditorTick != null) signalEditorTick();
        else UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        return true;
    }
}
