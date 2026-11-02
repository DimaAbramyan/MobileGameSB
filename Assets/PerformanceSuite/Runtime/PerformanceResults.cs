#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Performance
{
    [Serializable] public sealed class PerformanceDistribution
    {
        public int count;
        public double mean, median, p95, p99, maximum, minimum, standardDeviation;
        public static PerformanceDistribution Calculate(IList<double> values)
        {
            var result = new PerformanceDistribution();
            var sorted = new List<double>(values.Count);
            for (int i = 0; i < values.Count; i++)
                if (!double.IsNaN(values[i]) && !double.IsInfinity(values[i])) sorted.Add(values[i]);
            result.count = sorted.Count;
            if (sorted.Count == 0) return result;
            sorted.Sort();
            double sum = 0;
            foreach (double value in sorted) sum += value;
            result.mean = sum / sorted.Count;
            result.minimum = sorted[0]; result.maximum = sorted[sorted.Count - 1];
            result.median = sorted.Count % 2 == 0 ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) * .5 : sorted[sorted.Count / 2];
            result.p95 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * .95) - 1)];
            result.p99 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * .99) - 1)];
            double squares = 0;
            foreach (double value in sorted) squares += (value - result.mean) * (value - result.mean);
            result.standardDeviation = sorted.Count > 1 ? Math.Sqrt(squares / (sorted.Count - 1)) : 0;
            return result;
        }
    }
    [Serializable] public sealed class PerformanceMetric
    {
        public string name, unit, source, unavailableReason;
        public bool available;
        public long observedInvocations;
        public PerformanceDistribution distribution;
    }
    [Serializable] public sealed class PerformanceMemoryPoint
    {
        public double seconds;
        public long allocatedBytes, reservedBytes, managedUsedBytes, managedHeapBytes;
    }
    [Serializable] public sealed class PerformanceRepeat
    {
        public string scenarioId, status = "running", workload, parametersJson;
        public int repetition, seed, peakProjectiles, peakEntities, peakEnemies, peakLasers, peakChildParents;
        public long spawnedProjectiles, destroyedProjectiles, omittedChildProjectiles;
        public int longFrames33, longFrames50, longFrames100, gc0, gc1, gc2;
        public int coordinatorTransitions;
        public int highestChildGeneration, shipSwitches, extraHealthGrants, enemyKills;
        public int worldsBefore, worldsAfter = -1, enemiesAfter = -1, projectilesAfter = -1;
        public double setupMilliseconds, cleanupMilliseconds, averageFps, sampledSeconds;
        public double memoryGrowthBytesPerSecond;
        public long memoryBefore, memoryPeak, memoryLive, memoryAfter;
        public bool forcedCollection, defaultWorldUnchanged, cleanupVerified;
        public bool memoryTrendAvailable;
        public PerformanceDistribution frameMilliseconds;
        public List<PerformanceMetric> metrics = new();
        public List<PerformanceMemoryPoint> memory = new();
        public List<string> anomalies = new();
        public List<double> frames = new();
    }
    [Serializable] public sealed class PerformanceRun
    {
        public int schemaVersion = 1;
        public string runId, startedUtc, unityVersion, gameVersion, platform, device, os, processor, gpu, graphicsApi;
        public string backend, buildKind, quality, configurationJson, status, notes;
        public int processorCount, systemMemoryMb, graphicsMemoryMb, targetFrameRate, vSync, width, height;
        public bool editor, rendering, profilerEnabled;
        public List<PerformanceRepeat> repeats = new();
    }

    public static class PerformanceReport
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        public static string OutputRoot => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "../AgentArtifacts/Performance"))
            : Path.Combine(Application.persistentDataPath, "Performance");

        public static string Save(PerformanceRun run)
        {
            string directory = Path.Combine(OutputRoot, run.runId);
            Directory.CreateDirectory(directory);
            Write(Path.Combine(directory, "results.json"), JsonUtility.ToJson(run, true));
            var csv = new StringBuilder("scenario,repeat,status,seed,samples,fps,mean_ms,median_ms,p95_ms,p99_ms,max_ms,memory_before,memory_after,spawned,destroyed\n");
            var frames = new StringBuilder("scenario,repeat,sample,frame_ms\n");
            var metrics = new StringBuilder("scenario,repeat,metric,unit,available,samples,mean,p95,p99,source,unavailable_reason\n");
            var memory = new StringBuilder("scenario,repeat,seconds,allocated_bytes,reserved_bytes,managed_used_bytes,managed_heap_bytes\n");
            var md = new StringBuilder("# Android Performance Suite\n\n");
            md.AppendLine($"Run: {run.runId}; **{run.platform} / {run.buildKind}**; {run.device}; Unity {run.unityVersion}.");
            md.AppendLine("\nEditor results are not Android device benchmarks. Unavailable metrics are not zeros.\n");
            md.AppendLine("| Scenario | Repeat | Status | FPS | Mean ms | P95 ms | P99 ms | Memory after MiB |\n|---|---:|---|---:|---:|---:|---:|---:|");
            foreach (var r in run.repeats)
            {
                var d = r.frameMilliseconds ?? new PerformanceDistribution();
                csv.AppendLine(string.Join(",", Q(r.scenarioId), r.repetition, Q(r.status), r.seed, d.count, N(r.averageFps), N(d.mean), N(d.median), N(d.p95), N(d.p99), N(d.maximum), r.memoryBefore, r.memoryAfter, r.spawnedProjectiles, r.destroyedProjectiles));
                md.AppendLine($"| {r.scenarioId} | {r.repetition} | {r.status} | {N(r.averageFps)} | {N(d.mean)} | {N(d.p95)} | {N(d.p99)} | {N(r.memoryAfter / 1048576d)} |");
                for (int i = 0; i < r.frames.Count; i++) frames.AppendLine($"{Q(r.scenarioId)},{r.repetition},{i},{N(r.frames[i])}");
                foreach (var m in r.metrics)
                {
                    var s = m.distribution ?? new PerformanceDistribution();
                    metrics.AppendLine($"{Q(r.scenarioId)},{r.repetition},{Q(m.name)},{Q(m.unit)},{m.available},{s.count},{N(s.mean)},{N(s.p95)},{N(s.p99)},{Q(m.source)},{Q(m.unavailableReason)}");
                }
                foreach (var p in r.memory) memory.AppendLine($"{Q(r.scenarioId)},{r.repetition},{N(p.seconds)},{p.allocatedBytes},{p.reservedBytes},{p.managedUsedBytes},{p.managedHeapBytes}");
            }
            foreach (var r in run.repeats)
                foreach (var a in r.anomalies) md.AppendLine($"\n- {r.scenarioId} / {r.repetition}: {a}");
            md.AppendLine("\nMemory retention alone is not proof of a leak. Compare stabilized repetitions, live counts and post-cleanup memory on the same device/build settings.");
            Write(Path.Combine(directory, "summary.csv"), csv.ToString());
            Write(Path.Combine(directory, "frames.csv"), frames.ToString());
            Write(Path.Combine(directory, "metrics.csv"), metrics.ToString());
            Write(Path.Combine(directory, "memory.csv"), memory.ToString());
            Write(Path.Combine(directory, "report.md"), md.ToString());
            return directory;
        }
        private static void Write(string path, string text)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }
        public static string Q(string value) => "\"" + (value ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        public static string N(double value) => value.ToString("0.###", C);

        public static string Compare(PerformanceRun before, PerformanceRun after)
        {
            var md = new StringBuilder("# Performance comparison\n\n");
            bool comparable = before.platform == after.platform && before.editor == after.editor && before.device == after.device
                && before.graphicsApi == after.graphicsApi && before.backend == after.backend && before.buildKind == after.buildKind
                && before.quality == after.quality && before.width == after.width && before.height == after.height
                && before.vSync == after.vSync && before.targetFrameRate == after.targetFrameRate && before.rendering == after.rendering
                && before.profilerEnabled == after.profilerEnabled;
            md.AppendLine(comparable ? "Environment metadata matches. Workload/seed matches are checked per scenario." : "**Environments differ: descriptive comparison only; no regression verdict.**");
            md.AppendLine("\n| Scenario | Metric | Before mean ± SD | After mean ± SD | Delta % | Interpretation |\n|---|---|---:|---:|---:|---|");
            var ids = new HashSet<string>();
            foreach (var r in before.repeats) ids.Add(r.scenarioId);
            foreach (string id in ids)
            {
                var b = Successful(before, id); var a = Successful(after, id);
                if (b.Count == 0 || a.Count == 0) { md.AppendLine($"| {id} | all | — | — | — | No matching successful repeats |"); continue; }
                bool workloadMatches = b[0].parametersJson == a[0].parametersJson && b[0].seed == a[0].seed;
                CompareMetric(md, id, "FPS", b, a, r => r.averageFps, comparable && workloadMatches);
                CompareMetric(md, id, "mean frame ms", b, a, r => r.frameMilliseconds.mean, comparable && workloadMatches);
                CompareMetric(md, id, "P95 frame ms", b, a, r => r.frameMilliseconds.p95, comparable && workloadMatches);
                CompareMetric(md, id, "memory after bytes", b, a, r => r.memoryAfter, comparable && workloadMatches);
                var names = new HashSet<string>();
                foreach (var m in b[0].metrics) if (m.available) names.Add(m.name);
                foreach (string name in names)
                {
                    bool all = true;
                    foreach (var r in b) if (FindMetric(r, name) == null) all = false;
                    foreach (var r in a) if (FindMetric(r, name) == null) all = false;
                    if (all) CompareMetric(md, id, name, b, a, r => FindMetric(r, name).distribution.mean, comparable && workloadMatches);
                }
            }
            md.AppendLine("\nSD describes repeat-to-repeat spread. A difference exceeding 2 combined SD is a diagnostic candidate, not a statistical proof or automatic optimization claim. At least 3 successful repetitions per side are required for that label.");
            return md.ToString();
        }
        private static PerformanceMetric FindMetric(PerformanceRepeat r, string name) => r.metrics.Find(m => m.available && m.name == name);
        private static List<PerformanceRepeat> Successful(PerformanceRun run, string id) => run.repeats.FindAll(r => r.scenarioId == id && r.status == "completed" && r.frameMilliseconds != null && r.frameMilliseconds.count > 0);
        private static void CompareMetric(StringBuilder md, string id, string metric, List<PerformanceRepeat> b, List<PerformanceRepeat> a, Func<PerformanceRepeat, double> read, bool comparable)
        {
            var bv = new List<double>(); var av = new List<double>();
            foreach (var r in b) bv.Add(read(r)); foreach (var r in a) av.Add(read(r));
            var bd = PerformanceDistribution.Calculate(bv); var ad = PerformanceDistribution.Calculate(av);
            double difference = ad.mean - bd.mean;
            string verdict = !comparable ? "Different workload/environment" : b.Count < 3 || a.Count < 3 ? "Insufficient repetitions" : Math.Abs(difference) > 2 * Math.Sqrt(bd.standardDeviation * bd.standardDeviation + ad.standardDeviation * ad.standardDeviation) ? "Investigate difference" : "Within observed spread";
            md.AppendLine($"| {id} | {metric} | {N(bd.mean)} ± {N(bd.standardDeviation)} | {N(ad.mean)} ± {N(ad.standardDeviation)} | {(bd.mean == 0 ? "n/a" : N(difference / bd.mean * 100))} | {verdict} |");
        }
    }
}
#endif
