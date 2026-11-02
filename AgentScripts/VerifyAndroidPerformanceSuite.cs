using System;
using System.Collections.Generic;
using System.IO;
using Game.Performance;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyAndroidPerformanceSuite
{
    private static int checks;
    private static readonly List<string> passed = new();
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); passed.Add(message); }
    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode for deterministic regression checks.");
        checks = 0; passed.Clear();
        var d = PerformanceDistribution.Calculate(new double[] { 1, 2, 3, 4 });
        Check(d.mean == 2.5 && d.median == 2.5 && d.p95 == 4 && d.p99 == 4, "Even median, average and nearest-rank percentiles");
        var hundred = new List<double>(); for (int i = 1; i <= 100; i++) hundred.Add(i);
        d = PerformanceDistribution.Calculate(hundred);
        Check(d.p95 == 95 && d.p99 == 99 && d.maximum == 100, "P95/P99 on known distribution");
        d = PerformanceDistribution.Calculate(new double[] { double.NaN, double.PositiveInfinity, 5 });
        Check(d.count == 1 && d.mean == 5 && d.standardDeviation == 0, "Non-finite samples rejected");
        Check(PerformanceDistribution.Calculate(Array.Empty<double>()).count == 0, "Empty distribution explicitly has no samples");
        Check(!PerformanceSampler.ValidTiming(3195473662) && !PerformanceSampler.ValidTiming(double.NaN) && PerformanceSampler.ValidTiming(16.6), "Unsupported/invalid GPU timings rejected, never exported as real durations");
        using (var sampler = new PerformanceSampler(new PerformanceRepeat(), 100, 1, 1))
        { sampler.BeginMeasurement(); Check(sampler.ActiveRecorderCount > 0, "ProfilerRecorder restarted after Reset at measurement boundary"); }
        Check(PerformanceReport.Q("a,\"b") == "\"a,\"\"b\"", "CSV escaping");
        var logHost = new GameObject("Performance bounded-log regression"); logHost.SetActive(false);
        try
        {
            var runner = logHost.AddComponent<PerformanceSuiteRunner>();
            var repeat = new PerformanceRepeat(); for (int i = 0; i < 50; i++) repeat.anomalies.Add("fixture warning");
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PerformanceSuiteRunner).GetField("current", flags).SetValue(runner, repeat);
            typeof(PerformanceSuiteRunner).GetMethod("OnLog", flags).Invoke(runner, new object[] { "fixture late error", "", LogType.Error });
            Check(repeat.status == "failed", "Errors after bounded warning buffer saturation still fail the test");
        }
        finally { UnityEngine.Object.DestroyImmediate(logHost); }
        PerformanceSuiteEditor.CreateFixtures();
        var config = AssetDatabase.LoadAssetAtPath<PerformanceSuiteConfig>(PerformanceSuiteEditor.ConfigPath);
        Check(config != null && config.scenarios.Count >= 18, "Full scenario matrix generated");
        Check(config.bullet != null && config.homingBullet != null && config.ship != null && config.enemy != null, "Real runtime fixtures configured");
        var ids = new HashSet<string>(); foreach (var c in config.scenarios) Check(ids.Add(c.id), "Unique scenario ID: " + c.id);
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PerformanceSuiteEditor.ScenePath);
        Check(scene != null, "Dedicated scene exists");
        int before = World.All.Count; var defaultWorld = World.DefaultGameObjectInjectionWorld;
        bool injectedFailure = false;
        try { using var partial = new PerformanceWorkloadRuntime(config, config.scenarios[0], _ => throw new InvalidOperationException("injected initialization failure")); }
        catch (InvalidOperationException error) { injectedFailure = error.Message == "injected initialization failure"; }
        Check(injectedFailure && World.All.Count == before, "Partial initialization failure preserves primary error and disposes World before query creation");
        for (int repeat = 0; repeat < 3; repeat++) NativeLifecycle(config.bullet);
        Check(World.All.Count == before && ReferenceEquals(defaultWorld, World.DefaultGameObjectInjectionWorld), "Repeated real ECS workload releases its Worlds and preserves default World");
        var run = new PerformanceRun { runId = "regression-" + Guid.NewGuid().ToString("N"), platform = "Editor-regression", buildKind = "regression", status = "completed", editor = true,
            device = "fixture", graphicsApi = "fixture", backend = "fixture", quality = "fixture" };
        run.repeats.Add(new PerformanceRepeat { scenarioId = "statistics_fixture", status = "completed", parametersJson = "{}", seed = 53,
            averageFps = 400, frameMilliseconds = PerformanceDistribution.Calculate(new double[] { 1, 2, 3, 4 }), frames = new List<double> { 1, 2, 3, 4 } });
        string path = PerformanceReport.Save(run);
        var roundTrip = JsonUtility.FromJson<PerformanceRun>(File.ReadAllText(Path.Combine(path, "results.json")));
        Check(roundTrip.repeats.Count == 1 && roundTrip.repeats[0].frameMilliseconds.p99 == 4, "JSON round-trip preserves statistics");
        foreach (string file in new[] { "summary.csv", "metrics.csv", "frames.csv", "memory.csv", "report.md" })
            Check(File.Exists(Path.Combine(path, file)), "Export exists: " + file);
        Check(PerformanceReport.Compare(run, roundTrip).Contains("Insufficient repetitions"), "One repetition never produces an acceleration/regression verdict");
        roundTrip.platform = "Android";
        Check(PerformanceReport.Compare(run, roundTrip).Contains("Environments differ"), "Editor vs Android comparison is explicitly non-comparable");
        return new { checks, passed = passed.ToArray(), output = path };
    }
    private static void NativeLifecycle(EnemyBullet bullet)
    {
        var go = new GameObject("Performance regression registry"); go.SetActive(false);
        var registry = go.AddComponent<EnemyProjectileCollisionRegistry>();
        using var world = new World("Performance regression lifecycle", WorldFlags.Editor);
        try
        {
            registry.BindPerformanceWorld(world, null);
            using var spawner = EnemyProjectileEcsSpawner.CreatePerformanceWorld(world, registry);
            var requests = new List<EnemyProjectileSpawnRequest>();
            for (int i = 0; i < 5000; i++) requests.Add(new EnemyProjectileSpawnRequest(bullet, Vector3.zero, Vector3.up, 1, 2,
                default, 0, null, default, 0, 1, null, 1));
            Check(spawner.TrySpawnBatch(requests), "5000 projectiles created by runtime batch spawner");
            using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            Check(query.CalculateEntityCount() == 5000 && spawner.PerformanceSpawnedCount == 5000, "Runtime exceeds Preview 2048 cap without truncation");
            world.SetTime(new Unity.Core.TimeData(.1, .1f));
            world.GetOrCreateSystem<ProjectileMovementSystem>().Update(world.Unmanaged);
            using (var poses = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform)))
            using (var positions = poses.ToComponentDataArray<LocalTransform>(Allocator.Temp))
                Check(Math.Abs(positions[0].Position.y - .2f) < .001f, "Real ECS movement executes");
            var end = world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            world.SetTime(new Unity.Core.TimeData(100, 100));
            world.GetOrCreateSystem<ProjectileLifetimeSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ProjectileCleanupSystem>().Update(world.Unmanaged); end.Update();
            Check(query.CalculateEntityCount() == 0, "Real lifetime/ECB cleanup destroys all projectiles");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    public static object Start(string scenario, float seconds, int repetitions, bool soak)
    { PerformanceSuiteEditor.Run(scenario, seconds, repetitions, soak); return new { requested = true }; }
}
