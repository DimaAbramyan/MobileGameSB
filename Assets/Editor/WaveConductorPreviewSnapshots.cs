using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WaveConductorShootingPreview
{
    private bool snapshotsEnabled;
    internal void EnableSnapshots()
    {
        _ = projectiles.SnapshotManagedRoot;
        snapshotsEnabled = true;
        foreach (var cue in cues)
            if (cue.Session != null && cue.InitialState == null) CaptureInitialSession(cue);
    }
    private static void CaptureInitialSession(Cue cue)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotManaged);
        cue.InitialState = WavePreviewStateGraph.Capture(cue.Session);
        cue.InitialVisuals = cue.Session.CaptureVisuals();
    }
    internal sealed class Snapshot : IDisposable
    {
        private readonly WaveConductorShootingPreview owner;
        private readonly EnemyProjectilePreviewWorld.Snapshot projectiles;
        private readonly WavePreviewStateGraph managed;
        private readonly bool[] started;
        private readonly List<WaveShootingPreviewSession.VisualState> visuals = new();
        private readonly string warning;
        public int Frame { get; }
        public long EstimatedBytes { get; }
        internal Snapshot(WaveConductorShootingPreview owner)
        {
            using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotManaged);
            this.owner = owner; Frame = owner.StepCount; warning = owner.Warning;
            started = new bool[owner.cues.Count];
            var roots = new List<object> { owner.coordinatorPlayer, owner.observedGroups,
                owner.finishedObservations, owner.projectiles.SnapshotManagedRoot };
            for (int i = 0; i < started.Length; i++)
            {
                var cue = owner.cues[i]; started[i] = cue.Started;
                if (!cue.Started) continue;
                roots.Add(cue.Session); visuals.Add(cue.Session.CaptureVisuals());
            }
            managed = WavePreviewStateGraph.Capture(roots.ToArray());
            projectiles = new EnemyProjectilePreviewWorld.Snapshot(owner.projectiles);
            EstimatedBytes = projectiles.EstimatedBytes + managed.EstimatedBytes;
            foreach (var state in visuals) EstimatedBytes += state.EstimatedBytes;
        }
        internal void Restore()
        {
            using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotRestore);
            if (!owner.IsConfigurationCurrent) throw new InvalidOperationException("Preview snapshot configuration has changed.");
            var remap = projectiles.Restore(owner.projectiles);
            managed.Restore(remap);
            owner.StepCount = Frame; owner.Time = Frame * WaveShootingPreviewSession.StepInterval;
            owner.Warning = warning;
            for (int i = 0; i < started.Length; i++)
            {
                var cue = owner.cues[i]; cue.Started = started[i];
                if (!cue.Started && cue.Session != null)
                {
                    cue.InitialState.Restore(remap); cue.InitialVisuals.Restore();
                }
            }
            foreach (var state in visuals) state.Restore();
            foreach (var participant in owner.coordinatorParticipants.Values) participant.ResetBinding();
            owner.projectiles.RestoreClock(owner.Time);
            if (!owner.DeferRenderCapture) owner.PublishRenderState();
        }
        public void Dispose() => projectiles.Dispose();
    }
    internal Snapshot CaptureSnapshot() => new(this);
}

public sealed partial class WaveShootingPreviewSession
{
    internal sealed class VisualState
    {
        private readonly struct Pose
        {
            public readonly Transform Target;
            public readonly Vector3 Position, Scale;
            public readonly Quaternion Rotation;
            public readonly bool Active;
            public Pose(VisualPose pose)
            { Target = pose.Target; Position = pose.Position; Rotation = pose.Rotation; Scale = Target.localScale; Active = pose.Visible; }
            public void Restore()
            { Target.SetPositionAndRotation(Position, Rotation); Target.localScale = Scale; Target.gameObject.SetActive(Active); }
        }
        private readonly List<Pose> poses = new();
        public long EstimatedBytes => poses.Count * 80L;
        public VisualState(WaveShootingPreviewSession session)
        {
            using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.SnapshotVisuals);
            foreach (var pose in session.visualPoses.Values) poses.Add(new Pose(pose));
        }
        public void Restore() { foreach (var pose in poses) pose.Restore(); }
    }
    internal VisualState CaptureVisuals() => new(this);
}
