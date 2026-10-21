#if UNITY_EDITOR
using System;
using System.Diagnostics;

// Opt-in, main-thread diagnostics. Never participates in simulation state or RNG.
public static class WavePreviewProfiler
{
    public enum Area
    {
        PlayerUpdate, FixedStep, InitializeCue, Coordinator, SessionTick, Positions,
        WaveEvaluate, PostPipeline, PostCommand, Entrance, CheckpointPath, Formation,
        AttackSequence, AttackGroup, AttackSlot, QueueVolley, EmitDirection, EmitterGeometry, Facing,
        ProjectileStep, Spawn, BurstBounds, Homing, Burst, SpeedCurve, SizeCurve, Movement, Lifetime,
        Laser, LaserGeometry, Child, ChildTriggerUpdate, ChildVolley, ChildEmit, Cleanup, BurstSpawn,
        SnapshotCapture, SnapshotManaged, SnapshotNative, SnapshotVisuals, SnapshotRestore, CacheTrim,
        Publish, PublishVisuals, CaptureRender, Visibility, WindowGUI, SceneGUI, Timeline, Count
    }
    public enum Counter { FormationResolve, FormationLookupMiss, PhaseResolve, PhaseLookupMiss, PostResolve, PostLookupMiss,
        CompletionResolve, CompletionLookupMiss, LegacyFormation, LegacyEntry, LegacyPost }
    public sealed class Sample
    {
        public long calls, ticks, selfTicks, bytes, selfBytes;
        public long maximumTicks;
    }
    private struct Frame { public int area; public long start, allocation, children, childBytes; }
    public sealed class Recording
    {
        public readonly Sample[] Areas = new Sample[(int)Area.Count];
        public readonly double[] StepMilliseconds = new double[10000];
        public readonly long[] StepBytes = new long[10000];
        public int Steps;
        public readonly long[] Counters = new long[Enum.GetValues(typeof(Counter)).Length];
        private readonly Frame[] stack = new Frame[256];
        private int depth;
        public Recording() { for (int i = 0; i < Areas.Length; i++) Areas[i] = new Sample(); }
        internal void Begin(Area area)
        {
            if (depth == stack.Length) throw new InvalidOperationException("Profiling stack overflow.");
            stack[depth++] = new Frame { area = (int)area, start = Stopwatch.GetTimestamp(), allocation = GC.GetAllocatedBytesForCurrentThread() };
            if (UnitySamples) UnityEngine.Profiling.Profiler.BeginSample(markerNames[(int)area]);
        }
        internal void End()
        {
            long end = Stopwatch.GetTimestamp(), allocation = GC.GetAllocatedBytesForCurrentThread();
            if (UnitySamples) UnityEngine.Profiling.Profiler.EndSample();
            var frame = stack[--depth];
            long ticks = end - frame.start, bytes = allocation - frame.allocation;
            var sample = Areas[frame.area]; sample.calls++; sample.ticks += ticks;
            sample.selfTicks += ticks - frame.children; sample.bytes += bytes; sample.selfBytes += bytes - frame.childBytes;
            sample.maximumTicks = Math.Max(sample.maximumTicks, ticks);
            if (depth > 0) { stack[depth - 1].children += ticks; stack[depth - 1].childBytes += bytes; }
            if (frame.area == (int)Area.FixedStep && Steps < StepMilliseconds.Length)
            { StepMilliseconds[Steps] = ticks * 1000d / Stopwatch.Frequency; StepBytes[Steps++] = bytes; }
        }
    }
    public readonly struct Scope : IDisposable
    {
        private readonly Recording recording;
        internal Scope(Recording recording, Area area) { this.recording = recording; recording?.Begin(area); }
        public void Dispose() => recording?.End();
    }
    public static Recording Current { get; set; }
    private static readonly string[] markerNames = MakeMarkerNames();
    private static string[] MakeMarkerNames()
    { var result = new string[(int)Area.Count]; for (int i = 0; i < result.Length; i++) result[i] = "FastSeek." + (Area)i; return result; }
    public static bool UnitySamples { get; set; }
    public static void Count(Counter counter) { if (Current != null) Current.Counters[(int)counter]++; }
    // Harness-only isolation switches. Defaults preserve the normal Preview path.
    public static bool SkipNewSnapshots { get; set; }
    public static bool IntermediateVisuals { get; set; }
    public static int ProjectileLimit { get; set; } = -1;
    public static int EffectiveProjectileLimit(int normal) => ProjectileLimit < 0 ? normal : Math.Min(normal, ProjectileLimit);
    public static Scope Measure(Area area) => new(Current, area);
    public static void Reset() { Current = null; SkipNewSnapshots = IntermediateVisuals = UnitySamples = false; ProjectileLimit = -1; }
}
#endif
