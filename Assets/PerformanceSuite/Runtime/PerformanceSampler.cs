#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Performance
{
    public sealed class PerformanceSampler : IDisposable
    {
        private sealed class Counter : IDisposable
        {
            public string name, unit;
            public double multiplier;
            public long invocations;
            public ProfilerRecorder recorder;
            public readonly List<double> values;
            public Counter(int capacity) { values = new List<double>(capacity); }
            public void Dispose() { recorder.Dispose(); }
        }
        private readonly List<Counter> counters = new();
        private readonly List<double> cpu = new(), gpu = new();
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly PerformanceRepeat result;
        private readonly int sampleLimit;
        private readonly float memoryInterval;
        private int gc0, gc1, gc2;
        private double nextMemory;
        private ulong lastTiming;
        private bool disposed;
        private readonly bool frameTimingEnabled;

        public PerformanceSampler(PerformanceRepeat result, int limit, float interval, float expectedSeconds)
        {
            this.result = result; sampleLimit = limit; memoryInterval = interval;
            frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            int capacity = Math.Min(limit, Math.Max(256, (int)(expectedSeconds * 240)));
            result.frames.Capacity = capacity;
            gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(handle);
                bool system = d.Name.StartsWith("Android Performance Suite ", StringComparison.Ordinal)
                    && (d.Name.Contains("Projectile") || d.Name.Contains("EnemyChildAttackSystem") || d.Name.Contains("EnemyLaserSystem"));
                bool selected = d.Name == "Main Thread" || d.Name == "Render Thread" || d.Name == "GC.Alloc"
                    || d.Name == "GC.Collect" || d.Name == "GC.CollectIncremental" || d.Name == "Native Used Memory" || d.Name == "GC Allocated In Frame"
                    || d.Name == "Structural Changes" || d.Name == "World.EntityManager.StructuralChanges"
                    || d.Name.StartsWith("PerformanceSuite.", StringComparison.Ordinal) || system;
                if (!selected || counters.Count >= 48) continue;
                if (counters.Exists(c => c.name == d.Name)) continue;
                string unit = d.UnitType.ToString();
                var counter = new Counter(capacity)
                {
                    name = d.Name, unit = unit == "TimeNanoseconds" ? "ms" : unit,
                    multiplier = unit == "TimeNanoseconds" ? .000001 : 1,
                    recorder = ProfilerRecorder.StartNew(d.Category, d.Name, 1)
                };
                counters.Add(counter);
            }
        }

        public bool Sample(double frameMilliseconds, double seconds)
        {
            if (result.frames.Count >= sampleLimit) return false;
            result.frames.Add(frameMilliseconds);
            if (frameMilliseconds > 33.333333) result.longFrames33++;
            if (frameMilliseconds > 50) result.longFrames50++;
            if (frameMilliseconds > 100) result.longFrames100++;
            foreach (var counter in counters)
                if (counter.recorder.Valid && counter.recorder.Count > 0)
                {
                    var sample = counter.recorder.GetSample(counter.recorder.Count - 1);
                    counter.invocations += sample.Count;
                    counter.values.Add(sample.Value * counter.multiplier);
                }
            if (frameTimingEnabled) FrameTimingManager.CaptureFrameTimings();
            uint count = frameTimingEnabled ? FrameTimingManager.GetLatestTimings(1, timings) : 0;
            if (count > 0 && timings[0].frameStartTimestamp != lastTiming)
            {
                lastTiming = timings[0].frameStartTimestamp;
                if (ValidTiming(timings[0].cpuFrameTime)) cpu.Add(timings[0].cpuFrameTime);
                if (ValidTiming(timings[0].gpuFrameTime)) gpu.Add(timings[0].gpuFrameTime);
            }
            if (seconds >= nextMemory)
            {
                nextMemory = seconds + memoryInterval;
                result.memory.Add(Memory(seconds));
                result.memoryPeak = Math.Max(result.memoryPeak, result.memory[result.memory.Count - 1].allocatedBytes);
            }
            return true;
        }
        public void BeginMeasurement()
        {
            gc0 = GC.CollectionCount(0); gc1 = GC.CollectionCount(1); gc2 = GC.CollectionCount(2);
            // Unity Reset stops collection as well as clearing samples.
            foreach (var counter in counters) { counter.recorder.Reset(); counter.recorder.Start(); }
        }
        public int ActiveRecorderCount
        {
            get { int count = 0; foreach (var c in counters) if (c.recorder.Valid && c.recorder.IsRunning) count++; return count; }
        }
        public static PerformanceMemoryPoint Memory(double seconds) => new()
        {
            seconds = seconds,
            allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = Profiler.GetTotalReservedMemoryLong(),
            managedUsedBytes = Profiler.GetMonoUsedSizeLong(), managedHeapBytes = Profiler.GetMonoHeapSizeLong()
        };
        public void Finish()
        {
            result.frameMilliseconds = PerformanceDistribution.Calculate(result.frames);
            double total = 0;
            foreach (double value in result.frames) total += value;
            result.sampledSeconds = total / 1000;
            result.averageFps = total > 0 ? result.frames.Count * 1000d / total : 0;
            result.gc0 = GC.CollectionCount(0) - gc0; result.gc1 = GC.CollectionCount(1) - gc1; result.gc2 = GC.CollectionCount(2) - gc2;
            foreach (var c in counters)
            {
                bool available = c.values.Count > 0 && (c.unit != "ms" || c.invocations > 0);
                result.metrics.Add(new PerformanceMetric { name = c.name, unit = c.unit, source = "ProfilerRecorder previous completed frame; inclusive marker time",
                    available = available, observedInvocations = c.invocations,
                    unavailableReason = available ? "" : "No marker invocations observed; Burst/platform capture may not expose timing. No zero-cost claim.",
                    distribution = available ? PerformanceDistribution.Calculate(c.values) : new PerformanceDistribution() });
            }
            AddTiming("CPU Frame Time", cpu); AddTiming("GPU Frame Time", gpu);
            string[] required = { "Main Thread", "Render Thread", "GC.Alloc", "GC Allocated In Frame", "Native Used Memory", "GC.Collect", "Structural Changes", "Structural Changes Count" };
            foreach (string name in required)
                if (!result.metrics.Exists(m => m.name == name))
                    result.metrics.Add(new PerformanceMetric { name = name, unit = "unavailable", available = false,
                        unavailableReason = "No matching official profiler counter/marker discovered; no estimate substituted" });
            // Late-half linear regression is a trend diagnostic, not a leak verdict.
            int start = result.memory.Count / 2, n = result.memory.Count - start;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = start; i < result.memory.Count; i++)
            { var p = result.memory[i]; sx += p.seconds; sy += p.allocatedBytes; sxx += p.seconds * p.seconds; sxy += p.seconds * p.allocatedBytes; }
            double denominator = n * sxx - sx * sx;
            result.memoryTrendAvailable = n >= 3 && denominator > 0;
            if (result.memoryTrendAvailable) result.memoryGrowthBytesPerSecond = (n * sxy - sx * sy) / denominator;
            if (result.memoryGrowthBytesPerSecond > 1048576)
                result.anomalies.Add("Hypothesis: late-half allocated memory growth >1 MiB/s; inspect post-cleanup repetitions/caches before calling this a leak.");
            if (result.frames.Count > 120)
            {
                int half = result.frames.Count / 2;
                var first = result.frames.GetRange(0, half); var last = result.frames.GetRange(half, result.frames.Count - half);
                if (PerformanceDistribution.Calculate(last).p95 > PerformanceDistribution.Calculate(first).p95 * 1.25)
                    result.anomalies.Add("Hypothesis: second-half P95 frame time increased >25%; correlate object counts, GC and thermal state.");
            }
        }
        private void AddTiming(string name, List<double> values) => result.metrics.Add(new PerformanceMetric
        { name = name, unit = "ms", available = values.Count > 0, source = "FrameTimingManager, distinct frameStartTimestamp", distribution = PerformanceDistribution.Calculate(values),
            unavailableReason = values.Count == 0 ? "Frame timing disabled, unsupported, stale, or outside physical validation range; no estimate substituted" : "" });
        public static bool ValidTiming(double value) => double.IsFinite(value) && value > 0 && value <= 10000;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var c in counters) c.Dispose();
        }
    }
}
#endif
