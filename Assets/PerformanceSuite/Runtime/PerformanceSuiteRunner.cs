#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Game.Performance
{
    [DisallowMultipleComponent]
    public sealed class PerformanceSuiteRunner : MonoBehaviour
    {
        public PerformanceSuiteConfig configuration;
        public PerformanceRun Result { get; private set; }
        public string Phase { get; private set; } = "idle";
        public string LastOutput { get; private set; }
        public bool Running => Phase != "idle";
        public float Progress => queue.Count == 0 ? 0 : (float)queueIndex / queue.Count;
        private readonly List<(PerformanceCase scenario, int repetition)> queue = new();
        private int queueIndex;
        private PerformanceWorkloadRuntime workload;
        private PerformanceWorkloadRuntime cleanupWorkload;
        private PerformanceRepeat current;
        private PerformanceSampler sampler;
        private World defaultWorld;
        private double phaseStart, previousFrame;
        private float oldScale, oldFixed;
        private int oldFps, oldVsync;
        private bool oldBackground, subscribed;
        private bool cancelRequested, oldProfilerEnabled, oldBinaryLog;
        private string oldProfilerLog;
        private string tracePath;
        private double nextTraceSizeCheck;
        private UnityEngine.Random.State oldRandom;
        private readonly Stopwatch clock = new();
        private string launchToken = "";
        [Serializable] private sealed class CompletionManifest { public string launchToken, runId, status; }
        private static double Ms(long ticks) => ticks * 1000d / Stopwatch.Frequency;

        private void Start()
        {
#if !UNITY_EDITOR
            if (configuration != null && configuration.autoRunPlayer)
            {
                string scenario = null;
#if UNITY_ANDROID
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    launchToken = intent.Call<string>("getStringExtra", "perfLaunchToken") ?? "";
                    scenario = intent.Call<string>("getStringExtra", "perfScenario");
                    if (string.IsNullOrEmpty(scenario)) scenario = null;
                }
#endif
                StartSuite(scenario);
            }
#endif
        }
        public void StartSuite(string scenarioId = null, float secondsOverride = 0, int repetitionsOverride = 0, bool includeSoak = false)
        {
            if (Running) throw new InvalidOperationException("A suite is already running.");
            if (configuration == null) throw new InvalidOperationException("No performance configuration assigned.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "AndroidPerformanceSuite")
                throw new InvalidOperationException("Run only in the dedicated AndroidPerformanceSuite scene.");
            queue.Clear(); queueIndex = 0; cancelRequested = false;
            foreach (var source in configuration.scenarios)
            {
                if (source == null || !source.enabled || scenarioId != null && source.id != scenarioId) continue;
                if (source.workload == PerformanceWorkload.Soak && !includeSoak && scenarioId == null) continue;
                var scenario = JsonUtility.FromJson<PerformanceCase>(JsonUtility.ToJson(source));
                scenario.wavePrefab = source.wavePrefab;
                if (secondsOverride > 0) { scenario.measurementSeconds = secondsOverride; scenario.warmupSeconds = Math.Min(scenario.warmupSeconds, secondsOverride); }
                if (repetitionsOverride > 0) scenario.repetitions = repetitionsOverride;
                Validate(scenario);
                for (int i = 0; i < scenario.repetitions; i++) queue.Add((scenario, i));
            }
            if (queue.Count == 0) throw new InvalidOperationException("No enabled matching scenarios.");
            oldScale = Time.timeScale; oldFixed = Time.fixedDeltaTime; oldFps = Application.targetFrameRate;
            oldVsync = QualitySettings.vSyncCount; oldBackground = Application.runInBackground; oldRandom = UnityEngine.Random.state;
            Time.timeScale = 1; Time.fixedDeltaTime = 1f / 60; Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0; Application.runInBackground = true;
            Result = Metadata(configuration);
            oldProfilerEnabled = Profiler.enabled; oldBinaryLog = Profiler.enableBinaryLog; oldProfilerLog = Profiler.logFile;
            if (configuration.captureProfilerTrace)
            {
                string traceDirectory = System.IO.Path.Combine(PerformanceReport.OutputRoot, Result.runId);
                System.IO.Directory.CreateDirectory(traceDirectory);
                tracePath = System.IO.Path.Combine(traceDirectory, "profiler.raw");
                Profiler.logFile = tracePath; Profiler.enableBinaryLog = true; Profiler.enabled = true;
                Result.profilerEnabled = true;
            }
            Result.status = "running";
            Application.logMessageReceived += OnLog; subscribed = true;
            clock.Restart(); previousFrame = 0; Phase = "setup";
        }
        private static void Validate(PerformanceCase c)
        {
            if (string.IsNullOrWhiteSpace(c.id) || c.id.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Scenario ID must be a safe nonempty identifier.");
            if (c.projectiles < 1 || c.projectiles > 10000 || c.enemies < 1 || c.enemies > 256 || c.subwaves < 1 || c.subwaves > 8
                || c.childGenerations < 1 || c.childGenerations > 4 || c.repetitions < 1 || c.repetitions > 10
                || !float.IsFinite(c.childRootsPerSecond) || c.childRootsPerSecond < .1f || c.childRootsPerSecond > 128f
                || !float.IsFinite(c.measurementSeconds) || c.measurementSeconds < .2f || c.measurementSeconds > 7200
                || !float.IsFinite(c.warmupSeconds) || c.warmupSeconds < 0 || c.warmupSeconds > 600)
                throw new ArgumentOutOfRangeException(nameof(c), "Scenario safety limits exceeded.");
            if (!float.IsFinite(c.baseSpeed) || c.baseSpeed <= 0 || c.baseSpeed > 100 || !float.IsFinite(c.angularSpeed) || Math.Abs(c.angularSpeed) > 1440
                || !float.IsFinite(c.lateralSpeed) || Math.Abs(c.lateralSpeed) > 100 || !float.IsFinite(c.orbitRadius) || c.orbitRadius < 0 || c.orbitRadius > 100
                || !float.IsFinite(c.orbitRadialSpeed) || Math.Abs(c.orbitRadialSpeed) > 100)
                throw new ArgumentOutOfRangeException(nameof(c), "Movement parameter safety limits exceeded.");
        }
        private void Update()
        {
            if (!Running) return;
            double now = clock.Elapsed.TotalSeconds;
            double delta = now - previousFrame; previousFrame = now;
            try
            {
                if (Phase == "setup") { Setup(); return; }
                if (Phase == "cleanup") { Settle(now); return; }
                if (configuration.captureProfilerTrace && Profiler.enableBinaryLog && now >= nextTraceSizeCheck)
                {
                    nextTraceSizeCheck = now + 1;
                    if (System.IO.File.Exists(tracePath) && new System.IO.FileInfo(tracePath).Length > 268435456)
                    { Profiler.enabled = false; Profiler.enableBinaryLog = false; current.anomalies.Add("Profiler raw trace stopped at 256 MiB safety limit."); }
                }
                var scenario = queue[queueIndex].scenario;
                if (Profiler.GetTotalAllocatedMemoryLong() > configuration.abortAllocatedMegabytes * 1048576L)
                    throw new InvalidOperationException("Allocated-memory safety budget exceeded.");
                if (delta * 1000 > configuration.abortFrameMilliseconds && Phase == "measure")
                    throw new InvalidOperationException("Frame-duration safety budget exceeded.");
                workload.Tick((float)delta);
                workload.Observe(current);
                if (Phase == "warmup")
                {
                    if (now - phaseStart >= scenario.warmupSeconds)
                    {
                        sampler = new PerformanceSampler(current, configuration.maximumSamples, configuration.memoryInterval, scenario.measurementSeconds);
                        Phase = "recorder_warmup"; phaseStart = now;
                    }
                    return;
                }
                if (Phase == "recorder_warmup")
                {
                    if (now - phaseStart >= .25)
                    { sampler.BeginMeasurement(); Phase = "measure"; phaseStart = now; }
                    return;
                }
                if (!sampler.Sample(delta * 1000, now - phaseStart)) throw new InvalidOperationException("Bounded frame sample budget exhausted.");
                if (now - phaseStart >= scenario.measurementSeconds) EndRepeat("completed");
            }
            catch (Exception error)
            {
                if (current != null) current.anomalies.Add("Failure: " + error);
                if (Phase == "cleanup") { FinishRun("failed"); return; }
                EndRepeat("failed");
            }
        }
        private void Setup()
        {
            var item = queue[queueIndex];
            current = new PerformanceRepeat { scenarioId = item.scenario.id, workload = item.scenario.workload.ToString(), repetition = item.repetition,
                seed = item.scenario.seed, parametersJson = JsonUtility.ToJson(item.scenario), worldsBefore = World.All.Count,
                memoryBefore = Profiler.GetTotalAllocatedMemoryLong(), forcedCollection = configuration.forceCollectionAfterCleanup };
            current.memoryPeak = current.memoryBefore;
            Result.repeats.Add(current);
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            UnityEngine.Random.InitState(item.scenario.seed);
            long start = Stopwatch.GetTimestamp();
            workload = new PerformanceWorkloadRuntime(configuration, item.scenario);
            current.setupMilliseconds = Ms(Stopwatch.GetTimestamp() - start);
            Phase = "warmup"; phaseStart = clock.Elapsed.TotalSeconds;
        }
        private void EndRepeat(string status)
        {
            if (current == null) { FinishRun(status); return; }
            if (Phase == "cleanup") return;
            current.status = current.status == "failed" ? "failed" : status;
            sampler?.Finish(); sampler?.Dispose(); sampler = null;
            if (workload != null) workload.Observe(current);
            current.memoryLive = Profiler.GetTotalAllocatedMemoryLong();
            long start = Stopwatch.GetTimestamp();
            cleanupWorkload = workload;
            try { workload?.Dispose(); }
            finally { workload = null; current.cleanupMilliseconds = Ms(Stopwatch.GetTimestamp() - start); }
            Phase = "cleanup"; phaseStart = clock.Elapsed.TotalSeconds;
        }
        private void Settle(double now)
        {
            if (now - phaseStart < configuration.cleanupSettleSeconds) return;
            if (configuration.forceCollectionAfterCleanup) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
            current.memoryAfter = Profiler.GetTotalAllocatedMemoryLong();
            current.worldsAfter = World.All.Count;
            current.defaultWorldUnchanged = ReferenceEquals(defaultWorld, World.DefaultGameObjectInjectionWorld);
            current.projectilesAfter = cleanupWorkload?.ProjectileCount ?? 0;
            current.enemiesAfter = cleanupWorkload?.RemainingOwnedEnemies ?? 0;
            current.cleanupVerified = cleanupWorkload != null && !cleanupWorkload.OwnedWorldAlive && current.projectilesAfter == 0 && current.enemiesAfter == 0;
            if (current.worldsAfter != current.worldsBefore || !current.defaultWorldUnchanged || !current.cleanupVerified)
            { current.status = "failed"; current.anomalies.Add("Owned World lifecycle invariant failed."); }
            if (current.omittedChildProjectiles > 0) current.anomalies.Add("Runtime Child Attack safety budget omitted descendants: " + current.omittedChildProjectiles);
            if (current.peakProjectiles == 0 && current.status == "completed")
            { current.status = "failed"; current.anomalies.Add("No real runtime projectile activity observed."); }
            LastOutput = PerformanceReport.Save(Result);
            bool failed = current.status == "failed";
            queueIndex++;
            if (queueIndex >= queue.Count || failed || cancelRequested) { FinishRun(failed ? "failed" : cancelRequested ? "cancelled" : "completed"); return; }
            current = null; cleanupWorkload = null; Phase = "setup";
        }
        public void Cancel()
        {
            if (!Running) return;
            cancelRequested = true;
            if (current == null) { FinishRun("cancelled"); return; }
            if (current != null && Phase != "cleanup") EndRepeat("cancelled");
        }
        private void FinishRun(string status)
        {
            sampler?.Dispose(); sampler = null;
            workload?.Dispose(); workload = null;
            if (subscribed) { Application.logMessageReceived -= OnLog; subscribed = false; }
            Time.timeScale = oldScale; Time.fixedDeltaTime = oldFixed; Application.targetFrameRate = oldFps;
            QualitySettings.vSyncCount = oldVsync; Application.runInBackground = oldBackground; UnityEngine.Random.state = oldRandom;
            Profiler.enabled = false; Profiler.enableBinaryLog = oldBinaryLog; Profiler.logFile = oldProfilerLog; Profiler.enabled = oldProfilerEnabled;
            Phase = "idle"; clock.Stop();
            if (Result != null)
            {
                Result.status = status;
                try
                {
                    LastOutput = PerformanceReport.Save(Result);
                    if (!Application.isEditor)
                        System.IO.File.WriteAllText(System.IO.Path.Combine(PerformanceReport.OutputRoot, "complete.json"),
                            JsonUtility.ToJson(new CompletionManifest { launchToken = launchToken, runId = Result.runId, status = status }));
                }
                catch (Exception error) { Debug.LogError("Performance report export failed: " + error.Message); }
            }
        }
        private void OnLog(string condition, string trace, LogType type)
        {
            if (current == null || type == LogType.Log) return;
            bool error = type == LogType.Exception || type == LogType.Error || type == LogType.Assert;
            if (error) current.status = "failed";
            if (current.anomalies.Count >= 50)
            {
                if (error && current.anomalies.Count == 50) current.anomalies.Add("Additional errors occurred after the bounded log buffer filled.");
                return;
            }
            string safeMessage = condition.Split(new[] { "Environment Variables:" }, StringSplitOptions.None)[0];
            if (safeMessage.Length > 2000) safeMessage = safeMessage.Substring(0, 2000);
            current.anomalies.Add(type + ": " + safeMessage);
        }
        private void OnApplicationPause(bool paused) { if (paused && Running) { Cancel(); if (Running) FinishRun("interrupted_background"); } }
        private void OnDisable() { if (Running) { Cancel(); if (Running) FinishRun("interrupted"); } }
        private void OnDestroy() { if (Running) Cancel(); }
        private void OnGUI()
        {
            if (!Running || configuration == null || !configuration.showPlayerOverlay) return;
            GUILayout.BeginArea(new Rect(10, 10, 440, 100), GUI.skin.box);
            GUILayout.Label($"Performance Suite: {queueIndex + 1}/{queue.Count} — {Phase}");
            if (current != null) GUILayout.Label(current.scenarioId + " / repeat " + (current.repetition + 1));
            if (GUILayout.Button("Cancel performance run")) Cancel();
            GUILayout.EndArea();
        }
        private static PerformanceRun Metadata(PerformanceSuiteConfig config)
        {
            var r = new PerformanceRun { runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                startedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, gameVersion = Application.version,
                platform = Application.platform.ToString(), device = SystemInfo.deviceModel, os = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType, processorCount = SystemInfo.processorCount, systemMemoryMb = SystemInfo.systemMemorySize,
                gpu = SystemInfo.graphicsDeviceName, graphicsMemoryMb = SystemInfo.graphicsMemorySize, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                buildKind = Application.isEditor ? "Editor Play Mode" : Debug.isDebugBuild ? "Development Player" : "Instrumented Player",
                editor = Application.isEditor, rendering = config.renderProjectiles, profilerEnabled = Profiler.enabled,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], targetFrameRate = Application.targetFrameRate,
                vSync = QualitySettings.vSyncCount, width = Screen.width, height = Screen.height, configurationJson = JsonUtility.ToJson(config),
                notes = "Owned Game World; real runtime systems; fixed step 1/60; measurements include suite observer overhead. GC counts are process-wide per generation, not distinct collector pause count. Memory includes the process/Editor and retained report arrays." };
#if ENABLE_IL2CPP
            r.backend = "IL2CPP";
#else
            r.backend = "Mono";
#endif
            return r;
        }
    }
}
#endif
