using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

// Execute one case per Pipeline request; files are written only under AgentArtifacts.
public static class ProfileWaveFastSeek
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] Paths = {
        "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
        "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [StructLayout(LayoutKind.Sequential)] private struct ProcessMemory
    { public uint size, faults; public UIntPtr peakWorkingSet, workingSet, peakPaged, paged, peakNonPaged, nonPaged, pagefile, peakPagefile; }
    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemory memory, uint size);
    private static long WorkingSet()
    { var info = new ProcessMemory { size = (uint)Marshal.SizeOf<ProcessMemory>() }; return GetProcessMemoryInfo(GetCurrentProcess(), ref info, info.size) ? (long)info.workingSet.ToUInt64() : -1; }
    [DllImport("kernel32.dll")] private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);
    private static double ThreadMilliseconds()
    { return GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? (kernel + user) / 10000d : double.NaN; }
    private static double Ms(long ticks) => ticks * 1000d / Stopwatch.Frequency;
    [Serializable] public class Distribution
    {
        public int count; public double mean, minimum, maximum, p50, p95, p99, total;
        public Distribution(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray(); count = sorted.Length;
            if (count == 0) return;
            total = sorted.Sum(); mean = total / count; minimum = sorted[0]; maximum = sorted[count - 1];
            p50 = sorted[(int)Math.Ceiling(count * .5) - 1]; p95 = sorted[(int)Math.Ceiling(count * .95) - 1]; p99 = sorted[(int)Math.Ceiling(count * .99) - 1];
        }
    }
    [Serializable] public class Category
    { public string name; public long calls, allocatedBytes, selfAllocatedBytes; public double inclusiveMs, selfMs, maximumMs; }
    [Serializable] public class Batch
    { public int frame, steps, projectiles, snapshots; public double milliseconds, gapMilliseconds, threadCpuMilliseconds; public long allocatedBytes; }
    [Serializable] public class Result
    {
        public string wave, variant; public int repeat, seed = 53, steps, peakProjectiles, finalProjectiles, snapshots, worldsAfterDispose;
        public double wallSeconds, activeMilliseconds, threadCpuMilliseconds, processCpuMilliseconds, waitingMilliseconds, initialWaitMilliseconds, stepsPerSecond, speed;
        public double restoreMs; public int restoreSteps, restoredFrame;
        public long omitted, allocatedMainThreadBytes, snapshotEstimateBytes, unityMemoryBefore, unityMemoryPeak, unityMemoryLive, unityMemoryAfterDispose;
        public long managedMemoryBefore, managedMemoryLive, workingSetPeak, workingSetLive;
        public int gc0, gc1, gc2;
        public string gcMarker; public double gcMarkerMilliseconds; public int gcMarkerSamples;
        public bool prefabUnchanged, worldCleanup;
        public Distribution stepMs, stepAllocations, batchMs, gapMs, stepsPerUpdate;
        public List<Category> categories = new(); public List<Batch> batches = new(); public double[] rawStepMs; public long[] rawStepBytes;
        public Dictionary<string, long> counters;
        public AllocationCapture nativeAllocations;
        public List<SnapshotMemory> snapshotMemory;
    }
    public sealed class SnapshotMemory
    { public int copies; public long unityBytes, monoUsedBytes, managedBytes, estimatedBytes; public double captureMs; }
    public sealed class AllocationCapture : IDisposable
    {
        public long stepBytes, playerBytes, gcOutsideBytes;
        public int capturedSteps, frames, missingFrames, gcAllocations;
        public List<long> bytesPerStep = new();
        public Dictionary<string, long> selfBytes = new();
        public Dictionary<string, double> unityMarkerMs = new();
        private readonly bool previousEnabled = ProfilerDriver.enabled, previousEditor = ProfilerDriver.profileEditor;
        private readonly bool previousDeep = ProfilerDriver.deepProfiling;
        private int nextFrame;
        public AllocationCapture()
        {
            ProfilerDriver.deepProfiling = false; ProfilerDriver.profileEditor = true; ProfilerDriver.enabled = true;
            nextFrame = ProfilerDriver.lastFrameIndex + 1;
        }
        public void Read()
        {
            int last = ProfilerDriver.lastFrameIndex;
            if (nextFrame < ProfilerDriver.firstFrameIndex) { missingFrames += ProfilerDriver.firstFrameIndex - nextFrame; nextFrame = ProfilerDriver.firstFrameIndex; }
            for (; nextFrame <= last; nextFrame++)
            {
                using var view = ProfilerDriver.GetRawFrameDataView(nextFrame, 0);
                if (!view.valid) continue;
                frames++;
                int gc = view.GetMarkerId("GC.Alloc");
                var ids = new Dictionary<int, string>();
                for (int a = 0; a < (int)WavePreviewProfiler.Area.Count; a++)
                { string name = "FastSeek." + (WavePreviewProfiler.Area)a; int id = view.GetMarkerId(name); if (id != -1) ids[id] = ((WavePreviewProfiler.Area)a).ToString(); }
                var nativeIds = new Dictionary<int, string>();
                foreach (string name in new[] { "GC.Collect", "GarbageCollectIncremental", "GetComponentNullErrorMessage", "GetComponentNullErrorWrapper", "SceneView.RepaintAll" })
                { int id = view.GetMarkerId(name); if (id != -1) nativeIds[id] = name; }
                var ends = new Stack<(int End, string Area)>();
                int stepEnd = -1, playerEnd = -1; long currentStep = 0;
                for (int i = 0; i < view.sampleCount; i++)
                {
                    if (i == stepEnd) { bytesPerStep.Add(currentStep); capturedSteps++; stepEnd = -1; }
                    while (ends.Count > 0 && i >= ends.Peek().End) ends.Pop();
                    int marker = view.GetSampleMarkerId(i);
                    if (nativeIds.TryGetValue(marker, out string nativeName))
                    { unityMarkerMs.TryGetValue(nativeName, out double old); unityMarkerMs[nativeName] = old + view.GetSampleTimeNs(i) / 1000000d; }
                    if (ids.TryGetValue(marker, out string area))
                    {
                        int end = i + view.GetSampleChildrenCountRecursive(i) + 1;
                        ends.Push((end, area));
                        if (area == "FixedStep") { stepEnd = end; currentStep = 0; }
                        if (area == "PlayerUpdate") playerEnd = end;
                    }
                    if (marker == gc && view.GetSampleMetadataCount(i) > 0)
                    {
                        long bytes = view.GetSampleMetadataAsLong(i, 0); gcAllocations++;
                        if (stepEnd >= 0 && i < stepEnd) { currentStep += bytes; stepBytes += bytes; }
                        if (i < playerEnd) playerBytes += bytes; else gcOutsideBytes += bytes;
                        if (ends.Count > 0) { string key = ends.Peek().Area; selfBytes.TryGetValue(key, out long old); selfBytes[key] = old + bytes; }
                    }
                }
                if (stepEnd == view.sampleCount) { bytesPerStep.Add(currentStep); capturedSteps++; }
            }
        }
        public void Dispose() { ProfilerDriver.enabled = previousEnabled; ProfilerDriver.profileEditor = previousEditor; ProfilerDriver.deepProfiling = previousDeep; }
    }
    public static object Discover()
    {
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        var rows = new List<object>();
        foreach (var handle in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(handle);
            if (d.Name.IndexOf("GC", StringComparison.OrdinalIgnoreCase) >= 0 || d.Name == "Main Thread")
                rows.Add(new { name = d.Name, category = d.Category.ToString(), unit = d.UnitType.ToString() });
        }
        File.WriteAllText("AgentArtifacts/fast-seek-profiler-counters.json", Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented));
        return new { counters = rows, unity = Application.unityVersion, command = "ProfileWaveFastSeek.Run(index, variant, repeat)", variants = "warmup/off/profile/noSnapshots/minimal/visuals/gui" };
    }
    private static ProfilerRecorder GcRecorder(out string name)
    {
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(handle);
            if (d.Name == "GC.Collect")
            { name = d.Name; return new ProfilerRecorder(handle, 4096, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame); }
        }
        name = "unavailable"; return default;
    }
    public static async Task<object> Run(int index, string variant, int repeat)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Profiling requires Edit Mode.");
        foreach (var window in Resources.FindObjectsOfTypeAll<WavePreviewPlayerWindow>()) window.Close();
        int originalSeed = NumericRandomPreviewControls.SeedOffset; NumericRandomPreviewControls.SetSeedOffset(53);
        string path = Paths[index]; byte[] before = File.ReadAllBytes(path); int worldCount = World.All.Count;
        var root = PrefabUtility.LoadPrefabContents(path);
        var result = new Result { wave = root.name, variant = variant, repeat = repeat };
        WavePreviewPlayer player = null;
        ProfilerRecorder gc = default;
        AllocationCapture native = null;
        try
        {
            WavePreviewProfiler.Reset();
            WavePreviewProfiler.SkipNewSnapshots = variant == "noSnapshots" || variant == "memory";
            WavePreviewProfiler.IntermediateVisuals = variant == "visuals";
            if (variant == "minimal") WavePreviewProfiler.ProjectileLimit = 0;
            var recording = variant == "off" || variant == "warmup" ? null : new WavePreviewProfiler.Recording();
            if (variant == "alloc") { native = new AllocationCapture(); WavePreviewProfiler.UnitySamples = true; }
            // Preallocate observer state before starting the timed interval.
            var batches = new List<Batch>(4000);
            player = new WavePreviewPlayer(root.GetComponent<Wave>(), 65, new Vector3(0, -3));
            result.gcMarker = "disabled";
            if (recording != null) gc = GcRecorder(out result.gcMarker);
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            result.unityMemoryBefore = Profiler.GetTotalAllocatedMemoryLong(); result.managedMemoryBefore = GC.GetTotalMemory(false);
            result.unityMemoryPeak = result.unityMemoryBefore;
            var process = Process.GetCurrentProcess(); double processStart = process.TotalProcessorTime.TotalMilliseconds;
            long start = Stopwatch.GetTimestamp(), previousEnd = start; bool first = true;
            player.Seek(60); WavePreviewProfiler.Current = recording;
            var done = new TaskCompletionSource<bool>();
            void Tick()
            {
                if (done.Task.IsCompleted) return;
                try
                {
                    native?.Read();
                    long batchStart = Stopwatch.GetTimestamp();
                    double gap = Ms(batchStart - previousEnd);
                    if (first) { result.initialWaitMilliseconds = gap; first = false; }
                    int frame = player.CurrentFrame; long allocation = GC.GetAllocatedBytesForCurrentThread(); double cpu = ThreadMilliseconds();
                    player.Update(0);
                    double cpuElapsed = ThreadMilliseconds() - cpu;
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
                    long end = Stopwatch.GetTimestamp();
                    int steps = player.CurrentFrame - frame;
                    int projectiles = player.Simulation.ProjectileCount;
                    result.peakProjectiles = Math.Max(result.peakProjectiles, projectiles);
                    batches.Add(new Batch { frame = player.CurrentFrame, steps = steps, projectiles = projectiles, snapshots = player.SnapshotCount,
                        milliseconds = Ms(end - batchStart), gapMilliseconds = gap, threadCpuMilliseconds = cpuElapsed, allocatedBytes = allocated });
                    if (batches.Count % 30 == 0 || !player.IsSeeking)
                    {
                        result.unityMemoryPeak = Math.Max(result.unityMemoryPeak, Profiler.GetTotalAllocatedMemoryLong());
                        result.workingSetPeak = Math.Max(result.workingSetPeak, WorkingSet());
                    }
                    // Includes measurement/repaint observer overhead in the waiting bucket, not simulation body.
                    previousEnd = end;
                    if (!player.IsSeeking) done.TrySetResult(true);
                }
                catch (Exception e) { done.TrySetException(e); }
            }
            EditorApplication.update += Tick;
            try { await done.Task; } finally { EditorApplication.update -= Tick; }
            long finish = previousEnd;
            WavePreviewProfiler.Current = null;
            result.processCpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds - processStart;
            result.wallSeconds = Ms(finish - start) / 1000d;
            result.batches = batches; result.steps = player.CurrentFrame;
            result.activeMilliseconds = batches.Sum(b => b.milliseconds); result.threadCpuMilliseconds = batches.Sum(b => b.threadCpuMilliseconds);
            result.waitingMilliseconds = result.wallSeconds * 1000d - result.activeMilliseconds;
            result.stepsPerSecond = result.steps / result.wallSeconds; result.speed = 60d / result.wallSeconds;
            result.allocatedMainThreadBytes = batches.Sum(b => b.allocatedBytes);
            result.batchMs = new Distribution(batches.Select(b => b.milliseconds)); result.gapMs = new Distribution(batches.Skip(1).Select(b => b.gapMilliseconds));
            result.stepsPerUpdate = new Distribution(batches.Select(b => (double)b.steps));
            result.finalProjectiles = player.Simulation.ProjectileCount; result.omitted = player.Simulation.Omitted;
            result.snapshots = player.SnapshotCount; result.snapshotEstimateBytes = player.SnapshotMemoryBytes;
            result.unityMemoryLive = Profiler.GetTotalAllocatedMemoryLong(); result.managedMemoryLive = GC.GetTotalMemory(false);
            result.workingSetLive = WorkingSet();
            result.gc0 = GC.CollectionCount(0) - g0; result.gc1 = GC.CollectionCount(1) - g1; result.gc2 = GC.CollectionCount(2) - g2;
            if (gc.Valid) gc.Stop();
            if (gc.Valid)
            {
                var samples = new List<ProfilerRecorderSample>(); gc.CopyTo(samples); result.gcMarkerSamples = samples.Count;
                result.gcMarkerMilliseconds = samples.Sum(s => s.Value) / 1000000d;
            }
            if (recording != null)
            {
                result.counters = new Dictionary<string, long>();
                for (int c = 0; c < recording.Counters.Length; c++) result.counters[((WavePreviewProfiler.Counter)c).ToString()] = recording.Counters[c];
                result.rawStepMs = recording.StepMilliseconds.Take(recording.Steps).ToArray(); result.rawStepBytes = recording.StepBytes.Take(recording.Steps).ToArray();
                result.stepMs = new Distribution(result.rawStepMs); result.stepAllocations = new Distribution(result.rawStepBytes.Select(b => (double)b));
                for (int i = 0; i < recording.Areas.Length; i++)
                {
                    var s = recording.Areas[i];
                    result.categories.Add(new Category { name = ((WavePreviewProfiler.Area)i).ToString(), calls = s.calls, inclusiveMs = Ms(s.ticks), selfMs = Ms(s.selfTicks),
                        maximumMs = Ms(s.maximumTicks), allocatedBytes = s.bytes, selfAllocatedBytes = s.selfBytes });
                }
            }
            if (native != null)
            {
                for (int flush = 0; flush < 10 && native.capturedSteps < 1800; flush++) { await Task.Delay(30); native.Read(); }
                result.nativeAllocations = native;
            }
            if (variant == "memory")
            {
                var extraSnapshots = new List<IDisposable>(); result.snapshotMemory = new();
                var capture = typeof(WaveConductorShootingPreview).GetMethod("CaptureSnapshot", F);
                long estimates = 0;
                void Sample(int count, double ms) => result.snapshotMemory.Add(new SnapshotMemory { copies = count,
                    unityBytes = Profiler.GetTotalAllocatedMemoryLong(), monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
                    managedBytes = GC.GetTotalMemory(false), estimatedBytes = estimates, captureMs = ms });
                GC.Collect(); Sample(0, 0);
                try
                {
                    for (int copy = 1; copy <= 8; copy++)
                    {
                        long startCopy = Stopwatch.GetTimestamp();
                        var snapshot = (IDisposable)capture.Invoke(player.Simulation, null); extraSnapshots.Add(snapshot);
                        double ms = Ms(Stopwatch.GetTimestamp() - startCopy);
                        estimates += (long)snapshot.GetType().GetProperty("EstimatedBytes").GetValue(snapshot);
                        Sample(copy, ms);
                    }
                }
                finally { foreach (var snapshot in extraSnapshots) snapshot.Dispose(); extraSnapshots.Clear(); GC.Collect(); Sample(-1, 0); }
            }
            // Restore is separately timed after the 0→60 measurement, never mixed into its budget.
            if (variant != "noSnapshots" && variant != "memory")
            {
                player.Seek(58); long restoreStart = Stopwatch.GetTimestamp(); player.Update(0, 10000);
                result.restoreMs = Ms(Stopwatch.GetTimestamp() - restoreStart); result.restoreSteps = player.StepsProcessed; result.restoredFrame = player.LastRestoreFrame;
            }
        }
        finally
        {
            WavePreviewProfiler.Reset(); native?.Dispose(); if (gc.Valid) gc.Dispose(); player?.Dispose();
            result.unityMemoryAfterDispose = Profiler.GetTotalAllocatedMemoryLong();
            PrefabUtility.UnloadPrefabContents(root); NumericRandomPreviewControls.SetSeedOffset(originalSeed);
            result.worldsAfterDispose = World.All.Count; result.worldCleanup = result.worldsAfterDispose == worldCount;
            result.prefabUnchanged = before.SequenceEqual(File.ReadAllBytes(path));
        }
        string output = $"AgentArtifacts/fast-seek-profile-{index}-{variant}-{repeat}.json";
        File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        if (!result.prefabUnchanged || !result.worldCleanup || result.steps != 1800) throw new Exception("Profiling invariant failed: " + output);
        return new { result.wave, variant, repeat, result.wallSeconds, result.activeMilliseconds, result.waitingMilliseconds,
            stepsPerUpdate = result.stepsPerUpdate.mean, stepMs = result.stepMs?.mean, result.gc0, result.snapshotEstimateBytes, result.worldCleanup, output };
    }
    public static async Task<object> Gui(int index)
    {
        foreach (var old in Resources.FindObjectsOfTypeAll<WavePreviewPlayerWindow>()) old.Close();
        int seed = NumericRandomPreviewControls.SeedOffset; NumericRandomPreviewControls.SetSeedOffset(53);
        int worlds = World.All.Count; var root = PrefabUtility.LoadPrefabContents(Paths[index]);
        WavePreviewPlayerWindow window = null; WavePreviewProfiler.Reset();
        try
        {
            window = WavePreviewPlayerWindow.Open(root.GetComponent<Wave>());
            void Set(string name, object value) => typeof(WavePreviewPlayerWindow).GetField(name, F).SetValue(window, value);
            Set("followSelection", false); Set("previewDuration", 65f); Set("simulationTimeBudget", 24f); Set("maxStepsPerUpdate", 2048);
            Set("snapshotInterval", 2f); Set("snapshotMemoryLimitMB", 128); Set("showAdvanced", true); Set("showSeekDiagnostics", true);
            var player = (WavePreviewPlayer)typeof(WavePreviewPlayerWindow).GetMethod("EnsurePlayer", F).Invoke(window, null);
            var recording = new WavePreviewProfiler.Recording(); WavePreviewProfiler.Current = recording;
            var done = new TaskCompletionSource<bool>(); int previousFrame = 0; long previousTicks = 0;
            long start = Stopwatch.GetTimestamp(); var stepsPerUpdate = new List<double>(); var batchMs = new List<double>();
            player.Seek(60);
            void Observe()
            {
                if (done.Task.IsCompleted) return;
                try
                {
                    long ticks = recording.Areas[(int)WavePreviewProfiler.Area.PlayerUpdate].ticks;
                    if (ticks != previousTicks)
                    { batchMs.Add(Ms(ticks - previousTicks)); stepsPerUpdate.Add(player.CurrentFrame - previousFrame); previousTicks = ticks; previousFrame = player.CurrentFrame; }
                    if (!player.IsSeeking) done.TrySetResult(true);
                }
                catch (Exception e) { done.TrySetException(e); }
            }
            EditorApplication.update += Observe;
            try { await done.Task; } finally { EditorApplication.update -= Observe; }
            double wall = Ms(Stopwatch.GetTimestamp() - start); WavePreviewProfiler.Current = null;
            var categories = new List<Category>();
            for (int i = 0; i < recording.Areas.Length; i++)
            { var s = recording.Areas[i]; categories.Add(new Category { name = ((WavePreviewProfiler.Area)i).ToString(), calls = s.calls, inclusiveMs = Ms(s.ticks), selfMs = Ms(s.selfTicks), maximumMs = Ms(s.maximumTicks) }); }
            var result = new { wave = root.name, wallMilliseconds = wall, activeMilliseconds = batchMs.Sum(), outsidePlayerMilliseconds = wall - batchMs.Sum(),
                steps = player.CurrentFrame, stepsPerUpdate = new Distribution(stepsPerUpdate), batchMs = new Distribution(batchMs),
                finalProjectiles = player.Simulation.ProjectileCount, omitted = player.Simulation.Omitted, categories };
            File.WriteAllText($"AgentArtifacts/fast-seek-profile-{index}-gui.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return new { result.wave, result.wallMilliseconds, result.activeMilliseconds, result.outsidePlayerMilliseconds, result.steps };
        }
        finally { WavePreviewProfiler.Reset(); window?.Close(); PrefabUtility.UnloadPrefabContents(root); NumericRandomPreviewControls.SetSeedOffset(seed); if (World.All.Count != worlds) throw new Exception("GUI profiling leaked worlds."); }
    }
}
