using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

public static class ObservePlayModeFreezes
{
    public static async Task<object> Run()
    {
        if (!EditorApplication.isPlaying) return new { error = "PlayMode is not running" };
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var names = new List<string>();
        var units = new List<string>();
        var recorders = new List<ProfilerRecorder>();
        foreach (var handle in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(handle);
            bool selected = d.Name == "Main Thread" || d.Name == "GC.Alloc" || d.Name == "GC.Collect"
                || d.Name == "GC.Incremental.Collect" || d.Name == "PlayerLoop" || d.Name == "EditorLoop"
                || d.Name == "Gfx.WaitForPresentOnGfxThread" || d.Name == "WaitForTargetFPS"
                || d.Name == "WaitForJobGroupID" || d.Name == "Shader.CreateGPUProgram"
                || d.Name == "Instantiate" || d.Name == "Destroy"
                || d.Name == "Physics2D.Simulate" || d.Name == "Physics2D.OverlapCircle"
                || (d.Category.Name == "Scripts" && d.Name.StartsWith("Default World ") && d.Name.Contains("Projectile"));
            if (!selected) continue;
            var recorder = ProfilerRecorder.StartNew(d.Category, d.Name, 1);
            if (!recorder.Valid) { recorder.Dispose(); continue; }
            names.Add(d.Name); units.Add(d.UnitType.ToString()); recorders.Add(recorder);
        }
        var maximum = new long[recorders.Count];
        var totals = new double[recorders.Count];
        var spikeValues = new long[64, recorders.Count];
        var spikeMs = new double[64];
        var spikeFrames = new int[64];
        var spikeUtc = new string[64];
        int frames = 0, spikes = 0, observedFrame = Time.frameCount;
        double previous = EditorApplication.timeSinceStartup;
        double start = previous;
        DateTime startedUtc = DateTime.UtcNow;
        int[] collectionsBefore = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        var done = new TaskCompletionSource<bool>();
        EditorApplication.CallbackFunction update = () =>
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - start >= 120 || !EditorApplication.isPlaying) { done.TrySetResult(true); return; }
            if (EditorApplication.isPaused || Time.frameCount == observedFrame) return;
            observedFrame = Time.frameCount;
            double ms = (now - previous) * 1000;
            previous = now;
            if (now - start < 2) return; // Exclude monitor startup/compilation.
            frames++;
            bool spike = ms >= 80 && spikes < 64;
            for (int i = 0; i < recorders.Count; i++)
            {
                long value = recorders[i].LastValue;
                maximum[i] = Math.Max(maximum[i], value);
                totals[i] += value;
                if (spike) spikeValues[spikes, i] = value;
            }
            if (spike)
            {
                spikeMs[spikes] = ms; spikeFrames[spikes] = observedFrame;
                spikeUtc[spikes] = DateTime.UtcNow.ToString("o"); spikes++;
            }
        };
        EditorApplication.update += update;
        try
        {
            await done.Task;
            var statistics = new List<object>();
            for (int i = 0; i < names.Count; i++)
                statistics.Add(new { name = names[i], unit = units[i], max = maximum[i], mean = totals[i] / Math.Max(1, frames) });
            var events = new List<object>();
            for (int s = 0; s < spikes; s++)
            {
                var values = new Dictionary<string, long>();
                for (int i = 0; i < names.Count; i++) if (spikeValues[s, i] > 0) values[names[i]] = spikeValues[s, i];
                events.Add(new { utc = spikeUtc[s], frame = spikeFrames[s], editorFrameGapMs = spikeMs[s], markers = values });
            }
            var report = new { startedUtc, endedUtc = DateTime.UtcNow, seconds = EditorApplication.timeSinceStartup - start,
                frames, editorFrameGapsOver80ms = spikes, managedCollections = new[] { GC.CollectionCount(0) - collectionsBefore[0],
                    GC.CollectionCount(1) - collectionsBefore[1], GC.CollectionCount(2) - collectionsBefore[2] },
                statistics, events };
            string path = Path.GetFullPath("AgentScripts/PlayModeFreezeReport.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented));
            return new { path, report.seconds, frames, editorFrameGapsOver80ms = spikes, report.managedCollections };
        }
        finally
        {
            EditorApplication.update -= update;
            foreach (var recorder in recorders) recorder.Dispose();
        }
    }
}
