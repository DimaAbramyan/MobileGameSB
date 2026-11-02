using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class WaveConductorShootingPreview
{
    private WaveAttackCoordinator coordinator;
    private int coordinatorRevision;
    private bool coordinatorEnabled;
    private WaveAttackCoordinatorPlayer coordinatorPlayer;
    private readonly Dictionary<int, PreviewParticipant> coordinatorParticipants = new();
    public WaveAttackCoordinatorPlayer CoordinatorPlayer => coordinatorPlayer;
    private readonly Dictionary<WaveAttackCoordinatorPlayer.GroupState, string> observedGroups = new();
    private readonly List<WaveAttackCoordinatorPlayer.GroupState> finishedObservations = new();
    private void ObserveCoordinator()
    {
        if (eventSink == null || coordinatorPlayer == null) return;
        finishedObservations.Clear();
        foreach (var previous in observedGroups)
        {
            bool active = false;
            foreach (var group in coordinatorPlayer.ActiveGroups)
                if (ReferenceEquals(group, previous.Key)) { active = true; break; }
            if (active) continue;
            if (previous.Key.Status != previous.Value) RecordGroup(previous.Key);
            finishedObservations.Add(previous.Key);
        }
        for (int i = 0; i < finishedObservations.Count; i++) observedGroups.Remove(finishedObservations[i]);
        foreach (var group in coordinatorPlayer.ActiveGroups)
        {
            if (!observedGroups.TryGetValue(group, out string previous) || previous != group.Status)
            {
                RecordGroup(group);
                observedGroups[group] = group.Status;
            }
        }
    }
    private void RecordGroup(WaveAttackCoordinatorPlayer.GroupState group)
        => eventSink(new WavePreviewEvent(Time, WavePreviewEventKind.CoordinatorGroupChange,
            detail: $"{group.Index + 1}. {group.Settings.groupName}: {group.Status}"));
    private bool IsCoordinatorCurrent
    {
        get
        {
            var current = owner != null ? owner.GetComponent<WaveAttackCoordinator>() : null;
            return current == coordinator && (current == null || current.ConfigurationRevision == coordinatorRevision
                && IsCoordinatorEnabledForPreview(current) == coordinatorEnabled);
        }
    }
    internal static bool IsCoordinatorEnabledForPreview(WaveAttackCoordinator value)
    {
        if (value == null || !value.enabled || !value.Settings.enabled) return false;
        if (!EditorUtility.IsPersistent(value)) return value.isActiveAndEnabled;

        // Project prefab assets are never active in a scene. Use their authored
        // hierarchy state, as if the prefab had been instantiated for the preview.
        for (var parent = value.transform; parent != null; parent = parent.parent)
            if (!parent.gameObject.activeSelf) return false;
        return true;
    }
    private void InitializeCoordinator()
    {
        coordinator = owner.GetComponent<WaveAttackCoordinator>();
        coordinatorRevision = coordinator != null ? coordinator.ConfigurationRevision : 0;
        coordinatorEnabled = IsCoordinatorEnabledForPreview(coordinator);
        if (!coordinatorEnabled) return;
        var members = coordinatorParticipants;
        for (int i = 0; i < cues.Count; i++) members.Add(cues[i].Index, new PreviewParticipant(cues[i]));
        foreach (var group in coordinator.Settings.groups)
            if (group?.controlledSubWaves != null)
                foreach (var reference in group.controlledSubWaves)
                    if (reference != null && reference.subWavePrefab != null)
                        for (int i = 0; i < cues.Count; i++)
                            if (cues[i].Index == reference.scheduleIndex && cues[i].Wave.gameObject != reference.subWavePrefab)
                                members.Remove(reference.scheduleIndex);
        coordinatorPlayer = new WaveAttackCoordinatorPlayer(coordinator.Settings,
            index => members.TryGetValue(index, out var value) ? value : null);
        var warning = owner.GetComponent<WaveDangerWarningController>();
        coordinatorPlayer.Begin(warning != null && warning.ShouldPlayWarning ? warning.WarningDuration : 0f);
    }
    private sealed class PreviewParticipant : IWaveAttackCoordinatorParticipant
    {
        private readonly Cue cue;
        private object permissionOwner;
        private WaveAttackPermission permission;
        private int limit = -1;
        private WaveShootingPreviewSession bound;
        public PreviewParticipant(Cue cue) => this.cue = cue;
        public void ResetBinding() => bound = null;
        private WaveShootingPreviewSession Session
        {
            get
            {
                if (!cue.Started) return null;
                if (bound == cue.Session) return bound;
                bound = cue.Session;
                if (bound != null && permissionOwner != null)
                { bound.SetCycleLimit(limit); bound.SetPermission(permissionOwner, permission); }
                return bound;
            }
        }
        public WaveAttackParticipantState State => Session != null ? Session.State : WaveAttackParticipantState.Pending;
        public int CompletedCycles => Session?.CompletedCycles ?? 0;
        public bool IsIdle => Session == null || Session.IsIdle;
        public void SetPermission(object owner, WaveAttackPermission permission)
        { permissionOwner = owner; this.permission = permission; Session?.SetPermission(owner, permission); }
        public void ReleasePermission(object owner)
        { Session?.ReleasePermission(owner); permissionOwner = null; }
        public void SetCycleLimit(int limit) { this.limit = limit; Session?.SetCycleLimit(limit); }
        public void Reactivate(WaveAttackReactivation mode) => Session?.Reactivate(mode);
    }
    public void DrawCoordinatorInspectorStatus()
    {
        if (coordinatorPlayer == null) return;
        EditorGUILayout.LabelField("Attack Coordinator", EditorStyles.boldLabel);
        foreach (var state in coordinatorPlayer.ActiveGroups)
            EditorGUILayout.LabelField($"{state.Index + 1}. {state.Settings.groupName}", $"{state.Status} · {state.Remaining:0.00}s");
        EditorGUILayout.LabelField(coordinatorPlayer.IsComplete ? "Sequence completed" : coordinatorPlayer.NextGroupIndex < 0 ? "No pending groups" : $"Next group: {coordinatorPlayer.NextGroupIndex + 1}");
        if (coordinatorPlayer.Timeouts > 0) EditorGUILayout.HelpBox($"Coordinator timeouts: {coordinatorPlayer.Timeouts}", MessageType.Warning);
        for (int i = 0; i < cues.Count; i++)
            EditorGUILayout.LabelField($"SubWave {cues[i].Index + 1}: {cues[i].Wave.name}",
                cues[i].Session == null ? "Not spawned" : cues[i].Session.CoordinatorStatus);
    }
}
